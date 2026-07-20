// CopyrightCopyright (c) GDFrank
// SPDX-License-Identifier: MIT

using System;
using System.IO;

namespace Source77NW
{
    /// <summary>
    /// One parsed CSV/TSV field: its (possibly dequoted) Value and whether
    /// quoting occurred; carries the nested <see cref="Reader"/> that
    /// parses records and the TSV write-out helpers.
    /// </summary>
    /// <remarks>
    /// A quoted field is dequoted and trimmed into Value with
    /// <see cref="WasQuoted"/> set. Embedded quotes are allowed but every
    /// opening quote must have a closing quote. Write-out is always TSV
    /// (<see cref="WriteTsvValues(Csv[], TextWriter)"/>), quoting a value
    /// only when it contains quotes, tabs, or line breaks (or when
    /// forced).
    /// </remarks>
    public struct Csv : IComparable<Csv>, IDisposable
    {
        private const ushort issueSource = 65401;

        /// <summary>
        /// Parses CSV/TSV text one <see cref="GotRecord"/> at a time.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The value separator is TAB, COMMA, or SEMI - or NUL to
        /// auto-detect on the first multi-value record (with a
        /// TAB-presence re-check, since TAB wins when both appear).
        /// </para>
        /// <para>
        /// On a parse issue, <see cref="LastIssue"/> is set and
        /// <see cref="GotRecord"/> returns false; once the last record is
        /// consumed GotRecord keeps returning false until the next Reset.
        /// The Reader may be reused after <see cref="Dispose"/> (Reset
        /// re-creates its stacks).
        /// </para>
        /// <para>
        /// The surface is SOFT before the first Reset and after Dispose
        /// (agent-grade): GotRecord returns false, counts are 0, values
        /// default, and the write-out members write nothing.
        /// </para>
        /// </remarks>
        public sealed class Reader
        {
            /// <summary>Separator candidate categories (COMMA | TAB | SEMI).</summary>
            public const CharsCat SepCats = CharsCat.COMMA | CharsCat.TAB | CharsCat.SEMI;

            /// <summary>End-of-record categories (NUL | CR | LF).</summary>
            public const CharsCat EndOfRecordCats = CharsCat.NUL | CharsCat.Ascii_CR_LF;

            /// <summary>All separator + end-of-record categories.</summary>
            public const CharsCat AnySepCats = SepCats | EndOfRecordCats;

            /// <summary>Creates a Reader expecting 16 columns.</summary>
            public Reader() { _ExpectedSize = 16; _GrowSize = 16; }

            /// <summary>Creates a Reader expecting the given column count (clamped 16-1024).</summary>
            public Reader(ushort theExpectedColumnCount)
            {
                _ExpectedSize = theExpectedColumnCount;
                if (_ExpectedSize < 16) _ExpectedSize = 16;
                if (_ExpectedSize > 1024) _ExpectedSize = 1024;
                _GrowSize = (ushort)(_ExpectedSize / 4); // 25%
                if (_GrowSize < 16) _GrowSize = 16;
            }

            private const string s_Line_number_COLON_SP = "Line number: ";
            private const string s_Text_index_COLON_SP = "Val index: ";

            private readonly ushort _ExpectedSize;

            private readonly ushort _GrowSize;

            private ItemStack<Csv> _Names;

            private ItemStack<Csv> _Values;

            private Chars _unparsed;

            /// <summary>The text being parsed (from the last full Reset).</summary>
            public string Text { get; private set; }

            /// <summary>True when the first record is treated as value names.</summary>
            public bool HasHeader { get; private set; }

            /// <summary>The active separator (NUL until auto-detected).</summary>
            public char SepChar { get; private set; }

            private int _RecordText_bot;

            private int _RecordText_top;

            /// <summary>The currently parsed record text (set even when GotRecord fails).</summary>
            public Chars RecordText => new Chars(_RecordText_bot, _RecordText_top, Text);

            /// <summary>The Issue that stopped parsing; null when none.</summary>
            public Issue LastIssue { get; private set; }

            /// <summary>Line number of the last end-of-record seen.</summary>
            public int LastLineNumber { get; private set; }

            /// <summary>True when <see cref="LastIssue"/> is set.</summary>
            public bool HasIssue => LastIssue != null;

            /// <summary>True when Reset has been given text.</summary>
            public bool HasText => Text != null;

            /// <summary>Count of header names (0 when none).</summary>
            public int NameCount => _Names != null ? _Names.Count : 0;

