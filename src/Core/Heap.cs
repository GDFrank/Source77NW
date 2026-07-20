// CopyrightCopyright (c) GDFrank
// SPDX-License-Identifier: MIT

using System;

namespace Source77NW
{
    /// <summary>
    /// Contract for consumers that can bind to a Heap.Alloc&lt;T&gt; pooled
    /// allocator (see ItemStack, TextBuilder). Implementers acquire their
    /// buffers via Alloc.GetArray when bound, exact-size new T[] when not.
    /// </summary>
    public interface IHeapAlloc<T> : IDisposable
    {
        /// <summary>The bound allocator: null = exact-size arrays, set = pooled arrays.</summary>
        Heap.Alloc<T> Alloc { get; set; }
        /// <summary>Slot count for the consumer's first buffer.</summary>
        int OpenSize { get; set; }
    }

    /// <summary>
    /// Simple array-reuse system: per-element-type pools (Alloc&lt;T&gt;) that
    /// reduce GC allocation pressure and LOH fragmentation by recycling arrays
    /// instead of allocating fresh each time - without hogging memory unless
    /// asked to (EnableBIG/EnableLOH).
    /// </summary>
    /// <remarks>
    /// Usage: Alloc&lt;T&gt; consumers work best in using statements, e.g.
    /// using (var xWriter = Heap.New_TextBuilder(0)) { ... xWriter.ToString_and_Dispose(); }.
    /// For very large productions a consumer can be Reset() for shared reuse;
    /// the caller is then responsible for the final Dispose(). Arrays only
    /// return to the pool via FreeArray - a consumer that is GC'd instead
    /// silently forfeits the benefit, so always Reset() or Dispose().
    /// Pooled arrays must never remain exposed after Free.
    /// LOH: arrays at or over 85KB go to the Large Object Heap (not compacted
    /// by default); reusing them avoids expensive gen2 collections - enable
    /// via EnableLOH. GetArray/FreeArray take a per-type lock (~10-30ns
    /// uncontended); the pool pays off with high hit rates and consistent
    /// sizes - highly diverse one-off sizes are better left to the GC.
    /// </remarks>
    public static class Heap
    {
        /// <summary>Approximate CLR array object header size in bytes (used in allocation-size math).</summary>
        public const int ArrayHeaderSize = 24;
        /// <summary>Byte size at which arrays land on the Large Object Heap (pool's LOH band minimum).</summary>
        public const int LOH_MinSaveSize = 85_000;
        /// <summary>Largest array byte size the LOH band will pool (2x the minimum).</summary>
        public const int LOH_MaxSaveSize = LOH_MinSaveSize * 2;
        /// <summary>True when LOH-sized arrays are being pooled (see EnableLOH).</summary>
        public static bool LOH_Enabled { get; private set; } = false;
        /// <summary>True when BIG-band arrays are being pooled (see EnableBIG).</summary>
        public static bool BIG_Enabled { get; private set; } = false;

        private enum actionId : byte
        {
            Clear,
            Clear_LOH,
            Clear_LOH_and_BIG,
        }

        private delegate void triggerDO(actionId theId);
        private static readonly object _triggersLock = new object();
        private static readonly ItemStack<triggerDO> _triggers = new ItemStack<triggerDO>(8);

        // _triggers is pushed from Alloc<T> static ctors, which can run
        // concurrently for unrelated T - and ItemStack is NOT thread safe -
        // so all access goes through _triggersLock.

        private static void _addTrigger(triggerDO theTrigger)
        {
            lock (_triggersLock)
                _triggers.Push(theTrigger);
        }

        private static void _trigger(actionId theAction)
        {
            // snapshot under lock, invoke outside: keeps the lock window
            // tiny and avoids holding it across per-type Alloc<T>._Lock
            triggerDO[] xTriggers;
            lock (_triggersLock)
                xTriggers = _triggers.ToArray();

            int i = -1;
            while (++i < xTriggers.Length)
            {
                try
                {
                    xTriggers[i].Invoke(theAction);
                }
                catch { }
            }
        }

