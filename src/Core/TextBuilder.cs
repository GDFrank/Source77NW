// CopyrightCopyright (c) GDFrank
// SPDX-License-Identifier: MIT

#if NETCOREAPP && !NO_CORE
#define USE_CORE
#endif

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Source77NW
{
    /// <summary>
    /// A TextWriter that writes to memory: the buffer is created on the
    /// FIRST write and grows in chunks as needed; ToString returns the
    /// written text while allowing continued writing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The buffer initializes and grows in chunks determined by
    /// <see cref="OpenSize"/> and <see cref="GrowSize"/> (minimum
    /// <see cref="MinSize"/>, default per Heap.Alloc); both can be changed
    /// at any time. Setting <see cref="Alloc"/> binds buffer allocation to
    /// a heap pool; null (default) allocates exact arrays.
    /// </para>
    /// <para>
    /// This is a bare-metal core: NOT thread safe, and the Async/Flush
    /// family is NOT implemented - every member of it throws an
    /// <see cref="Issue"/> (ProgramIssue). Async callers belong at the
    /// agent layer, sync-complete; do not fatten this core.
    /// </para>
    /// <para>
    /// <see cref="Reset"/> clears and frees all buffers but keeps the
    /// <see cref="Alloc"/> binding; Dispose additionally unbinds it.
    /// </para>
    /// </remarks>
    public sealed class TextBuilder : TextWriter, IHeapAlloc<char>
    {
        // ====== PRIVATE METHODS AND PROPERTIES ======

        private const ushort issueSource = 65012;

        /// <summary>Default open/grow chunk size (Heap.Alloc&lt;char&gt;.ArrayDefaultLength).</summary>
        public static readonly int DefaultSize = Heap.Alloc<char>.ArrayDefaultLength;
        /// <summary>Minimum open/grow chunk size; smaller settings are raised to this.</summary>
        public const int MinSize = 256;

        private Heap.Alloc<char> _Alloc;
        private ItemStack<char[]> _bufferStack; // are "filled"
        private int _OpenSize;
        private int _GrowSize;
        private char[] _buffer; // CURRENT CHAR BUFFER
        private int _buffer_Length = 0; // 0 means _buf is null, IsOpen false
        private int _buffer_Top;

        /// <summary>The bound heap allocator; null = exact-size arrays (default), set = heap-pooled buffers.</summary>
        public Heap.Alloc<char> Alloc { get => _Alloc; set => _Alloc = value; } // null = exact mode, set = HeapMode

        /// <summary>
        /// Ensures the buffer has room for at least forCount more chars,
        /// growing it once if needed - pre-sizing before a batch of
        /// writes avoids repeated grow steps.
        /// </summary>
        public void EnsureRoom(int forCount) => _EnsureRoom(forCount);

        private Issue _issue_no_async()
        {
            return Issue.Create(issueSource, 86, "TextBuilder NO Async", IssueKind.ProgramIssue);
        }

        private void _EnsureRoom(int forCount)
        {
            if (forCount <= 0)
                return;

            // Fast path: current buffer has room
            if (_buffer != null && _buffer_Top + forCount <= _buffer_Length)
                return;

            char[] xNew = null;

            if (_buffer == null)
            {
                // First write: allocate initial buffer sized to OpenSize or forCount
                int iNewSize = OpenSize;
                if (iNewSize < forCount)
                    iNewSize = (forCount / GrowSize + 1) * GrowSize;

                xNew = (_Alloc != null) ? _Alloc.GetArray(iNewSize) : null;
                if (xNew == null) xNew = new char[iNewSize];

                _buffer = xNew;
                _buffer_Length = _buffer.Length;
                _buffer_Top = 0;
            }
            else if (_buffer_Top == _buffer_Length)
            {
                // Buffer exactly full: push it onto stack (fully filled, as _build requires),
                // allocate a fresh buffer sized to GrowSize or forCount.
                if (_bufferStack == null) _bufferStack = new ItemStack<char[]>();
                _bufferStack.Push(_buffer);

                int iNewSize = GrowSize;
                if (iNewSize < forCount)
                    iNewSize = (forCount / GrowSize + 1) * GrowSize;

                xNew = (_Alloc != null) ? _Alloc.GetArray(iNewSize) : null;
                if (xNew == null) xNew = new char[iNewSize];

                _buffer = xNew;
                _buffer_Length = _buffer.Length;
                _buffer_Top = 0;
            }
            else
            {
                // Partial room, not enough: grow current buffer in place (copy).
                // Keeps stacked-buffer integrity: only fully-filled arrays are ever pushed.
                int iRemain = _buffer_Length - _buffer_Top;
                int iNeed = forCount - iRemain;
                int iGrowChunks = (iNeed + GrowSize - 1) / GrowSize;
                int iNewSize = _buffer_Length + (iGrowChunks * GrowSize);

                xNew = (_Alloc != null) ? _Alloc.GetArray(iNewSize) : null;
                if (xNew == null) xNew = new char[iNewSize];

                if (_buffer_Top > 0)
                    Array.Copy(_buffer, 0, xNew, 0, _buffer_Top);

                if (_Alloc != null)
                    _Alloc.FreeArray(ref _buffer, true); // clears only if pooled; nulls _buffer

                _buffer = xNew;
                _buffer_Length = _buffer.Length; // WARNING: _buffer_Length MUST match _buffer.Length
                // _buffer_Top unchanged
            }
        }

        private void _build(out string sOutput, bool bOut, bool bFree)
        {
            sOutput = null; char[] xOut = null; int iOut = 0;
            int iFrom = 0; char[] xArray = null;

            if (bOut)
                xOut = new char[Count]; // the TOTAL _buffer & _bufferStack count

            if (_bufferStack != null && _bufferStack.Count > 0)
            {
                // SCAN STACK OF ARRAYS.  WARNING: PRESUMES ALL TOTALLY FILLED
                if (bFree)
                {
                    while (_bufferStack.Count > 0)
                    {
                        xArray = _bufferStack.Pluck();

                        iFrom = 0;
                        if (bOut)
                            while (iFrom < xArray.Length)
                            {
                                xOut[iOut++] = xArray[iFrom];
                                xArray[iFrom++] = default;
                            }
                        else
                            Array.Clear(xArray, 0, xArray.Length);

                        if (_Alloc != null)
                            _Alloc.FreeArray(ref xArray, false);
                    }

                    _bufferStack.Dispose();
                    _bufferStack = null;
                }
                else
                {
                    int cursor = 0;
                    while (_bufferStack.GotNext(ref cursor, out xArray, out _))
                    {
                        iFrom = 0;
                        if (bOut)
                            while (iFrom < xArray.Length)
                                xOut[iOut++] = xArray[iFrom++];
                    }
                }
            } // _bufferStack processing

            // now for populated _buffer (to _buffer_Top)

            if (_buffer != null)
            {
                iFrom = 0;

                if (bOut && bFree)
                    while (iFrom < _buffer_Top)
                    {
                        xOut[iOut++] = _buffer[iFrom];
                        _buffer[iFrom++] = default;
                    }
                else if (bOut)
                    while (iFrom < _buffer_Top)
                        xOut[iOut++] = _buffer[iFrom++];
            }

            if (bFree)
            {
                if (!bOut && _buffer != null && _buffer_Top > 0)
                    Array.Clear(_buffer, 0, _buffer_Top);

                if (_Alloc != null)
                    _Alloc.FreeArray(ref _buffer, false);
                _buffer = null;
                _buffer_Top = 0;
                _buffer_Length = 0;
                _Count = 0;
            }

            sOutput = bOut ? new string(xOut) : null;
        }

        private int _resize(int iSize)
        {
            if (iSize <= 0) return DefaultSize;
            if (iSize < MinSize) return MinSize;
            return iSize;
        }

        private int _Count = 0;


        // ====== PUBLIC METHODS AND PROPERTIES ======

        /// <summary>Count of written chars.</summary>
        public int Count => _Count;

        /// <summary>True when anything has been written (Count &gt; 0).</summary>
        public bool IsOpen { get { return Count > 0; } }

        /// <summary>
        /// Initial buffer size for the first write; values below
        /// <see cref="MinSize"/> are raised, &lt;= 0 resets to
        /// <see cref="DefaultSize"/>. Can be changed at any time.
        /// </summary>
        public int OpenSize
        {
            get { return _resize(_OpenSize); }
            set { _OpenSize = _resize(value); }
        }

        /// <summary>
        /// Growth chunk size for subsequent buffers; values below
        /// <see cref="MinSize"/> are raised, &lt;= 0 resets to
        /// <see cref="DefaultSize"/>. Can be changed at any time.
        /// </summary>
        public int GrowSize
        {
            get { return _resize(_GrowSize); }
            set { _GrowSize = _resize(value); }
        }

        /// <summary>Creates a TextBuilder with default open/grow sizes.</summary>
        public TextBuilder()
        {
            OpenSize = DefaultSize;
            GrowSize = DefaultSize;
        }

        /// <summary>Creates a TextBuilder with the given open/grow size (raised to <see cref="MinSize"/> when smaller).</summary>
        public TextBuilder(int theOpenSize_or_MinSize)
        {
            OpenSize = theOpenSize_or_MinSize;
            GrowSize = theOpenSize_or_MinSize;
            _buffer_Top = 0;
            _buffer_Length = 0; // indicating _buffer == null
        }

        /// <summary>Clears and frees all buffers; the <see cref="Alloc"/> binding is kept.</summary>
        public void Reset() { _build(out string _, false, true); }

        /// <summary>Returns the written text and performs <see cref="Reset"/>.</summary>
        public string ToString_and_Reset() { _build(out string s, true, true); return s; }

        /// <summary>Returns the written text and disposes (Reset + unbind <see cref="Alloc"/>).</summary>
        public string ToString_and_Dispose()
        {
            _build(out string s, true, true);
            Dispose(); // TextWriter.Dispose -> Dispose(true): unbinds _Alloc
            return s;
        }

        // ====== OVERRIDES ======

        /// <summary>Clears and frees all buffers and unbinds <see cref="Alloc"/>.</summary>
        protected override void Dispose(bool disposing) { _build(out string s, false, true); _Alloc = null; }

        /// <summary>Returns the current written text; writing may continue (buffers are recomposed each call, not freed).</summary>
        public override string ToString() { _build(out string s, true, false); return s; }

        /// <summary>Nominal encoding (Encoding.Default); the writer targets in-memory chars, no encoding is applied.</summary>
        public override Encoding Encoding { get { return Encoding.Default; } }

        /// <summary>Writes a single char.</summary>
        public override void Write(char value)
        {
            if (_buffer_Top < _buffer_Length)
            {
                _buffer[_buffer_Top++] = value;
            }
            else
            {
                _EnsureRoom(1);
                _buffer[_buffer_Top++] = value;
            }
            _Count++;
        }

#if USE_CORE
        /// <summary>Writes the span's chars (nothing when empty).</summary>
        public override void Write(ReadOnlySpan<char> theBuffer)
        {
            int count = theBuffer.Length;
            if (count < 1)
                return;
            _EnsureRoom(count);
            theBuffer.CopyTo(_buffer.AsSpan(_buffer_Top));
            _buffer_Top += count;
            _Count += count;
        }
#else
        /// <summary>Writes the array's chars (nothing when null or empty).</summary>
        public override void Write(char[] theBuffer)
        {
            if (theBuffer == null)
                return;
            int iCount = theBuffer.Length;
            if (iCount < 1)
                return;
            _EnsureRoom(iCount);
            Array.Copy(theBuffer, 0, _buffer, _buffer_Top, iCount);
            _buffer_Top += iCount;
            _Count += iCount;
        }
#endif
        /// <summary>
        /// Writes forCount chars of the array from theStartingIndex, SOFTLY:
        /// a null array or out-of-range index writes nothing, an over-long
        /// count is clamped to the array end - never a throw (deviates from
        /// the BCL TextWriter contract by design).
        /// </summary>
        public override void Write(char[] theBuffer, int theStartingIndex, int forCount)
        {
            if (theBuffer == null
            || theStartingIndex < 0
            || theStartingIndex >= theBuffer.Length)
            {
                return; // do nothing;
            }

            int iValueTop = theStartingIndex + forCount;

            if (iValueTop > theBuffer.Length)
            {
                iValueTop = theBuffer.Length;
            }

            if (theStartingIndex >= iValueTop)
            {
                return; // do nothing;
            }

#if USE_CORE
            Write(theBuffer.AsSpan(theStartingIndex, iValueTop - theStartingIndex));
#else
            int iActualCount = iValueTop - theStartingIndex;

            _EnsureRoom(iActualCount);

            Array.Copy(theBuffer, theStartingIndex, _buffer, _buffer_Top, iActualCount);

            _buffer_Top += iActualCount;
            _Count += iActualCount;
#endif
        }

        /// <summary>Writes the string's chars (nothing when null or empty).</summary>
        public override void Write(string theText)
        {
            if (string.IsNullOrEmpty(theText))
            {
                return;
            }
#if USE_CORE
            Write(theText.AsSpan()); // _Count incremented inside Write(ReadOnlySpan<char>)
#else
            _EnsureRoom(theText.Length);
            theText.CopyTo(0, _buffer, _buffer_Top, theText.Length);
            _buffer_Top += theText.Length;
            _Count += theText.Length;
#endif
        }

        /// <summary>Not supported.</summary><exception cref="Issue">Always; TextBuilder is sync-complete by contract.</exception>
        public override Task WriteLineAsync() => throw _issue_no_async();

        /// <summary>Not supported.</summary><exception cref="Issue">Always; TextBuilder is sync-complete by contract.</exception>
        public override Task WriteLineAsync(char value) => throw _issue_no_async();

        /// <summary>Not supported.</summary><exception cref="Issue">Always; TextBuilder is sync-complete by contract.</exception>
        public override Task WriteLineAsync(string value) => throw _issue_no_async();

        /// <summary>Not supported.</summary><exception cref="Issue">Always; TextBuilder is sync-complete by contract.</exception>
        public override Task WriteLineAsync(char[] buffer, int index, int count) => throw _issue_no_async();

        /// <summary>Not supported.</summary><exception cref="Issue">Always; TextBuilder is sync-complete by contract.</exception>
        public override Task FlushAsync() => throw _issue_no_async();

        /// <summary>Not supported.</summary><exception cref="Issue">Always; TextBuilder is sync-complete by contract.</exception>
        public override Task WriteAsync(char[] buffer, int index, int count) => throw _issue_no_async();

        /// <summary>Not supported.</summary><exception cref="Issue">Always; TextBuilder is sync-complete by contract.</exception>
        public override Task WriteAsync(char value) => throw _issue_no_async();

        /// <summary>Not supported.</summary><exception cref="Issue">Always; TextBuilder is sync-complete by contract.</exception>
        public override Task WriteAsync(string value) => throw _issue_no_async();

#if USE_CORE
        /// <summary>Not supported.</summary><exception cref="Issue">Always; TextBuilder is sync-complete by contract.</exception>
        public override Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default) => throw _issue_no_async();

        /// <summary>Not supported.</summary><exception cref="Issue">Always; TextBuilder is sync-complete by contract.</exception>
        public override Task WriteLineAsync(ReadOnlyMemory<char> buffer, CancellationToken cancellationToken = default) => throw _issue_no_async();
#endif
    }
}
