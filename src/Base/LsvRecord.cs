// Copyright (c) GDFrank
// SPDX-License-Identifier: MIT

//using System;

namespace Source77NW
{
    /// <summary>
    /// A Line Separated Values (LSV) record: in the spirit of csv, a
    /// text representation of records containing fields, marker-based so
    /// multi-line values need no escaping.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A record begins at a line whose first char is the
    /// <see cref="RecordMarker"/> COLON and spans to the next such line
    /// (or EOF); a COLON as the very first char of the text counts as
    /// marker (LF assumed). Text before the first RecordMarker is
    /// preamble - ignored by <see cref="Parsed"/>, free for notes.
    /// Within a record, each field begins at a line whose first char is
    /// the <see cref="FieldMarker"/> DOT; repeated markers collapse
    /// (":::" / "...") allowing visual separators.
    /// </para>
    /// <para>
    /// The ONLY restriction on stored value text: no value line may
    /// begin with COLON or DOT.
    /// </para>
    /// <para>
    /// Enumerators take (ref int cursor, ...): set cursor to zero and
    /// call until false; assume nothing about the cursor value. The
    /// accessor properties are invalid on a default instance.
    /// </para>
    /// </remarks>
    public struct LsvRecord
    {
        /// <summary>
        /// Parses the next record from the unparsed text (advancing it);
        /// false when no record remains. Preamble before the first
        /// RecordMarker is skipped.
        /// </summary>
        public static bool Parsed(ref Chars theUnparsed, out LsvRecord returnRecord)
        {
            returnRecord = default;

            bool bGot = theUnparsed.PluckedLinesUntil_LF_char(RecordMarker, out Chars vLines);

            if (bGot && vLines.BotChar_or_NUL != Chars.COLON)
            {
                // IGNORE junk before line 1st char RecordMarker

                // AND PARSE THE 1ST Record

                bGot = theUnparsed.PluckedLinesUntil_LF_char(RecordMarker, out vLines);

            }

            if (!bGot)
            {
                returnRecord = default;

                return false;
            }

            int iBot = vLines.BotIndex;

            // FIND BOT OF ITEMS

            vLines.PluckedLinesUntil_LF_char(FieldMarker, out Chars vHeader);

            returnRecord = new LsvRecord()
            {
                _Record = new Chars(iBot, vLines.TopIndex, vLines.TextBase),

                _FieldsBot = vHeader.TopIndex // none if same as vLines.TopIndex
            };

            return true;
        }

        /// <summary>The record marker char (COLON, at line start).</summary>
        public const char RecordMarker = Chars.COLON;

        /// <summary>The field marker char (DOT, at line start).</summary>
        public const char FieldMarker = Chars.DOT;

        private Chars _Record; // safely hidden

        private int _FieldsBot; // the 1st ItemMarker

        /// <summary>The RAW record: RecordMarker to the next RecordMarker or EOF, trailing line separator included.</summary>
        public Chars Record => _Record;
        /// <summary>The trimmed header text: just past the RecordMarker to the first FieldMarker.</summary>
        public Chars Context => new Chars(_Record.BotIndex + 1, _FieldsBot, _Record.TextBase).Trim();
        /// <summary>The trimmed fields text: first FieldMarker to record top.</summary>
        public Chars Fields => new Chars(_FieldsBot, _Record.TopIndex, _Record.TextBase).Trim();

        /// <summary>
        /// Enumerates the record's fields, returning each trimmed value
        /// following its FieldMarker (collapsed markers skipped); false
        /// at end.
        /// </summary>
        public bool GotNextFieldValue(ref int cursor, out Chars returnValue)
        {
            // value positions always > 0

            if (cursor < _Record.TopIndex)
            {
                if (cursor == 0) cursor = _FieldsBot; // sets the records 1st item

                // reconstruct the parsing cursor

                Chars vParse = new Chars(cursor, _Record.TopIndex, _Record.TextBase);

                if (vParse.PluckedLinesUntil_LF_char(FieldMarker, out returnValue))
                {
                    // returnValue includes the CR/LF (if any)

                    cursor = vParse.BotIndex; // positioned on the next ItemMarker

                    while (returnValue.BotChar_or_NUL == FieldMarker)
                    {
                        returnValue.PluckChar_or_NUL();
                        // removes all DOTS ... to the "value"
                    }

                    returnValue.Trim(); // removes any CR/LF

                    return true;
                }
            }

            returnValue = default;

            return false;
        }

        /// <summary>
        /// Enumerates the record's fields split into a name (the leading
        /// visible-or-quoted token) and the remaining value; false at end.
        /// </summary>
        public bool GotNextFieldNameAndValue(ref int cursor, out Chars returnName, out Chars returnValue)
        {
            if (GotNextFieldValue(ref cursor, out returnValue))
            {
                returnValue.GotNameAndText(out returnName, out returnValue);

                return true;
            }

            returnName = default;

            return false;
        }

        /// <summary>True for an LsvRecord with the same trimmed record text (compares copies; the instances are not mutated).</summary>
        public override bool Equals(object obj)
        {
            if (obj is LsvRecord vRecord)
            {
                Chars v1 = _Record;
                Chars v2 = vRecord._Record;

                return v1.Trim().Equals(v2.Trim());
            }

            return false;
        }

        /// <summary>Hash of the trimmed record text, consistent with Equals.</summary>
        public override int GetHashCode()
        {
            Chars v1 = _Record;

            return v1.Trim().GetHashCode();
        }

        /// <summary>The raw record text.</summary>
        public override string ToString() => _Record.ToString();
    }
}
