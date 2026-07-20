// CopyrightCopyright (c) GDFrank
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Source77NW;

// ================================================================
// ASSEMBLY IDENTITY
// Source77NW.Exe reads these [assembly:] attributes once, at first
// touch, to establish this exe's identity (Exe.DomainName,
// Exe.ExeCodeName, Exe.ExeGuid, Exe.ExeVersion, Exe.Contact,
// Exe.DomainFolderPath(...), etc - see Exe.cs remarks for the full
// contract). In a real multi-exe domain the block marked
// "domain-wide" below would move to one shared EntryAssembly.Domain.cs
// imported by every exe in the domain (compare AppLab's
// EXE$ExeInfo.cs + EXE.<code>.ExeInfo.cs split); a standalone sample
// keeps both groups together in this one file (JOB.SAMPLES D1).
// ================================================================

// -- domain-wide (would move to a shared Domain.cs in a multi-exe domain) --
[assembly: AssemblyCompany("GDFrank")] // repo convention, not 77NW.net
[assembly: AssemblyCopyright("Copyright (c) GDFrank - All rights reserved")]
[assembly: AssemblyMetadata("Contact", "mailto:NOBODYHERE@SAMPLES.invalid")] // .invalid = RFC 2606 reserved TLD, guaranteed dead (D5)
[assembly: AssemblyMetadata("DomainName", "Source77NW.example")] // .example = RFC 2606 reserved documentation TLD, declared dummy (D2)
[assembly: AssemblyMetadata("DomainGuid", "be9f2bd9-9566-4ec0-b381-cfa56e33a2cb")] // one guid shared by all AppRepo samples
#if DEBUG
[assembly: AssemblyMetadata("DeployDebug", "debug")]
#endif
#if CONSOLE
[assembly: AssemblyMetadata("ExeInterface", "console")]
#endif

// -- per-exe --
[assembly: AssemblyProduct("ListFiles.exe")]
[assembly: AssemblyMetadata("ExeCodeName", "LISTFILES")]
[assembly: Guid("1ace2562-ac4c-429d-94df-4dc8cf0714e7")]
[assembly: AssemblyVersion("2026.07.19.1200")] // explicit yyyy.MM.dd.HHmm; a 0.0.0.0 version falls back to the exe's file timestamp (see Exe.cs)

namespace Samples
{
    /// <summary>
    /// ListFiles - walks a folder, filters entries by <see cref="FileAttr"/>
    /// bits, and writes a TSV listing (FileName, Length, Attributes,
    /// Folder) meant to paste straight into Excel. A "real app" sample:
    /// establishes EntryAssembly identity through <see cref="Exe"/> (the
    /// [assembly:] block above) the way a production Source77NW exe
    /// would, then behaves like an ordinary command-line tool from
    /// there - see README.md beside this file.
    /// </summary>
    internal static class ListFilesMain
    {
        // Every module that raises Issues declares its issueSource;
        // each raise site gets a distinct Spot byte. Source77NW core
        // reserves 65,000+ - samples/apps use the low range.
        private const ushort issueSource = 100;

        /// <summary>User-facing message captions: Issue.Create renders any
        /// enum name with underscores as spaces, so messages live as
        /// identifiers - greppable, typo-proof, one per meaning.</summary>
        private enum Say
        {
            Unknown_option,
            Unexpected_extra_parameter,
            No_such_folder,
            Another_instance_is_already_running,
        }

        //==== OPTIONS (from the command line) ====

        private static string _FolderPath;                 // root to list; SOFT default = current dir
        private static FileAttr.Bits _IncludeBits;          // +<attrs> : must share one of these (0 = no filter)
        private static bool _GotInclude;
        private static FileAttr.Bits _ExcludeBits;          // -<attrs> : must share NONE of these
        private static bool _GotExclude;
        private static bool _Recurse;                       // -all : recurse inner folders (D10 default = top-only)
        private static bool _GotOut;                        // -out was given at all
        private static string _OutFilePath;                 // -out:<file> ; bare -out fills this from Exe.DomainFolderPath

        //==== COUNTERS ====

        private static int _Folders, _Files, _Shown, _Denied;

        //==== ENTRY ====

