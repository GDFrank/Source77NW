// Copyright (c) GDFrank - 77NW.net. All rights reserved.
// Licensed under the MIT License.
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Source77NW;

// ================================================================
// ASSEMBLY IDENTITY - PER-EXE
// Domain-wide attributes (Company, Copyright, Contact, DomainName,
// DomainGuid, DeployDebug, ExeInterface) live once in
// samples\Config.cs, linked in via samples\Directory.Build.props
// (supersedes SamplesCommon.cs). AssemblyVersion is auto-stamped the same way -
// see Exe.cs remarks for the full EntryAssembly identity contract.
// ================================================================

[assembly: AssemblyProduct("FileList.exe")]
[assembly: AssemblyMetadata("ExeCodeName", "FILELIST")]
[assembly: Guid("6f2c9a4e-3b7d-4c1a-9e5f-2d8a6c7b1f30")]

namespace Samples77NW
{
    /// <summary>
    /// FileList - property-selectable file/folder listing to csv, tsv,
    /// lsv, or md. A folder arg lists its files (top-level by default,
    /// -all recurses); a file arg reports just that file. Which columns
    /// appear and in what order is caller-chosen (-props / -preset) via
    /// <see cref="FileListPropertyId"/>, an EnumCodes-registered enum -
    /// see README.md beside this file for the full switch reference.
    /// </summary>
    internal static class FileListMain
    {
        // Samples/apps use the low issueSource range; Source77NW core
        // reserves 65,000+. ListFiles has 100, Lookup 101; FileList
        // takes 102.
        private const ushort issueSource = 102;

        private enum Say
        {
            Unknown_option,
            Unexpected_extra_parameter,
            No_such_file_or_folder,
            Unknown_property,
            Unknown_output_format,
        }

        //==================== PROPERTIES (EnumCodes-registered) ====================

        /// <summary>
        /// The columns FileList can report, in declaration order (also
        /// the "default" preset order - see <see cref="_PresetDefault"/>).
        /// Bits ARE the property id (see modules/EnumCodes.txt "container"
        /// principle) - no Text/Tags needed for a console-only sample.
        /// </summary>
        public enum FileListPropertyId : byte
        {
            NameExt = 0,
            Ext = 1,
            Name = 2,
            Path = 3,
            Folder = 4,
            Url = 5,
            Attr = 6,   // NT-attribute bits; meaningful mainly on Windows, harmlessly mostly-off elsewhere
            Size = 7,
            SizeText = 8,
            Updated = 9,
            Created = 10,
            Crc32 = 11, // computed only for files actually listing this column - see _PropertyValue
        }

        private enum OutputFormatId
        {
            csv,
            tsv,
            lsv,
            md,
        }

        private static readonly EnumCodes _PropCodes = EnumCodes.ForType(typeof(FileListPropertyId));

        // "default" = every property, declaration order (SPEC: PROPERTY
        // SELECTION/ORDER preset 1).
        private static readonly FileListPropertyId[] _PresetDefault =
        {
            FileListPropertyId.NameExt, FileListPropertyId.Ext, FileListPropertyId.Name,
            FileListPropertyId.Path, FileListPropertyId.Folder, FileListPropertyId.Url,
            FileListPropertyId.Attr, FileListPropertyId.Size, FileListPropertyId.SizeText,
            FileListPropertyId.Updated, FileListPropertyId.Created, FileListPropertyId.Crc32,
        };

        // Excel-friendly ordering (SPEC preset 2): Crc32 left OUT by
        // default (an extra per-file hash isn't a "quick spreadsheet
        // glance" default) - add it explicitly with -props if wanted.
        // Folder goes last for easy scroll/sort.
        private static readonly FileListPropertyId[] _PresetExcel =
        {
            FileListPropertyId.Name, FileListPropertyId.Ext, FileListPropertyId.Attr,
            FileListPropertyId.Size, FileListPropertyId.Updated, FileListPropertyId.Created,
            FileListPropertyId.Folder,
        };

        private const string _DateFmt = "yyyy-MM-dd HH:mm:ss";

        private static readonly string[] _SizeUnits = { "B", "KB", "MB", "GB", "TB", "PB" };

        //==== OPTIONS (from the command line) ====

        private static string _TargetPath;                 // file OR folder; SOFT default = current dir
        private static FileAttr.Bits _IncludeBits;
        private static bool _GotInclude;
        private static FileAttr.Bits _ExcludeBits;
        private static bool _GotExclude;
        private static bool _Recurse;
        private static bool _GotOut;
        private static string _OutFilePath;
        private static bool _OpenWhenDone;
        private static OutputFormatId _Format = OutputFormatId.csv;
        private static FileListPropertyId[] _Props = _PresetDefault;

