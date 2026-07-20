// CopyrightCopyright (c) GDFrank
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Text;
using System.Diagnostics;

namespace Source77NW
{
    /// <summary>
    /// Common file-system operations for the Source77NW space: path
    /// validation and normalization (including file:// URL conversion),
    /// well-known folder resolution, soft file/folder create-copy-delete,
    /// text and stream readers/writers with UTF-8 BOM control, BOM and
    /// encoding detection, path classification, and process starting.
    /// Soft surface throughout: *_or_null / Try* / Got* members report
    /// failure via null/false plus an out Issue instead of throwing.
    /// </summary>
    public static class FS
    {
        /// <summary>Environment.NewLine.</summary>
        public static readonly string LSep = Environment.NewLine;

        /// <summary>Directory separator as a string ("\" on Windows).</summary>
        public static readonly string DSep = Path.DirectorySeparatorChar.ToString();

        /// <summary>Alternate directory separator as a string ("/").</summary>
        public static readonly string DSepAlt = Path.AltDirectorySeparatorChar.ToString();

        /// <summary>Volume separator as a string (":").</summary>
        public static readonly string VSep = Path.VolumeSeparatorChar.ToString();

        /// <summary>The any-drive path prefix "*:\" recognized by
        /// <see cref="ValidPath_or_null"/> (probes all logical drives).</summary>
        public static readonly string STAR_COLON_DSep = AS.STAR + AS.COLON + DSep;

        /// <summary>Volume separator character (':').</summary>
        public static readonly char VSep_char = Path.VolumeSeparatorChar;

        /// <summary>Directory separator character ('\' on Windows).</summary>
        public static readonly char DSep_char = Path.DirectorySeparatorChar;

        /// <summary>Normal Windows path maximum (260) - unless long
        /// pathnames are enabled.</summary>
        public const int MaxPathLength = 260;

        private const ushort issueSource = 65011;

        // NOTE: shared builder - GetFileUrl_or_null locks on it (correct,
        // load-bearing); any future out-of-lock use would be a data race.
        private static volatile StringBuilder _FS_builder = new StringBuilder(MaxPathLength); // 260 is normal Windows path max (unless long pathnames enabled)

        /// <summary>Last-write time of <paramref name="thePath"/>, with
        /// creation time out. Times come from FileInfo; a missing file
        /// yields FileInfo's not-found defaults rather than a throw.</summary>
        public static DateTime GetLastWriteTime(string thePath, out DateTime returnCreationTime)
        {
            FileInfo xInfo = new FileInfo(thePath);
            returnCreationTime = xInfo.CreationTime;
            return xInfo.LastWriteTime;
        }

        /// <summary>Reframes a file-access exception as a caption-carrying
        /// Issue for <paramref name="thePath"/>, classified by
        /// <see cref="Issue.KindOf"/> (NoSuch / LockedAccess / NeedPermit /
        /// other). An Issue with no inner exception passes through
        /// unchanged; null parameters yield a ProgramIssue.</summary>
        public static Issue AsFileIssue(Exception theException, string thePath)
        {
            if (theException != null && thePath != null)
            {
                if (theException is Issue xIssue)
                {
                    if (theException.InnerException != null)
                    {
                        // we will reframe the Issue's inner exception
                        theException = theException.InnerException;
                    }
                    else
                    {
                        // otherwise ... we do nothing;
                        return xIssue;
                    }
                }

                IssueKind iKind = Issue.KindOf(theException);

                string sForFile = ResCode.Caption_or_empty(WordId.File) + AS.COLON_SP + AS.Quoted(thePath);

                if (theException != null)
                {
                    switch (iKind)
                    {
                        case IssueKind.NoSuch:
                            xIssue = Issue.Create(issueSource, 122, theException, iKind
                                , ResCode.Caption_or_empty(IssueId.File_not_found)
                                , sForFile)
                                ;
                            return xIssue;

                        case IssueKind.LockedAccess:
                            return Issue.Create(issueSource, 121, theException, iKind
                                , ResCode.Caption_or_empty(IssueId.Unable_to_access_locked_file)
                                , sForFile);

                        case IssueKind.NeedPermit:
                            return Issue.Create(issueSource, 124, theException, iKind
                                , ResCode.Caption_or_empty(IssueId.Unable_to_access_restricted_file)
                                , sForFile);

                        default:
                            return Issue.Create(issueSource, 123, theException, iKind
                                , ResCode.Caption_or_empty(IssueId.Unexpected_file_access_issue)
                                , sForFile
                                , theException.Message);
                    }
                }
            }

            return Issue.Create(issueSource, 86
                , "AsFileIssue null parameters"
                , IssueKind.ProgramIssue);
        }

        /// <summary>Path of a system special folder, separator-tailed.</summary>
        public static string FolderPath(Environment.SpecialFolder theId) => FS.EnsureDSepTail(Environment.GetFolderPath(theId));

        /// <summary>Well-known folders resolved by
        /// <see cref="FolderPath(FolderId)"/>. Coding expects a byte-backed
        /// plain enum (no flags) - do not widen.</summary>
        public enum FolderId : byte
        {
            SystemDrive = 0,
            UserDesktop = 1,
            UserDocuments = 2,
            UserProfile = 3,
            UserAppdataLocal = 4,
            UserAppdataRoaming = 5,
            UserTEMP = 6,
            ProgramFiles = 7,
            ProgramFilesX86 = 8,
            ProgramData = 9,
            UserPrograms = 10,
            CommonPrograms = 11,
            System = 12,
            Windows = 13,
            WindowsMedia = 14,
        }