            /// <summary>
            /// The discovered/generated name at the index: always returns a
            /// value - index.ToString() when no header name exists (even
            /// when <see cref="HasHeader"/> is false).
            /// </summary>
            public Csv Name(int index)
            {
                if (_Names != null)
                {
                    if (index < _Names.Count)
                    {
                        Csv vCsv = _Names[index];

                        if (vCsv.Value.NotEmpty)
                        {
                            return vCsv;
                        }
                    }
                }

                return new Csv()
                {
                    Value = new Chars(index.ToString()),
                };
            }

            /// <summary>The current record's field at the index (default when past the count or before Reset).</summary>
            public Csv Value(int index) => _Values != null && index < _Values.Count ? _Values[index] : default;

            /// <summary>Count of fields in the current record (0 before Reset).</summary>
            public int ValueCount => _Values != null ? _Values.Count : 0;

            /// <summary>Gets the index of the header name (case-insensitive); false (-1) when not found or before Reset.</summary>
            public bool FoundName(Chars theName, out int returnIndex)
            {
                for (int i = 0; i < NameCount; i++)
                {
                    if (_Names[i].Value.Equals(theName, true))
                    {
                        returnIndex = i;

                        return true;
                    }
                }

                returnIndex = -1;

                return false;
            }

            /// <summary>Frees the name/value stacks; the Reader may be reused after (Reset re-creates them).</summary>
            public void Dispose()
            {
                _Names?.Dispose();
                _Names = null;

                _Values?.Dispose();
                _Values = null;
            }

            private Issue _Issue_No_Text => Issue.Create(issueSource, 33, "No Val", IssueKind.BadOperation);

            /// <summary>Resets with the text, no header, auto-detected separator.</summary>
            public void Reset(string theText) => Reset(theText, false, Chars.NUL);

            /// <summary>Resets with the text and header flag, auto-detected separator.</summary>
            public void Reset(string theText, bool asHasHeader) => Reset(theText, asHasHeader, Chars.NUL);

            /// <summary>
            /// Resets with the text, header flag, and separator (TAB, COMMA,
            /// or SEMI; anything else means auto-detect). Null text sets
            /// <see cref="LastIssue"/>.
            /// </summary>
            public void Reset(string theText, bool asHasHeader, char theSeparator_TAB_COMMA_SEMI_else_autodetect)
            {
                Text = theText;

                if (Text == null)
                {
                    LastIssue = _Issue_No_Text;

                    return;
                }

                SepChar = theSeparator_TAB_COMMA_SEMI_else_autodetect;

                if (SepChar != Chars.TAB && SepChar != Chars.COMMA && SepChar != Chars.SEMI)
                {
                    SepChar = Chars.NUL;
                }

                Reset(asHasHeader);
            }

            /// <summary>Re-parses the current text from the top, keeping the header flag.</summary>
            public void Reset() => Reset(HasHeader);

            /// <summary>
            /// Re-parses the current text from the top with the given header
            /// flag; when true the first record is consumed into the names.
            /// </summary>
            public void Reset(bool asHasHeader)
            {
                if (Text == null)
                {
                    LastIssue = _Issue_No_Text;

                    return;
                }

                if (_Values == null)
                {
                    _Values = new ItemStack<Csv>(_ExpectedSize);
                    _Names = new ItemStack<Csv>(_ExpectedSize);
                }
                else
                {
                    _Values.Clear();
                    _Names.Clear();
                }

                LastIssue = null;

                LastLineNumber = 0;

                HasHeader = asHasHeader;

                _unparsed = new Chars(Text);

                _RecordText_bot = 0;

                _RecordText_top = 0;

                if (HasHeader)
                {
                    if (GotRecord())
                    {
                        _Names.Clear();

                        for (int i = 0; i < _Values.Count; i++)
                        {
                            _Names.Push(_Values[i]);
                        }
                    }
                }
            }

            /// <summary>The text being parsed.</summary>
            public override string ToString() => Text;

