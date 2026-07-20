// Copyright (c) GDFrank
// SPDX-License-Identifier: MIT

#if NETCOREAPP && !NO_CORE
#define USE_CORE
#endif

using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
#if USE_CORE
using System.Threading;
using System.Threading.Tasks;
#endif

namespace Source77NW
{
    /// <summary>
    /// Read-only Stream whose sole purpose is hot-swapping BytesReader's
    /// BaseStream mid-read (layering in GZip decompression or Crypto
    /// decryption) - BinaryReader, unlike BinaryWriter's OutStream, cannot
    /// swap its BaseStream itself.
    /// </summary>
    /// <remarks>
    /// NOT thread safe. Restrictions: decryption is not allowed while
    /// decompressing; decompression IS allowed while decrypting; only one
    /// decryption and one decompression may be active at a time. After
    /// Close() the inner stream is null (IsClosed true) and most members
    /// return defaults; writing members always throw (Issue). Flush is a
    /// deliberate no-op: read-only streams that throw on Flush are a
    /// landmine for framework code that flushes defensively.
    /// </remarks>
    public sealed class BytesReaderStream : Stream
    {
        private const ushort issueSource = 65003;

        private static Issue _issue(issueId iErr) => Issue.Create(issueSource, ((byte)iErr), iErr.ToString(), IssueKind.BadOperation);

        private enum issueId : byte
        {
            None, // 0
            Current_stream_is_not_GZipStream,
            BytesReaderStream_not_allowed,
            GZipStream_not_allowed,
            CryptoStream_not_allowed,
            Writing_not_allowed_in_Reader,
            Decryption_not_allowed_while_Decompressing,
            Current_stream_not_CryptoStream,
        }

        private Stream _BaseStream;

        // capacity 2 allows GZipStream inside CryptoStream; StartedDecryption
        // rejects IsDecompressing, so the stack depth never exceeds 1 per kind -
        // single-layer nesting is the intended, safe design.
        private readonly Stream[] _Streams = new Stream[2];

        private byte _Streams_Top = 0;

        /// <summary>True after Close(): the inner stream is null.</summary>
        public bool IsClosed => _BaseStream == null;

        /// <summary>True while GZip decompression is layered in.</summary>
        public bool IsDecompressing { get; private set; } = false;

        /// <summary>True while decryption is layered in.</summary>
        public bool IsDecrypting{ get; private set; } = false;

        /// <summary>Wraps theBaseStream. Throws (Issue) when it is itself a BytesReaderStream, GZipStream, or CryptoStream - layers are added via the Start* methods, never pre-wrapped.</summary>
        public BytesReaderStream(Stream theBaseStream)
        {
            if (theBaseStream is BytesReaderStream) throw _issue(issueId.BytesReaderStream_not_allowed);

            if (theBaseStream is GZipStream) throw _issue(issueId.GZipStream_not_allowed);

            if (theBaseStream is CryptoStream) throw _issue(issueId.CryptoStream_not_allowed);

            _BaseStream = theBaseStream;
        }

        /// <summary>Layers GZip decompression over the current stream; false when already decompressing.</summary>
        public bool StartedDecompression()
        {
            if (IsDecompressing) return false;

            GZipStream xZip = new GZipStream(_BaseStream, CompressionMode.Decompress, true);

            _Streams[_Streams_Top++] = _BaseStream;

            _BaseStream = xZip;

            IsDecompressing = true;

            return true;
        }

        /// <summary>Removes the GZip decompression layer, restoring the stream beneath; false when not decompressing.</summary>
        public bool StoppedDecompression()
        {
            if (_BaseStream == null || !IsDecompressing || !(_BaseStream is GZipStream)) return false;

            _BaseStream.Dispose(); // GZipStream leaves BaseStream open

            if (_Streams_Top > 0)
            {
                // POP to the outer stream
                _BaseStream = _Streams[--_Streams_Top];
            }
            else
            {
                _BaseStream = null;
            }

            IsDecompressing = false;

            return true;
        }

        /// <summary>Layers decryption with theKey over the current stream. False with the Issue when decompressing; false without one when already decrypting or the key yields no stream.</summary>
        public bool StartedDecryption(CryptoKey theKey, out Issue returnIssue)
        {
            if (IsDecompressing)
            {
                returnIssue = _issue(issueId.Decryption_not_allowed_while_Decompressing);

                return false;
            }

            returnIssue = null;

            if (IsDecrypting)
            {
                return false;
            }

            CryptoStream xCrypto = theKey.GetCryptoStream_or_null(_BaseStream, true);

            if (xCrypto == null)
            {
                return false;
            }

            // HOTSWAP

            _Streams[_Streams_Top++] = _BaseStream;

            _BaseStream = xCrypto;

            IsDecrypting = true;

            return true;
        }

        /// <summary>Removes the decryption layer, restoring the stream beneath; false when not decrypting.</summary>
        public bool StoppedDecryption()
        {
            if (_BaseStream == null || !IsDecrypting || !(_BaseStream is CryptoStream)) return false;

            _BaseStream.Dispose(); // leaves BaseStream open

            if (_Streams_Top > 0)
            {
                // POP to outer stream
                _BaseStream = _Streams[--_Streams_Top];
            }
            else
            {
                _BaseStream = null;
            }

            IsDecrypting = false;

            return true;
        }