        /// <summary>Full path of a <see cref="FolderId"/> folder,
        /// separator-tailed. Null for an unknown id (soft).</summary>
        public static string FolderPath(FolderId theId)
        {
            switch (theId)
            {
                case FolderId.SystemDrive:
                    return Environment.GetEnvironmentVariable("SystemDrive") + DSep;
                case FolderId.UserDesktop:
                    return FolderPath(Environment.SpecialFolder.Desktop);
                case FolderId.UserProfile:
                    return FolderPath(Environment.SpecialFolder.UserProfile);
                case FolderId.UserDocuments:
                    return FolderPath(Environment.SpecialFolder.MyDocuments);
                case FolderId.UserAppdataRoaming:
                    return FolderPath(Environment.SpecialFolder.ApplicationData);
                case FolderId.UserAppdataLocal:
                    return FolderPath(Environment.SpecialFolder.LocalApplicationData);
                case FolderId.ProgramFiles:
                    return FolderPath(Environment.SpecialFolder.ProgramFiles);
                case FolderId.ProgramFilesX86:
                    return FolderPath(Environment.SpecialFolder.ProgramFilesX86);
                case FolderId.ProgramData:
                    return FolderPath(Environment.SpecialFolder.CommonApplicationData);
                case FolderId.UserPrograms:
                    return FolderPath(Environment.SpecialFolder.Programs);
                case FolderId.CommonPrograms:
                    return FolderPath(Environment.SpecialFolder.CommonPrograms);
                case FolderId.System:
                    return FolderPath(Environment.SpecialFolder.System);
                case FolderId.Windows:
                    return FolderPath(Environment.SpecialFolder.Windows);
                case FolderId.WindowsMedia:
                    return FolderPath(Environment.SpecialFolder.Windows) + "Media" + DSep;
                case FolderId.UserTEMP:
                    return EnsureDSepTail(Environment.GetEnvironmentVariable("TEMP"));
            }
            return null;
        }

        /// <summary>The path with a guaranteed trailing directory separator;
        /// empty for a null/empty path (soft).</summary>
        public static string EnsureDSepTail(string thePath)
        {
            return string.IsNullOrEmpty(thePath) ? string.Empty : thePath.EndsWith(DSep) ? thePath : thePath + DSep;
        }

        /// <summary>The separator-tailed sub-path of
        /// <paramref name="theFileOrFolder"/> relative to
        /// <paramref name="theRelativeToFolder"/> (file name popped for
        /// files); null when the item is not under that folder (soft).</summary>
        public static string SubFolderPathOf(FileSystemInfo theFileOrFolder, DirectoryInfo theRelativeToFolder)
        {
            string sPath = theFileOrFolder.FullName;
            string sDir = EnsureDSepTail(theRelativeToFolder.FullName);
            if (sPath.Length <= sDir.Length) return null; // NO SUBPATH FOUND

            int i1 = -1;
            while (++i1 < sDir.Length)
                if (sPath[i1] != sDir[i1])
                    break;

            if (i1 != sDir.Length)
                return null; // NO SUBPATH FOUND - NOT EQUAL base folders

            int i2 = sPath.Length;

            if (theFileOrFolder is FileInfo xFile)
            {
                while (--i2 > i1) // pop the file name
                {
                    if (sPath[i2] == FS.DSep_char)
                        break;
                }
            }

            return EnsureDSepTail(sPath.Substring(i1, i2 - i1));
        }

        /// <summary>Validated, normalized, separator-tailed folder path (see
        /// <see cref="ValidPath_or_null"/>); null with a BadEntry issue when
        /// the path names an existing FILE (unless
        /// <paramref name="issue_if_is_file"/> = false).</summary>
        public static string ValidFolderPath_or_null(string thePath_or_FileUrl_or_NullOrWhiteSpace, out Issue returnIssue, bool issue_if_is_file = true)
        {
            string sPath = ValidPath_or_null(thePath_or_FileUrl_or_NullOrWhiteSpace, out returnIssue);

            if (sPath != null)
            {
                if (issue_if_is_file && File.Exists(sPath))
                {
                    returnIssue = Issue.Create(issueSource, 112
                    , ResCode.Caption_or_empty(IssueId.FolderPath_cannot_be_existing_file)
                    , AS.Quoted(thePath_or_FileUrl_or_NullOrWhiteSpace)
                    , IssueKind.BadEntry);

                    return null;
                }

                return EnsureDSepTail(sPath);
            }

            return sPath;
        }

        /// <summary>Validated, normalized file path (see
        /// <see cref="ValidPath_or_null"/>); null with a BadEntry issue when
        /// the path names an existing FOLDER (unless
        /// <paramref name="issue_if_is_folder"/> = false).</summary>
        public static string ValidFilePath_or_null(string thePath_or_FileUrl_or_NullOrWhiteSpace, out Issue returnIssue, bool issue_if_is_folder = true)
        {
            string sPath = ValidPath_or_null(thePath_or_FileUrl_or_NullOrWhiteSpace, out returnIssue);

            if (sPath != null)
            {
                if (issue_if_is_folder && Directory.Exists(sPath))
                {
                    returnIssue = Issue.Create(issueSource, 113
                    , ResCode.Caption_or_empty(IssueId.FilePath_cannot_be_existing_folder)
                    , AS.Quoted(thePath_or_FileUrl_or_NullOrWhiteSpace)
                    , IssueKind.BadEntry);

                    return null;
                }
            }

            return sPath;
        }