        /// <summary>Enables/disables pooling of LOH-sized arrays. Enabling also enables BIG; disabling clears the LOH pools of every element type.</summary>
        public static void EnableLOH(bool enable)
        {
            LOH_Enabled = enable;
            if (enable)
                BIG_Enabled = true;
            else
                _trigger(actionId.Clear_LOH);
        }

        /// <summary>Enables/disables pooling of BIG-band arrays. Disabling also disables LOH and clears both bands' pools of every element type.</summary>
        public static void EnableBIG(bool enable)
        {
            BIG_Enabled = enable;
            if (!enable)
            {
                LOH_Enabled = false; // disabling BIG also disables LOH (doc)
                _trigger(actionId.Clear_LOH_and_BIG);
            }
        }

        /// <summary>Empties the BIG and LOH pools of every element type (enabled flags unchanged).</summary>
        public static void ClearBIG()
        {
            _trigger(actionId.Clear_LOH_and_BIG);
        }

        /// <summary>Empties the LOH pools of every element type (enabled flags unchanged).</summary>
        public static void ClearLOH()
        {
            _trigger(actionId.Clear_LOH);
        }

        /// <summary>Empties every pool of every element type.</summary>
        public static void Clear()
        {
            _trigger(actionId.Clear);
        }


        // ===== FACTORY SHORTCUTS =====

        /// <summary>Creates a TextBuilder bound to the char pool (0 = default open size).</summary>
        public static TextBuilder New_TextBuilder(int theOpenSize)
            => Alloc<char>.New<TextBuilder>(theOpenSize);

        /// <summary>Creates an ItemStack&lt;T&gt; bound to the T pool (0 = default open size).</summary>
        public static ItemStack<T> New_ItemStack<T>(int theOpenSize)
            => Alloc<T>.New<ItemStack<T>>(theOpenSize);

        /// <summary>
        /// The per-element-type array pool: 8 length groups (small EXACT
        /// doubling bands, then BIG and LOH banded groups). One singleton per T;
        /// consumers bind via New/BindAlloc or IHeapAlloc.Alloc and acquire and
        /// release arrays through GetArray/FreeArray. Thread safe (per-type lock).
        /// </summary>
        public sealed class Alloc<T>
        {

            // ===== ALLOC<T> INSTANCE MEMBERS =============

            private Alloc() { }

            /// <summary>Smallest array LENGTH (element count) in the LOH band for this T.</summary>
            public static readonly int LOH_MinSaveLength;
            /// <summary>Largest array LENGTH (element count) the LOH band will pool for this T.</summary>
            public static readonly int LOH_MaxSaveLength;
            /// <summary>Approximate allocation size in bytes of a T[theArrayLength].</summary>
            public static long ArrayAllocSize(int theArrayLength) => ArrayHeaderSize + (long)ElementSize * theArrayLength;

            /// <summary>Approximate managed size of a T element (pointer width for reference types; see Exe.GetElementSize).</summary>
            public static readonly int ElementSize;
            /// <summary>Default array length for this T (derived from ElementSize).</summary>
            public static readonly int ArrayDefaultLength;

            /// <summary>Returns a pooled or new T[] of AT LEAST the requested length (0 = ArrayDefaultLength). The array may be LONGER than asked (group max or banded best-fit) - track array.Length, not the request.</summary>
            public T[] GetArray(int theMinArrayLength_or_zero_for_default)
            {
                lock (_Lock)
                {
                    return _GetArray(theMinArrayLength_or_zero_for_default);
                }
            }