        //==== ENTRY ====

        private static int Main()
        {
            // Boot contract (DEV.txt EXE BOOT PATTERN): Config.Initialized
            // is the FIRST action - it binds the domain Critical/Logging
            // standards before anything else runs. False = no domain
            // folder = no consent yet: Permit.Request presents the notice
            // and asks - no Y, no run (JOB.PERMIT 2026-07-29).
            if (!Config.Initialized())
            {
                if (!Config.Permit.Request()) return (int)ExitId.Canceled;
            }

            try
            {
                if (!_ParsedCommandLine(out Issue vIssue))
                {
                    _Report(vIssue);
                    return (int)ExitId.Failed;
                }

                _Banner();

                byte[] vCrcBuffer = null; // one 64K buffer reused across every Crc32 call (Crc32.txt reuse pattern)

                using (TextWriter xOut = _OpenOutput())
                {
                    if (File.Exists(_TargetPath))
                    {
                        // FILE ARG: report just that file, unfiltered
                        _WriteHeader(xOut, _Props, _Format);
                        _WriteRow(xOut, _Props, new FileInfo(_TargetPath), _Format, ref vCrcBuffer);
                    }
                    else
                    {
                        _WriteHeader(xOut, _Props, _Format);
                        _Walk(new DirectoryInfo(_TargetPath), xOut, ref vCrcBuffer);
                    }
                }

                if (_GotOut)
                {
                    Console.Error.WriteLine();
                    Console.Error.WriteLine("written to: " + _OutFilePath);

                    if (_OpenWhenDone)
                    {
                        FS.Started(_OutFilePath);
                    }
                }

                return (int)ExitId.Completed;
            }
            catch (Issue theIssue)
            {
                _Report(theIssue);
                return (int)(theIssue.IsProgrammingIssue ? ExitId.Critical : ExitId.Failed);
            }
            catch (Exception theException)
            {
                _Report(Issue.Create(issueSource, 1, theException, Issue.KindOf(theException)));
                return (int)ExitId.Critical;
            }
        }

        //==== COMMAND LINE ====

        // FileList [path] [+<attrs>] [-<attrs>] [-all] [-out[:<file>]]
        //          [-open] [-props:<name,name,...>] [-preset:excel]
        //          [-format:csv|tsv|lsv|md]
        private static bool _ParsedCommandLine(out Issue returnIssue)
        {
            returnIssue = null;

            Chars vParams = Exe.GetCommandLineParams();

            while (vParams.PluckedVisible_or_QuotedValue(out Chars vToken))
            {
                char cLead = vToken.BotChar_or_NUL;

                if (cLead == Chars.PLUS || cLead == Chars.DASH)
                {
                    vToken.PluckChar_or_NUL(); // consume '+' or '-'

                    if (cLead == Chars.DASH && vToken.Equals("all", ignoreCase: true))
                    {
                        _Recurse = true;
                        continue;
                    }

                    if (cLead == Chars.DASH && vToken.Equals("open", ignoreCase: true))
                    {
                        _OpenWhenDone = true;
                        continue;
                    }

                    if (cLead == Chars.DASH)
                    {
                        string sRest = vToken.ToString();

                        if (sRest.Equals("out", StringComparison.OrdinalIgnoreCase))
                        {
                            _GotOut = true;
                            continue;
                        }

                        if (sRest.StartsWith("out:", StringComparison.OrdinalIgnoreCase))
                        {
                            _GotOut = true;
                            _OutFilePath = sRest.Substring(4);
                            continue;
                        }

                        if (sRest.StartsWith("props:", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!_GotProps(sRest.Substring(6), out returnIssue)) return false;
                            continue;
                        }

                        if (sRest.StartsWith("preset:", StringComparison.OrdinalIgnoreCase))
                        {
                            string sPreset = sRest.Substring(7);

                            if (sPreset.Equals("excel", StringComparison.OrdinalIgnoreCase))
                            {
                                _Props = _PresetExcel;
                            }
                            else if (sPreset.Equals("default", StringComparison.OrdinalIgnoreCase))
                            {
                                _Props = _PresetDefault;
                            }
                            else
                            {
                                returnIssue = Issue.Create(issueSource, 5
                                    , Say.Unknown_option, "-preset:" + sPreset
                                    , IssueKind.BadEntry);
                                return false;
                            }
                            continue;
                        }

                        if (sRest.StartsWith("format:", StringComparison.OrdinalIgnoreCase))
                        {
                            string sFormat = sRest.Substring(7);

                            if (!Enum.TryParse(sFormat, true, out OutputFormatId iFormat))
                            {
                                returnIssue = Issue.Create(issueSource, 6
                                    , Say.Unknown_output_format, sFormat
                                    , IssueKind.BadEntry);
                                return false;
                            }
                            _Format = iFormat;
                            continue;
                        }
                    }

                    // remaining token: ARHSCE letters (only meaningful
                    // when walking a folder; harmlessly unused for a
                    // single-file target)
                    FileAttr vAttr = FileAttr.Get(vToken, out returnIssue);

                    if (returnIssue != null) return false;

                    if (cLead == Chars.PLUS)
                    {
                        _IncludeBits |= vAttr.Value;
                        _GotInclude = true;
                    }
                    else
                    {
                        _ExcludeBits |= vAttr.Value;
                        _GotExclude = true;
                    }

                    continue;
                }

                if (_TargetPath == null)
                {
                    _TargetPath = FS.ValidPath_or_null(Path.GetFullPath(vToken.ToString()), out returnIssue);

                    if (returnIssue != null) return false;

                    if (!File.Exists(_TargetPath) && !Directory.Exists(_TargetPath))
                    {
                        returnIssue = Issue.Create(issueSource, 7
                            , Say.No_such_file_or_folder, AS.Quoted(_TargetPath)
                            , IssueKind.NoSuch);
                        return false;
                    }

                    continue;
                }

                returnIssue = Issue.Create(issueSource, 8
                    , Say.Unexpected_extra_parameter, vToken.ToQuoted()
                    , IssueKind.BadEntry);
                return false;
            }

            if (_TargetPath == null)
            {
                _TargetPath = Environment.CurrentDirectory; // DEMO MODE
            }

            // Debug convenience (JOB.WRAP Part A, 2026-07-28): F5 in VS
            // with no -out/-open typed - write to Results and open it,
            // so a debugging pass doesn't need a manual file name to
            // see real output. Explicit -out/-open always still win.
            if (!_GotOut && Exe.DebuggerIsAttached && Exe.IsDebug)
            {
                _GotOut = true;
                _OpenWhenDone = true;
            }

            return true;
        }