        /// <summary>Validates and normalizes a path. Accepts a rooted path,
        /// a file:// URL (converted, %20 restored; other % escapes are a
        /// BadEntry issue), or an "*:\" prefix (probes every logical drive
        /// for an existing match, else the first drive). Null for
        /// null/whitespace input WITHOUT an issue (no harm no foul); null
        /// WITH a BadEntry issue for unrooted or invalid paths.</summary>
        public static string ValidPath_or_null(string thePath_or_FileUrl_or_NullOrWhiteSpace, out Issue returnIssue)
        {
            // note: converts a file URL to file system path

            returnIssue = null;

            if (string.IsNullOrWhiteSpace(thePath_or_FileUrl_or_NullOrWhiteSpace))
                return null; // NO Issue ...no harm no foul

            string sPath1 = thePath_or_FileUrl_or_NullOrWhiteSpace; // as either

            try
            {
                if (sPath1.StartsWith(STAR_COLON_DSep))
                {
                    string sPathTail = thePath_or_FileUrl_or_NullOrWhiteSpace.Substring(STAR_COLON_DSep.Length);
                    string[] xDrives = Environment.GetLogicalDrives();
                    string sFirstDrivePath = ValidPath_or_null(xDrives[0] + sPathTail, out returnIssue);

                    if (returnIssue != null) return null;

                    if (!File.Exists(sFirstDrivePath) && !Directory.Exists(sFirstDrivePath) && xDrives.Length > 1)
                    {
                        sPathTail = sFirstDrivePath.Substring(STAR_COLON_DSep.Length); // now "fixed"

                        for (int i = 1; i < xDrives.Length; i++)
                        {
                            string sPath2 = xDrives[i] + sPathTail;

                            if (File.Exists(sPath2)) // never an exception
                                return sPath2;

                            if (Directory.Exists(sPath2)) // never an exception
                                return EnsureDSepTail(sPath2);
                        }
                    }

                    return sFirstDrivePath;
                }

                if (AS.Url.IsFileScheme(sPath1, out string sPrefix, out string sValue))
                {
                    int iStart = sPrefix.Length;
                    int iLen = thePath_or_FileUrl_or_NullOrWhiteSpace.Length - iStart;
                    int iHash = thePath_or_FileUrl_or_NullOrWhiteSpace.IndexOf(Chars.HASH, iStart, iLen);

                    if (iHash > 0)
                    {
                        // JUST IN CASE THE URL contains HASH, like a web url
                        iLen -= (thePath_or_FileUrl_or_NullOrWhiteSpace.Length - iHash);
                        sPath1 = thePath_or_FileUrl_or_NullOrWhiteSpace.Substring(iStart, iLen);
                    }
                    else
                    {
                        sPath1 = thePath_or_FileUrl_or_NullOrWhiteSpace.Substring(iStart);
                    }

                    sPath1 = sPath1.Replace(AS.DSEP_url, DSep);
                    sPath1 = sPath1.Replace(AS.Url.SP, AS.SP); // %20
                    int iPct = sPath1.IndexOf(AS.PCT);

                    if (iPct >= 0)
                    {
                        // YET_TODO_CONSIDER other % chars
                        returnIssue = Issue.Create(issueSource, 114
                            , ResCode.Caption_or_empty(IssueId.Invalid_URL)
                            , AS.Quoted(sPath1)
                            , IssueKind.BadEntry);

                        return null;
                    }
                }

                if (!Path.IsPathRooted(sPath1))
                {
                    returnIssue = Issue.Create(issueSource, 115
                            , ResCode.Caption_or_empty(IssueId.Path_must_have_root_folder)
                            , ResCode.Caption_or_empty(WordId.Path) + AS.COLON_SP
                            , AS.Quoted(sPath1)
                            , IssueKind.BadEntry);

                    return null;
                }

                return Path.GetFullPath(sPath1);
            }
            catch (Exception e)
            {
                returnIssue = Issue.Create(issueSource, 110
                        , ResCode.Caption_or_empty(IssueId.Invalid_path)
                        , ResCode.Caption_or_empty(WordId.Path) + AS.COLON_SP
                        , AS.Quoted(sPath1)
                        , e.Message
                        , IssueKind.BadEntry);
            }

            return null;
        }

        /// <summary>True when the folder exists or was created here.
        /// False with an issue on failure (soft).</summary>
        public static bool FolderExists_or_Created(string theFolderPath, out Issue returnIssue)
        {
            try
            {
                returnIssue = null;
                if (Directory.Exists(theFolderPath))
                    return true;

                Directory.CreateDirectory(theFolderPath);
                return Directory.Exists(theFolderPath);

            }
            catch (Exception e)
            {
                returnIssue = Issue.Create(issueSource, 144, e, AS.Quoted(theFolderPath));
                return false;
            }
        }

        /// <summary>True when the folder is absent or was deleted here
        /// (recursive). False with an issue on failure (soft).</summary>
        public static bool FolderAbsent_or_Deleted(string theFolderPath, out Issue returnIssue)
        {
            try
            {
                returnIssue = null;
                if (!Directory.Exists(theFolderPath))
                    return true;

                Directory.Delete(theFolderPath, true);
                return !Directory.Exists(theFolderPath);
            }
            catch (Exception e)
            {
                returnIssue = Issue.Create(issueSource, 145, e);
                return false;
            }
        }

        /// <summary>True when the file is absent or was deleted here.
        /// False with an issue on failure (soft).</summary>
        public static bool FileAbsent_or_Deleted(string theFilePath, out Issue returnIssue)
        {
            try
            {
                returnIssue = null;
                if (!File.Exists(theFilePath))
                    return true;

                File.Delete(theFilePath);
                return !File.Exists(theFilePath);
            }
            catch (Exception e)
            {
                returnIssue = Issue.Create(issueSource, 146, e);
                return false;
            }
        }

        /// <summary>File name without extension; null with a BadEntry issue
        /// on an invalid path (soft).</summary>
        public static string FileNameOnly_or_null(string theFilePath, out Issue returnIssue)
        {
            try
            {
                returnIssue = null;
                return Path.GetFileNameWithoutExtension(theFilePath);
            }
            catch
            {
                returnIssue = Issue.Create(issueSource, 234
                , ResCode.Caption_or_empty(IssueId.Invalid_path)
                , AS.Quoted(theFilePath)
                , IssueKind.BadEntry);

                return null;
            }
        }

