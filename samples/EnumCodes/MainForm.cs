// Copyright (c) GDFrank
// SPDX-License-Identifier: MIT

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using Source77NW;

namespace Samples
{
    /// <summary>
    /// MainForm - built ENTIRELY from the Cmd truth table. One linear pass
    /// over the EnumCodes indexes creates every menu, item, and button;
    /// one shared click handler dispatches every interaction; one
    /// index-aligned slot array binds it all. See Cmd.cs and README.md.
    /// </summary>
    internal sealed class MainForm : Form
    {
        // ==== THE TABLE ====================================================

        // Reflection and validation happen exactly ONCE, here, at first
        // touch: value order, ByteCode uniqueness, and name uniqueness are
        // THROW-validated (LOUD), so everything below runs guard-free.
        private static readonly EnumCodes Cmds = EnumCodes.ForType(typeof(Cmd));

        /// <summary>One slot per enum member, index-aligned with Cmds - the
        /// index is the join key between the enum, the controls, the radio
        /// wiring, and the dispatch. Everything at runtime is Slots[i].</summary>
        private struct CmdSlot
        {
            public CodeDefId Def;           // what the member IS
            public int Radio;               // owning radio index; -1 = none
            public int[] Members;           // radio only: member indexes
            public ToolStripMenuItem Item;  // live menu item (state display)
            public Button But;              // optional mirrored button
        }

        private readonly CmdSlot[] _Slots = new CmdSlot[Cmds.Count];

        // ==== CONTROLS =====================================================

        private readonly MenuStrip _Menu = new MenuStrip();
        private readonly FlowLayoutPanel _Bar = new FlowLayoutPanel();
        private readonly TextBox _Log = new TextBox();

        private static readonly string _StatePath
            = Path.Combine(Path.GetTempPath(), "Source77NW.EnumCodes.state");

        // ==== BUILD ========================================================

        public MainForm()
        {
            Text = "EnumCodes - Source77NW sample";
            Width = 720;
            Height = 480;

            _Log.Multiline = true;
            _Log.ScrollBars = ScrollBars.Vertical;
            _Log.Dock = DockStyle.Fill;
            _Log.WordWrap = true;

            _Bar.Dock = DockStyle.Top;
            _Bar.AutoSize = true;
            _Bar.Padding = new Padding(4);

            // ONE registration call turns on icons everywhere: providers
            // are first-found-wins, so real files (icons\ beside the exe)
            // win when present, and the synth provider guarantees a
            // visible image with zero committed binaries.
            ResCode.Register((ResCode.GotStreamDO)_GotFileIcon);
            ResCode.Register((ResCode.GotStreamDO)_GotSynthIcon);

            _BuildFromTable();

            // Buttons mirror table entries - same index, same handler,
            // caption from the but field, tip from tip, icon from icon.
            _MakeButton(Cmd.File_Open);
            _MakeButton(Cmd.File_Save);
            _MakeButton(Cmd.Edit_Cut);

            Controls.Add(_Log);
            Controls.Add(_Bar);
            Controls.Add(_Menu);
            MainMenuStrip = _Menu;

            // defaults, then persisted toggle state (ByteCode) on top
            _SetChecked(Cmd.Edit_Word_Wrap, true);
            _SetChecked(Cmd.View_Medium, true);
            _LoadState();

            FormClosing += (s, e) => _SaveState();

            _Say("ready - every item above came from the Cmd enum");
        }

