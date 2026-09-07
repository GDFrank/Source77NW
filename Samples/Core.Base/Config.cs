// Copyright (c) GDFrank - 77NW.net. All rights reserved.
// Licensed under the MIT License.
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Reflection;
using Source77NW;

// ================================================================
// DOMAIN CONFIG - Samples77NW (ONE file, ONE class, nowhere else)
// Linked into every sample exe via samples\Directory.Build.props.
// Everything domain-level lives HERE:
//   - the domain-wide [assembly:] block below, feeding
//     Source77NW.Exe's identity (Exe.DomainName, Exe.Contact,
//     Exe.DomainGuid, ... - see Exe.cs remarks)
//   - static class Config: domain boot (Initialize), the Folders
//     and Settings stores (EnumVals/LSV files under the Config
//     folder), Permit, Issues (the domain standards for
//     Exe.Critical / Exe.Log handling, bound by Initialize),
//     Assets (the ResCode suppliers, registered by Initialize),
//     Lingo, and Data (#if TEST/DEMO).
// Interface-dependent code (the Critical/Log sinks) is compiled
// in/out via #if CONSOLE / #if SERVICE / else UI - not runtime
// dispatch.
// Each exe keeps only its PER-EXE attributes (AssemblyProduct,
// ExeCodeName, Guid) + Main() in its own <Name>.main.cs.
// BOOT CONTRACT (DEV.txt EXE BOOT PATTERN owns the spec) - every
// <Name>.main.cs Main(), in order:
//   1. if (!Config.Initialized())
//      {
//          if (!Config.Permit.Request())
//              return (int)ExitId.Canceled; // no Y, no run
//      }
//   2. initialize exe-specific settings
//   3. trigger the runtime action
// with MINIMAL other functionality in <Name>.main.cs.
// AssemblyVersion is auto-stamped at every build by
// samples\Directory.Build.props (0.0.0.0 fallback: see Exe.cs).
// ================================================================

[assembly: AssemblyCompany("Samples77NW")]
[assembly: AssemblyCopyright("Copyright (c) GDFrank")]
[assembly: AssemblyMetadata("Contact", "mailto:nobodyhere@Samples77NW.invalid")] // .invalid = RFC 2606 reserved TLD, guaranteed dead
[assembly: AssemblyMetadata("DomainName", "Samples77NW.invalid")] // RFC 2606 reserved TLD, declared dummy
[assembly: AssemblyMetadata("DomainGuid", "afda118f-d9ec-4fbd-aacf-79c5ecab021d")] // one guid shared by all AppRepo samples
#if DEBUG
[assembly: AssemblyMetadata("DeployDebug", "debug")]
#endif
#if DEMO
[assembly: AssemblyMetadata("DeployDemo", "demo")]
#endif
#if CONSOLE
[assembly: AssemblyMetadata("ExeInterface", "console")]
#elif SERVICE
[assembly: AssemblyMetadata("ExeInterface", "service")]
#endif

namespace Samples77NW
{
    /// <summary>
    /// Domain-level configuration for the Samples77NW domain - the ONE
    /// place (single file, single class) holding domain boot
    /// (<see cref="Initialized"/>), the Folders and Settings stores,
    /// Permit, and the domain Critical/Log/ResCode standards. Linked
    /// into every sample exe - see the banner above.
    /// </summary>
    /// <remarks>
    /// Folder and settings logic lives directly on Config - NOT in
    /// nested classes. <see cref="Permit"/> is the only public nested
    /// class; the private nested classes (Issues, Assets, Lingo, Data)
    /// exist purely for code management, each backing one kind of
    /// Config-level external interaction.
    /// <para>BLUESKY (on record): a Listeners-based NotifyId trigger
    /// set (e.g. NotifyId.LingoChanged) so apps can respond to lingo
    /// and other domain-defined changes.</para>
    /// </remarks>
    internal static class Config
    {
        /// <summary>
        /// Domain boot - the FIRST call from every exe's Main() in its
        /// <c>&lt;Name&gt;.main.cs</c>, before exe-specific settings are
        /// initialized and before the runtime action is triggered.
        /// False when the domain Config folder is absent - the domain
        /// has no consent to write anything yet, and the caller asks via
        /// <see cref="Permit.Request"/>, which on yes creates the folder
        /// and completes this initialization. True when initialization
        /// is complete (including when already initialized).
        /// </summary>
        public static bool Initialized()
        {
            if (_Initialized) return true;

            issueMgr.Initialize(); // registers log and Critical

            assetMgr.Initialize(); // registers the ResCode suppliers

            lingoMgr.Initialize(); // registers the ResCode suppliers

            string sDomainFolder = Exe.DomainFolderPath(Exe.DomainFolderId.UserProfile);

            _ConfigFolderPath = sDomainFolder + s_Config + FS.DSep; // Exe got it from [Assembly

            if (!Directory.Exists(_ConfigFolderPath))
                return false;

            return _Initialized_Step2();
        }

