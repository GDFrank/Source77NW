// Copyright (c) GDFrank
// SPDX-License-Identifier: MIT

#if NETCOREAPP && !NO_CORE
#define USE_CORE
#endif

using System;
using System.Collections;
using System.Collections.Generic;

namespace Source77NW
{
    /// <summary>
    /// A lightweight single-array list/stack/queue tool - our ArrayList - for
    /// building, managing, and producing arrays with minimal allocation. LIFO via
    /// Push/Pop, FIFO via Push/Pluck (bottom slides up, no shifting), sparse via
    /// Set, sorted via Sort/BinarySearchNearest. Cursor enumeration (GotNext,
    /// GotDown, FoundNext, FoundDown), IList/ICollection vocabulary, and optional
    /// binding to recycled Heap buffers (IHeapAlloc). Sealed for JIT optimization.
    /// </summary>
    /// <remarks>
    /// BARE-METAL CORE: NOT thread safe, no defensive ceremony; misuse of indexes
    /// throws (BCL exceptions by contract). The slots buffer is not created until
    /// the first Push/Stuff/Set. Dispose/Reset/Clear/Pop/Pluck set vacated slots
    /// to DefaultItem so items are released. StackAgent is the wrapped, read-only
    /// agent exposure; subclass it for invariants, tallies, or locking.
    /// </remarks>
    public sealed class ItemStack<T> : IHeapAlloc<T>, IDisposable, ICollection<T>, IList<T>
    {
        /// <summary>Creates an unopened stack; the first buffer will be theOpenSize slots (0 = DefaultSize).</summary>
        public ItemStack(int theOpenSize_0_for_default)
        {
            OpenSize = theOpenSize_0_for_default;
        }

        /// <summary>Adopts theItems as the fully populated slots buffer (no copy); OpenSize = its length. Throws on null.</summary>
        public ItemStack(T[] theItems)
        {
            _Slots = theItems ?? throw new ArgumentNullException(nameof(theItems));
            _SlotsLength = _Slots.Length;
            _ItemsBot = 0;
            _ItemsTop = _SlotsLength;
            OpenSize = _SlotsLength;
        }

        /// <summary>Creates an unopened stack with DefaultSize as the OpenSize.</summary>
        public ItemStack() { OpenSize = DefaultSize; }

        //=========== HEAP ==============

        /// <summary>Arbitrary caller tag object (volatile).</summary>
        public volatile object Tag;
        private Heap.Alloc<T> _Alloc;
        private T[] _Slots = null;
        private IComparer<T> _Comparer = Comparer<T>.Default;
        /// <summary>Arbitrary caller tag int (volatile).</summary>
        public volatile int TagInt = 0;
        private uint _CountUpdateNum = 0; // for Cursor to invalidate itself
        private int _SlotsLength = 0; // WARNING: TIGHTLY SYNC TO _Slots.Length
        private int _ItemsBot = 0;
        private int _ItemsTop = 0;
        private int __OpenSize = 0;
        private int __GrowSize = 0;

        //========== SUPPORT ====================
        private Exception _bad_index(int theIndex) => new IndexOutOfRangeException($"{GetType().FullName} Index: {theIndex}");

        //========== PUBLIC PROPERTIES ==========

        /// <summary>Heap allocator binding: null = exact-size arrays, set = pooled HeapMode arrays.</summary>
        public Heap.Alloc<T> Alloc { get => _Alloc; set => _Alloc = value; } // null = exact mode, set = HeapMode

        /// <summary>Default open size: the Heap allocator's default when bound, else per element size (Exe.GetElementSize).</summary>
        public int DefaultSize => GetDefaultSize(out _);

        /// <summary>Returns DefaultSize plus the element size it was derived from.</summary>
        public int GetDefaultSize(out int returnElementSize)
        {
            if (_Alloc != null)
            {
                returnElementSize = Heap.Alloc<T>.ElementSize;
                return Heap.Alloc<T>.ArrayDefaultLength;
            }

            returnElementSize = Exe.GetElementSize(typeof(T), out int iArraySize);

            return iArraySize;
        }

