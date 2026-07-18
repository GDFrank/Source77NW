// Copyright (c) GDFrank
// SPDX-License-Identifier: MIT

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Linq;
using System.Runtime.InteropServices;
#if NET5_0_OR_GREATER
using System.Runtime.Versioning;
#endif

namespace Source77NW
{
    /// <summary>
    /// Centralized identity of the running executable, established once by
    /// the static initializer from the EntryAssembly's [Assembly*]
    /// attributes and the current process. Also hosts the process-wide
    /// critical-issue handler (<see cref="SetCritical"/>/<see cref="Critical(Issue)"/>)
    /// and logging hook (<see cref="SetLogging"/>/<see cref="Log"/>), each
    /// settable exactly once.
    /// </summary>
    /// <remarks>
    /// UNCHECKED contract: at first touch the initializer verifies the
    /// Source77NW library was compiled WITHOUT arithmetic overflow checking
    /// (all library code assumes unchecked); it does not impose that on the
    /// EntryAssembly. A checked build fails immediately with an Issue.
    ///
    /// Identity metadata is declared in the EntryAssembly, typically split
    /// as EntryAssembly.Domain.cs (domain-wide) + EntryAssembly.&lt;code&gt;.cs
    /// (per-exe):
    /// <code>
    /// [assembly: AssemblyCompany("&lt;companyName&gt;")]
    /// [assembly: AssemblyCopyright("&lt;copyright&gt;")]
    /// [assembly: AssemblyMetadata("Contact", "&lt;contactInfo&gt;")]
    /// [assembly: AssemblyMetadata("DomainName", "&lt;domainName&gt;")]
    /// [assembly: AssemblyMetadata("DomainGuid", "&lt;domainGuid&gt;")]
    /// #if DEBUG
    /// [assembly: AssemblyMetadata("DeployDebug", "debug")]
    /// #endif
    /// #if ALPHA
    /// [assembly: AssemblyMetadata("DeployStage", "alpha")]
    /// #elif BETA
    /// [assembly: AssemblyMetadata("DeployStage", "beta")]
    /// #endif
    /// #if CONSOLE
    /// [assembly: AssemblyMetadata("ExeInterface", "console")]
    /// #elif SERVICE
    /// [assembly: AssemblyMetadata("ExeInterface", "service")]
    /// #endif
    ///
    /// [assembly: AssemblyProduct(@"&lt;productName&gt;")]
    /// [assembly: AssemblyMetadata("ExeCodeName", "&lt;assemblyCode&gt;")]
    /// [assembly: Guid(@"&lt;exeGuid&gt;")]
    /// [assembly: AssemblyVersion(version: "&lt;exeVersion&gt;")]
    /// </code>
    /// Missing metadata degrades softly: DomainName falls back to the exe
    /// file name, ExeCodeName to the exe name, ExeVersion 0.0.0.0 to a
    /// timestamp-derived version.
    /// A static-constructor Issue (unchecked build guard, missing exe
    /// path) surfaces to the first caller wrapped in a
    /// TypeInitializationException with the Issue as InnerException.
    /// </remarks>
    public static class Exe
    {
        /// <summary>Well-known per-domain folder roots resolved by
        /// <see cref="DomainFolderPath"/>. Values MAY BE PERSISTED and the
        /// coding pattern requires a byte-backed enum - do not renumber or
        /// widen.</summary>
        public enum DomainFolderId : byte
        {
            /// <summary>{UserProfile}\{DomainName}\</summary>
            UserProfile = 0,
            /// <summary>{LocalAppData}\{DomainName}\</summary>
            UserAppData = 1,
            /// <summary>{MyDocuments}\{DomainName}\</summary>
            UserDocuments = 2,
            /// <summary>{TEMP}\{DomainName}\</summary>
            UserTemp = 3,
            /// <summary>{ProgramData}\{DomainName}\</summary>
            ProgramData = 4,
        }