        private static bool _GotProps(string theCsvList, out Issue returnIssue)
        {
            returnIssue = null;

            string[] xTokens = theCsvList.Split(',');

            FileListPropertyId[] xProps = new FileListPropertyId[xTokens.Length];

            for (int i = 0; i < xTokens.Length; i++)
            {
                if (!_GotProperty(xTokens[i].Trim(), out xProps[i]))
                {
                    returnIssue = Issue.Create(issueSource, 9
                        , Say.Unknown_property, xTokens[i].Trim()
                        , IssueKind.BadEntry);
                    return false;
                }
            }

            _Props = xProps;
            return true;
        }

        // Property lookup by name, token, caption, OR index (SPEC:
        // "built via EnumCodes - string names/indexes/symbols").
        private static bool _GotProperty(string theToken, out FileListPropertyId returnId)
        {
            if (int.TryParse(theToken, out int iIndex))
            {
                if (iIndex >= 0 && iIndex < _PropCodes.Count)
                {
                    returnId = (FileListPropertyId)_PropCodes.Code(iIndex);
                    return true;
                }
                returnId = default;
                return false;
            }

            int i = _PropCodes.IndexOf(theToken);

            if (i >= 0)
            {
                returnId = (FileListPropertyId)_PropCodes.Code(i);
                return true;
            }

            returnId = default;
            return false;
        }

        //==== OUTPUT DESTINATION ====

        // Bare invocation: to stdout. "-out" bare: a timestamped file
        // under Config.FolderId.Results (opt-in output, never written
        // unless asked). "-out:<file>": that exact path.
        private static TextWriter _OpenOutput()
        {
            if (!_GotOut) return Console.Out;

            if (string.IsNullOrEmpty(_OutFilePath))
            {
                string sResultsFolder = Config.FolderPath(Config.FolderId.Results);

                _OutFilePath = sResultsFolder
                    + Exe.ExeNameOnly + "." + DateTime.Now.ToString("yyyyMMdd.HHmmss") + "." + _Format;
            }
            else
            {
                _OutFilePath = Path.GetFullPath(_OutFilePath);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_OutFilePath));

            return new StreamWriter(_OutFilePath, append: false, System.Text.Encoding.UTF8);
        }

        //==== THE WALK ====