            /// <summary>
            /// Parses the next record into the Values; false at end of text,
            /// before any Reset, or on a parse issue (<see cref="LastIssue"/>
            /// set: unclosed quote, or quoted multi-line value - not
            /// supported).
            /// </summary>
            public bool GotRecord()
            {
                if (_Values == null)
                {
                    return false; // soft before Reset / after Dispose (agent-grade)
                }

                _Values.Clear();

                _RecordText_bot = _unparsed.BotIndex;

                _RecordText_top = _RecordText_bot;

                if (_unparsed.IsEmpty || LastIssue != null)
                {
                    return false;
                }

                bool bSep_is_unknown = SepChar == Chars.NUL;

                bool bEndOfRecord = false;

                while (!bEndOfRecord) // PARSE-PUSH VALUE
                {
                    Chars vValue = default;

                    int iFieldBot = _unparsed.BotIndex;

                    int iFieldTop = iFieldBot;

                    bool bQuoting = false;

                    while (true) // PARSE FIELD CHARS - ONE CHAR PLUCK AT A TIME
                    {
                        char cBotChar = _unparsed.PluckChar_or_NUL();

                        CharsCat iPluckCat = Chars.Cat(cBotChar);

                        if (cBotChar == Chars.QUOTE)
                        {
                            bQuoting = !bQuoting;

                            continue; // skip the quoting char
                        }

                        // FIX: no separator discovery inside quoted fields
                        if (bSep_is_unknown && Chars.CatTrue(iPluckCat, SepCats))
                        {
                            if (bQuoting)
                            {
                                continue; // quoted: part of the value, not a candidate
                            }

                            bSep_is_unknown = false;

                            SepChar = cBotChar;

                            if (SepChar != Chars.TAB)
                            {
                                // SEE IF TAB CAN BE ASSUMED EVEN IF COMMA/SEMI

                                Chars v1 = new Chars(Text);

                                if (v1.PluckedLine(out Chars v2))
                                {
                                    if (v2.IsEmpty) v1.PluckedLine(out v2);

                                    if (v2.Contains(Chars.TAB))
                                    {
                                        SepChar = Chars.TAB;
                                    }
                                }
                            }
                        }

                        // FIX: embedded separator inside quotes is value content
                        if (cBotChar == SepChar)
                        {
                            if (bQuoting)
                            {
                                continue;
                            }

                            // Unquoted separator - this is a field boundary
                            iFieldTop = _unparsed.BotIndex;

                            break;
                        }

                        if (Chars.CatTrue(iPluckCat, EndOfRecordCats)) // CR / LF
                        {
                            LastLineNumber++;

                            iFieldTop = _unparsed.BotIndex; // could be TopIndex of text

                            if (bQuoting)
                            {
                                LastIssue = Issue.Create(issueSource, 34 // FIX: spot dedup (was 33, reused 3x)
                                , IssueId.Csv_Quoted_multi_line_values_not_supported
                                , s_Line_number_COLON_SP + LastLineNumber
                                , s_Text_index_COLON_SP + iFieldTop
                                , IssueKind.BadData);

                                return false;

                            }

                            if (cBotChar == Chars.CR && _unparsed.BotChar_or_NUL == Chars.LF)
                            {
                                _unparsed.PluckChar_or_NUL(); // pluck past LF
                            }

                            bEndOfRecord = true;

                            // WE ARE JUST PAST LF

                            break;
                        }

                    } // PARSE VALUE CHARS AND SKIP Sep/CR/LF

                    _RecordText_top = _unparsed.BotIndex; // JUST PAST CRLF or at EOF

                    vValue = new Chars(iFieldBot, iFieldTop, _unparsed.TextBase);

                    if (bQuoting) // STILL QUOTING? OOPS!
                    {
                        LastIssue = Issue.Create(issueSource, 35 // FIX: spot dedup (was 33, reused 3x)
                        , IssueId.Csv_value_QUOTE_without_end_QUOTE
                        , Chars.QUOTE + vValue.ToString() + Chars.QUOTE
                        , s_Line_number_COLON_SP + LastLineNumber
                        , s_Text_index_COLON_SP + vValue.BotIndex
                        , IssueKind.BadData);

                        return false;
                    }

                    Csv vField = Create(vValue);

                    _Values.Push(vField);

                    // WE ARE JUST PAST TAB/COMMA/SEMI/CR/LF/NUL

                } // PARSE-PUSH VALUE

                return true;

            }

            /// <summary>Writes the header names as one TSV record (writes nothing before Reset).</summary>
            public void WriteNamesRecord(TextWriter writer) { if (_Names != null) WriteTsvValues(_Names, writer); }

            /// <summary>Writes the current record's values as one TSV record (writes nothing before Reset).</summary>
            public void WriteValuesRecord(TextWriter writer) { if (_Values != null) WriteTsvValues(_Values, writer); }

            /// <summary>The header names as an array (null before Reset).</summary>
            public Csv[] NamesToArray() => _Names?.ToArray();

            /// <summary>The current record's values as an array (null before Reset).</summary>
            public Csv[] ValuesToArray() => _Values?.ToArray();

        } // Reader class


        //=========== Csv STRUCT METHODS/PROPERTIES

        private const string QUOTE_QUOTE = AS.QUOTE + AS.QUOTE;