        private static int Main()
        {
            // Sample-grade boot (D1): a real multi-exe domain wires this
            // once via a shared ExeBoot.Initialize() - see AppLab's
            // EXE$ExeBoot.cs - a standalone sample inlines the one call
            // it actually needs: a process-wide critical-issue handler.
            Exe.SetCritical(theCritical =>
                Console.Error.WriteLine(theCritical.Header_Detail_Message_Inner));

            try
            {
                if (!_ParsedCommandLine(out Issue vIssue))
                {
                    _Report(vIssue);
                    return (int)ExitId.Failed;
                }

                // ONE ListFiles at a time, machine-wide: a KindId.File lock
                // holds the file open exclusively - the open IS the lock.
                string sLockPath = Path.Combine(Path.GetTempPath(), "Source77NW.ListFiles.lock");

                if (!ExeLock.GotLock(sLockPath, ExeLock.KindId.File, out ExeLock _, out Issue vLockIssue))
                {
                    _Report(vLockIssue ?? Issue.Create(issueSource, 1
                        , Say.Another_instance_is_already_running
                        , IssueKind.LockedAccess));

                    return (int)ExitId.Canceled;
                }

                // Banner and the run summary always go to stderr, never
                // stdout: the whole point of the TSV upgrade is a clean
                // stdout stream a shell can redirect straight into a file
                // or a pipe, whether or not -out was given.
                _Banner();

                using (TextWriter xOut = _OpenOutput())
                {
                    _Walk(new DirectoryInfo(_FolderPath), xOut);
                }

                Console.Error.WriteLine();
                Console.Error.WriteLine(string.Format("{0:N0} folders, {1:N0} files; {2:N0} shown, {3:N0} not accessible."
                    , _Folders, _Files, _Shown, _Denied));

                if (_GotOut)
                {
                    Console.Error.WriteLine("written to: " + _OutFilePath);
                }

                return (int)ExitId.Completed;
            }
            catch (Issue theIssue)
            {
                // The library is LOUD on contract violations - and every
                // raise, whatever the module, arrives here as one type.
                _Report(theIssue);

                return (int)(theIssue.IsProgrammingIssue ? ExitId.Critical : ExitId.Failed);
            }
            catch (Exception theException)
            {
                // A stranger: classify it into the same regime and report
                // it the same way - one dispatch, even for foreign types.
                _Report(Issue.Create(issueSource, 2, theException, Issue.KindOf(theException)));

                return (int)ExitId.Critical;
            }
            finally
            {
                ExeLock.DisposeAll(); // release everything at shutdown
            }
        }

        //==== COMMAND LINE ====

        // ListFiles [path] [+<attrs>] [-<attrs>] [-all] [-out[:<file>]]
        //
        // Parsed with zero intermediate strings: Exe.GetCommandLineParams
        // returns the process command line as a Chars cursor already
        // positioned past the exe path, and every token is a Chars VIEW
        // into that one string - plucked, tested, and consumed in place.
        // "-all" and the "-out"/"-out:<file>" forms are reserved -<word>
        // tokens; everything else after a bare '-' or '+' is parsed as
        // FileAttr ARHSCE letters (D8: standard cmdline +/- practice).
        private static bool _ParsedCommandLine(out Issue returnIssue)
        {
            returnIssue = null;

            Chars vParams = Exe.GetCommandLineParams();

            while (vParams.PluckedVisible_or_QuotedValue(out Chars vToken))
            {
                char cLead = vToken.BotChar_or_NUL;

                if (cLead == Chars.PLUS || cLead == Chars.DASH)
                {
                    vToken.PluckChar_or_NUL(); // consume the '+' or '-'

                    if (cLead == Chars.DASH && vToken.Equals("all", ignoreCase: true))
                    {
                        _Recurse = true;
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
                    }

                    // remaining token: ARHSCE letters, parsed straight from
                    // the Chars view - a bad letter is a BadEntry Issue
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

                if (_FolderPath == null)
                {
                    // bare token: the root folder - resolved to a full path
                    // (FS validation requires rooted), then validated to an
                    // Issue (NoSuch and friends), not to an exception
                    _FolderPath = FS.ValidFolderPath_or_null(Path.GetFullPath(vToken.ToString()), out returnIssue);

                    if (returnIssue != null) return false;

                    if (!Directory.Exists(_FolderPath))
                    {
                        returnIssue = Issue.Create(issueSource, 6
                            , Say.No_such_folder, AS.Quoted(_FolderPath)
                            , IssueKind.NoSuch);

                        return false;
                    }

                    continue;
                }

                returnIssue = Issue.Create(issueSource, 5
                    , Say.Unexpected_extra_parameter, vToken.ToQuoted()
                    , IssueKind.BadEntry);

                return false;
            }

            // SOFT semantics: an absent folder is a state, not an error
            if (_FolderPath == null)
            {
                _FolderPath = Environment.CurrentDirectory;
            }

            return true;
        }

        //==== OUTPUT DESTINATION (D6) ====

        // Bare invocation: TSV to stdout (pipeable/redirectable as-is).
        // "-out" bare: a timestamped file under Exe.DomainFolderPath's
        // UserDocuments demo folder. "-out:<file>": that exact path.
        private static TextWriter _OpenOutput()
        {
            if (!_GotOut) return Console.Out;

            if (string.IsNullOrEmpty(_OutFilePath))
            {
                _OutFilePath = Exe.DomainFolderPath(Exe.DomainFolderId.UserDocuments)
                    + Exe.ExeNameOnly + "." + DateTime.Now.ToString("yyyyMMdd.HHmmss") + ".tsv";
            }
            else
            {
                _OutFilePath = Path.GetFullPath(_OutFilePath);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_OutFilePath));

            return new StreamWriter(_OutFilePath, append: false, System.Text.Encoding.UTF8);
        }