        /// <summary>
        /// The whole UI in one linear pass over indexes 0..Count-1. Menu
        /// members open a top menu; actions/toggles become items under the
        /// current menu with Text/tip/shortcut/icon pulled through ResCode;
        /// radio members insert separators and wire their Tags-listed
        /// toggles back to themselves. Every item gets Tag = index and the
        /// ONE shared click handler.
        /// </summary>
        private void _BuildFromTable()
        {
            for (int i = 0; i < _Slots.Length; i++) _Slots[i].Radio = -1;

            ToolStripMenuItem xMenu = null;     // current top menu
            int iRadioOpen = -1;                // radio whose members are flowing

            for (int i = 0; i < Cmds.Count; i++)
            {
                ResCode vCode = ResCode.For(Cmds.Code(i));

                CodeDefId iDef = _DefOf(vCode);

                _Slots[i].Def = iDef;

                if (iDef == CodeDefId.menu)
                {
                    iRadioOpen = -1;

                    xMenu = new ToolStripMenuItem(vCode.Val(CodeValId.mnu).ToString());

                    _Menu.Items.Add(xMenu);

                    _Slots[i].Item = xMenu;

                    continue;
                }

                if (iDef == CodeDefId.radio)
                {
                    // the association, read straight off the declaration:
                    // every Cmd in Tags is a member of this radio
                    _Slots[i].Members = _MembersOf(vCode);

                    foreach (int iMember in _Slots[i].Members)
                        _Slots[iMember].Radio = i;

                    if (xMenu != null && xMenu.DropDownItems.Count > 0)
                        xMenu.DropDownItems.Add(new ToolStripSeparator());

                    iRadioOpen = i;

                    continue; // the radio itself is structure, not an item
                }

                // a member past the open radio's toggles closes the radio
                if (iRadioOpen >= 0 && _Slots[i].Radio != iRadioOpen)
                {
                    xMenu?.DropDownItems.Add(new ToolStripSeparator());

                    iRadioOpen = -1;
                }

                // ---- a clickable item: THE BINDING ----

                var xItem = new ToolStripMenuItem(vCode.Val(CodeValId.mnu).ToString());

                xItem.ToolTipText = vCode.Val(CodeValId.tip).ToString();

                if (vCode.GotVal(CodeValId.ctrl, out Chars vCtrl))
                    xItem.ShortcutKeys = _KeysOf(vCtrl);

                if (vCode.GotVal(CodeValId.icon, out Chars vIcon))
                    xItem.Image = _IconImage_or_null(vIcon);

                xItem.Tag = i;              // index -> slot: the whole bind
                xItem.Click += _OnCmd;      // ONE handler for everything

                xMenu?.DropDownItems.Add(xItem);

                _Slots[i].Item = xItem;
            }
        }

        /// <summary>A toolbar button mirroring a table entry: same index in
        /// Tag, same shared handler - a second control surface for free.</summary>
        private void _MakeButton(Cmd theCmd)
        {
            int i = Cmds.IndexOf(theCmd);   // binary search, ONCE, at startup

            ResCode vCode = ResCode.For(theCmd);

            var xBut = new Button();

            xBut.Text = vCode.Val(CodeValId.but).ToString();
            xBut.AutoSize = true;

            if (vCode.GotVal(CodeValId.icon, out Chars vIcon))
            {
                xBut.Image = _IconImage_or_null(vIcon);
                xBut.TextImageRelation = TextImageRelation.ImageBeforeText;
            }

            xBut.Tag = i;
            xBut.Click += _OnCmd;

            _Bar.Controls.Add(xBut);

            _Slots[i].But = xBut;
        }

        // ==== DISPATCH =====================================================

        /// <summary>
        /// EVERY interaction lands here - menu click, shortcut key, button
        /// push. Tag int -> array index -> slot: O(1), zero searching, no
        /// dictionaries, no per-control handlers. Toggle and radio state
        /// resolve from the table, then the code dispatches as a message.
        /// </summary>
        private void _OnCmd(object s, EventArgs e)
        {
            int i = (int)(s is ToolStripItem xItem ? xItem.Tag : ((Control)s).Tag);

            ref CmdSlot rSlot = ref _Slots[i];

            if (rSlot.Def == CodeDefId.toggle)
            {
                if (rSlot.Radio >= 0)
                {
                    // radio behavior from the table association: check the
                    // clicked member, clear its siblings
                    foreach (int iMember in _Slots[rSlot.Radio].Members)
                        _Slots[iMember].Item.Checked = iMember == i;
                }
                else
                {
                    rSlot.Item.Checked ^= true;
                }
            }

            _Dispatch((Cmd)Cmds.Code(i), rSlot.Item != null && rSlot.Item.Checked);
        }