        private int _Open_zero_size(int iSize) { if (iSize <= 0) return DefaultSize; return iSize; }

        /// <summary>Slot count for the first buffer (settable any time; values &lt;= 0 become DefaultSize).</summary>
        public int OpenSize
        {
            get { return _Open_zero_size(__OpenSize); }
            set 
            { 
                __OpenSize = _Open_zero_size(value);
            }
        }

        /// <summary>Slot count added per growth (settable any time; 0 or less falls back to OpenSize).</summary>
        public int GrowSize
        {
            get { return (__GrowSize <= 0) ? OpenSize: __GrowSize; }
            set { __GrowSize = value < 0 ? 0: value; }
        }

        /// <summary>Comparer used by Sort and BinarySearch (null resets to Comparer&lt;T&gt;.Default).</summary>
        public IComparer<T> Comparer { get => _Comparer; set => _Comparer = value ?? Comparer<T>.Default; }
        /// <summary>typeof(T).</summary>
        public Type ItemType => typeof(T);
        /// <summary>The T default, representing item NOT SET.</summary>
        public T DefaultItem => default;
        /// <summary>True if theItem equals the T default.</summary>
        public bool IsDefaultItem(T theItem) => EqualityComparer<T>.Default.Equals(theItem, default);
        /// <summary>True when the stack holds no items.</summary>
        public bool IsEmpty => _ItemsBot >= _ItemsTop;
        /// <summary>True when the stack holds at least one item.</summary>
        public bool NotEmpty => _ItemsBot < _ItemsTop;
        /// <summary>Count of items currently in the stack.</summary>
        public int Count => _ItemsTop - _ItemsBot;
        /// <summary>Bumped by every count-changing operation; lets Cursor detect invalidation.</summary>
        public uint CountUpdateNum => _CountUpdateNum;
        /// <summary>Length of the current slots buffer (0 when reset).</summary>
        public int SlotsLength => _SlotsLength;
        /// <summary>Unused slots in the buffer (SlotsLength - Count).</summary>
        public int EmptyCount => SlotsLength - Count;
        /// <summary>Index of the last item (Count - 1; -1 when empty).</summary>
        public int EndItemIndex => Count - 1;
        /// <summary>True when GrowSize allows growth.</summary>
        public bool CanGrow => GrowSize > 0;
        /// <summary>Free slots above the top item.</summary>
        public int RoomAtTop => _SlotsLength - _ItemsTop;
        /// <summary>Free slots below the bottom item (FIFO slide headroom).</summary>
        public int RoomAtBot => _ItemsBot;
        /// <summary>True when the slots buffer is null (unopened or Reset).</summary>
        public bool IsReset => _Slots == null;
        /// <summary>True when reset AND unbound from any Heap allocator.</summary>
        public bool IsDisposed => _Slots == null && _Alloc == null;

        /// <summary>Always false (IList contract).</summary>
        public bool IsReadOnly => false;

        //========== INDEXER ==========
        
        /// <summary>Gets or sets the item at theIndex (0 to Count-1). Throws on invalid index.</summary>
        public T this[int theIndex]
        {
            get
            {
                if (theIndex >= 0 && theIndex < Count)
                {
                    return _Slots[_ItemsBot + theIndex];
                }
                throw _bad_index(theIndex);
            }
            set
            {
                if (theIndex >= 0 && theIndex < Count)
                {
                    _Slots[_ItemsBot + theIndex] = value;
                    return;
                }
                throw _bad_index(theIndex);
            }
        }

        // ========== PUBLIC METHODS ==============

#if USE_CORE
        /// <summary>Returns the items as a ReadOnlySpan (normalizes the bottom to slot 0 first).</summary>
        public ReadOnlySpan<T> AsSpan()
        {
            if (_ItemsBot != 0) _ItemsBot_to_0();
            return _Slots.AsSpan(0, Count);
        }
#endif

