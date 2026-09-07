// Copyright (c) GDFrank - 77NW.net. All rights reserved.
// Licensed under the MIT License.
// SPDX-License-Identifier: MIT

using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Source77NW;

// ================================================================
// ASSEMBLY IDENTITY - PER-EXE
// Domain-wide attributes live once in samples\Config.cs,
// linked via samples\Directory.Build.props (supersedes
// SamplesCommon.cs). AssemblyVersion is auto-stamped the same
// way - see Exe.cs remarks.
// ================================================================

[assembly: AssemblyProduct("ListView.exe")]
[assembly: AssemblyMetadata("ExeCodeName", "LISTVIEW")]
[assembly: Guid("9a3d5e7c-1f4b-4a8d-8c6e-5b2f9d0a4e17")]

namespace Samples77NW
{
    /// <summary>
    /// ListView - a WinForms front-end for FileList.exe: shells out to
    /// it with UI-constructed arguments (folder, filters, properties,
    /// format), then loads the result into a real
    /// <see cref="System.Windows.Forms.ListView"/> control. The
    /// interesting parts live in MainForm.cs; Main only hosts the
    /// boot call and the message loop inside the house error regime
    /// (see the EnumCodingEG sample's main.cs for the same pattern).
    /// </summary>
    internal static class ListViewMain
    {
        // Samples/apps use the low issueSource range; Source77NW core
        // reserves 65,000+. FileList has 102; ListView takes 103.
        private const ushort issueSource = 103;

        [STAThread]
        private static int Main()
        {
            // Boot contract (DEV.txt EXE BOOT PATTERN): Config.Initialized
            // is the FIRST action, before any UI or exe-specific setting
            // is touched. False = no domain folder = no consent yet:
            // Permit.Request presents the notice DialogBox and asks - no
            // Y, no run (JOB.PERMIT 2026-07-29).
            if (!Config.Initialized())
            {
                if (!Config.Permit.Request()) return (int)ExitId.Canceled;
            }

            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetHighDpiMode(HighDpiMode.SystemAware);

                Application.Run(new MainForm());

                return (int)ExitId.Completed;
            }
            catch (Issue theIssue)
            {
                MessageBox.Show(theIssue.Header_Detail_Message_Inner
                    , "ListView sample"
                    , MessageBoxButtons.OK, MessageBoxIcon.Error);

                return (int)(theIssue.IsProgrammingIssue ? ExitId.Critical : ExitId.Failed);
            }
            catch (Exception theException)
            {
                Issue vIssue = Issue.Create(issueSource, 1, theException, Issue.KindOf(theException));

                MessageBox.Show(vIssue.Header_Detail_Message_Inner
                    , "ListView sample"
                    , MessageBoxButtons.OK, MessageBoxIcon.Error);

                return (int)ExitId.Critical;
            }
        }
    }
}