            /// <summary>Returns theArray to the pool (nulling the caller's reference - never alias a freed array). isDirty true clears the array if it is actually saved; dropped arrays go to the GC unclear.</summary>
            public void FreeArray(ref T[] theArray, bool isDirty)
            {
                T[] xArray = theArray;
                theArray = null; // caller surrenders the reference: never alias
                                 // a pooled array another caller may receive
                if (xArray == null) return;

                lock (_Lock)
                {
                    // clear only when actually saved: dropped arrays go to GC
                    // anyway, and a full Array.Clear (up to LOH sizes) on a
                    // dropped array is wasted work
                    if (_FreeArray(xArray) && isDirty)
                        Array.Clear(xArray, 0, xArray.Length);
                }
            }


            // ===== ALLOC<T> STATIC MEMBERS =============

            private static readonly object _Lock = new object();

            static Alloc()
            {
                // ElementSize feeds only the coarse LOH-band check, so exactness
                // is not critical - but the static ctor must never throw.
                // ref T: pointer width (a T[] of references is a pointer array).
                // value T: managed element size.

                ElementSize = Exe.GetElementSize(typeof(T), out int iLength);
                int iES = (ElementSize > 0) ? ElementSize : 1;
                LOH_MinSaveLength = (LOH_MinSaveSize - ArrayHeaderSize) / iES;
                LOH_MaxSaveLength = (LOH_MaxSaveSize - ArrayHeaderSize) / iES;
                ArrayDefaultLength = iLength;
                _Singleton = new Alloc<T>();
                _addTrigger(_triggered); // one time only
                _heapGroups_initialize();
            }

            private static void _triggered(actionId theId)
            {
                // invoked from Heap._trigger on the caller's thread:
                // must take _Lock against concurrent GetArray/FreeArray
                lock (_Lock)
                {
                    heapGroup vGroup;
                    switch (theId)
                    {
                        case actionId.Clear:
                            int i = -1;
                            while (++i < _heapGroups_length)
                            {
                                vGroup = _heapGroups[i];
                                vGroup.Clear();
                                _heapGroups[i] = vGroup; // SAVE TO STRUCT ARRAY
                            }
                            return;

                        case actionId.Clear_LOH_and_BIG:
                            vGroup = _heapGroups[_heapGroup_BIG];
                            vGroup.Clear();
                            _heapGroups[_heapGroup_BIG] = vGroup; // SAVE TO STRUCT ARRAY
                            goto case actionId.Clear_LOH;

                        case actionId.Clear_LOH:
                            vGroup = _heapGroups[_heapGroup_LOH];
                            vGroup.Clear();
                            _heapGroups[_heapGroup_LOH] = vGroup; // SAVE TO STRUCT ARRAY
                            return;
                    }
                }
            }

            private static Alloc<T> _Singleton;

            /// <summary>Creates a TItem (any IHeapAlloc&lt;T&gt; with a parameterless ctor) bound to this T's pool singleton, with the given open size.</summary>
            public static TItem New<TItem>(int theOpenSize)
                where TItem : class, IHeapAlloc<T>, new()
            {
                TItem xItem = new TItem();
                xItem.Alloc = _Singleton;
                xItem.OpenSize = theOpenSize;
                return xItem;
            }

            /// <summary>Binds (true) or unbinds (false) theItem to this T's pool singleton; false only when theItem is null.</summary>
            public static bool BindAlloc(IHeapAlloc<T> theItem, bool asEnabled_else_disabled)
            {
                if (theItem != null)
                {
                    theItem.Alloc = asEnabled_else_disabled ? _Singleton : null;
                    return true;
                }

                return false;
            }


            #region // ====== PRIVATE T BUFFER MANAGEMENT MEMBERS =============

            // =================
            // GROUP LAYOUT (_heapGroups_length = 8):
            //   [0]      EXACT  1 .. D                (D = ArrayDefaultLength mod16)
            //   [1..5]   EXACT  doubling bands: ..2D, ..4D, ..8D, ..16D, ..32D
            //   [6]      BIG    banded: last exact max+1 .. LOH band min-1
            //   [7]      LOH    banded: LOH min .. max save lengths
            //
            // EXACT groups pool arrays of EXACTLY the group max length.
            // BANDED groups pool variable lengths; when full, an incoming
            // larger array replaces the smallest saved one (high-water).
            // All calls arrive under _Lock (GetArray/FreeArray).