        private static bool _Initialized_Step2() // FIX: was _Initailized_Step2 (typo)
        {
            _FoldersInit();

            _SettingsInit();

            byte iLingoNum = AS.ParseByte(Setting(SettingId.LingoNum), out _);

            SetActiveLingo(iLingoNum);

            _Initialized = true;

            return true;
        }

        /// <summary>
        /// The domain folder set backing the Folders store. Member names
        /// double as LSV field tokens and environment-variable suffixes -
        /// single words, NO DOT tokens.
        /// </summary>
        public enum FolderId : uint
        {
            Config = 0 | Bits.ValFlag.folder | GrpId.Base | OptId.Readonly, // Config.txt
            Stash = 1 | Bits.ValFlag.folder | GrpId.Base, // <userProfile>\<domain>\Stash\
            Results = 2 | Bits.ValFlag.folder | GrpId.Base, // <userProfile>\<domain>\Results\
            Settings = 3 | Bits.ValFlag.folder | GrpId.Stash, // <userProfile>\<domain>\Stash
            Jobs = 4 | Bits.ValFlag.folder | GrpId.Stash, // ditto
            Links = 5 | Bits.ValFlag.folder | GrpId.Stash,// ditto
            Icons = 6 | Bits.ValFlag.folder | GrpId.Stash,// ditto
            Tools = 7 | Bits.ValFlag.folder | GrpId.Stash,// ditto
            Studio = 8 | Bits.ValFlag.folder | GrpId.Stash,// ditto
            AnyDrivePublicFiles = 09 | Bits.ValFlag.folder | GrpId.AnyDrive, // e.g. "*:\@\"
            AnyDriveLocalFiles = 10 | Bits.ValFlag.folder | GrpId.AnyDrive,  // e.g. "*:\@@\"
            OneDrive = 11 | Bits.ValFlag.folder | GrpId.Sys | OptId.Readonly,
            ExeState = 12 | Bits.ValFlag.folder | GrpId.Sys | OptId.Readonly,
        }

        /// <summary>Grp nibble assignments for <see cref="FolderId"/>.</summary>
        private enum GrpId : uint
        {
            Base = Bits.GrpFlag.Grp1,  // Exe.DomainUserProfile
            Stash = Bits.GrpFlag.Grp2, // <base>/Stash
            AnyDrive = Bits.GrpFlag.Grp4, // "*:\"<folder> value FS.ValidPath_else_null() 
            Sys = Bits.GrpFlag.Grp3,   // external system folders discovered at runtime
            Exe = Bits.GrpFlag.Grp4,   // Exe folder
        }

        /// <summary>
        /// Cmd-nibble option flags for <see cref="FolderId"/> (Cmd is
        /// just a name for a nibble here).
        /// </summary>
        private enum OptId : uint
        {
            Readonly = 1 << Bits.Cmd_Shift,
        }


        /// <summary>
        /// The domain-level user settings backing the Settings store.
        /// Member names double as LSV field tokens - single words, NO
        /// DOT tokens.
        /// </summary>
        public enum SettingId : uint
        {
            LingoNum = 0, // 0..255
            TextEditor = 1 | Bits.ValFlag.file, // user/domain preferred text editor
            ZoomFont = 2 | Bits.ValFlag.float32, // display zoom font factor (1.0 = 100%)
        }

        // **** Config PRIVATE HEAP ****


        private const ushort issueSource = 4716; // FIX: was static ushort (house style = const)
        private const string s_Config = "Config";
        private const string s_Settings = "Settings";
        private const string s_Folders = "Folders";
        private static string _ConfigFolderPath; // set upon initialize
        private static EnumVals _Folders;
        private static EnumVals _Settings;
        private static string _AnyDrivePublic_infix = "Public"; // modifyable in _FolderInit();
        private static string _AnyDriveLocal_infix = "Local";  // modifyable in _FolderInit();
        private static bool _Initialized = false; // onetime initialize
        private static bool _AllowFolderValuesAsEnvironmentValues = false; // controls folders logic