        /// <summary>Sets this[theIndex] = theValue, growing (sparse, DefaultItem-filled) as needed. Throws if theIndex &lt; 0.</summary>
        public void Set(int theIndex, T theValue)
        {
            if (theIndex < 0) throw _bad_index(theIndex);
            if (_ItemsBot != 0) _ItemsBot_to_0();
            if (theIndex >= _SlotsLength)
                EnsureRoom(theIndex + 1 - _ItemsTop);
            _Slots[theIndex] = theValue;
            if (theIndex >= _ItemsTop)
            {
                _CountUpdateNum++;
                _ItemsTop = theIndex + 1;
            }
        }

        /// <summary>Removes and returns the top item (LIFO). Throws when empty.</summary>
        public T Pop() => Pluck(EndItemIndex);
        
        /// <summary>Removes and returns the bottom item (FIFO; bottom slides up, no shifting). Throws when empty.</summary>
        public T Pluck() => Pluck(0);
        
        /// <summary>Appends theValue at the top and returns its item index. Grows as needed.</summary>
        public int Push(T theValue)
        {
            if (_ItemsTop >= _SlotsLength) EnsureRoom(1);
            _CountUpdateNum++;
            int iSlot = _ItemsTop++;
            _Slots[iSlot] = theValue;
            return iSlot - _ItemsBot;
        }
        
        /// <summary>Inserts theValue at the bottom (index 0).</summary>
        public void Stuff(T theValue) => Stuff(0, theValue);

        /// <summary>Inserts theValue at theIndex, shifting items up (block copy). Index Count appends. Throws on invalid index.</summary>
        public void Stuff(int theIndex, T theValue)
        {
            if (theIndex < 0 || theIndex > Count)
                throw _bad_index(theIndex);

            _CountUpdateNum++;
            int iSlot = _ItemsBot + theIndex;

            if (iSlot == _ItemsBot && _ItemsBot > 0)
            {
                // stuff below the mid Slots items
                _Slots[--_ItemsBot] = theValue;
                return;
            }

            if (RoomAtTop < 1)
                EnsureRoom(1);

            iSlot = _ItemsBot + theIndex; // Recalculate index if _ItemsBot moved to 0

            if (iSlot == _ItemsTop)
            {
                _Slots[_ItemsTop++] = theValue;
                return;
            }

            // Replace the manual backward loop with an optimized block copy.
            // Array.Copy safely handles overlapping source and destination regions when shifting up.
            int itemsToMove = _ItemsTop - iSlot;
            if (itemsToMove > 0)
            {
#if USE_CORE
                if (itemsToMove >= 16)
                    _Slots.AsSpan(iSlot, itemsToMove).CopyTo(_Slots.AsSpan(iSlot + 1));
                else
#endif
                Array.Copy(_Slots, iSlot, _Slots, iSlot + 1, itemsToMove);
            }
            _Slots[iSlot] = theValue;
            _ItemsTop++;
        }

        /// <summary>Removes and returns the item at theIndex. Bottom/top removals only trim; middle removals compress (slower). Throws on invalid index.</summary>
        public T Pluck(int theIndex)
        {
            if (theIndex < 0 || theIndex >= Count)
                throw _bad_index(theIndex);

            _CountUpdateNum++;
            int iSlot = _ItemsBot + theIndex;

            T xValue = _Slots[iSlot];

            if (iSlot == _ItemsTop - 1) // POP TOP ITEM
            {
                _Slots[iSlot] = DefaultItem;
                _ItemsTop--;
            }
            else if (iSlot == _ItemsBot) // PLUCK BOTTOM ITEM
            {
                _Slots[_ItemsBot] = DefaultItem;
                _ItemsBot++;
            }
            else // Middle item
            {
                int itemsToMove = _ItemsTop - iSlot - 1;
                // Shift items down by 1
#if USE_CORE
                if (itemsToMove >= 16)
                    _Slots.AsSpan(iSlot + 1, itemsToMove).CopyTo(_Slots.AsSpan(iSlot));
                else
#endif
                Array.Copy(_Slots, iSlot + 1, _Slots, iSlot, itemsToMove);

                // (CountUpdateNum was already bumped at method entry)

                _Slots[--_ItemsTop] = DefaultItem; // Clear the now-unused top slot
            }
            if (Count == 0)
                _ItemsBot = _ItemsTop = 0;
            return xValue;
        }

