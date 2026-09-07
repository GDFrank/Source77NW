// Copyright (c) GDFrank - 77NW.net. All rights reserved.
// Licensed under the MIT License.
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Source77NW;

namespace Samples77NW
{
    /// <summary>
    /// The whole sample lives here: builds the argument-construction UI
    /// for FileList.exe's properties/filters/format, shells out to it
    /// (see <see cref="_RunFileList"/>), then parses whatever format was
    /// chosen back into a real <see cref="System.Windows.Forms.ListView"/>
    /// (see <see cref="_LoadResults"/>). Folder choice persists across
    /// runs via its own exe-scoped settings store (<see cref="_Settings"/>
    /// below) - an actual set+persist, not just the read-only Value() use
    /// FileList makes of the domain Config store.
    /// </summary>
    internal sealed class MainForm : Form
    {
        private const ushort issueSource = 104;

        // Mirrors FileList's (internal) FileListPropertyId names/order
        // exactly. ListView shells out to FileList as a subprocess
        // rather than referencing its types (see ListView.csproj), so
        // this list is kept in sync BY HAND with FileList.ExeMain.cs's
        // enum - see README.md "Coupling" note.
        private static readonly string[] _PropertyNames =
        {
            "NameExt", "Ext", "Name", "Path", "Folder", "Url",
            "Attr", "Size", "SizeText", "Updated", "Created", "Crc32",
        };

        private static readonly string[] _PresetExcel =
        {
            "Name", "Ext", "Attr", "Size", "Updated", "Created", "Folder",
        };

        //==================== ListView's OWN exe settings ====================

        // An exe-owned store, separate from the domain Config.Settings
        // store (two-tier rule, DEV.txt EXE BOOT PATTERN): just the
        // last-used source folder, persisted as LSV under
        // Config.FolderId.Settings - JOB.CONFORM D2 (supersedes the
        // Prefs-backed store; Prefs itself is retired).
        private static class _Settings
        {
            private enum Grp : uint { Base = Bits.GrpFlag.Grp0 }

            public enum Id : uint
            {
                LastFolder = 0 | Bits.ValFlag.folder | Grp.Base,
            }

            private static Chars _DefaultValue(EnumCodes theCodes, int theIndex)
            {
                switch ((Id)theCodes.Code(theIndex))
                {
                    case Id.LastFolder: return new Chars(Environment.CurrentDirectory);
                }
                return Chars.Nothing;
            }

            // <ExeCodeName>.Settings.txt under the domain Settings folder -
            // extension matches Config.cs's own Folders.txt/Settings.txt
            // precedent (the .txt is LSV-formatted content, not a
            // house-wide .lsv extension convention).
            private static string _FilePath => Config.FolderPath(Config.FolderId.Settings)
                + Exe.ExeCodeName + AS.DOT + "Settings" + AS.DOT_txt;

            private static EnumVals _Store;

            public static EnumVals Store
            {
                get
                {
                    if (_Store == null)
                    {
                        _Store = EnumVals.Create(typeof(Id), _DefaultValue);

                        string sText = FS.GetText_or_null(_FilePath, out _);

                        if (sText != null)
                        {
                            _Store.LoadAsLsvFormat(new Chars(sText));
                        }
                    }
                    return _Store;
                }
            }

            public static void Save() => FS.TrySavingText(_FilePath, Store.ToLsv());
        }

        //==================== CONTROLS ====================

        private TextBox _txtFolder;
        private CheckBox _chkRecurse;
        private TextBox _txtInclude;
        private TextBox _txtExclude;
        private CheckedListBox _clbProperties;
        private ComboBox _cmbFormat;
        private Label _lblStatus;
        private System.Windows.Forms.ListView _lvResults;

        // The column ORDER a preset intends ("Folder last" etc.) - a
        // plain checkbox scan would lose this since CheckedListBox
        // iterates in declaration order regardless of which preset
        // button was clicked. Updated by the preset buttons; manually
        // toggled boxes outside the active preset are appended after,
        // in declaration order (see _RunFileList).
        private string[] _ActiveOrder = _PropertyNames;

        public MainForm()
        {
            _BuildUI();
            _LoadSettings();
        }

        //==================== UI CONSTRUCTION ====================