        /// <summary>
        /// The enum value as a MESSAGE id: one switch is the entire
        /// operational map of the app. Grep any Cmd member and you get its
        /// declaration (Cmd.cs), its wiring (the table build), and its
        /// behavior (this switch) - uses identified throughout the code.
        /// </summary>
        private void _Dispatch(Cmd theCmd, bool isChecked)
        {
            _Say(theCmd);

            switch (theCmd)
            {
                case Cmd.File_Open:
                    using (var xDlg = new OpenFileDialog())
                        if (xDlg.ShowDialog(this) == DialogResult.OK)
                            _Say("opened " + xDlg.FileName);
                    break;

                case Cmd.File_Save:
                    _Say("(save goes here)");
                    break;

                case Cmd.File_Exit:
                    Close();
                    break;

                case Cmd.Edit_Cut:
                    if (_Log.SelectionLength > 0) _Log.Cut();
                    break;

                case Cmd.Edit_Word_Wrap:
                    _Log.WordWrap = isChecked;
                    break;

                case Cmd.View_Small:
                    _Log.Font = new Font(_Log.Font.FontFamily, 8f);
                    break;

                case Cmd.View_Medium:
                    _Log.Font = new Font(_Log.Font.FontFamily, 10f);
                    break;

                case Cmd.View_Large:
                    _Log.Font = new Font(_Log.Font.FontFamily, 13f);
                    break;
            }
        }

        // ==== TABLE READERS ================================================

        /// <summary>The first CodeDefId in the member's EnumInfo Tags -
        /// what the member IS; unspecified acts as action.</summary>
        private static CodeDefId _DefOf(ResCode theCode)
        {
            if (theCode.HasInfo && theCode.CodeInfo.HasTags)
                foreach (object x in theCode.CodeInfo.Tags)
                    if (x is CodeDefId iDef)
                        return iDef;

            return CodeDefId.unspecified;
        }

        /// <summary>Every Cmd in the member's Tags, as slot indexes - the
        /// declared association list (radio members, in menu order).</summary>
        private static int[] _MembersOf(ResCode theCode)
        {
            int iCount = 0;

            foreach (object x in theCode.CodeInfo.Tags)
                if (x is Cmd) iCount++;

            var xMembers = new int[iCount];

            int i = 0;

            foreach (object x in theCode.CodeInfo.Tags)
                if (x is Cmd iCmd)
                    xMembers[i++] = Cmds.IndexOf(iCmd);

            return xMembers;
        }

        // ==== ctrl FIELD -> Keys ===========================================

        /// <summary>"ctrl-X" / "ctrl-shift-S" to WinForms Keys: the ctrl
        /// field plucked apart on '-' with Chars views - no Split, no
        /// intermediate strings.</summary>
        private static Keys _KeysOf(Chars theCtrl)
        {
            Keys vKeys = Keys.None;

            while (theCtrl.PluckedDelimitedText(out Chars vPart, Chars.DASH))
            {
                if (vPart.Equals("ctrl", ignoreCase: true)) { vKeys |= Keys.Control; continue; }
                if (vPart.Equals("shift", ignoreCase: true)) { vKeys |= Keys.Shift; continue; }
                if (vPart.Equals("alt", ignoreCase: true)) { vKeys |= Keys.Alt; continue; }

                if (vPart.Length == 1)
                {
                    char c = char.ToUpperInvariant(vPart.PluckChar_or_NUL());

                    if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
                        vKeys |= (Keys)c;
                }
            }

            return vKeys;
        }

        // ==== icon FIELD -> Image (via registered stream providers) ========

        /// <summary>Provider 1: a real file icons\&lt;name&gt; beside the exe.
        /// Absent folder or file = not-found (SOFT), the chain continues.</summary>
        private static bool _GotFileIcon(ResKind theKind, string theName, out BytesReader returnReader)
        {
            returnReader = null;

            if (theKind != ResKind.icon)
                return false;

            string sPath = Path.Combine(AppContext.BaseDirectory, "icons", theName);

            if (!File.Exists(sPath))
                return false;

            return BytesReader.Created(sPath, out returnReader, out _);
        }