        #region //**** FOLDERS METHODS *******************************

        /// <summary>The member token of the folder id.</summary>
        /// <summary>Creates the Folders store and loads its LSV file when present.</summary>
        private static void _FoldersInit()
        {
            _Folders = EnumVals.Create(typeof(FolderId), _get_folders_default, _folder_is_RO);

            string sText = FS.GetText_or_null(_Folders_ConfigFilePath, out _);

            if (sText != null)
            {
                _Folders.LoadAsLsvFormat(new Chars(sText));
            }
        }

        private static GrpId GrpOf(FolderId theId)
        {
            GrpId iGrp = (GrpId)Bits.GrpFlag_from_flags(_Folders.Codes.Flag32(_Folders.Codes.IndexOf(theId)));
            return iGrp;
        }

        private static string _Folders_ConfigFilePath => _ConfigFolderPath + s_Folders + AS.DOT_txt;

        /// <summary>
        /// Sets the folder path (an invalid path falls back to the
        /// current value) and persists the store unless asPersisted is
        /// false. False for an unknown or read-only member.
        /// </summary>
        public static bool Set(FolderId theId, string theValue, bool asPersisted = true)
        {
            int iIndex = _Folders.Codes.IndexOf(theId);

            if (iIndex < 0) return false;

            string sPath = FS.ValidFolderPath_or_null(theValue, out _);

            if (sPath == null)
                sPath = FolderPath(theId);

            // SetValue refuses read-only members via _folder_is_RO.
            if (!_Folders.SetValue(iIndex, new Chars(sPath))) return false;

            if (asPersisted)
                return _FoldersSaved();

            return true;
        }

        /// <summary>The current path of the folder id (default when unset).</summary>
        public static string FolderPath(FolderId theId)
        {
            string sPath = _Folders.Value(_Folders.Codes.IndexOf(theId)).ToString();

            if (GrpOf(theId) == GrpId.AnyDrive)
            {
                // try to ensure sPath is proper AnyDrivePath
                if (!FS.IsAnyDrivePath(sPath))
                {
                    string sPath2= FS.AsAnyDrivePath_or_null(sPath);
                    if (sPath2 != null)
                        sPath = sPath2;
                }
            }


            return sPath;
        }
        
        /// <summary>The current <see cref="FolderId.Results"/> path.</summary>
        public static string ResultsFolderPath => FolderPath(FolderId.Results);

        /// <summary>The computed default path of the folder id.</summary>
        public static string FolderDefault(FolderId theId) => _FolderDefault(_Folders.Codes.IndexOf(theId));

        /// <summary>
        /// Persists the Folders store (read-only members are skipped by
        /// <see cref="EnumVals.SaveAsLsvFormat"/>).
        /// </summary>
        private static bool _FoldersSaved()
        {
            // FIX: was a hand-rolled LSV writer that opened the context
            // record with FieldMarker instead of RecordMarker, never ended
            // that line, re-implemented the read-only skip, and disposed
            // the builder three ways. EnumVals.ToLsv IS this function.
            Issue xIssue = FS.TrySavingText(_Folders_ConfigFilePath, _Folders.ToLsv());

            return xIssue == null;
        }

        /// <summary>EnumVals hook: supplies <see cref="_FolderDefault"/> for the Folders codes.</summary>
        private static Chars _get_folders_default(EnumCodes xCodes, int index)
        {
            if (!_Folders.Codes.Equals(xCodes)) return Chars.Nothing;

            return new Chars(_FolderDefault(index));
        }

        /// <summary>EnumVals hook: a member is read-only when its enum value carries <see cref="OptId.Readonly"/>.</summary>
        private static bool _folder_is_RO(EnumCodes xCodes, int iIndex)
        {
            // FIX: was a hardcoded switch (FolderId.Config only) that ignored
            // the Opt.Readonly flag already declared on the enum - the flag
            // is now the single source of truth.
            return Bits.CmdFlag_from_flags(xCodes.Flag32(iIndex)) == (uint)OptId.Readonly;
        }