        private static void _Walk(DirectoryInfo theRoot, TextWriter theOut, ref byte[] refCrcBuffer)
        {
            ItemStack<DirectoryInfo> vWork = new ItemStack<DirectoryInfo>();

            vWork.Push(theRoot);

            while (vWork.NotEmpty)
            {
                DirectoryInfo vAt = vWork.Pop();

                FileSystemInfo[] xEntries;

                try
                {
                    xEntries = vAt.GetFileSystemInfos();
                }
                catch (Exception theException)
                {
                    _Report(FS.AsFileIssue(theException, vAt.FullName));
                    continue;
                }

                for (int i = 0; i < xEntries.Length; i++)
                {
                    FileSystemInfo xEntry = xEntries[i];

                    bool bIsFolder = 0 != (xEntry.Attributes & FileAttributes.Directory);

                    if (bIsFolder)
                    {
                        if (_Recurse) vWork.Push((DirectoryInfo)xEntry);
                        continue;
                    }

                    FileAttr vAttr = FileAttr.Get(xEntry);

                    // D8 (carried from ListFiles/OLD): include tested
                    // first, exclude second - a letter in both wins as
                    // an exclusion.
                    if (_GotInclude && !vAttr.Selected(_IncludeBits)) continue;
                    if (_GotExclude && vAttr.Selected(_ExcludeBits)) continue;

                    _WriteRow(theOut, _Props, (FileInfo)xEntry, _Format, ref refCrcBuffer);
                }
            }

            vWork.Dispose();
        }

        //==== PROPERTY VALUES ====

        private static string _PropertyValue(FileListPropertyId theId, FileInfo theFile, ref byte[] refCrcBuffer)
        {
            switch (theId)
            {
                case FileListPropertyId.NameExt: return theFile.Name;
                case FileListPropertyId.Ext: return theFile.Extension;
                case FileListPropertyId.Name: return FS.FileNameOnly(theFile);
                case FileListPropertyId.Path: return theFile.FullName;
                case FileListPropertyId.Folder: return FS.EnsureDSepTail(theFile.DirectoryName);
                case FileListPropertyId.Url: return FS.GetFileUrl_or_null(theFile.FullName) ?? string.Empty;
                case FileListPropertyId.Attr: return FileAttr.Get(theFile).ToString();
                case FileListPropertyId.Size: return theFile.Length.ToString();
                case FileListPropertyId.SizeText: return _SizeText(theFile.Length);
                case FileListPropertyId.Updated: return theFile.LastWriteTime.ToString(_DateFmt);
                case FileListPropertyId.Created: return theFile.CreationTime.ToString(_DateFmt);
                case FileListPropertyId.Crc32:
                    Crc32.TryCompute(theFile.FullName, ref refCrcBuffer, out uint iCrc);
                    return iCrc.ToString("X8");
            }
            return string.Empty;
        }

        private static string _SizeText(long theBytes)
        {
            double d = theBytes;
            int iUnit = 0;
            while (d >= 1024 && iUnit < _SizeUnits.Length - 1)
            {
                d /= 1024;
                iUnit++;
            }
            return (iUnit == 0 ? theBytes.ToString() : d.ToString("0.##")) + AS.SP + _SizeUnits[iUnit];
        }

        //==== OUTPUT WRITERS (csv / tsv / lsv / md) ====

        private static void _WriteHeader(TextWriter writer, FileListPropertyId[] props, OutputFormatId format)
        {
            byte[] xUnused = null;

            switch (format)
            {
                case OutputFormatId.csv: _WriteDelimited(writer, props, ',', null, ref xUnused); break;
                case OutputFormatId.tsv: _WriteDelimited(writer, props, '\t', null, ref xUnused); break;
                case OutputFormatId.md:
                    writer.Write("| ");
                    for (int i = 0; i < props.Length; i++)
                    {
                        if (i > 0) writer.Write(" | ");
                        writer.Write(props[i].ToString());
                    }
                    writer.WriteLine(" |");
                    writer.Write("|");
                    for (int i = 0; i < props.Length; i++) writer.Write("---|");
                    writer.WriteLine();
                    break;
                case OutputFormatId.lsv:
                    break; // each record below carries its own context line
            }
        }

        private static void _WriteRow(TextWriter writer, FileListPropertyId[] props, FileInfo theFile, OutputFormatId format, ref byte[] refCrcBuffer)
        {
            switch (format)
            {
                case OutputFormatId.csv: _WriteDelimited(writer, props, ',', theFile, ref refCrcBuffer); break;
                case OutputFormatId.tsv: _WriteDelimited(writer, props, '\t', theFile, ref refCrcBuffer); break;
                case OutputFormatId.md:
                    writer.Write("| ");
                    for (int i = 0; i < props.Length; i++)
                    {
                        if (i > 0) writer.Write(" | ");
                        writer.Write(_PropertyValue(props[i], theFile, ref refCrcBuffer).Replace("|", "\\|"));
                    }
                    writer.WriteLine(" |");
                    break;
                case OutputFormatId.lsv:
                    writer.Write(LsvRecord.RecordMarker);
                    writer.Write("FILE ");
                    writer.WriteLine(theFile.FullName);
                    for (int i = 0; i < props.Length; i++)
                    {
                        writer.Write(LsvRecord.FieldMarker);
                        writer.Write(props[i].ToString());
                        writer.Write(AS.SP);
                        writer.WriteLine(_PropertyValue(props[i], theFile, ref refCrcBuffer));
                    }
                    break;
            }
        }