        /// <summary>Provider 2: synthesizes a 16x16 letter tile from the
        /// stream NAME - so the sample shows icons with zero committed
        /// binaries, and shows first-found-wins chaining (drop real files
        /// into icons\ and they take over, no code change).</summary>
        private static bool _GotSynthIcon(ResKind theKind, string theName, out BytesReader returnReader)
        {
            returnReader = null;

            if (theKind != ResKind.icon || string.IsNullOrEmpty(theName))
                return false;

            using (var xBmp = new Bitmap(16, 16))
            {
                using (var xG = Graphics.FromImage(xBmp))
                using (var xBrush = new SolidBrush(Color.FromArgb(64 + (theName.Length * 37) % 128, 96, 160)))
                using (var xFont = new Font(FontFamily.GenericSansSerif, 9f, FontStyle.Bold))
                {
                    xG.FillRectangle(xBrush, 0, 0, 16, 16);
                    xG.DrawString(char.ToUpperInvariant(theName[0]).ToString(), xFont, Brushes.White, -1f, 0f);
                }

                var xMs = new MemoryStream();

                xBmp.Save(xMs, ImageFormat.Png);

                xMs.Position = 0;

                returnReader = BytesReader.Create_or_null(xMs, out _);

                return returnReader != null;
            }
        }

        /// <summary>Resolves an icon NAME through ResCode.GotReader and
        /// copies the stream into a detached Bitmap; any failure is SOFT -
        /// no image, life goes on.</summary>
        private static Image _IconImage_or_null(Chars theName)
        {
            if (theName.IsEmpty)
                return null;

            if (!ResCode.GotReader(ResKind.icon, theName.ToString(), out BytesReader xReader))
                return null;

            try
            {
                using (var xMs = new MemoryStream())
                {
                    xReader.BaseStream.CopyTo(xMs);

                    xMs.Position = 0;

                    using (var xImg = Image.FromStream(xMs))
                        return new Bitmap(xImg);
                }
            }
            catch
            {
                return null;
            }
            finally
            {
                xReader.Close_and_Dispose();
            }
        }

        // ==== BYTECODE PERSISTENCE =========================================

        // Index and Name are runtime-only; ByteCode is the PERSISTED
        // reference. The state file is [CodesVersion][ByteCode...] of the
        // checked toggles. On load, a version mismatch discards the file
        // (EnumCodesAttribute.Version guards renumbering), and each byte
        // comes back through IndexOfByteCode - a DIRECT array element here,
        // because Cmd's ByteCodes are dense (MaxByteCode == Count-1).

        private void _SaveState()
        {
            try
            {
                using (var xMs = new MemoryStream())
                {
                    xMs.WriteByte(Cmds.CodesVersion);

                    for (int i = 0; i < _Slots.Length; i++)
                        if (_Slots[i].Def == CodeDefId.toggle && _Slots[i].Item.Checked)
                            xMs.WriteByte(Cmds.ByteCode(i));

                    File.WriteAllBytes(_StatePath, xMs.ToArray());
                }
            }
            catch { } // SOFT: state is a convenience, never worth a crash
        }

        private void _LoadState()
        {
            try
            {
                if (!File.Exists(_StatePath))
                    return;

                byte[] xBytes = File.ReadAllBytes(_StatePath);

                if (xBytes.Length < 1 || xBytes[0] != Cmds.CodesVersion)
                    return; // version bumped: stored ByteCodes are void

                for (int b = 1; b < xBytes.Length; b++)
                {
                    int i = Cmds.IndexOfByteCode(xBytes[b]); // direct: dense

                    if (i < 0 || _Slots[i].Def != CodeDefId.toggle)
                        continue; // SOFT: stale byte = ignored

                    if (_Slots[i].Radio >= 0)
                        foreach (int iMember in _Slots[_Slots[i].Radio].Members)
                            _Slots[iMember].Item.Checked = iMember == i;
                    else
                        _Slots[i].Item.Checked = true;

                    _Dispatch((Cmd)Cmds.Code(i), true); // re-apply effects
                }
            }
            catch { }
        }

        // ==== HELPERS ======================================================

        /// <summary>Sets a toggle's check state via the table - any code
        /// path can drive UI state through Slots, the same join key.</summary>
        private void _SetChecked(Cmd theCmd, bool isChecked)
        {
            _Slots[Cmds.IndexOf(theCmd)].Item.Checked = isChecked;
        }

        private void _Say(Cmd theCmd)
        {
            // NameAsToken + ByteCode: the table talking back
            _Say(Cmds.NameAsToken(Cmds.IndexOf(theCmd))
                + "  (ByteCode " + ((byte)theCmd).ToString() + ")");
        }

        private void _Say(string theLine)
        {
            _Log.AppendText(theLine + Environment.NewLine);
        }
    }
}