        /// <summary>File name without extension.</summary>
        public static string FileNameOnly(FileInfo theFileInfo) => theFileInfo.Name.Substring(0, theFileInfo.Name.Length - theFileInfo.Extension.Length);

        /// <summary>File name without extension, with the extension and
        /// separator-tailed folder path out.</summary>
        public static string FileNameOnly(FileInfo theFileInfo, out string returnExtension, out string returnFolderPath)
        {
            returnExtension = theFileInfo.Extension;
            returnFolderPath = EnsureDSepTail(theFileInfo.DirectoryName);
            return theFileInfo.Name.Substring(0, theFileInfo.Name.Length - returnExtension.Length);
        }

        /// <summary>file:// URL for the item's full path.</summary>
        public static string GetFileUrl(FileSystemInfo theFile) => GetFileUrl_or_null(theFile.FullName);

        /// <summary>file:// URL for the path; null on failure (issue
        /// discarded).</summary>
        public static string GetFileUrl_or_null(string thePath) => GetFileUrl_or_null(thePath, out Issue _);

        /// <summary>Builds a file:// URL from a path (or re-verifies an
        /// existing file URL): spaces become %20, separators become '/'.
        /// Null with a BadEntry issue when the path is invalid or contains
        /// control characters (soft).</summary>
        public static string GetFileUrl_or_null(string thePath, out Issue returnIssue)
        {
            // https://en.wikipedia.org/wiki/Percent-encoding#Types_of_URI_characters
            // https://msdn.microsoft.com/en-us/library/zttxte6w.aspx
            // https://www.w3.org/TR/html401/interact/forms.html#h-17.13.4.1

            string sPath = thePath;

            bool bIsUrl = AS.Url.IsFileScheme(thePath, out string sPrefix, out string sValue);

            if (bIsUrl)
            {
                // grab the "non-prefix" part
                sPath = thePath.Substring(sPrefix.Length);
                returnIssue = null;
                // we will rebuild and verify url path chars
            }
            else
            {
                sPath = ValidPath_or_null(thePath, out returnIssue);
                if (sPath == null)
                    return null;
            }

            lock (_FS_builder)
            {
                _FS_builder.Clear();

                _FS_builder.Append(AS.Url.file_COLON_SLASH3);

                for (int i = 0; i < sPath.Length; i++)
                {
                    char c = sPath[i];

                    if (char.IsControl(c))
                    {
                        returnIssue =
                              Issue.Create(issueSource, 211
                              , ResCode.Caption_or_empty(IssueId.Url_may_not_contain_control_characters)
                              , thePath
                              , IssueKind.BadEntry);

                        return null;
                    }

                    if (c == ' ')
                    {
                        _FS_builder.Append(AS.Url.SP);
                    }
                    else if (c == DSep_char)
                    {
                        _FS_builder.Append(AS.DSEP_url);
                    }
                    else
                    {
                        _FS_builder.Append(c);
                    }
                }

                string sResult = _FS_builder.ToString();

                _FS_builder.Clear();

                return sResult;
            }
        }


        /// <summary>Write FileStream (Create or Append, share-read), creating
        /// the folder as needed; optionally prefixes a UTF-8 BOM on fresh
        /// files. Null with a file issue on failure (soft).</summary>
        public static FileStream GetFileWriter_or_null
            ( string theFilePath, out Issue returnIssue
            , bool prefix_UTF8_BOM = false, bool append = false)
        {
            const byte _UTF8_BOM0 = 0xEF;
            const byte _UTF8_BOM1 = 0xBB;
            const byte _UTF8_BOM2 = 0xBF;

            try
            {
                string sDir = Path.GetDirectoryName(theFilePath);

                if (!Directory.Exists(sDir))
                {
                    Directory.CreateDirectory(sDir);
                }

                FileStream xStream = new FileStream(theFilePath, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read);

                if (!append && prefix_UTF8_BOM)
                {
                    xStream.WriteByte(_UTF8_BOM0);
                    xStream.WriteByte(_UTF8_BOM1);
                    xStream.WriteByte(_UTF8_BOM2);
                }

                returnIssue = null;

                return xStream;
            }
            catch (Exception e)
            {
                returnIssue = AsFileIssue(e, theFilePath);
            }

            return null;
        }

        /// <summary>TextWriter over <see cref="GetFileWriter_or_null"/>;
        /// null with a file issue on failure (soft).</summary>
        public static TextWriter GetTextWriter_or_null
            (string theFilePath, out Issue returnIssue
            , bool prefix_UTF8_BOM = false, bool append = false)
        {
            Stream xStream = GetFileWriter_or_null(theFilePath, out returnIssue, prefix_UTF8_BOM, append);
            return xStream == null ? null : new StreamWriter(xStream);
        }


        /// <summary>Writes <paramref name="theText"/> (null = empty) to the
        /// file. Returns null on success, else the file issue (soft).</summary>
        public static Issue TrySavingText
            (string theFilePath, string theText
            , bool prefix_UTF8_BOM = false, bool append = false)

        {
            if (theText == null) theText = string.Empty;

            TextWriter xWriter = GetTextWriter_or_null(theFilePath, out Issue xIssue, prefix_UTF8_BOM, append);

            if (xIssue != null)
            {
                return xIssue;
            }

            try
            {
                xWriter.Write(theText);

                xWriter.Dispose();

                return null;
            }
            catch (Exception e)
            {
                try { xWriter.Dispose(); } catch { }

                return AsFileIssue(e, theFilePath);
            }
        }

