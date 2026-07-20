// CopyrightCopyright (c) GDFrank
// SPDX-License-Identifier: MIT

using Source77NW;

namespace Samples
{
    /// <summary>
    /// Cmd - THE TRUTH TABLE. This one enum declares the entire UI of the
    /// EnumCodes sample: every menu, item, toggle, radio group, button,
    /// shortcut, tip, and icon reference. Nothing else declares structure -
    /// no XML, no designer resx, no string keys. MainForm.cs reads this
    /// table once at startup and builds everything from it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// FIELDS: each member's EnumInfo text is one VBAR-delimited string in
    /// Chars PluckedDelimitedText format. The field names are the CodeValId
    /// tokens themselves - cap mnu but tip ctrl icon echo blob - so the id
    /// enum and the text format share one vocabulary, and ResCode keys the
    /// lookup with ValIds.NameAsToken. Absent fields default: cap falls
    /// back to NameAsCaption ("Edit_Cut" -> "Edit Cut"), mnu/but ensure an
    /// '&amp;' accelerator (a single-letter value marks that letter of cap).
    /// </para>
    /// <para>
    /// STRUCTURE: the first CodeDefId in each member's EnumInfo Tags says
    /// what the member IS (menu / action / toggle / radio). Order says
    /// where it LIVES: a menu member is followed by its children; a radio
    /// member is followed by its toggles and displays between separators.
    /// </para>
    /// <para>
    /// ASSOCIATIONS (per G): Tags may also carry OTHER Cmd members as
    /// cross references. A radio lists its member toggles, in menu order,
    /// right in its Tags - so the grouping is identified in the enum
    /// itself. The final behavior lives in the operational elements
    /// (MainForm wires check/uncheck), but WHO belongs to WHAT is table
    /// data, greppable at the declaration.
    /// </para>
    /// <para>
    /// MESSAGES: the enum values double as message ids - every click,
    /// shortcut, and button lands in one dispatch switching on Cmd, so a
    /// grep for any member shows its declaration, its wiring, and its
    /// handling. ByteCodes are dense (0..Count-1), so byte-to-index lookup
    /// is a direct array element; Version=1 guards persisted state.
    /// </para>
    /// </remarks>
    [EnumCodes("EnumCodes sample command table", null, 1)]
    public enum Cmd : byte
    {
        // ---- File ------------------------------------------------------

        [EnumInfo(CodeDefId.menu)]
        File = 0,

        [EnumInfo("|tip Open a document|ctrl ctrl-O|icon file.open.ico|"
                , CodeDefId.action)]
        File_Open = 1,                  // cap defaults "File Open"

        [EnumInfo("|cap Save|mnu S|but Save|tip Save the document"
                + "|ctrl ctrl-S|icon file.save.ico|"
                , CodeDefId.action)]
        File_Save = 2,

        [EnumInfo("|cap Exit|tip Exit the sample|"
                , CodeDefId.action)]
        File_Exit = 3,

        // ---- Edit ------------------------------------------------------

        [EnumInfo(CodeDefId.menu)]
        Edit = 4,

        [EnumInfo("|cap Cut|mnu t|but Cut|tip Cut selected text"
                + "|ctrl ctrl-X|icon edit.cut.ico|"
                , CodeDefId.action)]
        Edit_Cut = 5,

        [EnumInfo("|cap Word Wrap|tip Toggle word wrap in the log|"
                , CodeDefId.toggle)]
        Edit_Word_Wrap = 6,             // standalone toggle (no radio)

        // ---- View ------------------------------------------------------

        [EnumInfo(CodeDefId.menu)]
        View = 7,

        // The radio: CodeDefId.radio plus its member toggles as Tags, in
        // the order they appear in the menu. The three toggles follow
        // immediately (order rule) and render between separators.
        [EnumInfo("|cap Text Size|tip Log text size|"
                , CodeDefId.radio, Cmd.View_Small, Cmd.View_Medium, Cmd.View_Large)]
        View_Size = 8,

        [EnumInfo("|cap Small|tip Small log text|", CodeDefId.toggle)]
        View_Small = 9,

        [EnumInfo("|cap Medium|tip Medium log text|", CodeDefId.toggle)]
        View_Medium = 10,

        [EnumInfo("|cap Large|tip Large log text|", CodeDefId.toggle)]
        View_Large = 11,
    }
}
