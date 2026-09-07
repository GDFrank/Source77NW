// Copyright (c) GDFrank - 77NW.net. All rights reserved.
// Licensed under the MIT License.
// SPDX-License-Identifier: MIT

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Source77NW
{
    /// <summary>
    /// NT - isolated NT system functions, grouped so call sites read as
    /// NT.&lt;function&gt; / NT.&lt;Subject&gt;.&lt;function&gt;. Everything
    /// here is best-effort bool: on non-Windows targets (or when a shell
    /// call fails) the operations simply return false rather than throw -
    /// the Windows-ness is contained inside this class, not pushed onto
    /// callers.
    /// </summary>
    /// <remarks>
    /// Curated 2026-07-26 from AppLab's static partial NT grouping class as
    /// Sys (so "NT" stayed the tier name while Sys was the call shape);
    /// renamed BACK to NT 2026-07-30 - "Sys" read as ambiguous/generic
    /// (System-adjacent) where "NT" states plainly "this is NT talk",
    /// matching the tier name (folder/dll/project) exactly, with no
    /// translation between them. Lives in the pure classification folder
    /// src\NT.Base\ (siblings src\NT.Forms\, src\NT.WPF\ reserved for
    /// Forms/WPF-specific modules as they arrive). Partial by design -
    /// direct functions arrive as sibling NT-&lt;name&gt;.cs files; subject
    /// classes nest INSIDE NT as partial files (NT.&lt;Name&gt;.cs), keeping
    /// their AppLab names (NT.Recycle, NT.Mgmt, NT.MediaDrive...) - no new
    /// top-level names to choose. The AppLab #if !NO_DLLIMPORT / !X32 gates did not travel:
    /// this repo defines neither flag; the 32-bit-process limitation is
    /// handled at runtime (see Recycle).
    /// </remarks>
    public static partial class NT
    {
        /// <summary>
        /// Attempts graceful shutdown (CloseMainWindow + WaitForExit 2s) of
        /// every running process whose name matches the file name (without
        /// extension) of theExeFullName, falling back to Kill() when graceful
        /// fails. True when all matched processes were stopped or were
        /// already gone; false for an invalid/absent path or any survivor.
        /// </summary>
        public static bool StoppedProcess(string theExeFullName)
        {
            theExeFullName = FS.ValidPath_or_null(theExeFullName, out _);

            if (theExeFullName == null || !File.Exists(theExeFullName))
            {
                return false;
            }

            string sNameOnly = FS.FileNameOnly_or_null(theExeFullName, out _);

            // NOTE: FS.FileNameOnly_or_null strips the extension (e.g. "myapp" from "myapp.exe"),
            // matching Process.ProcessName which also omits the extension on Windows.
            // Do not change this helper without verifying the match still holds.

            if (sNameOnly == null) return false;

            int iFailedCount = 0;

            Process[] xList = Process.GetProcessesByName(sNameOnly);

            for (int i = 0; i < xList.Length; i++)
            {
                Process x = xList[i];

                bool bOK = true;

                try
                {
                    x.CloseMainWindow();

                    try
                    {
                        if (x.WaitForExit(2000))
                        {
                            x.Close();
                            bOK = true;
                        }
                        else
                        {
                            bOK = false;
                        }
                    }
                    catch
                    {
                        bOK = false;
                    }
                }
                catch (Exception e)
                {
                    bOK = e is InvalidOperationException;
                }

                if (!bOK)
                {
                    try
                    {
                        x.Kill();
                        bOK = true;
                    }
                    catch (Exception e)
                    {
                        bOK = e is InvalidOperationException;
                    }
                }

                if (!bOK)
                {
                    iFailedCount++;
                }

                try { x.Dispose(); } catch { }
            }

            return iFailedCount == 0;
        }

        /// <summary>
        /// The Windows Recycle Bin: <see cref="DidSend"/> moves a file to the
        /// bin (shell recycle, undo-able); <see cref="DidEmptyBin"/> empties
        /// it (no confirmation, sound, or progress UI). 64-bit process on a
        /// 64-bit Windows only - both return false everywhere else.
        /// </summary>
        public static class Recycle
        {
            /// <summary>Moves thePath to the Recycle Bin via the shell (SHFileOperation, FOF_ALLOWUNDO); true on success. False on non-Windows, 32-bit process/OS, or shell failure. If the bin is disabled for the target drive the shell may permanently delete yet still report success (shell limitation).</summary>
            public static bool DidSend(string thePath)
            {
                if (!Exe.Is64BitOS) return false;

                // NOTE: 32-bit process is explicitly not supported.
                // SHFILEOPSTRUCT requires Pack=1 on 32-bit,
                // which needs a separate struct declaration.

                try
                {
                    if (Exe.Is64BitProcess)
                        return X64.DidSend(thePath);
                }
                catch { }

                return false;
            }

            /// <summary>Empties the Recycle Bin (SHEmptyRecycleBin; no confirmation, sound, or progress UI); true on success, false on non-Windows or shell failure.</summary>
            public static bool DidEmptyBin()
            {
                try
                {
                    return 0 == SHEmptyRecycleBin(IntPtr.Zero, null, RecycleFlag.SHERB_NOSOUND | RecycleFlag.SHERB_NOCONFIRMATION | RecycleFlag.SHERB_NOPROGRESSUI);
                }
                catch { }

                return false;
            }

            private enum RecycleFlag : int
            {
                SHERB_NOCONFIRMATION = 0x00000001, // No confirmation when emptying
                SHERB_NOPROGRESSUI = 0x00000002,
                SHERB_NOSOUND = 0x00000004, // No sound when emptying is complete
            }

            [DllImport("Shell32.dll")]
            private static extern int SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, RecycleFlag dwFlags);

            // NOTE: X64 name reflects that this struct layout (without Pack=1) is required
            // for 64-bit processes. A 32-bit process would need the same struct with Pack=1.
            private static class X64
            {
                public static bool DidSend(string path)
                {
                    const int FO_DELETE = 3;
                    const int FOF_ALLOWUNDO = 0x40;
                    const int FOF_NOCONFIRMATION = 0x0010;

                    // NOTE: FOF_ALLOWUNDO causes the shell to recycle rather than delete.
                    // If the Recycle Bin is disabled for the target drive, the file may be
                    // permanently deleted while this method still returns true (shell limitation).
                    SHFILEOPSTRUCT args = new SHFILEOPSTRUCT
                    {
                        wFunc = FO_DELETE,
                        pFrom = path + '\0' + '\0',
                        fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION
                    };

                    return 0 == SHFileOperation(ref args);
                }

                [DllImport("shell32.dll", CharSet = CharSet.Auto)]
                private static extern int SHFileOperation(ref SHFILEOPSTRUCT FileOp);

                [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)] // No Pack=1 - required for x64
                private struct SHFILEOPSTRUCT
                {
                    public IntPtr hwnd;
                    [MarshalAs(UnmanagedType.U4)]
                    public int wFunc;
                    public string pFrom;
                    public string pTo;
                    public short fFlags;
                    [MarshalAs(UnmanagedType.Bool)]
                    public bool fAnyOperationsAborted;
                    public IntPtr hNameMappings;
                    public string lpszProgressTitle;
                }

                /* https://stackoverflow.com/questions/2342628/deleting-file-to-recycle-bin-on-windows-x64-in-c-sharp
                 * Under x64, the SHFILEOPSTRUCT must be declared without the Pack = 1 parameter, or it will fail.
                 *
                 * http://msdn.microsoft.com/en-us/library/bb762164(v=VS.85).aspx
                 * http://social.msdn.microsoft.com/Forums/en-US/netfx64bit/thread/dd2eea32-8a6b-4c5a-8acc-568020d558cb/
                 */
            }
        } // Recycle
    } // NT
} // namespace