        /// <summary>
        /// Default resolution: an environment variable
        /// (<see cref="Exe.EnvironmentNamePrefix"/> + member token) naming
        /// a valid folder wins; else the per-member default; else
        /// Stash + member token.
        /// </summary>
        private static string _FolderDefault(int theIndex)
        {
            if (theIndex < 0 || theIndex >= _Folders.Codes.Count) return null;

            string sName = _Folders.KeyName(theIndex);
            string sValue = null;

            if (_AllowFolderValuesAsEnvironmentValues)
            {
                sValue = Environment.GetEnvironmentVariable(Exe.EnvironmentNamePrefix + sName);
            }

            if (!string.IsNullOrEmpty(sValue))
            {
                string sEvalPath = FS.ValidFolderPath_or_null(sValue, out _);
                if (sEvalPath != null)
                {
                    return sEvalPath;
                }
            }

            // DEFAULTS
            string sPath = null;

            switch ((FolderId)_Folders.Codes.Code(theIndex))
            {
                case FolderId.Config: sPath = Exe.DomainFolderPath(Exe.DomainFolderId.UserProfile) + sName + FS.DSep; break;
                case FolderId.Stash: sPath = Exe.DomainFolderPath(Exe.DomainFolderId.UserProfile) + sName + FS.DSep; break;
                case FolderId.Studio: sPath = FS.FolderPath(FS.FolderId.UserProfile) + sName + FS.DSep; break;
                case FolderId.AnyDrivePublicFiles: sPath = FS.STAR_COLON_DSep + _AnyDrivePublic_infix + FS.DSep; break;
                case FolderId.AnyDriveLocalFiles: sPath = FS.STAR_COLON_DSep + _AnyDriveLocal_infix + FS.DSep; break;
                case FolderId.ExeState: sPath = FolderDefault(FolderId.Stash) + Exe.ExeCodeName + FS.DSep; break;
                case FolderId.OneDrive:
                    string s = Environment.GetEnvironmentVariable(sName)?.Trim(); // SEEMS ALWAYS SET;
                    if (s != null && Directory.Exists(s))
                    {
                        return FS.EnsureDSepTail(s);
                    }
                    // no evar/setting default for OneDrive
                    return Exe.DomainFolderPath(Exe.DomainFolderId.UserProfile) + sName + FS.DSep;
            }

            if (!string.IsNullOrEmpty(sPath))
            {
                return sPath;
            }

            return FolderPath(FolderId.Stash) + sName + FS.DSep;
        }

        #endregion


        #region //**** SETTINGS METHODS *******************************

        private static string _Settings_ConfigFilePath => _ConfigFolderPath + s_Settings + AS.DOT_txt;

        private static int _IndexOf(SettingId theId) => _Settings.Codes.IndexOf(theId);

        /// <summary>Sets the setting value and persists the store unless andSave is false.</summary>
        public static void Set(SettingId theId, string theValue, bool andSave = true)
        {
            _Settings.SetValue(theId, new Chars(theValue));

            if (andSave)
                _SettingsSave(out _);
        }

        /// <summary>The current value of the setting (empty when unset - no domain defaults yet).</summary>
        public static string Setting(SettingId theId) => _Settings.Value(_IndexOf(theId)).ToString();

        /// <summary>Persists the Settings store.</summary>
        private static void _SettingsSave(out Issue returnIssue)
        {
            string sText = _Settings.ToLsv();

            returnIssue = FS.TrySavingText(_Settings_ConfigFilePath, sText);
        }

        /// <summary>Creates the Settings store and loads its LSV file when present.</summary>
        private static void _SettingsInit()
        {
            _Settings = EnumVals.Create(typeof(SettingId), null);

            _SettingsLoad();
        }

        private static void _SettingsLoad()
        {
            string sText = FS.GetText_or_null(_Settings_ConfigFilePath, out _);

            if (sText == null) return;

            try
            {
                Chars vText = new Chars(sText);
                _Settings.LoadAsLsvFormat(vText);
            }
            catch (Exception ex)
            {
                Exe.Critical(Issue.Create(issueSource, 33, ex, IssueKind.ProgramIssue));
            }

        }

        #endregion


        /// <summary>
        /// Permission to use domain apps - the domain consent mechanism
        /// (JOB.PERMIT 2026-07-29). Consent is DOMAIN-level, for every
        /// exe in the domain at once: the existence of the domain Config
        /// folder IS the consent marker. <see cref="Request"/> presents
        /// <see cref="LicenseText"/> per interface and, on yes, creates
        /// the folder and completes the file-dependent initialization
        /// that <see cref="Config.Initialized"/> could not. The JOB.WRAP-
        /// era per-exe PERMIT.&lt;ExeCodeName&gt;.bin markers are retired.
        /// </summary>
        public static class Permit
        {
            /// <summary>What agreeing means: creation of the domain
            /// folder under the user's profile - the ONLY folder the
            /// samples use, essential to the demo, and deletable at any
            /// time. One shared string for every exe in the domain.</summary>
            public const string LicenseText =
                  "You are running a Samples77NW demo sample.\n"
                + "To proceed it needs to create\n"
                + "a folder under your user profile:\n"
                + "  <userProfile>\\Samples77NW.invalid\\\n"
                + "That folder is the ONLY folder used.\n"
                + "Settings and various other outputs land there.\n"
                + "It is essential to the demo.\n"
                + "You can delete it at any time.\n"
                + "Nothing else on your system is touched.";