        /// <summary>Resolves a <see cref="DomainFolderId"/> to its full path:
        /// the system folder + <see cref="DomainName"/> + separator, always
        /// separator-tailed. Null for an unknown id (soft).</summary>
        public static string DomainFolderPath(DomainFolderId theId)
        {
            switch (theId)
            {
                case DomainFolderId.UserProfile:
                    return FS.FolderPath(FS.FolderId.UserProfile) + DomainName + FS.DSep;
                case DomainFolderId.UserAppData:
                    return FS.FolderPath(FS.FolderId.UserAppdataLocal) + DomainName + FS.DSep;
                case DomainFolderId.UserDocuments:
                    return FS.FolderPath(FS.FolderId.UserDocuments) + DomainName + FS.DSep;
                case DomainFolderId.UserTemp:
                    return FS.FolderPath(FS.FolderId.UserTEMP) + DomainName + FS.DSep;
                case DomainFolderId.ProgramData:
                    return FS.FolderPath(FS.FolderId.ProgramData) + DomainName + FS.DSep;
            }
            return null;
        }

        private const ushort issueSource = 65000;
        private const string s_COMPUTERNAME = @"COMPUTERNAME";
        private const string s_VersionFmt = "yyyy.MM.dd.HHmm"; // FOR DEFAULT VERSION STRING
        private const string s_debug = "debug";
        private const string s_alpha = "alpha";
        private const string s_beta = "beta";
        private const string s_console = "console";
        private const string s_service = "service";
        private const string UNDER = "_";
        private const char DOTchar = '.';


        //==== GENERAL PUBLIC METHODS ========

        /// <summary>The process command line as a <see cref="Chars"/> token
        /// cursor, positioned past the executable path - ready to pluck the
        /// actual parameters.</summary>
        public static Chars GetCommandLineParams()
        {
            Chars vTokens = new Chars(Environment.CommandLine);
            vTokens.PluckVisible_or_QuotedValue(out _); // SKIP EXE PATH
            return vTokens;
        }

        /// <summary>The entry assembly (see Assembly.GetEntryAssembly).</summary>
        public static Assembly GetEntryAssembly() => Assembly.GetEntryAssembly();

        /// <summary>The executing (library) assembly.</summary>
        public static Assembly GetExecutingAssembly() => Assembly.GetExecutingAssembly();

        /// <summary>Soft instance creation via the parameterless constructor,
        /// public or non-public. False with <paramref name="returnIssue"/>
        /// set when construction fails; false with null issue when
        /// <paramref name="theType"/> is null.</summary>
        public static bool CreatedInstance(Type theType, out object returnInstance, out Issue returnIssue)
        {
            returnIssue = null;
            returnInstance = null;

            if (theType == null) return false;

            try
            {
                returnInstance = Activator.CreateInstance(
                    theType,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    binder: null,
                    args: null,
                    culture: null);

                return true;
            }
            catch (Exception e)
            {
                returnIssue = Issue.Create(issueSource, 222, e, IssueKind.ProgramIssue);
            }
            return false;
        }

        /// <summary>Marshal-size of <paramref name="theType"/>'s elements,
        /// with <paramref name="returnDefaultSize"/> = how many fit a 4096-
        /// byte heap. See the full overload.</summary>
        public static int GetElementSize(Type theType, out int returnDefaultSize) => GetElementSize(theType, out returnDefaultSize, 4096);

        /// <summary>Marshal-size in bytes of one element of
        /// <paramref name="theType"/> (element type for arrays; pointer size
        /// for by-ref, interfaces, and non-blittable classes). Zero for a
        /// null type (soft). <paramref name="returnDefaultSize"/> receives
        /// how many elements fit in <paramref name="forHeapSize"/> bytes
        /// (minimum 1, capped at int.MaxValue).</summary>
        public static int GetElementSize(Type theType, out int returnDefaultSize, long forHeapSize)
        {
            returnDefaultSize = 0;
            int iElementSize = 0;

            if (theType == null) return 0;

            if (theType.IsArray)
                theType = theType.GetElementType();

            if (theType.IsByRef)
                iElementSize = IntPtr.Size;
            else
                try
                {
                    iElementSize = Marshal.SizeOf(theType);
                }
                catch
                {
                    // Falls back to pointer size for interfaces,
                    // objects, non-blittable classes, etc.
                    iElementSize = IntPtr.Size;
                }

            if (forHeapSize > iElementSize)
            {
                long iSize = forHeapSize / iElementSize; // excluding 24 array header
                if (iSize > int.MaxValue)
                    iSize = int.MaxValue;
                returnDefaultSize = (int)iSize;
            }
            else
                returnDefaultSize = 1;

            return iElementSize;
        }