        private T[] _GetNewSlots(int iMinLength)
        {
            T[] xSlots = null;
            if (_Alloc != null)
            {
                xSlots = _Alloc.GetArray(iMinLength);
                if (xSlots != null)
                    return xSlots;
            }
            return new T[iMinLength];
        }

        /// <summary>Ensures forCount free slots above the top item, normalizing and growing (modulo GrowSize) as needed.</summary>
        public void EnsureRoom(int forCount)
        {
            // returns true/false for possible future "Freezing"

            if (forCount <= RoomAtTop) 
                return;
            
            if (_ItemsBot > 0)
            {
                _ItemsBot_to_0();
                if (forCount <= RoomAtTop)
                    return;
            }

            if (_Slots == null)
            {
                int iOpenSize = OpenSize;
                if (forCount > iOpenSize)
                {
                    int iNeedItems = forCount - OpenSize;
                    int iMoreItems = ((iNeedItems + GrowSize -1)/ GrowSize) * GrowSize; // modulo GrowSize
                    iOpenSize += iMoreItems;
                }
                _Slots = _GetNewSlots(iOpenSize);
                _SlotsLength = _Slots.Length; // pool may return LONGER than asked
                _ItemsTop = 0; // _ItemsBot was forced to 0 above
                return;
            }

            int iNeedRoom = forCount - RoomAtTop;
            int iGrowChunks = (iNeedRoom + GrowSize - 1) / GrowSize;
            int iMinNewLength = _SlotsLength + (iGrowChunks * GrowSize);
            if (iMinNewLength < OpenSize) 
                iMinNewLength = OpenSize;

            T[] xNewSlots = _GetNewSlots(iMinNewLength);

            int iTop = _ItemsTop; // preserve item top across the swap
            if (_Slots != null && iTop > 0) // not there initially or after Reset();
                Array.Copy(_Slots, 0, xNewSlots, 0, iTop);

            if (_Alloc != null) // HeapMode: offer old slots to the pool
                _Alloc.FreeArray(ref _Slots, true); // clears only if pooled; nulls _Slots

            _Slots = xNewSlots;
            _SlotsLength = xNewSlots.Length; // pool may return LONGER than asked
            _ItemsTop = iTop; // _ItemsBot was forced to 0 above
        }

        /// <summary>Removes all DefaultItem slots, compacting remaining items down.</summary>
        public void Compress()
        {
            if (_ItemsBot != 0) _ItemsBot_to_0();

            int iNewTop = 0;
            int iLen = _ItemsTop;
            int cursor = -1;
            while (++cursor < iLen)
            {
                T xItem = _Slots[cursor];
                if (!IsDefaultItem(xItem))
                {
                    if (iNewTop != cursor)
                        _Slots[iNewTop] = xItem;
                    iNewTop++;
                }
            }
#if USE_CORE
            if (_ItemsTop - iNewTop >= 32)
                _Slots.AsSpan(iNewTop, _ItemsTop - iNewTop).Clear();
            else
#endif
            Array.Clear(_Slots, iNewTop, _ItemsTop - iNewTop);
            _ItemsTop = iNewTop;
        }

        /// <summary>Clears all items to DefaultItem and Count to 0 (buffer kept).</summary>
        public void Clear()
        {
            _CountUpdateNum++;
            if (_Slots != null)
                Array.Clear(_Slots, _ItemsBot, Count);
            _ItemsBot = 0;
            _ItemsTop = 0;
        }

        /// <summary>Clears, releases the slots buffer (returned to the Heap pool when bound), and nulls it.</summary>
        public void Reset()
        {
            _CountUpdateNum++;
            Clear();
            if (_Alloc != null)
                _Alloc.FreeArray(ref _Slots, false);
            _Slots = null;
            _SlotsLength = 0;
        }

        /// <summary>Reset plus unbinds the Heap allocator. Still usable: any Push/Set/Stuff opens a new buffer.</summary>
        public void Dispose()
        {
            _CountUpdateNum++;
            Reset(); // does DidShare _Slots if sharing
            _Alloc = null; // only way to "un share"
        }

