// CopyrightCopyright (c) GDFrank
// SPDX-License-Identifier: MIT

using System;
using System.Threading;

namespace Source77NW
{
    /// <summary>
    /// A keyed registry of timer-driven pulsers: each started Pulser
    /// beats a <see cref="PulseDO"/> callback with its Handle and a
    /// running pulse count.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Instances are created only by <see cref="Started"/> (the
    /// factory), which registers the pulser under a caller-supplied
    /// key; the returned Handle is a process-unique id usable for
    /// lookup and disposal. A key is REQUIRED: a null key starts
    /// nothing and returns false.
    /// </para>
    /// <para>
    /// Callbacks run on thread-pool threads via
    /// <see cref="System.Threading.Timer"/>; a pulse already in
    /// flight may still deliver after disposal (disposal does not
    /// barrier the callback), and a callback slower than the beat
    /// period may overlap the next pulse (inherent to Timer).
    /// </para>
    /// </remarks>
    public sealed class Pulser : IDisposable
    {
        /// <summary>The pulse callback: receives the pulser's Handle and the 1-based pulse count.</summary>
        public delegate void PulseDO(uint theHandle, uint thePulseCount);

        /// <summary>
        /// Creates, registers, and starts a pulser beating theDO after
        /// the delay then every beat interval; false (Handle 0) when
        /// theKey is null or already registered.
        /// </summary>
        public static bool Started
            ( PulseDO thePulseDO
            , object theKey
            , int theDelayBeating_millisecs
            , int theThenBeatEvery_millisecs
            , out uint returnHandle)
        {
            returnHandle = 0;

            if (theKey == null)
            {
                return false;
            }

            lock (_pulsers)
            {
                int i = _pulsers_IndexOf(theKey); // null returns -1

                if (i >= 0)
                {
                    return false; // already running
                }

                Pulser xPulser = new Pulser();

                _pulsers.Push(xPulser);

                xPulser._Start // AKA Restart
                    ( thePulseDO
                    , theDelayBeating_millisecs
                    , theThenBeatEvery_millisecs
                    , theKey
                    );

                returnHandle = xPulser.Handle;

                return true;
            }
        }

        /// <summary>True (with the Handle) when theKey has a registered pulser; else false and 0.</summary>
        public static bool GotHandle_of_Key(object theKey, out uint returnHandle)
        {
            lock (_pulsers)
            {
                int i = _pulsers_IndexOf(theKey);

                if (i >= 0)
                {
                    returnHandle = _pulsers[i].Handle;

                    return true;
                }
            }

            returnHandle = 0;

            return false;
        }

        /// <summary>True (with the key) when theHandle has a registered pulser; else false and null.</summary>
        public static bool GotKey_of_Handle(uint theHandle, out object returnKey)
        {
            lock (_pulsers)
            {
                int i = _pulsers_IndexOf(theHandle);

                if (i >= 0)
                {
                    returnKey = _pulsers[i]._Key;

                    return true;
                }
            }

            returnKey = null;

            return false;
        }

        /// <summary>Stops and unregisters every pulser.</summary>
        public static void DisposeAll()
        {
            lock (_pulsers)
            {
                while (_pulsers.EndItemIndex >= 0)
                {
                    Pulser xPulser = _pulsers.Pluck(_pulsers.EndItemIndex);

                    xPulser._Dispose();
                }
            }
        }

        /// <summary>Stops and unregisters theKey's pulser, if registered.</summary>
        public static void Dispose(object theKey)
        {
            lock (_pulsers)
            {
                int i = _pulsers_IndexOf(theKey);

                if (i >= 0)
                {
                    Pulser xPulser = _pulsers.Pluck(i);

                    xPulser.Dispose();
                }
            }
        }

        /// <summary>Stops and unregisters theHandle's pulser, if registered.</summary>
        public static void Dispose(uint theHandle)
        {
            lock (_pulsers)
            {
                int i = _pulsers_IndexOf(theHandle);

                if (i >= 0)
                {
                    Pulser xPulser = _pulsers.Pluck(i);

                    xPulser.Dispose();
                }
            }
        }

        private static readonly ItemStack<Pulser> _pulsers = new ItemStack<Pulser>(3);

        private static int _pulsers_HandleCounter = 0;

        private static int _pulsers_IndexOf(object theKey)
        {
            if (theKey == null) return -1;

            int i = -1;

            while (++i < _pulsers.Count)
            {
                object xKey = _pulsers[i]._Key;

                if (xKey != null && xKey.Equals(theKey))
                {
                    return i;
                }
            }
            return -1;
        }

        private static int _pulsers_IndexOf(uint theHandle)
        {
            int i = -1;

            while (++i < _pulsers.Count)
            {
                if (_pulsers[i].Handle == theHandle)
                {
                    return i;
                }
            }

            return -1;
        }


        // INSTANCE ============================

        internal Pulser() { } // preserves pre-curation effective visibility; Started is the factory

        /// <summary>Hash of the Handle.</summary>
        public override int GetHashCode() => Handle.GetHashCode();

        /// <summary>The process-unique pulser id, assigned at construction.</summary>
        public uint Handle { get; private set; } = (uint)Interlocked.Increment(ref _pulsers_HandleCounter);

        private PulseDO _DO_Pulse;

        private Timer _SystemTimer;

        private object _Key;

        private uint _PulseCounter = 0;

        private void _Upon_pulse(object theObject)
        {
            if (theObject is uint iHandle)
            {
                _DO_Pulse?.Invoke(iHandle, ++_PulseCounter);
            }
        }

        private void _Start(PulseDO theDO, int thePulseDelay_millisecs, int thePulseEvery_millisecs, object theKey)
        {
            _DO_Pulse = theDO;

            _PulseCounter = 0;

            _Key = theKey;

            _SystemTimer = new Timer
                ( _Upon_pulse
                , Handle
                , thePulseDelay_millisecs
                , thePulseEvery_millisecs);
        }

        /// <summary>Stops this pulser and unregisters it, if still registered.</summary>
        public void Dispose()
        {
            // item may already have been Pluck()'d by the static
            // Dispose(key/Handle) paths - double-dispose is handled.

            lock (_pulsers)
            {
                _Dispose();

                int i = _pulsers_IndexOf(Handle);

                if (i >= 0)
                {
                     _pulsers.Pluck(i);
                }
            }
        }

        private void _Dispose()
        {
            _DO_Pulse = null;

            _SystemTimer?.Dispose();

            _SystemTimer = null;
        }

    }
}