        /// <summary>
        /// Creates a Csv from the value: trimmed; a quoted value is
        /// dequoted, re-trimmed, and marked <see cref="WasQuoted"/>.
        /// </summary>
        public static Csv Create(Chars theValue)
        {
            theValue.Trim();

            bool bWasQuoted = false;

            if (theValue.Length > 1)
            {
                if (theValue.IsQuoted())
                {
                    theValue.PluckChar_or_NUL();
                    theValue.PopChar_or_NUL();
                    theValue.Trim();
                    bWasQuoted = true;
                }
            }

            return new Csv()
            {
                Value = theValue,
                WasQuoted = bWasQuoted,
            };
        }

        /// <summary>The field value (dequoted and trimmed when it was quoted).</summary>
        public Chars Value { get; private set; }

        /// <summary>True when the parsed field was quoted.</summary>
        public bool WasQuoted { get; private set; }

        /// <summary>True when the value contains a quote char.</summary>
        public bool Contains_QUOTE => Value.Contains(Chars.QUOTE);

        /// <summary>True when the value contains a doubled quote.</summary>
        public bool Contains_QUOTE_QUOTE => Value.Contains(QUOTE_QUOTE, false);

        /// <summary>True when the value contains CR or LF.</summary>
        public bool Contains_Lines => Value.Contains(CharsCat.Ascii_CR_LF);

        /// <summary>The value text.</summary>
        public override string ToString() => Value.ToString();

        /// <summary>True when the other object's ToString equals the value; false for null.</summary>
        public override bool Equals(object obj) => obj is Csv vCsv ? Equals(vCsv) : obj != null && Value.Equals(obj.ToString());

        /// <summary>True when the other Csv's value equals this value.</summary>
        public bool Equals(Csv other) => Value.Equals(other.Value);

        /// <summary>True when the Chars equals the value.</summary>
        public bool Equals(Chars other) => Value.Equals(other);

        /// <summary>True when the string equals the value.</summary>
        public bool Equals(string other) => Value.Equals(other);

        /// <summary>Orders by value.</summary>
        public int CompareTo(Csv other) => Value.CompareTo(other.Value);

        /// <summary>Hash of the value.</summary>
        public override int GetHashCode() => Value.GetHashCode();

        /// <summary>
        /// Writes the value TSV-style: quoted only when forced or when it
        /// contains quotes, line breaks, or tabs (COMMA/SEMI need no
        /// quoting in TSV output).
        /// </summary>
        public void WriteAsCsvInput(TextWriter writer, bool forceQuoting)
        {
            CharsCat iCatSum = Value.CatSum();

            bool bQuotes = 0 != (iCatSum & CharsCat.QUOTE);

            bool bMultiline = 0 != (iCatSum & CharsCat.Ascii_CR_LF);

            bool bTabs = 0 != (iCatSum & CharsCat.TAB);

            if (forceQuoting || bQuotes || bMultiline || bTabs)
            {
                // YET_TO_CONSIDER QUOTES IN Values

                writer.Write(Chars.QUOTE);
                Value.Write(writer);
                writer.Write(Chars.QUOTE);
            }
            else
            {
                Value.Write(writer);
            }
        }

        /// <summary>Clears the value.</summary>
        public void Dispose()
        {
            Value = default;
        }

        // Csv Struct STATIC METHODS AND CONSTANTS

        /// <summary>Writes the values as one TAB-separated record line (all values quoted).</summary>
        public static void WriteTsvValues(Csv[] theValues, TextWriter writer)
        {
            for (int i = 0; i < theValues.Length; i++)
            {
                if (i > 0) { writer.Write(Chars.TAB); }

                theValues[i].WriteAsCsvInput(writer, true);
            }

            writer.WriteLine();
        }

        /// <summary>Writes the values as one TAB-separated record line (all values quoted).</summary>
        public static void WriteTsvValues(ItemStack<Csv> theValues, TextWriter writer)
        {
            for (int i = 0; i < theValues.Count; i++)
            {
                if (i > 0) { writer.Write(Chars.TAB); }

                theValues[i].WriteAsCsvInput(writer, true);
            }

            writer.WriteLine();

        }

        /// <summary>
        /// Csv parse issue ids; member names ARE their captions (fed to
        /// Issue messages as params).
        /// </summary>
        [EnumCodes]
        public enum IssueId : byte
        {
            NoIssue,
            Csv_value_QUOTE_without_end_QUOTE,
            Csv_Quoted_multi_line_values_not_supported,
            Csv_operation_requires_header_columns,
        }
    }
}