        //==== SYSTEM PROPERTIES =======

        /// <summary>The COMPUTERNAME environment value.</summary>
        public static string DeviceName => Environment.GetEnvironmentVariable(s_COMPUTERNAME);

        /// <summary>True on a 64-bit operating system.</summary>
        public static bool Is64BitOS => Environment.Is64BitOperatingSystem;

        /// <summary>True in a 64-bit process.</summary>
        public static bool Is64BitProcess => Environment.Is64BitProcess;

        /// <summary>True while a debugger is attached.</summary>
        public static bool DebuggerIsAttached => Debugger.IsAttached;

        /// <summary>Start time of the current process.</summary>
        public static DateTime ProcessStartTime { get; private set; }

        /// <summary>Id of the current process.</summary>
        public static int ProcessId { get; private set; }

        /// <summary>Name of the current process.</summary>
        public static string ProcessName { get; private set; }

        /// <summary>True when the process runs in the Windows Administrator
        /// role (evaluated fresh per call). Windows-only.</summary>
#if NET5_0_OR_GREATER
        [SupportedOSPlatform("windows")]
#endif
        public static bool IsAdmin
        {
            get
            {
                using (var xIdentity = WindowsIdentity.GetCurrent())
                {
                    return new WindowsPrincipal(xIdentity).IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
        }

        //==== ENTRYASSEMBLY PROPERTIES =======

        /// <summary>Uniform text of the EntryAssembly [Guid]; empty when
        /// absent.</summary>
        public static string ExeGuid { get; private set; }

        /// <summary>Four-part [AssemblyVersion]; a 0.0.0.0 version is
        /// replaced by one derived from the exe file's last-write time.</summary>
        public static string ExeVersion { get; private set; }

        /// <summary>Full path of the running executable file.</summary>
        public static string ExeFilePath { get; private set; }

        /// <summary>Folder of the executable, separator-tailed.</summary>
        public static string ExeFolderPath { get; private set; }

        /// <summary>Executable file name without extension.</summary>
        public static string ExeNameOnly { get; private set; }

        /// <summary>Executable file name with extension.</summary>
        public static string ExeName_DOT_Ext => Path.GetFileName(ExeFilePath);

        /// <summary>"ExeCodeName" assembly metadata; falls back to
        /// <see cref="ExeNameOnly"/>.</summary>
        public static string ExeCodeName { get; private set; }

        /// <summary>"DomainName" assembly metadata; falls back to
        /// <see cref="ExeName_DOT_Ext"/>. Names the per-domain folders
        /// (see <see cref="DomainFolderPath"/>).</summary>
        public static string DomainName { get; private set; }

        /// <summary>Second-level part of <see cref="DomainName"/> (all
        /// before the last dot); falls back to <see cref="ExeNameOnly"/>.</summary>
        public static string DomainSLD { get; private set; }

        /// <summary>Top-level part of <see cref="DomainName"/> (after the
        /// last dot); empty when the name has no dot.</summary>
        public static string DomainTLD { get; private set; }

        /// <summary>"Contact" assembly metadata; empty when absent.</summary>
        public static string Contact { get; private set; } = string.Empty;

        /// <summary>"DomainGuid" assembly metadata, normalized to canonical
        /// Guid text; empty when absent.</summary>
        public static string DomainGuid { get; private set; } = string.Empty;

        /// <summary>"DeployStage" assembly metadata ("alpha"/"beta");
        /// empty when absent.</summary>
        public static string DeployStage { get; private set; } = string.Empty;

        /// <summary>"DeployDebug" assembly metadata ("debug" on DEBUG
        /// builds); empty when absent.</summary>
        public static string DeployDebug { get; private set; } = string.Empty;

        /// <summary>"ExeInterface" assembly metadata ("console"/"service");
        /// empty when absent.</summary>
        public static string ExeInterface { get; private set; } = string.Empty;

        /// <summary>"EnvironmentNamePrefix" assembly metadata; falls back
        /// to <see cref="DomainSLD"/> + "_".</summary>
        public static string EnvironmentNamePrefix { get; private set; } = string.Empty;

        /// <summary><see cref="ExeVersion"/> suffixed with ".{DeployStage}"
        /// and/or ".{DeployDebug}" when present.</summary>
        public static string ExeVersion_withDeployDebugTag
        {
            get
            {
                string sTag = string.Empty;
                if (!string.IsNullOrEmpty(DeployStage))
                    sTag += AS.DOT + DeployStage;
                if (!string.IsNullOrEmpty(DeployDebug))
                    sTag += AS.DOT + DeployDebug;
                return ExeVersion + sTag;
            }
        }

        private static bool _bool(string s1, string s2) => string.Equals(s1, s2, StringComparison.OrdinalIgnoreCase);

        /// <summary>True when <see cref="DeployDebug"/> = "debug".</summary>
        public static bool IsDebug => _bool(DeployDebug, s_debug);

        /// <summary>True when <see cref="DeployStage"/> = "alpha".</summary>
        public static bool IsAlpha => _bool(DeployStage, s_alpha);

        /// <summary>True when <see cref="DeployStage"/> = "beta".</summary>
        public static bool IsBeta => _bool(DeployStage, s_beta);

        /// <summary>True when <see cref="ExeInterface"/> = "console".</summary>
        public static bool IsConsole => _bool(ExeInterface, s_console);

        /// <summary>True when <see cref="ExeInterface"/> = "service".</summary>
        public static bool IsService => _bool(ExeInterface, s_service);



        //==== INITIALIZATION ======

        static Exe()
        {
            try
            {
                // FAILS IF compile settings not correct
                int iOverflow = int.MaxValue; ++iOverflow;
                // ALL Source77NW namespace assumes NO CHECKING
            }
            catch
            {
                throw Issue.Create(issueSource, 10
                    , "Build int overflow check must be disabled."
                    , IssueKind.ProgramIssue);
            }

            Process xProcess = Process.GetCurrentProcess();
            ProcessId = xProcess.Id;
            ProcessName = xProcess.ProcessName;
            ProcessStartTime = xProcess.StartTime;
            ExeFilePath = xProcess.MainModule.FileName;

            Assembly xAsm = GetEntryAssembly();
            if (xAsm != null)
            {
                string sLoc = xAsm.Location;
                if (!string.IsNullOrEmpty(sLoc)
                && string.Equals(Path.GetFileName(sLoc), Path.GetFileName(ExeFilePath), StringComparison.OrdinalIgnoreCase))
                    ExeFilePath = sLoc;

                ExeVersion = xAsm.GetName().Version.ToString(4); // never null for a loaded assembly
                if (ExeVersion == "0.0.0.0")
                {
                    DateTime dNow = FS.GetLastWriteTime(ExeFilePath, out _);
                    ExeVersion = new Version(dNow.ToString(s_VersionFmt)).ToString(4);
                }

                GuidAttribute xGuid = (GuidAttribute)Attribute.GetCustomAttribute(xAsm, typeof(GuidAttribute));
                ExeGuid = AS.UniformGuidText_or_null(xGuid?.Value);
                DomainGuid = AS.UniformGuidText_or_null(xAsm.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "DomainGuid")?.Value.Trim() ?? string.Empty);
                if (ExeGuid == null) ExeGuid = string.Empty;
                if (DomainGuid == null) DomainGuid = string.Empty;

                Contact = xAsm.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "Contact")?.Value.Trim() ?? string.Empty;
                ExeCodeName = xAsm.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "ExeCodeName")?.Value.Trim() ?? string.Empty;
                DomainName = xAsm.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "DomainName")?.Value.Trim() ?? string.Empty;
                DeployStage = xAsm.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "DeployStage")?.Value.Trim() ?? string.Empty;
                DeployDebug = xAsm.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "DeployDebug")?.Value.Trim() ?? string.Empty;
                ExeInterface = xAsm.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "ExeInterface")?.Value.Trim() ?? string.Empty;
                EnvironmentNamePrefix = xAsm.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "EnvironmentNamePrefix")?.Value.Trim() ?? string.Empty;
            }

            if (string.IsNullOrEmpty(ExeFilePath))
                throw Issue.Create(issueSource, 11
                    , "Requires Process or EntryAssembly filepath."
                    , IssueKind.ProgramIssue);

            ExeFolderPath = FS.EnsureDSepTail(Path.GetDirectoryName(ExeFilePath));
            ExeNameOnly = Path.GetFileNameWithoutExtension(ExeFilePath);

            if (string.IsNullOrEmpty(DomainName)) DomainName = ExeName_DOT_Ext;
            if (string.IsNullOrEmpty(ExeCodeName)) ExeCodeName = ExeNameOnly;

            int i = DomainName.LastIndexOf(DOTchar);
            DomainSLD = i >= 0 ? DomainName.Substring(0, i) : DomainName;

            if (string.IsNullOrEmpty(DomainSLD)) DomainSLD = ExeNameOnly;
            if (string.IsNullOrEmpty(EnvironmentNamePrefix))
                EnvironmentNamePrefix = DomainSLD + UNDER;

            if (!string.IsNullOrEmpty(DomainGuid))
                DomainGuid = new Guid(DomainGuid).ToString(); // NORMALIZE

            DomainTLD = (i = DomainName.LastIndexOf(DOTchar)) < 0 ? string.Empty : DomainName.Substring(i + 1);
        }



        //==== CRITICAL ISSUES/EXCEPTIONS ====

        private static Action<Issue> _DO_Critical = null;

        /// <summary>Installs the process-wide critical-issue handler.
        /// One-time only: false (no change) when a handler is already
        /// installed.</summary>
        public static bool SetCritical(Action<Issue> theAction)
        {
            if (_DO_Critical != null)
                return false;
            _DO_Critical = theAction;
            return true;
        }

        /// <summary>Routes <paramref name="theIssue"/> to the installed
        /// critical handler; with no handler - or a handler that itself
        /// throws - the issue is thrown instead.</summary>
        public static void Critical(Issue theIssue)
        {
            if (_DO_Critical != null)
            {
                try
                {
                    _DO_Critical.Invoke(theIssue);
                    return;
                }
                catch { }
            }
            throw theIssue;
        }

        internal static void Critical(object theRegarding, ushort theSource, byte theSpot)
        => Critical(Issue.Create(theSource, theSpot, theRegarding?.ToString(), IssueKind.ProgramIssue));

        internal static void Critical(Exception theException, ushort theSource, byte theSpot)
        {
            if (theException is Issue xIssue)
                Critical(xIssue);
            else if (theException != null)
                Critical(Issue.Create(theSource, theSpot, theException, IssueKind.ProgramIssue));
        }



        //==== LOGGING SYSTEM OR DEBUGGING OR WHATEVER

        private static Func<byte, object, bool> _DO_Logging = null;

        /// <summary>True when a logger is installed.</summary>
        public static bool IsLogging => _DO_Logging != null;

        /// <summary>Installs the process-wide logger. One-time only: false
        /// (no change) when a logger is already installed or
        /// <paramref name="theLogger"/> is null.</summary>
        public static bool SetLogging(Func<byte, object, bool> theLogger)
        {
            // SetLogging ONETIME AND ONETIME ONLY
            if (_DO_Logging == null && theLogger != null)
            {
                _DO_Logging = theLogger;
                return true;
            }
            return false;
        }

        /// <summary>Soft log: routes (<paramref name="anyCode"/>,
        /// <paramref name="anyWhat"/>) to the installed logger. False when
        /// no logger is installed or the logger throws - never itself
        /// throws.</summary>
        public static bool Log(byte anyCode, object anyWhat)
        {
            if (_DO_Logging != null)
            {
                try
                {
                    _DO_Logging.Invoke(anyCode, anyWhat);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            return false;
        }

    }
}
