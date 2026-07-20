// Copyright (c) GDFrank
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Threading;

namespace Source77NW
{
    /// <summary>
    /// Centralized mutex and file locking: named locks are acquired once
    /// and shared via a per-name registry, released individually or all
    /// at shutdown.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="GotLock(string, KindId, out ExeLock, out Issue)"/>
    /// returns the existing registered lock when the same kind+name is
    /// already held (a shared instance - one <see cref="Release"/>
    /// releases it for all holders). Mutex kinds wait about 10 seconds
    /// for acquisition; an abandoned mutex counts as acquired.
    /// <see cref="KindId.File"/> locks open (or create) the file
    /// EXCLUSIVELY (FileShare.None) - the exclusive open IS the lock
    /// (OS-enforced on Windows; advisory between processes on Unix).
    /// </para>
    /// <para>
    /// The registry is thread-safe. <see cref="DisposeAll"/> releases
    /// everything (application shutdown).
    /// </para>
    /// </remarks>
    public sealed class ExeLock
    {
        private ExeLock() { }

        /// <summary>
        /// Lock naming kind: how the given name is scoped before use.
        /// </summary>
        public enum KindId : byte
        {
            /// <summary>The name is prefixed with @"Global\" (OS-global: shared across all sessions).</summary>
            Global = 0,
            /// <summary>The name is prefixed with the domain guid/name.</summary>
            Domain = 1,
            /// <summary>The name is prefixed with the exe guid (or domain@exe code name).</summary>
            Exe = 2,
            /// <summary>The name is a file path; the file is held open exclusively (the open is the lock).</summary>
            File = 3,
        }

        private static string _DomainPrefix()
        {
            string sPrefix = Exe.DomainGuid;

            if (string.IsNullOrEmpty(sPrefix))
            {
                sPrefix = Exe.DomainName;
            }

            return sPrefix + AS.AT;
        }
        private static string _ExePrefix()
        {
            string sPrefix = Exe.ExeGuid;

            if (string.IsNullOrEmpty(sPrefix))
            {
                sPrefix = Exe.DomainName + AS.AT + Exe.ExeCodeName;
            }

            return sPrefix + AS.AT;
        }

        /// <summary>Gets (or reuses) the lock of the kind and name; false when not acquired.</summary>
        public static bool GotLock(string theMutexNameOrFilePath, KindId theId, out ExeLock returnLock) => GotLock(theMutexNameOrFilePath, theId, out returnLock, out _);

        /// <summary>
        /// Gets (or reuses) the lock of the kind and name, surfacing any
        /// Issue; false when the name is empty/invalid or acquisition
        /// fails (mutex wait timeout, file already locked, etc.).
        /// </summary>
        public static bool GotLock(string theMutexNameOrFilePath, KindId theId, out ExeLock returnLock, out Issue returnIssue)
        {
            returnLock = null; returnIssue = null;

            string sLockName = theMutexNameOrFilePath?.Trim();

            if (string.IsNullOrEmpty(sLockName))
            {
                returnIssue = Issue.Create(issueSource, 11, IssueKind.BadParam);

                return false;
            }

            lock (_locks)
            {
                switch (theId)
                {
                    case KindId.Global:

                        sLockName = @"Global\" + sLockName;

                        break;

                    case KindId.File:

                        sLockName = FS.ValidPath_or_null(sLockName, out returnIssue);

                        if (returnIssue != null) return false;

                        break;

                    case KindId.Domain:

                        sLockName = _DomainPrefix() + sLockName;

                        break;

                    case KindId.Exe:

                        sLockName = _ExePrefix() + sLockName;

                        break;
                }

                int cursor = 0;

                while (_locks.GotDown(ref cursor, out ExeLock xItem, out _))
                {
                    if (xItem.Kind == theId && xItem._LockName == sLockName)
                    {
                        returnLock = xItem;

                        return true;
                    }
                }

                if (theId != KindId.File)
                {
                    Mutex xMutex = _get_locked_mutex_or_null(sLockName, out returnIssue);

                    if (xMutex != null)
                    {
                        returnLock = new ExeLock()
                        {
                            Kind = theId,
                            _LockName = sLockName,
                            _mutex = xMutex,
                        };

                        _locks.Push(returnLock);

                        return true;
                    }
                }
                else
                {
                    FileStream xStream = _get_locked_stream_or_null(sLockName, out returnIssue);

                    if (xStream != null)
                    {
                        returnLock = new ExeLock()
                        {
                            Kind = theId,
                            _LockName = sLockName,
                            _stream = xStream,
                        };

                        _locks.Push(returnLock);

                        return true;
                    }
                }
            }

            returnLock = null;

            return false;
        }

