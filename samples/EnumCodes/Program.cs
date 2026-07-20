// CopyrightCopyright (c) GDFrank
// SPDX-License-Identifier: MIT

using System;
using System.Windows.Forms;
using Source77NW;

namespace Samples
{
    /// <summary>
    /// EnumCodes sample entry - the interesting parts live in Cmd.cs (the
    /// truth table) and MainForm.cs (the table-driven build and dispatch).
    /// Main only hosts the message loop inside the house error regime.
    /// </summary>
    internal static class Program
    {
        // Samples/apps use the low issueSource range; Source77NW core
        // reserves 65,000+. This sample takes the 8xx series.
        private const ushort issueSource = 800;

        [STAThread]
        private static int Main()
        {
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
                // LOUD arrivals - including any Cmd table validation
                // failure (duplicate values, ByteCodes, or names) raised
                // by EnumCodes.ForType at first touch.
                MessageBox.Show(theIssue.Header_Detail_Message_Inner
                    , "EnumCodes sample"
                    , MessageBoxButtons.OK, MessageBoxIcon.Error);

                return (int)(theIssue.IsProgrammingIssue ? ExitId.Critical : ExitId.Failed);
            }
            catch (Exception theException)
            {
                // a stranger: classified into the same one-type regime
                Issue vIssue = Issue.Create(issueSource, 1, theException, Issue.KindOf(theException));

                MessageBox.Show(vIssue.Header_Detail_Message_Inner
                    , "EnumCodes sample"
                    , MessageBoxButtons.OK, MessageBoxIcon.Error);

                return (int)ExitId.Critical;
            }
        }
    }
}