        /// <summary>Read FileStream (Open, share-read); null with a file
        /// issue on failure (soft).</summary>
        public static FileStream GetFileReader_or_null
            (string theFilePath, out Issue returnIssue)
        {
            returnIssue = null;
            try
            {
                FileStream xStream = new FileStream(theFilePath, FileMode.Open
                    , FileAccess.Read, FileShare.Read);
                return xStream;
            }
            catch (Exception e)
            {
                returnIssue = AsFileIssue(e, theFilePath);
            }

            return null;
        }

        /// <summary>Byte-order-mark classification detected by
        /// <see cref="GotBOMFlag"/>. Deprecated = UTF-1 or UTF-7 marks.</summary>
        [Flags]
        public enum BOMFlag : byte
        {
            none = 0,

            /// <summary>EF BB BF</summary>
            UTF_8 = 1 << 0,  // 1
            /// <summary>FF FE</summary>
            UTF_16LE = 1 << 1,  // 2
            /// <summary>FE FF</summary>
            UTF_16BE = 1 << 2,  // 4
            /// <summary>FF FE 00 00</summary>
            UTF_32LE = 1 << 3,  // 8
            /// <summary>00 00 FE FF</summary>
            UTF_32BE = 1 << 4,  // 16
            /// <summary>UTF-1 (F7 64 4C) or UTF-7 (2B 2F 76 ..)</summary>
            Deprecated = 1 << 5,  // 32

            Any_16 = UTF_16BE | UTF_16LE,
            Any_32 = UTF_32BE | UTF_32LE,
            Any_16_or_32 = Any_16 | Any_32,
            Any_LE = UTF_16LE | UTF_32LE,
            Any_BE = UTF_16BE | UTF_32BE,
        }

        /// <summary>Detects a byte-order mark at the START of the stream
        /// (position preserved). True when a BOM was found. False - with
        /// <see cref="BOMFlag.none"/> - for BOM-less streams and for null /
        /// unreadable / unseekable streams (soft).</summary>
        public static bool GotBOMFlag(Stream theStream, out BOMFlag returnBOMFlag)
        {
            returnBOMFlag = BOMFlag.none;

            if (theStream == null || !theStream.CanRead || !theStream.CanSeek)
            {
                return false;
            }

            long iOriginalPosition = theStream.Position;

            byte[] xBuf = new byte[4];

            theStream.Position = 0;

            int iRead = theStream.Read(xBuf, 0, 4);

            if (iRead >= 4)
            {
                if (xBuf[0] == 0xFF && xBuf[1] == 0xFE && xBuf[2] == 0x00 && xBuf[3] == 0x00)
                {
                    // UTF-32 LE: FF FE 00 00  (must check before UTF-16 LE)
                    returnBOMFlag = BOMFlag.UTF_32LE;
                }
                else if (xBuf[0] == 0x00 && xBuf[1] == 0x00 && xBuf[2] == 0xFE && xBuf[3] == 0xFF)
                {
                    // UTF-32 BE: 00 00 FE FF
                    returnBOMFlag = BOMFlag.UTF_32BE;
                }
            }

            if (returnBOMFlag == 0 && iRead >= 3)
            {
                if (xBuf[0] == 0xEF && xBuf[1] == 0xBB && xBuf[2] == 0xBF)
                {
                    // UTF-8: EF BB BF
                    returnBOMFlag = BOMFlag.UTF_8;
                }
                else if (xBuf[0] == 0xF7 && xBuf[1] == 0x64 && xBuf[2] == 0x4C)
                {
                    // UTF-1: F7 64 4C
                    returnBOMFlag = BOMFlag.Deprecated;
                }
                else if (xBuf[0] == 0x2B && xBuf[1] == 0x2F && xBuf[2] == 0x76)
                {
                    // UTF-7: 2B 2F 76 + (38 | 39 | 2B | 2F)
                    if (iRead >= 4 && (xBuf[3] == 0x38 || xBuf[3] == 0x39
                    || xBuf[3] == 0x2B || xBuf[3] == 0x2F))
                    {
                        returnBOMFlag = BOMFlag.Deprecated;
                    }
                }
            }

            if (returnBOMFlag == BOMFlag.none && iRead >= 2)
            {
                if (xBuf[0] == 0xFF && xBuf[1] == 0xFE)
                {
                    // UTF-16 LE: FF FE
                    returnBOMFlag = BOMFlag.UTF_16LE;
                }
                else if (xBuf[0] == 0xFE && xBuf[1] == 0xFF)
                {
                    // UTF-16 BE: FE FF
                    returnBOMFlag = BOMFlag.UTF_16BE;
                }
            }

            theStream.Position = iOriginalPosition; // always reset to original position

            return returnBOMFlag != BOMFlag.none;
        }

        /// <summary>StreamReader over <see cref="GetFileReader_or_null"/>
        /// (BOM-aware); null with a file issue on failure (soft).</summary>
        public static TextReader GetTextReader_or_null(string theFilePath, out Issue returnIssue)
        {
            Stream xStream = GetFileReader_or_null(theFilePath, out returnIssue);

            if (xStream == null)
            {
                return null;
            }

            xStream.Position = 0; // REPOSITION TO BOM (if any)

            return new StreamReader(xStream);
        }

        /// <summary>The file's entire text (BOM-aware); null with a file
        /// issue on failure (soft).</summary>
        public static string GetText_or_null(string theFilePath, out Issue returnIssue)
        {
            returnIssue = null;

            TextReader xReader = GetTextReader_or_null
                (theFilePath, out returnIssue);
            if (xReader != null)
            {
                try
                {
                    string s = xReader.ReadToEnd();
                    xReader.Dispose();
                    return s;
                }
                catch (Exception e)
                {
                    try { xReader.Dispose(); } catch {}
                    returnIssue = AsFileIssue(e, theFilePath);
                }
            }

            return null;
        }