            private const byte _heapGroups_length = 8;
            private const byte _heapGroup_BIG = _heapGroups_length - 2;
            private const byte _heapGroup_LOH = _heapGroups_length - 1;

            private static readonly heapGroup[] _heapGroups = new heapGroup[_heapGroups_length];

            private static int _mod16(int x) => (x + 15) & ~15;

            private static void _heapGroups_initialize()
            {
                int iLOH_MinLength = LOH_MinSaveLength;
                int iLOH_MaxLength = LOH_MaxSaveLength;

                // EXACT groups [0 .. _heapGroup_BIG-1]: doubling sizes
                int iLenMin = 1;
                int iLenMax = _mod16(ArrayDefaultLength);
                if (iLenMax < 16) iLenMax = 16; // never-throw insurance

                int i = -1;
                while (++i < _heapGroup_BIG)
                {
                    byte iCountMax = (i == 0) ? (byte)4 : (byte)2;

                    _heapGroups[i] = new heapGroup(heapGroup.Kind.EXACT,
                        iLenMin, iLenMax, iCountMax);

                    iLenMin = iLenMax + 1;
                    iLenMax *= 2; // D, 2D, 4D, ...
                    if (iLenMax >= iLOH_MinLength)
                        iLenMax = iLOH_MinLength - 1; // keep exact bands below LOH
                }

                // [6] BIG: variable lengths up to the LOH threshold
                _heapGroups[_heapGroup_BIG] = new heapGroup(heapGroup.Kind.BIG,
                    iLenMin, iLOH_MinLength - 1, 2);

                // [7] LOH: variable lengths within the LOH save window
                _heapGroups[_heapGroup_LOH] = new heapGroup(heapGroup.Kind.LOH,
                    iLOH_MinLength, iLOH_MaxLength, 2);
            }

            // groups are contiguous ascending: first group whose max covers
            // forLength wins. returns -1 = above LOH max, never pooled.
            private static int _groupIndex(int forLength, out heapGroup vGroup)
            {
                int i = -1;
                while (++i < _heapGroups_length)
                {
                    if (forLength <= _heapGroups[i].MaxArrayLength)
                    {
                        vGroup = _heapGroups[i];
                        return i;
                    }
                }
                vGroup = default;
                return -1;
            }

            private static T[] _GetArray(int iMinLength)
            {
                if (iMinLength <= 0)
                    iMinLength = ArrayDefaultLength;
               
                iMinLength = _mod16(iMinLength); // nearest mod 16 length

                int index = _groupIndex(iMinLength, out heapGroup vGroup);
                if (index >= 0)
                {
                    if (vGroup.PluckedArray(iMinLength, out T[] xArray))
                    {
                        _heapGroups[index] = vGroup; // UPDATE THE STRUCT ARRAY
                        return xArray;
                    }

                    // EXACT miss: allocate the group size so the array is
                    // poolable on FreeArray. BANDED miss: allocate as asked mod 16.
                    if (vGroup.IsExact)
                        return new T[vGroup.MaxArrayLength];
                }

                return new T[iMinLength];
            }

            private static bool _FreeArray(T[] xArray)
            {
                int index = _groupIndex(xArray.Length, out heapGroup vGroup);
                if (index < 0) return false;

                bool bSaved = vGroup.StuffedArray(xArray);
                if (bSaved)
                    _heapGroups[index] = vGroup; // SAVE TO STRUCT ARRAY
                return bSaved;
            }

            private struct heapGroup
            {
                public enum Kind : byte { EXACT, BIG, LOH }