        /// <summary>Cursor enumeration bottom-up: int cursor = 0; while (GotNext(ref cursor, out item, out index)) ... After Pluck(index), do cursor-- first.</summary>
        public bool GotNext(ref int cursor, out T returnItem, out int returnIndex)
        {
            int iSlot = _ItemsBot + cursor++;
            if (iSlot >= _ItemsBot && iSlot < _ItemsTop)
            {
                returnItem = _Slots[iSlot];
                returnIndex = iSlot - _ItemsBot;
                return true;
            }
            returnItem = DefaultItem;
            returnIndex = -1;
            return false;
        }

        /// <summary>Cursor enumeration top-down: int cursor = 0; while (GotDown(ref cursor, out item, out index)) ...</summary>
        public bool GotDown(ref int cursor, out T returnItem, out int returnIndex)
        {
            int iSlot = _ItemsTop - 1 - cursor++;
            if (iSlot >= _ItemsBot && iSlot < _ItemsTop)
            {
                returnItem = _Slots[iSlot];
                returnIndex = iSlot - _ItemsBot;
                return true;
            }
            returnItem = DefaultItem;
            returnIndex = -1;
            return false;
        }

        /// <summary>Cursor search bottom-up for the next item equal to theItem; true with its index.</summary>
        public bool FoundNext(ref int cursor, T theItem, out int returnIndex)
        {
            while (GotNext(ref cursor, out T xItem, out returnIndex))
                if (EqualityComparer<T>.Default.Equals(xItem, theItem))
                    return true;
            returnIndex = -1;
            return false;
        }

        /// <summary>Cursor search top-down for the next item equal to theItem; true with its index.</summary>
        public bool FoundDown(ref int cursor, T theItem, out int returnIndex)
        {
            while (GotDown(ref cursor, out T xItem, out returnIndex))
                if (EqualityComparer<T>.Default.Equals(xItem, theItem))
                    return true;
            returnIndex = -1;
            return false;
        }

        /// <summary>Index of the first item equal to item, or -1 (linear search).</summary>
        public int IndexOf(T item)
        {
            int cursor = 0;
            if (FoundNext(ref cursor, item, out int index))
                return index;
            return -1;
        }

        /// <summary>Sorts all items using Comparer.</summary>
        public void Sort() => Sort(0, Count);
      
        /// <summary>Sorts forLength items starting at theStartingIndex using Comparer. Throws on invalid range.</summary>
        public void Sort(int theStartingIndex, int forLength)
        {
            if (_ItemsBot != 0) _ItemsBot_to_0();
            if (Count < 2 || forLength <= 1) return;
            if (theStartingIndex < 0 || theStartingIndex + forLength > Count)
                throw _bad_index(theStartingIndex);
#if USE_CORE
            _Slots.AsSpan(theStartingIndex, forLength).Sort(_Comparer);
#else
            Array.Sort(_Slots, theStartingIndex, forLength, _Comparer);
#endif
        }

        /// <summary>Binary search over all items using Comparer; index of forItem or negative.</summary>
        public int BinarySearch(T forItem) => BinarySearch(0, Count, forItem);
        
        /// <summary>Binary search over the given range using Comparer; index of forItem or negative (also -1 on invalid range).</summary>
        public int BinarySearch(int theStartingIndex, int forLength, T forItem)
        {
            if (_ItemsBot != 0) _ItemsBot_to_0();
            if (Count == 0 || theStartingIndex < 0 || forLength < 0 || theStartingIndex + forLength > Count) return -1;
            return Array.BinarySearch(_Slots, theStartingIndex, forLength, forItem, _Comparer);
        }
        
        /// <summary>Binary search over all items; always returns a valid index: the item's when found, else its sorted insertion point.</summary>
        public int BinarySearchNearest(T forItem, out bool found) => BinarySearchNearest(0, Count, forItem, out found);
        
