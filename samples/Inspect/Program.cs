// Copyright (c) GDFrank
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using Source77NW;

namespace Samples
{
    /// <summary>
    /// Inspect - walks a folder tree and reports each entry's attribute
    /// bits in FileAttr "ARHSCE" / FileAttrX "DVORSTN" dot-style, with
    /// an optional attribute filter, depth limit, and choice of walk
    /// order. A small real tool whose purpose is to show several
    /// Source77NW types cooperating - see README.md beside this file.
    /// </summary>
    internal static class Program
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
            The_option_needs_a_number,
            Unexpected_extra_parameter,
            No_such_folder,
            Another_instance_is_already_running,
        }

        //==== OPTIONS (from the command line) ====

        private static string _FolderPath;              // root to walk; SOFT default = current dir
        private static FileAttr _Filter;                // ARHSCE selection; unset = show all
        private static bool _GotFilter;                 // filter argument was given
        private static int _MaxDepth = int.MaxValue;    // -d N
        private static bool _Breadth;                   // -b : FIFO instead of LIFO

        //==== COUNTERS ====

        private static int _Folders, _Files, _Shown, _Denied;

        //==== ENTRY ====

        private static int Main()
        {
            try
            {
                if (!_ParsedCommandLine(out Issue vIssue))
                {
                    _Report(vIssue);
                    return (int)ExitId.Failed;
                }

                // ONE inspect at a time, machine-wide: a KindId.File lock
                // holds the file open exclusively - the open IS the lock.
                string sLockPath = Path.Combine(Path.GetTempPath(), "Source77NW.Inspect.lock");

                if (!ExeLock.GotLock(sLockPath, ExeLock.KindId.File, out ExeLock _, out Issue vLockIssue))
                {
                    _Report(vLockIssue ?? Issue.Create(issueSource, 1
                        , Say.Another_instance_is_already_running
                        , IssueKind.LockedAccess));

                    return (int)ExitId.Canceled;
                }

                _Banner();

                _Walk(new DirectoryInfo(_FolderPath));

                Console.WriteLine();
                Console.WriteLine(string.Format("{0:N0} folders, {1:N0} files; {2:N0} shown, {3:N0} not accessible."
                    , _Folders, _Files, _Shown, _Denied));

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

        // Inspect [folder] [attrs] [-d depth] [-b]
        //
        // Parsed with zero intermediate strings: Exe.GetCommandLineParams
        // returns the process command line as a Chars cursor already
        // positioned past the exe path, and every token is a Chars VIEW
        // into that one string - plucked, tested, and consumed in place.
        private static bool _ParsedCommandLine(out Issue returnIssue)
        {
            returnIssue = null;

            Chars vParams = Exe.GetCommandLineParams();

            while (vParams.PluckedVisible_or_QuotedValue(out Chars vToken))
            {
                if (vToken.BotChar_or_NUL == Chars.DASH)
                {
                    vToken.PluckChar_or_NUL(); // consume the '-'

                    if (vToken.Equals("b", ignoreCase: true))
                    {
                        _Breadth = true;
                        continue;
                    }

                    if (vToken.Equals("d", ignoreCase: true))
                    {
                        // next token must be ALL digits (plucked empty)
                        if (vParams.PluckedVisible_or_QuotedValue(out Chars vNum)
                            && vNum.PluckedDigits(out int iDepth)
                            && vNum.IsEmpty)
                        {
                            _MaxDepth = iDepth;
                            continue;
                        }

                        returnIssue = Issue.Create(issueSource, 3
                            , Say.The_option_needs_a_number, "-d"
                            , IssueKind.BadEntry);

                        return false;
                    }

                    returnIssue = Issue.Create(issueSource, 4
                        , Say.Unknown_option, vToken.ToQuoted()
                        , IssueKind.BadEntry);

                    return false;
                }

                if (_FolderPath == null)
                {
                    // first bare token: the root folder - resolved to a
                    // full path (FS validation requires rooted), then
                    // validated to an Issue (NoSuch and friends), not to
                    // an exception
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

                if (!_GotFilter)
                {
                    // second bare token: ARHSCE letters, parsed straight
                    // from the Chars view - a bad letter is a BadEntry Issue
                    _Filter = FileAttr.Get(vToken, out returnIssue);

                    if (returnIssue != null) return false;

                    _GotFilter = true;

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

        //==== THE WALK ====

        /// <summary>A pending folder on the work stack.</summary>
        private struct Visit
        {
            public DirectoryInfo Folder;
            public int Depth;
        }

        // The traversal is a LOOP, not recursion: pending folders live on
        // an ItemStack. The SAME structure gives both orders - Pop() takes
        // from the top (LIFO: depth-first), Pluck() takes from the bottom
        // (FIFO: breadth-first) - and the sliding-window design makes the
        // bottom take as cheap as the top.
        private static void _Walk(DirectoryInfo theRoot)
        {
            ItemStack<Visit> vWork = new ItemStack<Visit>();

            vWork.Push(new Visit { Folder = theRoot, Depth = 0 });

            while (vWork.NotEmpty)
            {
                Visit vAt = _Breadth ? vWork.Pluck() : vWork.Pop();

                FileSystemInfo[] xEntries;

                try
                {
                    xEntries = vAt.Folder.GetFileSystemInfos();
                }
                catch (Exception theException)
                {
                    // SOFT at the walk level: a folder we cannot read is
                    // reported (reframed as a Kind-classified Issue) and
                    // the walk continues.
                    _Denied++;

                    _Report(FS.AsFileIssue(theException, vAt.Folder.FullName));

                    continue;
                }

                for (int i = 0; i < xEntries.Length; i++)
                {
                    FileSystemInfo xEntry = xEntries[i];

                    FileAttrX vX = FileAttrX.Get(xEntry);

                    bool bIsFolder = vX.Selected(FileAttrX.Bits.D_Directory);

                    if (bIsFolder)
                    {
                        _Folders++;

                        if (vAt.Depth < _MaxDepth)
                        {
                            vWork.Push(new Visit { Folder = (DirectoryInfo)xEntry, Depth = vAt.Depth + 1 });
                        }
                    }
                    else
                    {
                        _Files++;
                    }

                    // filter: show only entries sharing a selected bit
                    if (_GotFilter && !_Filter.Selected(xEntry)) continue;

                    _Shown++;

                    Console.WriteLine(string.Format("{0} {1} {2,14}  {3}{4}"
                        , FileAttr.Get(xEntry)                                  // "A.H..."
                        , vX                                                    // "D......"
                        , bIsFolder ? "<DIR>" : ((FileInfo)xEntry).Length.ToString("N0")
                        , new string(' ', vAt.Depth * 2)
                        , xEntry.Name));
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
        private static void _Report(Issue theIssue)
        {
            if (theIssue.IsProgrammingIssue)
            {
                Console.Error.WriteLine(theIssue.Header_Detail_Message_Inner);

                return;
            }

            if (theIssue.IsAny(IssueKind.NeedPermit, IssueKind.WrongPermit, IssueKind.LockedAccess))
            {
                Console.WriteLine("  ! " + theIssue.Message.Replace(FS.LSep, AS.SP + AS.DASH + AS.SP));

                return;
            }

            if (theIssue.Kind == IssueKind.BadEntry)
            {
                Console.WriteLine(theIssue.Message);

                _Usage();

                return;
            }

            Console.WriteLine(theIssue.Header_Message);
        }

        //==== TEXT ====

        private static void _Banner()
        {
            Console.WriteLine(Exe.ExeNameOnly + " - Source77NW sample");
            Console.WriteLine("  root:   " + _FolderPath);
            Console.WriteLine("  filter: " + (_GotFilter ? _Filter.ToString() : "(none - showing all)"));
            Console.WriteLine("  order:  " + (_Breadth ? "breadth-first (FIFO Pluck)" : "depth-first (LIFO Pop)"));

            // Windows-only surface, guarded for everyone else
            if (OperatingSystem.IsWindows() && Exe.IsAdmin)
            {
                Console.WriteLine("  note:   running elevated (Administrator)");
            }

            Console.WriteLine();
        }

        private static void _Usage()
        {
            Console.WriteLine();
            Console.WriteLine("usage: Inspect [folder] [attrs] [-d depth] [-b]");
            Console.WriteLine("  folder   root to walk (default: current directory)");
            Console.WriteLine("  attrs    ARHSCE letters - show only entries with any of");
            Console.WriteLine("           these bits, e.g. H, AR, \"A.H...\" (default: all)");
            Console.WriteLine("  -d N     maximum depth below the root (default: unlimited)");
            Console.WriteLine("  -b       breadth-first order (default: depth-first)");
        }
    }
}