            // DialogBox caption: always a domain-decided thing - the
            // [AssemblyProduct] of the running exe (G 2026-07-29; an
            // Exe.cs accessor remains a candidate).
            private static string _Caption
                => Exe.GetEntryAssembly()?.GetCustomAttribute<AssemblyProductAttribute>()?.Product
                    ?? Exe.ExeName_DOT_Ext;

            /// <summary>
            /// Presents <see cref="LicenseText"/> and asks the user; on
            /// yes, creates the domain Config folder (the consent act)
            /// and completes initialization - true therefore GUARANTEES
            /// full initialization. False when the user declines, the
            /// folder cannot be created, the interface cannot ask
            /// (SERVICE), or <see cref="Config.Initialized"/> was never
            /// called first. True immediately when already initialized.
            /// Never throws.
            /// </summary>
            public static bool Request()
            {
                if (_Initialized) return true;

                if (_ConfigFolderPath == null) return false; // boot contract violated: Initialized() never ran

#if CONSOLE
                Console.Out.WriteLine(LicenseText);
                Console.Out.WriteLine();
                Console.Out.Write("Create this folder and continue? [Y/N] ");

                string sAnswer = Console.In.ReadLine();

                bool bYes = sAnswer != null && sAnswer.Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase);
#elif SERVICE
                // a service cannot ask; the app that installs a service
                // does the permit work (G 2026-07-29)
                Exe.Log(0, "Permit.Request: a service cannot ask - no permit.");

                bool bYes = false;
#else // UI - samples: simple DialogBox
                bool bYes = System.Windows.Forms.DialogResult.OK
                    == System.Windows.Forms.MessageBox.Show(LicenseText
                        , _Caption
                        , System.Windows.Forms.MessageBoxButtons.OKCancel
                        , System.Windows.Forms.MessageBoxIcon.Information);
#endif

                if (!bYes) return false;

                if (!FS.FolderExists_or_Created(_ConfigFolderPath, out _)) return false;

                return _Initialized_Step2();
            }

        }


        /// <summary>
        /// The DOMAIN STANDARDS for Critical handling and Logging, bound
        /// to <see cref="Exe.ConfigCritical"/> / <see cref="Exe.ConfigLogging"/>
        /// by <see cref="Config.Initialized"/>. Interface-dependent sinks
        /// are compiled in/out via #if CONSOLE / #if SERVICE / else UI -
        /// not runtime dispatch.
        /// </summary>
        private static class issueMgr
        {
            public static void Initialize()
            {
                Exe.ConfigLogging(_Log);
                Exe.ConfigCritical(_Critical);
            }

            private static bool _Log(byte theDomainDefinedCode, object theValue)
            {
#if CONSOLE
                Console.Error.WriteLine("LOG " + theDomainDefinedCode + AS.SP + theValue);
                return true;
#elif SERVICE
                // STUB: domain service log sink to be defined
                System.Diagnostics.Trace.WriteLine("LOG " + theLevel + AS.SP + theValue);
                return false; // false until the domain standard is defined
#else // UI
                // STUB: domain UI/app log sink to be defined
                System.Diagnostics.Debug.WriteLine("LOG " + theDomainDefinedCode + AS.SP + theValue);
                return false; // false until the domain standard is defined
#endif
            }

            private static void _Critical(Issue theIssue)
            {
#if CONSOLE
                Console.Error.WriteLine(theIssue.Header_Detail_Message_Inner);
#elif SERVICE
                // STUB: domain service standard (event log / log file) to be defined
                System.Diagnostics.Trace.WriteLine(theIssue.Header_Detail_Message_Inner);
#else // UI
                // STUB: domain UI standard (dialog) to be defined
                System.Diagnostics.Debug.WriteLine(theIssue.Header_Detail_Message_Inner);
#endif
            }

        }