                public heapGroup(Kind theKind, int iMinLength, int iMaxLength,
                                 byte iMaxCount)
                {
                    _Arrays = null;
                    _Kind = theKind;
                    _MinArrayLength = iMinLength;
                    _MaxArrayLength = iMaxLength;
                    _Pool_MaxCount = iMaxCount;
                    _Pool_Count = 0;
                }

                private T[][] _Arrays;     // pool slots [0 .. _Pool_Count-1]
                private readonly Kind _Kind;
                private readonly int _MinArrayLength;
                private readonly int _MaxArrayLength;
                private readonly byte _Pool_MaxCount;
                private byte _Pool_Count;

                public bool IsExact => _Kind == Kind.EXACT;
                public int MinArrayLength => _MinArrayLength;
                public int MaxArrayLength => _MaxArrayLength;
               
                // ----- pluck (GetArray side) -----

                public bool PluckedArray(int iLength, out T[] xArray) // heapGroup
                {
                    xArray = null;
                    if (_Pool_Count == 0) return false;

                    if (IsExact)
                    {
                        // every saved array is _MaxArrayLength (>= iLength): POP
                        xArray = _Arrays[--_Pool_Count];
                        _Arrays[_Pool_Count] = null;
                        return true;
                    }

                    // BANDED: smallest saved array that still fits iLength
                    int iBestLen = int.MaxValue;
                    int iBestIndex = -1;

                    int i = -1;
                    while (++i < _Pool_Count)
                    {
                        int iLen = _Arrays[i].Length;
                        if (iLen >= iLength && iLen < iBestLen)
                        {
                            iBestLen = iLen;
                            iBestIndex = i;
                        }
                    }

                    if (iBestIndex < 0) return false; // none big enough

                    xArray = _Arrays[iBestIndex];
                    _Arrays[iBestIndex] = _Arrays[--_Pool_Count]; // swap-with-last
                    _Arrays[_Pool_Count] = null;
                    return true;
                }

                // ----- stuff (FreeArray side) -----

                public bool StuffedArray(T[] xArray) // heapGroup
                {
                    if (xArray == null) return false;
                    if (_Kind == Kind.LOH && !LOH_Enabled) return false;
                    if (_Kind == Kind.BIG && !BIG_Enabled) return false;

                    if (IsExact && xArray.Length != _MaxArrayLength)
                        return false; // EXACT pools one length only

                    if (_Arrays == null)
                        _Arrays = new T[_Pool_MaxCount][];
                    else if (__AlreadyStuffed(xArray))
                        return false; // double-Free guard

                    if (_Pool_Count < _Pool_MaxCount)
                    {
                        _Arrays[_Pool_Count++] = xArray; // APPEND
                        return true;
                    }

                    if (IsExact) return false; // full; all same size, drop it

                    return _Free_ReplacedSmallest(xArray); // BANDED + full
                }

                private bool __AlreadyStuffed(T[] xArray)
                {
                    int i = -1;
                    while (++i < _Pool_Count)
                        if (object.ReferenceEquals(_Arrays[i], xArray))
                            return true;
                    return false;
                }

                private bool _Free_ReplacedSmallest(T[] xArray)
                {
                    // pool full: evict the smallest entry if xArray is larger

                    int iSmallestLen = int.MaxValue;
                    int iSmallestIndex = -1;

                    int i = -1;
                    while (++i < _Pool_Count)
                    {
                        int iLen = _Arrays[i].Length;
                        if (iLen < iSmallestLen)
                        {
                            iSmallestLen = iLen;
                            iSmallestIndex = i;
                        }
                    }

                    if (xArray.Length <= iSmallestLen)
                        return false; // not an upgrade

                    _Arrays[iSmallestIndex] = xArray;
                    return true;
                }

                // ----- maintenance -----

                public void Clear() // heapGroup
                {
                    while (_Pool_Count > 0)
                        _Arrays[--_Pool_Count] = null;
                }
            }

            #endregion // PRIVATE T BUFFER MANAGEMENT MEMBERS

        }
    }
}