        private static void _WriteDelimited(TextWriter writer, FileListPropertyId[] props, char sep, FileInfo theFile, ref byte[] refCrcBuffer)
        {
            for (int i = 0; i < props.Length; i++)
            {
                if (i > 0) writer.Write(sep);

                string sVal = theFile == null ? props[i].ToString() : _PropertyValue(props[i], theFile, ref refCrcBuffer);

                _WriteQuotedIfNeeded(writer, sVal, sep);
            }
            writer.WriteLine();
        }

        private static void _WriteQuotedIfNeeded(TextWriter writer, string theValue, char sep)
        {
            bool bNeedsQuote = theValue.IndexOf(sep) >= 0 || theValue.IndexOf('"') >= 0
                || theValue.IndexOf('\r') >= 0 || theValue.IndexOf('\n') >= 0;

            if (!bNeedsQuote)
            {
                writer.Write(theValue);
                return;
            }

            writer.Write('"');
            writer.Write(theValue.Replace("\"", "\"\""));
            writer.Write('"');
        }

        //==== REPORTING ====

        private static void _Report(Issue theIssue)
        {
            if (theIssue.IsProgrammingIssue)
            {
                Console.Error.WriteLine(theIssue.Header_Detail_Message_Inner);
                return;
            }

            if (theIssue.IsAny(IssueKind.NeedPermit, IssueKind.WrongPermit, IssueKind.LockedAccess))
            {
                Console.Error.WriteLine("  ! " + theIssue.Message.Replace(FS.LSep, AS.SP + AS.DASH + AS.SP));
                return;
            }

            if (theIssue.Kind == IssueKind.BadEntry)
            {
                Console.Error.WriteLine(theIssue.Message);
                _Usage();
                return;
            }

            Console.Error.WriteLine(theIssue.Header_Message);
        }

        //==== TEXT ====

        private static void _Banner()
        {
            Console.Error.WriteLine(Exe.ExeNameOnly + " " + Exe.ExeVersion + " - Source77NW sample (" + Exe.DomainName + ")");
            Console.Error.WriteLine("  target:  " + _TargetPath);
            Console.Error.WriteLine("  format:  " + _Format);
            Console.Error.WriteLine("  props:   " + string.Join(",", Array.ConvertAll(_Props, p => p.ToString())));
            Console.Error.WriteLine("  scope:   " + (_Recurse ? "all inner folders (-all)" : "top-level only (default)"));
            Console.Error.WriteLine("  output:  " + (_GotOut ? "file (see below)" : "stdout"));
            Console.Error.WriteLine();
        }

        private static void _Usage()
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("usage: FileList [path] [+<attrs>] [-<attrs>] [-all] [-out[:<file>]] [-open]");
            Console.Error.WriteLine("                [-props:<name,name,...>] [-preset:excel|default] [-format:csv|tsv|lsv|md]");
            Console.Error.WriteLine("  path            file or folder (default: current directory)");
            Console.Error.WriteLine("  +<attrs>        ARHSCE letters - include only files sharing one of these bits");
            Console.Error.WriteLine("  -<attrs>        ARHSCE letters - exclude files sharing one of these bits");
            Console.Error.WriteLine("                  (a letter in both is excluded - exclude applied after include)");
            Console.Error.WriteLine("  -all            recurse inner folders (default: top-level only; folder targets only)");
            Console.Error.WriteLine("  -out            write to a timestamped file under the samples Results folder");
            Console.Error.WriteLine("  -out:file        write to that exact file path");
            Console.Error.WriteLine("  -open            open the output file when done (with -out)");
            Console.Error.WriteLine("  -props:a,b,c     explicit column list, by name, token, caption, or index");
            Console.Error.WriteLine("  -preset:excel    Excel-friendly column order (no Crc32; Folder last)");
            Console.Error.WriteLine("  -preset:default  every property, declaration order (the default)");
            Console.Error.WriteLine("  -format:csv|tsv|lsv|md   output format (default: csv)");
        }
    }
}