        //==== THE WALK ====

        // The traversal is a LOOP, not recursion: pending folders live on
        // an ItemStack (LIFO - depth-first is the only order this sample
        // needs; contrast Inspect's Pop()/Pluck() switch). Scope is D10:
        // default = the given folder's immediate children only; -all
        // pushes subfolders back onto the same worklist so the walk goes
        // all the way down.
        private static void _Walk(DirectoryInfo theRoot, TextWriter theOut)
        {
            theOut.WriteLine(string.Join("\t", "FileName", "Length", "Attributes", "Folder")); // header row (default on)

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
                    // SOFT at the walk level: a folder we cannot read is
                    // reported (reframed as a Kind-classified Issue) and
                    // the walk continues.
                    _Denied++;

                    _Report(FS.AsFileIssue(theException, vAt.FullName));

                    continue;
                }

                for (int i = 0; i < xEntries.Length; i++)
                {
                    FileSystemInfo xEntry = xEntries[i];

                    bool bIsFolder = 0 != (xEntry.Attributes & FileAttributes.Directory);

                    if (bIsFolder)
                    {
                        _Folders++;

                        if (_Recurse)
                        {
                            vWork.Push((DirectoryInfo)xEntry);
                        }

                        continue; // ListFiles lists FILES only; folders are walked, not printed
                    }

                    _Files++;

                    FileAttr vAttr = FileAttr.Get(xEntry);

                    // D8: contradictions permitted, applied in this
                    // documented order - include is tested first, exclude
                    // second, so a letter named in both wins as an
                    // exclusion.
                    if (_GotInclude && !vAttr.Selected(_IncludeBits)) continue;
                    if (_GotExclude && vAttr.Selected(_ExcludeBits)) continue;

                    _Shown++;

                    // Attributes = FileAttr's fixed 6-char ARHSCE text -
                    // sortable and column-aligned once pasted into Excel.
                    // NO FileAttrX here: that's NT structural esoterica,
                    // out of scope for this sample (per JOB.SAMPLES step 4).
                    theOut.WriteLine(string.Join("\t"
                        , xEntry.Name
                        , ((FileInfo)xEntry).Length
                        , vAttr.ToString()
                        , vAt.FullName));
                }
            }

            vWork.Dispose();
        }

        //==== REPORTING (dispatch by Kind, not by type) ====

        // The whole program handles ONE exception type. What differs is
        // the Kind - data, not hierarchy - so follow-up is a dispatch:
        // access problems get a friendly line and life goes on; user
        // entry problems get the usage; programming faults get the full
        // forensic trail (kind + source + spot + message + inner chain).
        // Everything here goes to stderr - stdout is reserved for TSV.
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
            Console.Error.WriteLine("  root:    " + _FolderPath);
            Console.Error.WriteLine("  include: " + (_GotInclude ? FileAttr.Get(_IncludeBits).ToString() : "(none - all files)"));
            Console.Error.WriteLine("  exclude: " + (_GotExclude ? FileAttr.Get(_ExcludeBits).ToString() : "(none)"));
            Console.Error.WriteLine("  scope:   " + (_Recurse ? "all inner folders (-all)" : "top-level only (default, D10)"));
            Console.Error.WriteLine("  output:  " + (_GotOut ? "file (see below)" : "stdout"));

            // Windows-only surface, guarded for everyone else
            if (OperatingSystem.IsWindows() && Exe.IsAdmin)
            {
                Console.Error.WriteLine("  note:    running elevated (Administrator)");
            }

            Console.Error.WriteLine();
        }

        private static void _Usage()
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("usage: ListFiles [path] [+<attrs>] [-<attrs>] [-all] [-out[:<file>]]");
            Console.Error.WriteLine("  path      root folder to list (default: current directory)");
            Console.Error.WriteLine("  +<attrs>  ARHSCE letters - show only files sharing one of these");
            Console.Error.WriteLine("            bits, e.g. +H, +AR (default: no include filter)");
            Console.Error.WriteLine("  -<attrs>  ARHSCE letters - hide files sharing one of these bits");
            Console.Error.WriteLine("            (default: no exclude filter; a letter in both +/- is");
            Console.Error.WriteLine("            excluded - exclude is applied after include)");
            Console.Error.WriteLine("  -all      recurse inner folders (default: top-level only)");
            Console.Error.WriteLine("  -out      write TSV to a timestamped file under");
            Console.Error.WriteLine("            Exe.DomainFolderPath(UserDocuments) instead of stdout");
            Console.Error.WriteLine("  -out:file write TSV to that exact file path instead of stdout");
        }
    }
}