        /// <summary>Disposes every layered stream down to and including the base; IsClosed becomes true.</summary>
        public sealed override void Close()
        {
            while (true)
            {
                if (_BaseStream == null) return;

                if (_BaseStream is GZipStream)
                {
                    IsDecompressing = false;
                }
                else if (_BaseStream is CryptoStream)
                {
                    IsDecrypting = false;
                }

                _BaseStream.Dispose();

                _BaseStream = null;

                if (_Streams_Top > 0)
                {
                    _BaseStream = _Streams[--_Streams_Top];

                    continue;
                }

                return;
            }
        }

        /// <summary>Closes the current stream (releasing it to its owner semantics) then disposes the base Stream.</summary>
        protected sealed override void Dispose(bool disposing)
        {
            Close();
            base.Dispose(disposing);
        }

        /// <summary>Reads one byte from the current stream (throws NullReferenceException when closed).</summary>
        public sealed override int ReadByte() => _BaseStream.ReadByte();

        /// <summary>Soft Read: exceptions are returned instead of thrown (0 bytes read on failure).</summary>
        public int Read(byte[] buffer, int offset, int count, out Exception returnException)
        {
            try
            {
                returnException = null;

                return _BaseStream.Read(buffer, offset, count);
            }
            catch (Exception ex)
            {
                returnException = ex;

                return 0;
            }

        }

        /// <summary>Reads from the current stream (0 when closed).</summary>
        public sealed override int Read(byte[] buffer, int offset, int count)
        {
            return _BaseStream?.Read(buffer, offset, count) ?? 0;
        }

        /// <summary>The current stream's CanRead (false when closed).</summary>
        public sealed override bool CanRead => _BaseStream?.CanRead ?? false;
        /// <summary>The current stream's CanSeek (false when closed).</summary>
        public sealed override bool CanSeek => _BaseStream?.CanSeek ?? false;
        /// <summary>Always false: this stream is read-only even over writable bases.</summary>
        public sealed override bool CanWrite => false; // even if can
        /// <summary>The current stream's CanTimeout (false when closed).</summary>
        public sealed override bool CanTimeout => _BaseStream?.CanTimeout ?? false;
        /// <summary>Delegates to the current stream (false when closed).</summary>
        public sealed override bool Equals(object obj) => _BaseStream?.Equals(obj) ?? false;
        /// <summary>Delegates to the current stream (0 when closed).</summary>
        public sealed override int GetHashCode() => _BaseStream?.GetHashCode() ?? 0;
        /// <summary>Delegates to the current stream (empty when closed).</summary>
        public sealed override string ToString() => _BaseStream?.ToString() ?? string.Empty;
        /// <summary>The current stream's Length (0 when closed).</summary>
        public override long Length => _BaseStream?.Length ?? 0;
        /// <summary>Seeks the current stream (0 when closed).</summary>
        public sealed override long Seek(long offset, SeekOrigin origin) => _BaseStream?.Seek(offset, origin) ?? 0;
        /// <summary>The current stream's Position (get 0 / set ignored when closed).</summary>
        public sealed override long Position
        {
            get { return _BaseStream?.Position ?? 0; }
            set { if (_BaseStream != null) _BaseStream.Position = value; }
        }

#if USE_CORE
        /// <summary>Delegates to the current stream (0 when closed).</summary>
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return _BaseStream?.ReadAsync(buffer, cancellationToken) ?? ValueTask.FromResult(0);
        }

        /// <summary>Delegates to the current stream (0 when closed).</summary>
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return _BaseStream?.ReadAsync(buffer, offset, count, cancellationToken) ?? Task.FromResult(0);
        }
#else
        /// <summary>Delegates to the current stream.</summary>
        public sealed override object InitializeLifetimeService() => _BaseStream.InitializeLifetimeService();
#endif

        /// <summary>Delegates to the current stream (throws NullReferenceException when closed).</summary>
        public sealed override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return _BaseStream.BeginRead(buffer, offset, count, callback, state);
        }

        /// <summary>Delegates to the current stream.</summary>
        public sealed override int EndRead(IAsyncResult asyncResult)
        {
            return _BaseStream.EndRead(asyncResult);
        }


        // Stream guidance is that read-only streams implement
        // Flush as a no-op; throwing is a landmine for framework/composed code
        // that flushes defensively (CopyTo patterns, wrapper disposal).
        /// <summary>Deliberate no-op (read-only stream; see remarks).</summary>
        public override void Flush() { }
        /// <summary>Always throws (Issue): writing is not allowed in a reader stream.</summary>
        public sealed override void SetLength(long value) => throw _issue(issueId.Writing_not_allowed_in_Reader);
        /// <summary>Always throws (Issue): writing is not allowed in a reader stream.</summary>
        public sealed override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state) => throw _issue(issueId.Writing_not_allowed_in_Reader);
        /// <summary>Always throws (Issue): writing is not allowed in a reader stream.</summary>
        public sealed override void EndWrite(IAsyncResult asyncResult) => throw _issue(issueId.Writing_not_allowed_in_Reader);
        /// <summary>Always throws (Issue): writing is not allowed in a reader stream.</summary>
        public sealed override void WriteByte(byte value) => throw _issue(issueId.Writing_not_allowed_in_Reader);
        /// <summary>Always throws (Issue): writing is not allowed in a reader stream.</summary>
        public sealed override void Write(byte[] buffer, int offset, int count) => throw _issue(issueId.Writing_not_allowed_in_Reader);

    }
}
