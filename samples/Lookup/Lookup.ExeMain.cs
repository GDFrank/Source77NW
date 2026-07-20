// Copyright (c) GDFrank
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
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
[assembly: AssemblyProduct("Lookup.exe")]
[assembly: AssemblyMetadata("ExeCodeName", "LOOKUP")]
[assembly: Guid("ca1396cd-ad8b-4ff3-91df-2e823b513e07")]
[assembly: AssemblyVersion("2026.07.19.1200")] // explicit yyyy.MM.dd.HHmm; a 0.0.0.0 version falls back to the exe's file timestamp (see Exe.cs)

namespace Samples
{
    /// <summary>
    /// Lookup - loads an LSV glossary document ONCE into a single
    /// string, indexes every record as Chars views into that string
    /// (no intermediate strings), sorts the index, and answers key
    /// queries by binary search over the views - materializing text
    /// only when an answer is written out. The zero-copy load pattern
    /// used by AppLab's Cmd.OpsUser, in miniature. This is an identity
    /// pass only (JOB.SAMPLES step 5): the [assembly:] block above is
    /// new, everything else below is the existing Lookup behavior,
    /// unchanged - see README.md beside this file.
    /// </summary>
    internal static class LookupMain
    {
        // Every module that raises Issues declares its issueSource;
        // each raise site gets a distinct Spot byte. Source77NW core
        // reserves 65,000+ - samples/apps use the low range.
        private const ushort issueSource = 101;

        /// <summary>User-facing message captions: Issue.Create renders any
        /// enum name with underscores as spaces.</summary>
        private enum Say
        {
            Unknown_option,
            The_option_needs_a_file_path,
            Not_a_GLOSSARY_document,
        }

        private const string DocName = "GLOSSARY";

        private const string RecordCode = "def"; // the record type we index; others (rem, dividers) are skipped

        /// <summary>One indexed record: the key VIEW and the record it
        /// came from - four ints and a string reference each, all
        /// pointing into the one loaded document text. Nothing here is
        /// a new string.</summary>
        private struct Entry
        {
            public Chars Key;           // = the name after the record code, captured once
            public LsvRecord Record;    // named fields, enumerated on demand
        }

        private static readonly LsvDoc _Doc = new LsvDoc();

        private static ItemStack<Entry> _Entries;

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
                // keys wait on their own stack until the doc is loaded
                ItemStack<Chars> vKeys = new ItemStack<Chars>();

                if (!_ParsedCommandLine(vKeys, out string sFilePath, out Issue vIssue)
                    || !_Loaded(sFilePath, out vIssue))
                {
                    _Report(vIssue);
                    return (int)ExitId.Failed;
                }

                Console.WriteLine(string.Format("{0}: {1:N0} entries indexed over one {2:N0}-char string."
                    , _Doc.DocName, _Entries.Count, _Doc.DocText.Length));
                Console.WriteLine();