        /// <summary>Copies the file into the folder (same name, overwrite).
        /// Returns null on success, else the issue (soft).</summary>
        public static Issue TryCopyFile(FileInfo theSourceFile, DirectoryInfo theTargetFolder)
        {
            FileInfo xFile2 = new FileInfo(theTargetFolder.FullName + DSep + theSourceFile.Name);

            return TryCopyFile(theSourceFile, xFile2);
        }

        /// <summary>Copies source to target (overwrite). Returns null on
        /// success, else an issue naming both paths (soft).</summary>
        public static Issue TryCopyFile(FileInfo theSourceFile, FileInfo theTargetFile)
        {
            Issue getIssueMsg(string sCaption, Exception xException)
            {
                return Issue.Create(issueSource, 156
                    , sCaption
                    , ResCode.Caption_or_empty(WordId.Source) + AS.SP + AS.Quoted(theSourceFile.FullName)
                    , ResCode.Caption_or_empty(WordId.Target) + AS.SP + AS.Quoted(theTargetFile.FullName)
                    , xException?.Message ?? string.Empty
                    );
            }

            try
            {
                if (!theSourceFile.Exists)
                {
                    return getIssueMsg(ResCode.Caption_or_empty(IssueId.Source_file_not_found), null);
                }

                theSourceFile.CopyTo(theTargetFile.FullName, true);

                return null;
            }
            catch (Exception ex)
            {
                return getIssueMsg(ResCode.Caption_or_empty(IssueId.Unable_to_write_to_file), ex);
            }
        }