        /// <summary>Binary search over the given range (clamped to 0..Count); always returns the found index or the sorted insertion point - Stuff(here, item) keeps sorted order.</summary>
        public int BinarySearchNearest(int theIndex, int forLength, T forItem, out bool found)
        {
            if (_ItemsBot != 0) _ItemsBot_to_0();

            // Clamp the requested range into [0, Count] rather than erroring;
            // this method always returns a valid insertion index (never -1).
            if (theIndex < 0) theIndex = 0;
            else if (theIndex > Count) theIndex = Count;
            if (forLength < 0) forLength = 0;
            else if (theIndex + forLength > Count) forLength = Count - theIndex;

            // Search the clamped range directly so we never see BinarySearch's -1 sentinel.
            int iReturn = (forLength > 0)
                ? Array.BinarySearch(_Slots, theIndex, forLength, forItem, _Comparer)
                : ~theIndex; // empty range: insertion point is the range start

            if (iReturn >= 0) // exact hit
            {
                found = true;
                return iReturn;
            }

            found = false;
            return ~iReturn; // insertion index in [0, Count]; Stuff(here, item) keeps sorted order
        }

        /// <summary>Returns each item's ToString() as a string[] (DefaultItem slots become string.Empty).</summary>
        public string[] ToStringArray()
        {
            if (_ItemsBot != 0) _ItemsBot_to_0();
            string[] xBuf = new string[Count];
            int i = -1;
            while (++i < Count)
                xBuf[i] = IsDefaultItem(_Slots[i]) ? string.Empty : _Slots[i].ToString();
            return xBuf;
        }
        /// <summary>ToStringArray then Clear.</summary>
        public string[] ToStringArray_and_Clear() { string[] x = ToStringArray(); Clear(); return x; }
        /// <summary>ToStringArray then Dispose.</summary>
        public string[] ToStringArray_and_Dispose() { string[] x = ToStringArray(); Dispose(); return x; }

        /// <summary>All items ToString(), one per line (string.Empty when empty).</summary>
        public override string ToString()
        {
            if (Count < 1) return string.Empty;
            return string.Join(Environment.NewLine, ToStringArray()) + Environment.NewLine;
        }
        /// <summary>ToString then Clear.</summary>
        public string ToString_and_Clear() { string s = ToString(); Clear(); return s; }
        /// <summary>ToString then Dispose.</summary>
        public string ToString_and_Dispose() { string s = ToString(); Dispose(); return s; }

        private T[] _ToArray(bool disposing)
        {
            // disposing should only be set when upon Dispose()

            if (_ItemsBot != 0) _ItemsBot_to_0();

            T[] xArray;

            if (disposing && Count > 0 && Count == _SlotsLength)
            {
                _CountUpdateNum++;
                xArray = _Slots;
                _Slots = null;
                _SlotsLength = 0;
                return xArray;
            }

            xArray = new T[Count];
            if (_Slots != null && Count > 0)
            {
#if USE_CORE
                if (Count >= 16)
                    _Slots.AsSpan(0, Count).CopyTo(xArray);
                else
#endif
                Array.Copy(_Slots, 0, xArray, 0, Count);
            }
            return xArray;
        }
        /// <summary>Copies the items to a new T[Count].</summary>
        public T[] ToArray() => _ToArray(false);
        /// <summary>ToArray then Clear.</summary>
        public T[] ToArray_and_Clear() { T[] x = _ToArray(false); Clear(); return x; }
        /// <summary>ToArray then Dispose. When Count fills the buffer exactly, the buffer itself is returned (no copy).</summary>
        public T[] ToArray_and_Dispose() { T[] x = _ToArray(true); Dispose(); return x; }

        // ========== PRIVATE METHODS ==============

        private void _ItemsBot_to_0()
        {
            if (_ItemsBot == 0) return;
            int iCount = _ItemsTop - _ItemsBot;   // floating items to shift down
            if (iCount > 0)
            {
                // shift the floating block down to slot 0 (overlapping, src above dst)
#if USE_CORE
                if (iCount >= 16)
                    _Slots.AsSpan(_ItemsBot, iCount).CopyTo(_Slots.AsSpan(0));
                else
#endif
                Array.Copy(_Slots, _ItemsBot, _Slots, 0, iCount);
            }
            // clear the vacated tail [iCount .. _ItemsTop)
            int iClear = _ItemsTop - iCount;
            if (iClear > 0)
            {
#if USE_CORE
                if (iClear >= 32)
                    _Slots.AsSpan(iCount, iClear).Clear();
                else
#endif
                Array.Clear(_Slots, iCount, iClear);
            }
            _ItemsBot = 0;
            _ItemsTop = iCount;

            // DOES NOT AFFECT Count so no CountUpdate;
        }