                if (vKeys.IsEmpty)
                {
                    // interactive: a key per line, empty line ends
                    string sLine;

                    while (!string.IsNullOrWhiteSpace(sLine = _Prompt()))
                    {
                        _Answer(new Chars(sLine).Trim());
                    }
                }
                else
                {
                    // FIFO through the queried keys, in the order given
                    while (vKeys.NotEmpty)
                    {
                        _Answer(vKeys.Pluck());
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

        // Lookup [-f file.lsv] [key ...]
        //
        // Keys are plucked as Chars views over the process command line
        // and PUSHED AS VIEWS onto an ItemStack<Chars> - they never
        // become strings either.
        private static bool _ParsedCommandLine(ItemStack<Chars> push_keys, out string returnFilePath, out Issue returnIssue)
        {
            returnIssue = null;

            returnFilePath = null;

            Chars vParams = Exe.GetCommandLineParams();

            while (vParams.PluckedVisible_or_QuotedValue(out Chars vToken))
            {
                if (vToken.BotChar_or_NUL == Chars.DASH)
                {
                    vToken.PluckChar_or_NUL(); // consume the '-'

                    if (vToken.Equals("f", ignoreCase: true))
                    {
                        if (vParams.PluckedVisible_or_QuotedValue(out Chars vPath))
                        {
                            returnFilePath = vPath.ToString(); // a real path leaves the view world

                            continue;
                        }

                        returnIssue = Issue.Create(issueSource, 2
                            , Say.The_option_needs_a_file_path, "-f"
                            , IssueKind.BadEntry);

                        return false;
                    }

                    returnIssue = Issue.Create(issueSource, 3
                        , Say.Unknown_option, vToken.ToQuoted()
                        , IssueKind.BadEntry);

                    return false;
                }

                push_keys.Push(vToken);
            }

            // SOFT default: the glossary shipped beside the exe
            if (returnFilePath == null)
            {
                returnFilePath = Exe.ExeFolderPath + "glossary.lsv";
            }

            return true;
        }

        //==== LOAD AND INDEX (the OpsUser pattern) ====

        // ONE file read -> ONE string (LsvDoc.DocText). Every record is
        // then a set of (BotIndex, TopIndex) views into that string:
        // the key is the record's name view, the value is the record
        // itself with its fields still unenumerated. Sorting compares
        // the key views in place; nothing is copied out.
        private static bool _Loaded(string theFilePath, out Issue returnIssue)
        {
            if (!_Doc.LoadedDocFromFile(theFilePath, out returnIssue))
            {
                return false;
            }

            if (!_Doc.IsDocName(DocName))
            {
                returnIssue = Issue.Create(issueSource, 4
                    , Say.Not_a_GLOSSARY_document
                    , AS.Quoted(theFilePath)
                    , IssueKind.BadData);

                return false;
            }

            // Estimate capacity from average record length (~90 chars).
            int iInitial = _Doc.DocText.Length / 90;

            if (iInitial < 16) iInitial = 16;

            _Entries = new ItemStack<Entry>(iInitial)
            {
                // sorting/searching compares the KEY VIEWS, ignore-case -
                // across DIFFERENT base strings when the probe is a query
                Comparer = Comparer<Entry>.Create((a, b) => a.Key.CompareTo(b.Key, ignoreCase: true))
            };

            while (_Doc.PluckedItemRecord(out LsvRecord vRecord))
            {
                // the OpsUser move: split the record context into its
                // record code and the remainder, then dispatch on the code
                vRecord.Context.GotNameAndText(out Chars vCode, out Chars vKey);

                if (!vCode.Equals(RecordCode, ignoreCase: true)) continue;
                // ^ ':rem' remarks, ':::' dividers, unknown codes: skipped

                vKey.Trim();

                if (vKey.IsEmpty) continue;

                _Entries.Push(new Entry()
                {
                    Key = vKey,        // parsed ONCE, kept as the view
                    Record = vRecord,  // named fields, enumerated on demand
                });
            }

            _Entries.Sort();

            return true;
        }

        //==== LOOKUP ====

        // The probe Entry's Key is a view over the QUERY text; the
        // stored keys are views over the DOCUMENT text. CompareTo
        // compares content, so one binary search spans both worlds -
        // still without a string on either side.
        private static void _Answer(Chars theKey)
        {
            if (theKey.IsEmpty) return;

            int iIndex = _Entries.BinarySearchNearest(new Entry() { Key = theKey }, out bool bFound);

            if (!bFound)
            {
                if (iIndex > _Entries.EndItemIndex) iIndex = _Entries.EndItemIndex;

                Console.Write("? ");
                theKey.Write(Console.Out);
                Console.Write(" - not found; nearest is ");
                _Entries[iIndex].Key.Write(Console.Out);
                Console.WriteLine();
                Console.WriteLine();

                return;
            }

            Entry vEntry = _Entries[iIndex];

            // Chars.Write streams the views to the writer: even the
            // OUTPUT needs no ToString.
            Console.Write(": ");
            vEntry.Key.Write(Console.Out);
            Console.WriteLine();

            int iCursor = 0;

            while (vEntry.Record.GotNextFieldNameAndValue(ref iCursor, out Chars vName, out Chars vValue))
            {
                // fields are optional and repeatable - whatever the record
                // carries is what prints, in order

                Console.Write("  ");
                vName.Write(Console.Out);

                int iPad = 6 - vName.Length;

                Console.Write(new string(' ', iPad > 1 ? iPad : 1));

                // a multi-line value is ONE view with embedded line
                // separators; re-indent it line by line - still views
                bool bFirst = true;

                vValue.Trim();

                while (vValue.PluckedLine(out Chars vLine))
                {
                    if (!bFirst) Console.Write("        ");

                    bFirst = false;

                    vLine.Write(Console.Out);
                    Console.WriteLine();
                }
            }

            Console.WriteLine();
        }

        //==== TEXT ====

        private static string _Prompt()
        {
            Console.Write("key> ");

            return Console.ReadLine();
        }

        private static void _Report(Issue theIssue)
        {
            if (theIssue.IsProgrammingIssue)
            {
                Console.Error.WriteLine(theIssue.Header_Detail_Message_Inner);

                return;
            }

            if (theIssue.Kind == IssueKind.BadEntry)
            {
                Console.WriteLine(theIssue.Message);
                Console.WriteLine();
                Console.WriteLine("usage: Lookup [-f file.lsv] [key ...]");
                Console.WriteLine("  no keys: interactive - a key per line, empty line ends");

                return;
            }

            Console.WriteLine(theIssue.Header_Message);
        }
    }
}