        /// <summary>Releases and removes every registered lock (application shutdown).</summary>
        public static void DisposeAll()
        {
            lock (_locks)
            {
                while (_locks.Count > 0)
                {
                    ExeLock xLock = _locks.Pop();
                    xLock._Dispose();
                }

                _locks.Reset();
            }
        }

        private const ushort issueSource = 65112;

        private const int _default_wait_millisecs = 10222; // 10.222 seconds

        private static ItemStack<ExeLock> _locks = new ItemStack<ExeLock>(4);

        private static Mutex _get_locked_mutex_or_null(string theMutexName, out Issue xIssue)
        {
            xIssue = null; Mutex xMutex = null;

            try
            {
                xMutex = new Mutex(false, theMutexName, out bool bCreatedNew);

                // don't care about getting initial ownership, so false
                // and most likely, we already own it

                if (xMutex.WaitOne(_default_wait_millisecs))
                {
                    return xMutex;
                }

                xMutex.Dispose();
            }
            catch (AbandonedMutexException)
            {
                // Mutex was abandoned by previous owner; we now own it.
                return xMutex;
            }
            catch (Exception e)
            {
                xIssue = Issue.Create(issueSource, 33, e, IssueKind.ProgramIssue);
                try { xMutex.Dispose(); } catch { }
            }

            return null;
        }

        private static FileStream _get_locked_stream_or_null(string sFilePath, out Issue xIssue)
        {
            // Opens OR CREATES the file EXCLUSIVELY (FileShare.None) -
            // the exclusive open IS the lock (G 2026-07-17: whole-file
            // only; the 16-byte range lock retired - Windows belt-and-
            // suspenders, unsupported on macOS). null & xIssue on
            // invalid path, access denial, or an already-held lock.

            xIssue = null;

            FileStream xLock = null;

            try
            {
                xLock = File.Open(sFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

                return xLock;
            }
            catch (Exception e)
            {
                xLock?.Close();    // close if partially opened
                xIssue = Issue.Create(issueSource, 0, e, IssueKind.ProgramIssue);
            }

            return null;
        }


        // ================  INSTANCE MEMBERS

        private Mutex _mutex;

        private FileStream _stream;

        private string _LockName;

        /// <summary>The lock's naming kind.</summary>
        public KindId Kind { get; private set; }

        /// <summary>The lock kind and its scoped name.</summary>
        public override string ToString()
        {
            return " Lock:"
                + Kind.ToString()
                + " SimpleName:" + _LockName;
        }

        /// <summary>Releases this lock and removes it from the registry (shared instance: released for all holders).</summary>
        public void Release()
        {
            lock (_locks)
            {
                int cursor = 0;

                while (_locks.GotDown(ref cursor, out ExeLock xLock, out int iLock))
                {
                    if (Equals(xLock))
                    {
                        _Dispose();

                        _locks.Pluck(iLock);

                        return;
                    }
                }
            }
        }

        private void _Dispose()
        {
            _mutex?.Dispose();
            _mutex = null;

            _stream?.Close();
            _stream = null;

            _LockName = null;
        }

    }
}