        private void _BuildUI()
        {
            Text = "ListView - FileList front-end (Source77NW sample)";
            Width = 940;
            Height = 640;
            StartPosition = FormStartPosition.CenterScreen;

            TableLayoutPanel xRoot = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            xRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            xRoot.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            xRoot.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            xRoot.Controls.Add(_BuildOptionsPanel(), 0, 0);

            _lvResults = new System.Windows.Forms.ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
            };
            xRoot.Controls.Add(_lvResults, 0, 1);

            _lblStatus = new Label { Dock = DockStyle.Fill, AutoSize = false, Height = 24, Text = "Ready.", Padding = new Padding(4) };
            xRoot.Controls.Add(_lblStatus, 0, 2);

            Controls.Add(xRoot);
        }

        private Control _BuildOptionsPanel()
        {
            TableLayoutPanel xPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 4,
                Padding = new Padding(8),
            };

            // Row 0: folder + browse + recurse
            _txtFolder = new TextBox { Width = 420 };
            Button xBrowse = new Button { Text = "Browse..." };
            xBrowse.Click += (s, e) => _BrowseFolder();
            _chkRecurse = new CheckBox { Text = "Recurse subfolders (-all)", AutoSize = true };

            xPanel.Controls.Add(new Label { Text = "Folder:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            xPanel.Controls.Add(_txtFolder, 1, 0);
            xPanel.Controls.Add(xBrowse, 2, 0);
            xPanel.Controls.Add(_chkRecurse, 3, 0);

            // Row 1: include/exclude ARHSCE filters
            _txtInclude = new TextBox { Width = 100 };
            _txtExclude = new TextBox { Width = 100 };

            xPanel.Controls.Add(new Label { Text = "Include (+ARHSCE):", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
            xPanel.Controls.Add(_txtInclude, 1, 1);
            xPanel.Controls.Add(new Label { Text = "Exclude (-ARHSCE):", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 1);
            xPanel.Controls.Add(_txtExclude, 3, 1);

            // Row 2: properties (checked list, EnumCodes association
            // hints - here just names/order - drive the UI; the UI owns
            // rendering, see modules/EnumCodes.txt) + presets + format
            _clbProperties = new CheckedListBox { Height = 140, Width = 420, CheckOnClick = true };
            for (int i = 0; i < _PropertyNames.Length; i++)
            {
                _clbProperties.Items.Add(_PropertyNames[i], true); // "default" preset = everything checked
            }

            FlowLayoutPanel xPresetButtons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown };
            Button xDefaultPreset = new Button { Text = "Default preset", AutoSize = true };
            xDefaultPreset.Click += (s, e) => { _ApplyPreset(_PropertyNames); _ActiveOrder = _PropertyNames; };
            Button xExcelPreset = new Button { Text = "Excel preset", AutoSize = true };
            xExcelPreset.Click += (s, e) => { _ApplyPreset(_PresetExcel); _ActiveOrder = _PresetExcel; };
            xPresetButtons.Controls.Add(xDefaultPreset);
            xPresetButtons.Controls.Add(xExcelPreset);

            _cmbFormat = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
            _cmbFormat.Items.AddRange(new object[] { "csv", "tsv", "lsv", "md" });
            _cmbFormat.SelectedIndex = 0;

            FlowLayoutPanel xFormatPanel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown };
            xFormatPanel.Controls.Add(new Label { Text = "Format:", AutoSize = true });
            xFormatPanel.Controls.Add(_cmbFormat);

            xPanel.Controls.Add(new Label { Text = "Properties:", AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Left }, 0, 2);
            xPanel.Controls.Add(_clbProperties, 1, 2);
            xPanel.Controls.Add(xPresetButtons, 2, 2);
            xPanel.Controls.Add(xFormatPanel, 3, 2);

            // Row 3: the List button
            Button xList = new Button { Text = "List", Width = 100, Height = 32 };
            xList.Click += (s, e) => _RunFileList();
            xPanel.Controls.Add(xList, 1, 3);

            return xPanel;
        }

        private void _ApplyPreset(string[] theNames)
        {
            for (int i = 0; i < _clbProperties.Items.Count; i++)
            {
                string sName = (string)_clbProperties.Items[i];
                _clbProperties.SetItemChecked(i, Array.IndexOf(theNames, sName) >= 0);
            }
        }

        //==================== SETTINGS ====================

        private void _LoadSettings()
        {
            _txtFolder.Text = _Settings.Store.Value(_Settings.Id.LastFolder).ToString();
        }

        private void _SaveSettings()
        {
            _Settings.Store.SetValue(_Settings.Id.LastFolder, new Chars(_txtFolder.Text));
            _Settings.Save();
        }

        private void _BrowseFolder()
        {
            using (FolderBrowserDialog xDialog = new FolderBrowserDialog())
            {
                if (Directory.Exists(_txtFolder.Text))
                {
                    xDialog.SelectedPath = _txtFolder.Text;
                }

                if (xDialog.ShowDialog(this) == DialogResult.OK)
                {
                    _txtFolder.Text = xDialog.SelectedPath;
                }
            }
        }

        //==================== RUN FileList.exe ====================

        private void _RunFileList()
        {
            if (!Directory.Exists(_txtFolder.Text) && !File.Exists(_txtFolder.Text))
            {
                _lblStatus.Text = "No such file or folder: " + _txtFolder.Text;
                return;
            }

            List<string> vProps = new List<string>();

            // Preset order first (skipping anything the user unchecked)...
            for (int i = 0; i < _ActiveOrder.Length; i++)
            {
                int iIndex = _clbProperties.Items.IndexOf(_ActiveOrder[i]);
                if (iIndex >= 0 && _clbProperties.GetItemChecked(iIndex))
                {
                    vProps.Add(_ActiveOrder[i]);
                }
            }

            // ...then any checked box the active preset didn't cover
            // (hand-toggled extras), declaration order, appended after.
            for (int i = 0; i < _clbProperties.Items.Count; i++)
            {
                string sName = (string)_clbProperties.Items[i];
                if (_clbProperties.GetItemChecked(i) && !vProps.Contains(sName))
                {
                    vProps.Add(sName);
                }
            }

            if (vProps.Count == 0)
            {
                _lblStatus.Text = "Select at least one property.";
                return;
            }

            string sFormat = (string)_cmbFormat.SelectedItem;

            string sOutPath = Config.FolderPath(Config.FolderId.Results)
                + "ListView." + DateTime.Now.ToString("yyyyMMdd.HHmmss") + "." + sFormat;

            Directory.CreateDirectory(Path.GetDirectoryName(sOutPath));

            StringBuilder xArgs = new StringBuilder();
            xArgs.Append('"').Append(_txtFolder.Text).Append('"');

            if (_chkRecurse.Checked) xArgs.Append(" -all");

            string sInclude = _txtInclude.Text.Trim();
            if (sInclude.Length > 0) xArgs.Append(" +").Append(sInclude);

            string sExclude = _txtExclude.Text.Trim();
            if (sExclude.Length > 0) xArgs.Append(" -").Append(sExclude);

            xArgs.Append(" -props:").Append(string.Join(",", vProps));
            xArgs.Append(" -format:").Append(sFormat);
            // Quote the WHOLE "-out:..." token (not just the path) so
            // the leading quote char is what Chars-based command-line
            // parsing (PluckedVisible_or_QuotedValue) keys off of -
            // quoting only the path after the colon would leave a token
            // that does not itself start with a quote, so an embedded
            // space (a real possibility under UserProfile) would split
            // it into two arguments instead of staying one.
            xArgs.Append(" \"-out:").Append(sOutPath).Append('"');

            string sFileListExe = Path.Combine(Exe.ExeFolderPath, "FileList", "FileList.exe");

            if (!File.Exists(sFileListExe))
            {
                _lblStatus.Text = "FileList.exe not found at " + sFileListExe + " - build ListView (its target copies FileList's output alongside it).";
                return;
            }

            _lblStatus.Text = "Running FileList...";
            Application.DoEvents();

            ProcessStartInfo xInfo = new ProcessStartInfo
            {
                FileName = sFileListExe,
                Arguments = xArgs.ToString(),
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            string sStdErr;
            int iExitCode;

            using (Process xProcess = Process.Start(xInfo))
            {
                sStdErr = xProcess.StandardError.ReadToEnd();
                xProcess.WaitForExit();
                iExitCode = xProcess.ExitCode;
            }

            if (iExitCode != (int)ExitId.Completed || !File.Exists(sOutPath))
            {
                _lblStatus.Text = "FileList exited " + iExitCode + ": " + sStdErr.Trim();
                return;
            }

            _SaveSettings();

            _LoadResults(sOutPath, sFormat, vProps.ToArray());

            _lblStatus.Text = "Loaded " + _lvResults.Items.Count + " row(s) from " + sOutPath;
        }

        //==================== LOAD RESULTS (per format) ====================

        private void _LoadResults(string theFilePath, string theFormat, string[] theProps)
        {
            _lvResults.Items.Clear();
            _lvResults.Columns.Clear();

            for (int i = 0; i < theProps.Length; i++)
            {
                _lvResults.Columns.Add(theProps[i], 120);
            }

            string sText = File.ReadAllText(theFilePath);

            List<string[]> vRows;

            switch (theFormat)
            {
                case "csv": vRows = _ParseDelimited(sText, Chars.COMMA); break;
                case "tsv": vRows = _ParseDelimited(sText, Chars.TAB); break;
                case "lsv": vRows = _ParseLsv(sText, theProps); break;
                case "md": vRows = _ParseMarkdown(sText); break;
                default: vRows = new List<string[]>(); break;
            }

            for (int r = 0; r < vRows.Count; r++)
            {
                string[] vRow = vRows[r];

                ListViewItem xItem = new ListViewItem(vRow.Length > 0 ? vRow[0] : string.Empty);

                for (int c = 1; c < vRow.Length; c++)
                {
                    xItem.SubItems.Add(vRow[c]);
                }

                _lvResults.Items.Add(xItem);
            }
        }

        // csv/tsv: reuse Csv.Reader.Reader (auto-handles quoting) with
        // the header row consumed separately (columns are already known
        // from theProps, in the same order FileList wrote them).
        private static List<string[]> _ParseDelimited(string theText, char theSep)
        {
            List<string[]> vRows = new List<string[]>();

            Csv.Reader xReader = new Csv.Reader();
            xReader.Reset(theText, asHasHeader: true, theSep);

            while (xReader.GotRecord())
            {
                string[] vRow = new string[xReader.ValueCount];
                for (int i = 0; i < xReader.ValueCount; i++)
                {
                    vRow[i] = xReader.Value(i).ToString();
                }
                vRows.Add(vRow);
            }

            return vRows;
        }

        // lsv: one LsvRecord per file (":FILE <path>" header + ".<Prop>
        // <value>" fields, in theProps order - see FileList.ExeMain.cs
        // _WriteRow). Fields are matched by NAME (not just position) so
        // this stays correct even if a future FileList build reorders.
        private static List<string[]> _ParseLsv(string theText, string[] theProps)
        {
            List<string[]> vRows = new List<string[]>();

            Chars vUnparsed = new Chars(theText);

            while (LsvRecord.Parsed(ref vUnparsed, out LsvRecord vRecord))
            {
                Dictionary<string, string> vFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                int cursor = 0;
                while (vRecord.GotNextFieldNameAndValue(ref cursor, out Chars vName, out Chars vValue))
                {
                    vFields[vName.ToString()] = vValue.ToString();
                }

                string[] vRow = new string[theProps.Length];
                for (int i = 0; i < theProps.Length; i++)
                {
                    vFields.TryGetValue(theProps[i], out vRow[i]);
                    if (vRow[i] == null) vRow[i] = string.Empty;
                }

                vRows.Add(vRow);
            }

            return vRows;
        }

        // md: "| a | b |" rows; header + "|---|---|" separator skipped.
        // "\|" (our writer's escape for a literal pipe) is unescaped back.
        private static List<string[]> _ParseMarkdown(string theText)
        {
            List<string[]> vRows = new List<string[]>();

            string[] vLines = theText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < vLines.Length; i++)
            {
                string sLine = vLines[i].Trim();

                if (sLine.Length == 0) continue;

                if (i == 1) continue; // the "|---|---|" separator row

                if (sLine.StartsWith("|")) sLine = sLine.Substring(1);
                if (sLine.EndsWith("|")) sLine = sLine.Substring(0, sLine.Length - 1);

                string[] vCells = sLine.Split('|');

                for (int c = 0; c < vCells.Length; c++)
                {
                    vCells[c] = vCells[c].Trim().Replace("\\|", "|");
                }

                if (i == 0) continue; // header row - columns already known from theProps

                vRows.Add(vCells);
            }

            return vRows;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _SaveSettings();
            base.OnFormClosing(e);
        }
    }
}
