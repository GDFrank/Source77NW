// CopyrightCopyright (c) GDFrank
// SPDX-License-Identifier: MIT

using System;

namespace Source77NW
{
    /// <summary>
    /// An Action&lt;T&gt; event invoker: listeners add/remove themselves
    /// through locally supplied methods, and <see cref="Notify"/>
    /// broadcasts to them in LIFO sequence. Meant as a private tool of
    /// the owning type.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Thread safe: locks around list activity; callbacks are invoked
    /// OUTSIDE the lock (snapshot) to prevent deadlock. Listener
    /// exceptions are swallowed per-listener (isolation): a throwing
    /// listener does not block the remaining broadcast or wedge the
    /// instance. Adding is
    /// refused while notifying or disposed; removing while notifying
    /// nulls the slot for later compression - the removed listener may
    /// still receive the in-flight notification (it was in the
    /// snapshot).
    /// </para>
    /// <para>
    /// <see cref="NotifyAndDispose"/> marks the instance disposed BEFORE
    /// the final broadcast, so listeners cannot re-register during it.
    /// </para>
    /// </remarks>
    public sealed class Listeners<T> : IDisposable
    {
        /// <summary>Creates with the initial/grow size (minimum 4).</summary>
        public Listeners(int theGrowSize)
        {
            if (theGrowSize < 4) theGrowSize = 4;

            _Listeners = new ItemStack<Action<T>>(theGrowSize);
        }

        private ItemStack<Action<T>> _Listeners;


        private volatile bool _NeedsCompressing = false;

        private volatile bool _IsNotifying = false;

        private volatile bool _IsDisposed = false;

        /// <summary>True while broadcasting to listeners.</summary>
        public bool IsNotifying => _IsNotifying;

        /// <summary>True when disposing or disposed.</summary>
        public bool IsDisposed => _IsDisposed;

        /// <summary>Count of registered listeners.</summary>
        public int Count => _Listeners.Count;

        /// <summary>
        /// Adds (true) or removes (false) the listener. Add returns false
        /// when notifying, disposed, null, or already added; remove
        /// returns false when disposed, null, or not present (a removal
        /// during notify takes effect after the broadcast).
        /// </summary>
        public bool Listener(Action<T> theDO, bool add_else_remove)
        {
            if (IsDisposed || theDO == null) return false;

            lock (_Listeners)
            {
                int i1 = _Listeners.Count;

                while (--i1 >= 0)
                {
                    if (_Listeners[i1].Equals(theDO))
                    {
                        break;
                    }
                }

                // ADD
                if (add_else_remove)
                {
                    if (IsNotifying || i1 >= 0)
                    {
                        return false;
                    }

                    _Listeners.Push(theDO);

                    return true;
                }

                // REMOVE
                if (i1 < 0)
                {
                    return false;
                }

                if (IsNotifying)
                {
                    _NeedsCompressing = true;

                    _Listeners[i1] = null;
                }
                else
                {
                    _Listeners.Pluck(i1);
                }

                return true;
            }
        }

        private void _invoke_unless_null(Action<T> xItem, T iNotification)
        {
            try
            {
                xItem?.Invoke(iNotification);
            }
            catch
            {
                // per-listener isolation (G 2026-07-17): a throwing
                // listener must not block the remaining listeners or
                // wedge the instance; the failure is the listener's.
            }
        }

        private bool _Notify(T theNotification, bool andDispose)
        {
            Action<T>[] xSnapshot;

            lock (_Listeners)
            {
                if (IsDisposed || IsNotifying || theNotification == null) return false;

                _IsNotifying = true;

                _IsDisposed = andDispose;

                // Create snapshot of listeners to invoke outside the lock
                int count = _Listeners.Count;
                xSnapshot = new Action<T>[count];
                for (int i = 0; i < count; i++)
                {
                    xSnapshot[i] = _Listeners[i];
                }
            }

            // Invoke callbacks outside the lock to prevent deadlock
            if (_IsDisposed)
            {
                // LIFO sequence for dispose
                for (int i = xSnapshot.Length - 1; i >= 0; i--)
                {
                    _invoke_unless_null(xSnapshot[i], theNotification);
                }

                Dispose();
            }
            else
            {
                // LIFO sequence for normal notify
                for (int i = xSnapshot.Length - 1; i >= 0; i--)
                {
                    _invoke_unless_null(xSnapshot[i], theNotification);
                }

                lock (_Listeners)
                {
                    if (_NeedsCompressing)
                    {
                        _Listeners.Compress();
                        _NeedsCompressing = false;
                    }

                    _IsNotifying = false;
                }
            }

            return true;
        }

        /// <summary>Broadcasts to listeners in LIFO sequence; false when disposed, already notifying, or null notification.</summary>
        public bool Notify(T theNotification) => _Notify(theNotification, false);

        /// <summary>Broadcasts a final notification (instance marked disposed first) then disposes.</summary>
        public bool NotifyAndDispose(T theNotification) => _Notify(theNotification, true);

        /// <summary>Removes all listeners and marks the instance disposed.</summary>
        public void Dispose()
        {
            lock (_Listeners)
            {
                _IsDisposed = true;
                _IsNotifying = false;
                _Listeners.Dispose();
            }
        }

    }
}