        /// <summary>Copies a folder's files (overwrite) and, by default, its
        /// subfolders recursively; creates the target as needed. Returns null
        /// on success, else the first issue encountered (soft). Enumerates
        /// incrementally to minimize memory for unknown file/folder counts.</summary>
        public static Issue TryCopyFolder(DirectoryInfo theSourceFolder, DirectoryInfo theTargetFolder, bool copySubFolders = true)
        {
            // DESIGNED THIS WAY TO MINIMIZE MEMORY USAGE FOR UNKNOWN FILE/FOLDER COUNTS

            Issue getIssueMsg(string sCaption, Exception xException)
            {
                return Issue.Create(issueSource, 157 // FIX: spot dedup (was 156, reused by TryCopyFile)
                    , sCaption
                    , ResCode.Caption_or_empty(WordId.Source) + AS.SP + AS.Quoted(theSourceFolder.FullName)
                    , ResCode.Caption_or_empty(WordId.Target) + AS.SP + AS.Quoted(theTargetFolder.FullName)
                    , xException?.Message ?? string.Empty
                    );
            }

            if (!theSourceFolder.Exists)
            {
                return getIssueMsg(ResCode.Caption_or_empty(IssueId.Source_folder_not_found), null);
            }

            try
            {
                DirectoryInfo[] xDirs1 = theSourceFolder.GetDirectories();

                if (!theTargetFolder.Exists)
                {
                    Directory.CreateDirectory(theTargetFolder.FullName);
                }

                foreach (FileInfo xFile1 in theSourceFolder.GetFiles())
                {
                    string sTargetFilePath = Path.Combine(theTargetFolder.FullName, xFile1.Name);

                    xFile1.CopyTo(sTargetFilePath, true); // Overwrite if exists
                }

                if (copySubFolders)
                {
                    foreach (DirectoryInfo xDir1 in xDirs1)
                    {
                        DirectoryInfo xDir2 = new DirectoryInfo(theTargetFolder.FullName + DSep + xDir1.Name);

                        Issue xIssue = TryCopyFolder(xDir1, xDir2, true);

                        if (xIssue != null)
                        {
                            return xIssue;
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                return getIssueMsg(ResCode.Caption_or_empty(IssueId.Unable_to_write_to_folder), ex);
            }
        }

        /// <summary>Classification returned by <see cref="KindOfPath"/>.</summary>
        public enum PathKind : byte
        {
            /// <summary>Empty input or no recognizable value.</summary>
            None = 0,
            /// <summary>Not a valid path or recognized URL.</summary>
            Unknown = 1,
            ExistingFile = 2,
            ExistingFolder = 3,
            /// <summary>Valid path; nothing exists there yet.</summary>
            FileOrFolder = 4,
            /// <summary>http or https URL.</summary>
            UrlHttp = 5,
            /// <summary>ftp or ftps URL.</summary>
            UrlFtp = 6,
            UrlMailTo = 7,
            /// <summary>exe: URL (internal format).</summary>
            UrlExe = 8,
        }

        /// <summary>Classifies the first visible line of
        /// <paramref name="thePath"/> as a URL kind or a file-system path,
        /// normalizing the out value for valid paths (existing folders come
        /// separator-tailed).</summary>
        public static PathKind KindOfPath(string thePath, out string returnValidPath_or_theValue)
        {
            returnValidPath_or_theValue = Chars.GetFirstVisibleLine(thePath);

            if (returnValidPath_or_theValue.Length < 1)
            {
                return PathKind.None;
            }

            AS.Url.PrefixId iPrefix = AS.Url.GetPrefixId(returnValidPath_or_theValue, out _, out _);

            switch (iPrefix)
            {
                // WARNING ENSURE ALL Url.PrefixId represented here
                case AS.Url.PrefixId.none: return PathKind.None;
                case AS.Url.PrefixId.file: break; // further analysis needed
                case AS.Url.PrefixId.http: return PathKind.UrlHttp;
                case AS.Url.PrefixId.https: return PathKind.UrlHttp;
                case AS.Url.PrefixId.ftp: return PathKind.UrlFtp;
                case AS.Url.PrefixId.ftps: return PathKind.UrlFtp;
                case AS.Url.PrefixId.mailto: return PathKind.UrlMailTo;
                case AS.Url.PrefixId.exe: return PathKind.UrlExe;
                case AS.Url.PrefixId.unknown: break; // further analysis needed
            }

            string sPath = ValidPath_or_null(returnValidPath_or_theValue, out _);

            if (sPath == null)
            {
                return PathKind.Unknown; // leave returnValidPath as is
            }

            returnValidPath_or_theValue = sPath; // reset to normalized path

            if (File.Exists(returnValidPath_or_theValue))
            {
                return PathKind.ExistingFile;
            }

            // see if existing directory

            if (Directory.Exists(returnValidPath_or_theValue))
            {
                if (!returnValidPath_or_theValue.EndsWith(DSep))
                    returnValidPath_or_theValue += DSep;

                return PathKind.ExistingFolder;
            }

            return PathKind.FileOrFolder; // non-existing file or folder
        }


        /// <summary>Start options for <see cref="Started(string, string, string, string, StartFlag, out Process, out Issue)"/>.
        /// The low 2 bits map directly onto ProcessWindowStyle
        /// (Normal/Hidden/Minimized/Maximized); high bits add NoWindow,
        /// NoShell (UseShellExecute off), and AsAdmin (RunAs verb).</summary>
        [Flags]
        public enum StartFlag : byte
        {
            Normal = ProcessWindowStyle.Normal,         // 0
            Hidden = ProcessWindowStyle.Hidden,         // 1
            Minimized = ProcessWindowStyle.Minimized,   // 2
            Maximized = ProcessWindowStyle.Maximized,   // 3
            NoWindow = 1 << 5,
            NoShell = 1 << 6,
            AsAdmin = 1 << 7,
            NoShell_AsAdmin = NoShell | AsAdmin,
        }

        /// <summary>Starts a process for a path or URL with full control of
        /// arguments, startup folder, verb, and <see cref="StartFlag"/>
        /// options (AsAdmin implies the RunAs verb when no verb is given).
        /// False without an issue for a null path (soft); false with an
        /// issue when the start fails.</summary>
        public static bool Started(string thePath_or_Url
            , string theArguments_or_null
            , string theStartupFolder_or_null
            , string theVerb_or_null
            , StartFlag theFlags_or_0_for_defaults
            , out Process returnProcess_if_any
            , out Issue returnIssue
            )
        {
            const string s_RunAs = "RunAs";
            const ushort issueSource = 65004; // FIX: was 10032, app-range value inside 1.* library

            if (thePath_or_Url == null)
            {
                returnProcess_if_any = null;
                returnIssue = null;
                return false;
            }

            if (theVerb_or_null == null)
            {
                if (0 != (theFlags_or_0_for_defaults & StartFlag.AsAdmin))
                {
                    theVerb_or_null = s_RunAs;
                }
            }

            ProcessStartInfo xInfo = new ProcessStartInfo()
            {
                FileName = thePath_or_Url,

                Arguments = theArguments_or_null,

                WorkingDirectory = theStartupFolder_or_null,

                CreateNoWindow = 0 != (theFlags_or_0_for_defaults & StartFlag.NoWindow),

                UseShellExecute = 0 == (theFlags_or_0_for_defaults & StartFlag.NoShell),

                Verb = theVerb_or_null,

                WindowStyle = (ProcessWindowStyle)((byte)theFlags_or_0_for_defaults & 3)
            };

            try
            {
                returnIssue = null;
                returnProcess_if_any = Process.Start(xInfo);
                return true;
            }
            catch (Exception e)
            {
                //   T:System.InvalidOperationException:
                //   T:System.ArgumentNullException:
                //   T:System.ObjectDisposedException:
                //   T:System.IO.FileNotFoundException:
                //   T:System.ComponentModel.Win32Exception:

                returnIssue = Issue.Create(issueSource, 0, IssueId.Unable_to_open_file_or_folder, thePath_or_Url, e);
                returnProcess_if_any = null;
                return false;
            }
        }

        /// <summary>Starts a path or URL with defaults. See the full
        /// overload.</summary>
        public static bool Started(string thePath_or_Url) => Started(thePath_or_Url
                , null
                , null
                , null
                , 0
                , out _
                , out _
                );

        /// <summary>Starts a path or URL with the given flags. See the full
        /// overload.</summary>
        public static bool Started(string thePath_or_Url, StartFlag theFlag) => Started(thePath_or_Url
                , null
                , null
                , null
                , theFlag
                , out _
                , out _
                );

        /// <summary>Starts a path or URL with arguments. See the full
        /// overload.</summary>
        public static bool Started(string thePath_or_Url, string theArguments) => Started(thePath_or_Url
                , theArguments
                , null
                , null
                , 0
                , out _
                , out _
                );

        /// <summary>The shell verbs available for the path; null on any
        /// failure (soft).</summary>
        public static string[] GetStartVerbs_or_null(string thePath)
        {
            try
            {
                return new ProcessStartInfo(thePath).Verbs;
            }
            catch { }

            return null;
        }

        /// <summary>Classification returned by
        /// <see cref="EncodingDetector.DetectEncoding"/>.</summary>
        public enum TextEncodingType
        {
            PlainAscii,
            Utf8,
            Utf16LE,
            Utf16BE,
            Unknown
        }

        /// <summary>Heuristic text-encoding detection for raw bytes: BOM
        /// first, then a zero-byte-ratio UTF-16 heuristic, then pure-ASCII,
        /// then UTF-8 sequence validation.</summary>
        public static class EncodingDetector
        {
            /// <summary>The detected <see cref="TextEncodingType"/> of
            /// <paramref name="bytes"/>; Unknown for null/empty input or
            /// undetectable content (soft).</summary>
            public static TextEncodingType DetectEncoding(byte[] bytes)
            {
                if (bytes == null || bytes.Length == 0)
                    return TextEncodingType.Unknown;

                // --- 1. Check for BOM ---
                if (bytes.Length >= 3 &&
                    bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                    return TextEncodingType.Utf8;

                if (bytes.Length >= 2)
                {
                    if (bytes[0] == 0xFF && bytes[1] == 0xFE)
                        return TextEncodingType.Utf16LE;

                    if (bytes[0] == 0xFE && bytes[1] == 0xFF)
                        return TextEncodingType.Utf16BE;
                }

                // --- 2. Count zero bytes (UTF-16 heuristic) ---
                int zeroCount = 0;
                for (int i = 0; i < bytes.Length; i++)
                {
                    if (bytes[i] == 0x00)
                        zeroCount++;
                }

                double zeroRatio = (double)zeroCount / bytes.Length;

                // UTF-16 typically has ~50% zero bytes for ASCII-range text
                if (zeroRatio > 0.30)
                {
                    // Try to guess endianness
                    if (bytes.Length > 1 && bytes[0] == 0x00)
                        return TextEncodingType.Utf16BE;

                    return TextEncodingType.Utf16LE;
                }

                // --- 3. Check if all bytes are ASCII ---
                bool allAscii = true;
                for (int i = 0; i < bytes.Length; i++)
                {
                    if (bytes[i] > 0x7F)
                    {
                        allAscii = false;
                        break;
                    }
                }

                if (allAscii)
                    return TextEncodingType.PlainAscii;

                // --- 4. Validate UTF-8 sequences ---
                if (IsValidUtf8(bytes))
                    return TextEncodingType.Utf8;

                return TextEncodingType.Unknown;
            }

            private static bool IsValidUtf8(byte[] bytes)
            {
                int i = 0;
                while (i < bytes.Length)
                {
                    byte b = bytes[i];

                    if (b <= 0x7F)
                    {
                        // ASCII
                        i++;
                    }
                    else if (b >= 0xC2 && b <= 0xDF)
                    {
                        // 2-byte sequence
                        if (i + 1 >= bytes.Length) return false;
                        if (!IsContinuation(bytes[i + 1])) return false;
                        i += 2;
                    }
                    else if (b >= 0xE0 && b <= 0xEF)
                    {
                        // 3-byte sequence
                        if (i + 2 >= bytes.Length) return false;
                        if (!IsContinuation(bytes[i + 1]) || !IsContinuation(bytes[i + 2])) return false;
                        i += 3;
                    }
                    else if (b >= 0xF0 && b <= 0xF4)
                    {
                        // 4-byte sequence
                        if (i + 3 >= bytes.Length) return false;
                        if (!IsContinuation(bytes[i + 1]) ||
                            !IsContinuation(bytes[i + 2]) ||
                            !IsContinuation(bytes[i + 3])) return false;
                        i += 4;
                    }
                    else
                    {
                        return false;
                    }
                }

                return true;
            }

            private static bool IsContinuation(byte b)
            {
                return (b & 0xC0) == 0x80;
            }
        }

        /// <summary>Caption ids for FS issue messages, resolved via
        /// ResCode.Caption_or_empty (member names ARE the captions,
        /// underscores as spaces). Values feed captions as message params
        /// while the raise-site spot stays a separate literal (see DEV
        /// issue-source pattern). Values are persisted-adjacent - keep
        /// unique, do not renumber.</summary>
        [EnumCodes]
        public enum IssueId : byte
        {
            NoIssue = 0,
            Invalid_parameters = 43,
            Already_running = 1,
            Invalid_path = 2,
            Invalid_URL = 3,
            No_files_to_process = 44,
            File_not_found = 4,
            File_could_not_be_created = 5,
            File_must_NOT_exist = 6,
            File_or_Folder_must_exist = 7,
            Source_file_not_found = 8,
            Source_folder_not_found = 9,
            FolderPath_cannot_be_existing_file = 10,
            FilePath_cannot_be_existing_folder = 11,
            Folder_could_not_be_created = 12,
            Folder_must_NOT_exist = 13,
            Folder_not_found = 14,
            Folder_must_be_empty = 15,
            FolderPath_cannot_be_root = 16,
            Folders_must_be_distinct = 17,
            Path_must_have_root_folder = 18,
            Root_folder_operation_not_supported = 19,
            Url_may_not_contain_control_characters = 20,
            Unexpected_file_access_issue = 21,
            Cannot_find_exe_file = 22,
            Nothing_to_search_for = 23,
            Failed_to_start_exe = 24,
            Operation_failed = 25,
            Unable_to_write_to_file = 26,
            Unable_to_access_locked_file = 27,
            Unable_to_write_to_folder = 28,
            Unable_to_write_to_stream = 29,
            Unable_to_open_directory = 30,
            Unable_to_open_file_or_folder = 31,
            Unable_to_load_file = 32,
            Unable_to_load_bitmap = 33,
            Unable_to_set_value = 34,
            Unable_to_create_file = 35,
            Unable_to_delete_file = 37,
            Unable_to_replace_file = 45, // FIX: was 43, duplicating Invalid_parameters. The dup made EnumCodes.ForType throw Enum_values_are_not_unique, which silently EMPTIED every FS.IssueId caption via ResCode.Caption_or_empty. 45 = next free (44 = No_files_to_process). Persist caveat: 43 was ambiguous anyway.
            Unable_to_create_folder = 36,
            Unable_to_delete_folder = 38,
            Unable_to_delete_contents = 39,
            Unable_to_find_marked_text = 40,
            Unable_to_access_restricted_file = 41,
            Unable_to_access_locked_file_or_files = 42,
        }
    }
}