        /// <summary>
        /// The domain's ResCode supplier seat: registers the value and
        /// stream providers and the lingo-number authority with
        /// <see cref="ResCode"/>. The providers are STUBS pending the
        /// domain resource-packaging decision; supplementary data can
        /// live in <see cref="dataMgr"/> and be #if-ed in or out.
        /// </summary>
        private static class assetMgr
        {
            /// <summary>
            /// Registers <see cref="_GotValue"/>, <see cref="_GotStream"/>,
            /// and the lingo-number authority with ResCode. Called by
            /// <see cref="Config.Initialized"/>.
            /// </summary>
            public static bool Initialize()
            {
                // FIX: was an empty stub returning false - these are the
                // registrations the suppliers below exist for.

                // assetMgr is used by ResCode to resolve ResKind stream
                // requests. Usually these file names as packed into
                // embedded resources (see ResPack.cs).
                // BUT any method may be used to override or map the
                // default icon/blob/echo name to local standards.

                // lingoMgr may also call on assetMgr to retrieve
                // files/streams for its use in initialization.

                return ResCode.Register(_GotStream);
            }

            /// <summary>
            /// ResCode stream supplier - resolves named resource streams.
            /// ResCode asks by the actual resource file name (e.g.
            /// "edit.cut.ico"), wherever it is stored. STUB: the packaging
            /// is this EntryAssembly's open decision - direct embedding,
            /// xres, or ResPack streams with their tailing manifest
            /// (DEV-level builder required) are all candidates.
            /// </summary>
            private static bool _GotStream(ResKind theKind, string theStreamName, out BytesReader returnReader)
            {
                returnReader = null;

                // STUB: resolve theKind.ToString() + AS.DOT + theStreamName
                // to a stream, then BytesReader.Create_or_null(stream, out _).

                return false;
            }

        }

        /// <summary> Stub to call lingoMgr.SetActiveLingo</summary>
        public static bool SetActiveLingo(byte theLingoNum) => lingoMgr.SetActiveLingo(theLingoNum);

        /// <summary>
        /// Reserved seat for lingo functions - kept out of the operations
        /// code.
        /// </summary>
        private static class lingoMgr
        {
            public static bool Initialize()
            {
                // lingoMgr _GotValue is what ResCode calls
                // to resolve value overrides to any embedded
                // values.

                // lingoMgr may also call on assetMgr to retrieve
                // ResCode.ResKind.lingo files/streams for its use in
                // in initialization.

                bool didAll = ResCode.Register(_GotValue);

                didAll &= ResCode.RegisterAsLingoNumAuthority(out _DO_SetResCodeActiveLingo);

                return didAll;
            }

            private static Action<byte> _DO_SetResCodeActiveLingo = null;

            /// <summary>Pushes the active lingo number to ResCode; false until the authority is registered.</summary>
            public static bool SetActiveLingo(byte theLingoNum)
            {
                if (_DO_SetResCodeActiveLingo == null) return false;
                _DO_SetResCodeActiveLingo(theLingoNum);
                return true;
            }

            /// <summary>
            /// ResCode value supplier - resolves lingo-override CodeValId
            /// values for <see cref="ResCode.ActiveLingoNum"/>. STUB: the
            /// override source is this EntryAssembly's open decision -
            /// embedded compressed LsvRecord packs (per lingo, storable as
            /// strings during development then embedded), EnumInfo
            /// Text/Tags, or ResPack are all candidates.
            /// </summary>
            private static bool _GotValue(ResCode theCode, CodeValId theId, out Chars returnValue)
            {
                returnValue = default;

                // STUB: resolve theCode.ToString() + AS.DOT + theId.ToString()
                // against the active lingo's override values.

                return false;
            }




            // lingoMgr is used by ResCode to resolve CodeValId values. 

            // Lingo num 0 is reserved for Source77NW default values (en-us)
            // All Source77NW EnumCodes are encoded in num 0 lingo.

            // lingoMgr may use those EnumCodes defined values as desired
            // and define 1..n lingos as desired, perhaps 1=sp 2=fr 3=de ???

            // Source77NW.ResPack may help in building/accessing resources.
            // And lingoMgr can use assetMgr to load ResKind.lingo ResPacks.
            // But any means may be used to supply overrides.
        }

        /// <summary>
        /// Reserved seat for TEST/DEMO/... data, with #if include/exclude
        /// triggers - kept out of the operations code.
        /// </summary>
        private static class dataMgr
        {
        }

    }
}
