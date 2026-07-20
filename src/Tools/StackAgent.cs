// Copyright (c) GDFrank
// SPDX-License-Identifier: MIT

using System;
using System.Collections;
using System.Collections.Generic;

namespace Source77NW
{
    /// <summary>
    /// A read-only agent facade over an <see cref="ItemStack{T}"/>
    /// core: only non-mutating members are exposed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the deliberate extension surface of the core/agent
    /// pattern: the data-access members are VIRTUAL so subclasses can
    /// add invariants, tallies, or locking - the cost is paid only
    /// when overridden. The <see cref="ItemStack{T}"/> core itself
    /// stays sealed and bare-metal; wrap, don't derive.
    /// </para>
    /// <para>
    /// The sizing constructors hand the newly created MUTABLE core
    /// back to the creator via an out parameter, so the creator keeps
    /// write access while consumers of the agent see only the
    /// read-only surface. The protected parameterless constructor is
    /// for subclasses that build their own core: they MUST assign
    /// <see cref="Stack"/> before any member is used.
    /// </para>
    /// </remarks>
    public class StackAgent<T> : IEnumerable<T>
    {
        /// <summary>Creates an agent over a new stack of the given open size, returning the mutable core to the creator.</summary>
        public StackAgent(int theOpenSize, out ItemStack<T> returnStack)
        {
            Stack = returnStack = new ItemStack<T>(theOpenSize);
        }

        /// <summary>Creates an agent over a new stack holding theArray's items, returning the mutable core to the creator.</summary>
        public StackAgent(T[] theArray, out ItemStack<T> returnStack)
        {
            Stack = returnStack = new ItemStack<T>(theArray);
        }

        /// <summary>Creates an agent over an existing stack.</summary>
        public StackAgent(ItemStack<T> theStack)
        {
            Stack = theStack;
        }

        /// <summary>For subclasses building their own core: assign <see cref="Stack"/> before use.</summary>
        protected StackAgent()
        {
        }

        /// <summary>The wrapped core; subclasses have full (mutating) access.</summary>
        protected ItemStack<T> Stack;

        /// <summary>The stack's default item (default(T)).</summary>
        public T DefaultItem => Stack.DefaultItem;
        /// <summary>The item type T.</summary>
        public Type ItemType => Stack.ItemType;
        /// <summary>True when theItem equals the default item.</summary>
        public bool IsDefault(T theItem) => Stack.IsDefaultItem(theItem);

        /// <summary>The item count.</summary>
        public virtual int Count => Stack.Count;

        /// <summary>The item at the index.</summary>
        public virtual T this[int index] => Stack[index];

        /// <summary>The stack's struct enumerator (no allocation in foreach).</summary>
        public virtual ItemStack<T>.Cursor GetEnumerator() => Stack.GetEnumerator();
        IEnumerator<T> IEnumerable<T>.GetEnumerator() { return GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }

        /// <summary>Enumerates items bottom-up: cursor=0; call until false.</summary>
        public virtual bool GotNext(ref int cursor, out T returnItem, out int returnIndex)
            => Stack.GotNext(ref cursor, out returnItem, out returnIndex);

        /// <summary>Enumerates items top-down: cursor=0; call until false.</summary>
        public virtual bool GotDown(ref int cursor, out T returnItem, out int returnIndex)
            => Stack.GotDown(ref cursor, out returnItem, out returnIndex);

        /// <summary>Finds theItem's next occurrence bottom-up; cursor=0; false when no more.</summary>
        public virtual bool FoundNext(ref int cursor, T theItem, out int returnIndex)
            => Stack.FoundNext(ref cursor, theItem, out returnIndex);

        /// <summary>Finds theItem's next occurrence top-down; cursor=0; false when no more.</summary>
        public virtual bool FoundDown(ref int cursor, T theItem, out int returnIndex)
            => Stack.FoundDown(ref cursor, theItem, out returnIndex);

        /// <summary>Binary-searches a sorted stack for theItem (see the core's contract).</summary>
        public virtual int BinarySearch(T theItem)  => Stack.BinarySearch(theItem);

        /// <summary>Copies the items to a new string array via ToString.</summary>
        public virtual string[] ToStringArray() => Stack.ToStringArray();

        /// <summary>Copies the items to a new array.</summary>
        public virtual T[] ToArray()      => Stack.ToArray();

        /// <summary>The core's text form.</summary>
        public override string ToString() => Stack.ToString();

    }
}