        #region // IList & ICollection MEMBERS (& Enumerator) interface

        /// <summary>IList: same as Push.</summary>
        public void Add(T theItem) => Push(theItem);
        /// <summary>True if an equal item is present (linear search).</summary>
        public bool Contains(T theItem) => IndexOf(theItem) >= 0;
        /// <summary>IList: same as Stuff(index, item).</summary>
        public void Insert(int index, T item) => Stuff(index, item);
        /// <summary>IList: same as Pluck(index) (return value discarded).</summary>
        public void RemoveAt(int index) => Pluck(index);
        /// <summary>Copies the items into array at arrayIndex. Throws on null, bad index, or insufficient room.</summary>
        public void CopyTo(T[] array, int arrayIndex)
        {
            if (array == null) throw new ArgumentNullException(nameof(array));
            if (arrayIndex < 0) throw _bad_index(arrayIndex);
            if (array.Length - arrayIndex < Count)
                throw new ArgumentException($"Destination array is too small. Needs space for {Count} items.");
            if (Count > 0)
                Array.Copy(_Slots, _ItemsBot, array, arrayIndex, Count);
        }
        /// <summary>Removes the first item equal to theItem; true if one was removed.</summary>
        public bool Remove(T theItem)
        {
            int index = IndexOf(theItem);
            if (index >= 0)
            {
                Pluck(index);
                return true;
            }
            return false;
        }

        /// <summary>Allocation-free struct enumerator (foreach support). Throws InvalidOperationException if the stack's Count changes mid-enumeration (see CountUpdateNum).</summary>
        public struct Cursor : IEnumerator<T>
        {
            // Value type struct Enumerator avoids garbage collection allocation churn
            private readonly ItemStack<T> _stack;
            private T _item;
            private int _cursor; //IS ITEM INDEX, NOT THE SLOT INDEX
            private uint _UpdateNum;
            /// <summary>Creates a cursor over the stack, capturing its CountUpdateNum for the change guard.</summary>
            public Cursor(ItemStack<T> stack)
            {
                _stack = stack;
                _cursor = -1;
                _item = default;
                _UpdateNum = stack.CountUpdateNum;
            }
            /// <summary>The item at the cursor (default before the first MoveNext).</summary>
            public T Current => _item;
            object IEnumerator.Current => _item;

            /// <summary>True when the stack's Count has changed since this cursor was created or Reset (the next MoveNext will throw).</summary>
            public bool CountChanged => _UpdateNum != _stack.CountUpdateNum;

            /// <summary>Advances to the next item (bottom-up); false at end. Throws InvalidOperationException when <see cref="CountChanged"/> (item CHANGES are fine, count changes are not).</summary>
            public bool MoveNext()
            {
                // Item changes OK, just not CountChanged
                if (CountChanged)
                    throw new InvalidOperationException($"Stack count changed: " + nameof(_stack));
                int next = _cursor + 1;
                if (next >= 0 && next < _stack.Count)
                {
                    _cursor = next;
                    _item = _stack[_cursor];
                    return true;
                }
                return false;
            }

            /// <summary>Rewinds the cursor and re-captures the count guard.</summary>
            public void Reset()
            {
                _UpdateNum = _stack.CountUpdateNum;
                _cursor = -1;
                _item = _stack.DefaultItem;
            }

            /// <summary>No-op (nothing to release in a struct cursor).</summary>
            public void Dispose() { }
        }

        /// <summary>Returns a struct Cursor enumerator over the items (bottom-up).</summary>
        public Cursor GetEnumerator() => new Cursor(this);
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion

    }
}
