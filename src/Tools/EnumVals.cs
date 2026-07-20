// CopyrightCopyright (c) GDFrank
// SPDX-License-Identifier: MIT

using System;
using System.IO;

namespace Source77NW
{
    /// <summary>
    /// Loads, audits, and saves text values for the members of an
    /// EnumCodes enum: name-value pairs, LSV records, and delegate hooks
    /// for default values, read-only members, and external value audits.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Create with <see cref="Create"/> (enum Type or EnumCodes). Optional
    /// delegates: DefaultValueDO supplies per-member defaults;
    /// IsReadOnlyDO marks members read-only (a read-only member always
    /// returns its default and its internal value is kept empty);
    /// AuditValueDO replaces the built-in <see cref="GetAuditedValue(EnumCodes, int, Chars, out string)"/>
    /// validation, which uses the member's Bits.ValId (truth, folder,
    /// file, path, line, numerics, date, datetime) to normalize the value
    /// and report error lines.
    /// </para>
    /// <para>
    /// <see cref="SaveLsv"/> writes non-read-only values as an LSV record;
    /// <see cref="LoadLsv"/> sets values by matching field names to member
    /// tokens (read-only members softly refuse via SetValue).
    /// </para>
    /// </remarks>
    public sealed class EnumVals
    {
        private enum _IssueId : byte // keeping private for now
        {
            NoIssue = 0,
            Invalid_param_name = 1,
            Missing_parm_value = 2,
            Invalid_param_value = 3,
            Unable_to_set_value = 4,
            Invalid_date = 5,
            Invalid_numeric_value = 6,
        }

        /// <summary>Supplies the default value for the member at the index.</summary>
        public delegate Chars DefaultValueDO(EnumCodes theCodes, int theIndex);

        /// <summary>True when the member at the index is read-only.</summary>
        public delegate bool IsReadOnlyDO(EnumCodes theCodes, int theIndex);

        /// <summary>Audits/normalizes the value for the member at the index; error lines returned (null when clean).</summary>
        public delegate Chars AuditValueDO(EnumCodes theCodes, int theIndex, Chars theValue, out string returnErrLines);

        /// <summary>Notification raised after LoadLsv sets its values.</summary>
        public delegate void UponLoaded();

        private const ushort issueSource = 65113;

        private static char RecordMarker => LsvRecord.RecordMarker;

        private static char ValueMarker => LsvRecord.FieldMarker;

        /// <summary>The default LSV context: "EnumVals" + the enum type name.</summary>
        public Chars LsvContext_default => new Chars(typeof(EnumVals).Name + AS.SP + Codes.EnumType.Name);

        private DefaultValueDO _DO_DefaultValue = null;

        private IsReadOnlyDO _DO_IsReadOnly = null;

        private AuditValueDO _DO_AuditValue = null;

        private Action _DO_JustLoadedValues = null;

        private Chars[] _Values;

        private Chars _Context = Chars.Nothing;

        /// <summary>The EnumCodes whose members carry the values.</summary>
        public EnumCodes Codes { get; private set; }

        private string[] _KeyNames;

        /// <summary>
        /// Creates an EnumVals for the enum Type or EnumCodes (anything
        /// else raises Critical), with the optional delegate hooks.
        /// </summary>
        public static EnumVals Create(object theCodesType_Codes_or_Keys
            , DefaultValueDO theDefaultDO = null
            , IsReadOnlyDO theIsReadOnlyDO = null
            , AuditValueDO theAuditValueDO = null
            , Action theJustLoadedValuesDO = null)
        {
            EnumVals x = new EnumVals();

            if (theCodesType_Codes_or_Keys is Type xType)
                x.Codes = EnumCodes.ForType(xType);
            else if (theCodesType_Codes_or_Keys is EnumCodes xCodes)
                x.Codes = xCodes;
            else
                Exe.Critical(Issue.Create(issueSource, 69, IssueKind.BadParam));

            x._KeyNames = new string[x.Codes.Count];
            int iKeyNames = -1;
            while (++iKeyNames < x._KeyNames.Length)
                x._KeyNames[iKeyNames] = x.Codes.NameAsToken(iKeyNames);

            x._Values = new Chars[x.Codes.Count];
            x._DO_IsReadOnly = theIsReadOnlyDO;
            x._DO_DefaultValue = theDefaultDO;
            x._DO_AuditValue = theAuditValueDO;
            x._DO_JustLoadedValues = theJustLoadedValuesDO;
            return x;
        }

        /// <summary>Uninitialized instance; use <see cref="Create"/> instead.</summary>
        private EnumVals() { }

        /// <summary>The LSV record context (trimmed); empty reads as <see cref="LsvContext_default"/>.</summary>
        public Chars LsvContext
        {
            get
            {
                if (_Context.IsEmpty) return LsvContext_default;
                return _Context.Trim();
            }
            set
            {
                _Context = value.Trim();
            }
        }

        /// <summary>The member token at the index (null when out of range).</summary>
        public string KeyName(int theIndex)
        {
            if (theIndex >= 0 && theIndex < _KeyNames.Length)
                return _KeyNames[theIndex];
            return null;
        }

        /// <summary>The value for the enum member (empty Chars for an unknown member).</summary>
        public Chars Value(Enum theCode) => Value(Codes.IndexOf(theCode));

        /// <summary>
        /// The value at the index: the set value, or the default when the
        /// value is empty or the member is read-only; empty Chars out of
        /// range.
        /// </summary>
        public Chars Value(int theIndex)
        {
            if (theIndex >= 0 && theIndex < Codes.Count)
            {
                Chars vVal = _Values[theIndex];
                if (vVal.IsEmpty || IsReadOnly(theIndex))
                    return DefaultValue(theIndex);
                return vVal;
            }
            return Chars.Nothing;
        }

        /// <summary>
        /// Audits and re-stores the value at the index (AuditValueDO when
        /// set, else the built-in audit), returning the audited value with
        /// the prior value and any error lines.
        /// </summary>
        public Chars Value_audited(int theIndex, out Chars returnOldValue, out string returnErrLines)
        {
            returnErrLines = null;
            if (theIndex < 0 || theIndex >= _Values.Length)
            {
                returnOldValue = Chars.Nothing;
                return returnOldValue;
            }
            returnOldValue = _Values[theIndex].Trim();
            if (_DO_AuditValue != null)
                return _Values[theIndex] = _DO_AuditValue(Codes, theIndex, returnOldValue, out returnErrLines);
            return _Values[theIndex] = GetAuditedValue(Codes, theIndex, returnOldValue, out returnErrLines);
        }


        /// <summary>True when the member at the index is read-only (IsReadOnlyDO; false when no delegate).</summary>
        public bool IsReadOnly(int theIndex)
        {
            return _DO_IsReadOnly?.Invoke(Codes, theIndex) ?? false;
        }

        /// <summary>The default value for the member at the index (DefaultValueDO; empty when no delegate).</summary>
        public Chars DefaultValue(int theIndex)
        {
            return _DO_DefaultValue?.Invoke(Codes, theIndex) ?? Chars.Nothing;
        }

        /// <summary>Sets the value for the enum member; false for an unknown or read-only member.</summary>
        public bool SetValue(Enum theEnum, Chars theValue) => SetValue(Codes.IndexOf(theEnum), theValue);

        /// <summary>
        /// Sets the trimmed value at the index; false out of range, and
        /// false for a read-only member (whose internal value is cleared).
        /// </summary>
        public bool SetValue(int theIndex, Chars theValue)
        {
            if (theIndex < 0 || theIndex >= _Values.Length) return false;
            if (IsReadOnly(theIndex))
            {
                _Values[theIndex] = Chars.Nothing;
                return false;
            }
            _Values[theIndex] = theValue.Trim();
            return true;
        }

        /// <summary>Enumerates members in ByteCode sequence with their key name and value; false at end.</summary>
        public bool GotNext(ref int cursor, out int returnCodeIndex, out string returnKeyName, out Chars returnValue)
        {
            if (Codes.GotNextByteCode(ref cursor, out _, out returnCodeIndex)) // ByteCode sequence
            {
                returnKeyName = _KeyNames[returnCodeIndex];
                returnValue = Value(returnCodeIndex);
                return true;
            }
            returnKeyName = null;
            returnCodeIndex = -1;
            returnValue = Chars.Nothing;
            return false;
        }

        /// <summary>The values as LSV text.</summary>
        public override string ToString() => ToLsv();

        /// <summary>The values as LSV text (<see cref="SaveLsv"/> into a builder).</summary>
        public string ToLsv()
        {
            using (var x2 = Heap.New_TextBuilder(0))
            {
                SaveLsv(x2);
                return x2.ToString_and_Dispose();
            }
        }

        /// <summary>
        /// Writes the context record line then one field line per
        /// non-read-only member (name SP value), in ByteCode sequence.
        /// </summary>
        public void SaveLsv(TextWriter writer)
        {
            // NOTE: SaveLsv skips ReadOnly items, LoadLsv does not - intentional:
            // SetValue handles a loaded read-only name by clearing + returning false.

            writer.Write(RecordMarker);
            LsvContext.Write(writer);
            writer.WriteLine();
            int cursor = 0;
            while (GotNext(ref cursor, out int iCodeIndex, out string sName, out Chars vValue))
            {
                if (IsReadOnly(iCodeIndex)) continue;
                writer.Write(ValueMarker);
                writer.Write(sName);
                writer.Write(AS.SP);
                vValue.Write(writer);
                writer.WriteLine();
            }
        }

        /// <summary>
        /// Parses the LSV record and sets values whose field names match
        /// member tokens (unknown names ignored), raising UponLoaded;
        /// false when no record parses. Clears existing values first
        /// unless addingValues.
        /// </summary>
        public bool LoadLsv(Chars theLSV, bool addingValues = false)
        {
            if (!addingValues)
                ClearValues();

            if (LsvRecord.Parsed(ref theLSV, out LsvRecord v1))
            {
                _Context = v1.Context;
                int cursor = 0;
                while (v1.GotNextFieldNameAndValue(ref cursor, out Chars vName, out Chars vValue))
                {
                    string sName = vName.ToString();
                    int iIndex = Codes.IndexOf(sName);
                    if (iIndex >= 0)
                        SetValue(iIndex, vValue.Trim());
                }
                _DO_JustLoadedValues?.Invoke();
                return true;
            }
            _Context = LsvContext_default;
            return false;
        }

        /// <summary>
        /// Clears values then sets them from name,value parameter pairs
        /// (null or unmatched names skip their value); the first invalid
        /// pair stops the load and returns its Issue, null when clean.
        /// </summary>
        public Issue TryLoadNameValuePairs(params object[] theParams)
        {
            ClearValues();
            const string s_Param_ = "Param: ";
            int iLen = theParams.Length;
            int iParamsEnd = iLen - 1;
            int cursor = -1;
            while (++cursor < theParams.Length)
            {
                object xKeyName = theParams[cursor];
                if (xKeyName == null)
                {
                    ++cursor; // skip the value part
                    continue;
                }

                string sKeyName = xKeyName.ToString();
                int iIndex = Codes.IndexOf(sKeyName);
                sKeyName = KeyName(iIndex); // normalized keyname

                if (string.IsNullOrWhiteSpace(sKeyName))
                {
                    ++cursor; // skip the value part
                    continue;
                }

                if (iIndex < 0) // NOT a Code
                    return Issue.Create(issueSource, 56
                        , _IssueId.Invalid_param_name
                        , s_Param_ + cursor.ToString() + AS.SP + xKeyName.ToString()
                        );

                if (cursor >= iParamsEnd)
                    return Issue.Create(issueSource, 58 // FIX: spot dedup (was 56, reused 3x)
                        , _IssueId.Missing_parm_value
                        , s_Param_ + iParamsEnd.ToString() + AS.SP + xKeyName.ToString()
                        );

                string sValue = theParams[++cursor].ToString();

                Chars vValue = GetAuditedValue(Codes, iIndex, new Chars(sValue), out string sErrLines);

                if (!string.IsNullOrEmpty(sErrLines))
                    return Issue.Create(issueSource, 59 // FIX: spot dedup (was 56, reused 3x)
                        , _IssueId.Invalid_param_value
                        , s_Param_ + cursor.ToString() + AS.SP + xKeyName.ToString() + AS.SP + sValue
                        , sErrLines
                        );

                if (!SetValue(iIndex, vValue))
                    return Issue.Create(issueSource, 57
                        , _IssueId.Unable_to_set_value
                        , s_Param_ + cursor.ToString() + AS.SP + xKeyName.ToString() + AS.SP + sValue
                        , sErrLines
                        );
            }
            return null;
        }

        /// <summary>Clears all internal values to empty.</summary>
        public void ClearValues()
        {
            int i = -1;
            while (++i < _Values.Length)
                _Values[i] = Chars.Nothing;
        }

        /// <summary>Re-audits and re-stores every value.</summary>
        public void AuditValues()
        {
            int i = -1;
            while (++i < _Values.Length)
                _Values[i] = Value_audited(i, out _, out _);
        }

        /// <summary>Built-in audit for the enum member's value (resolves its EnumCodes then audits).</summary>
        public static Chars GetAuditedValue(Enum theEnum, Chars theValue, out string returnErrLines)
        {
            EnumCodes xCodes = EnumCodes.ForCode(theEnum, out int index);
            return GetAuditedValue(xCodes, index, theValue, out returnErrLines);
        }

        /// <summary>
        /// Built-in audit: normalizes the trimmed value per the member's
        /// Bits.ValId (truth, folder/file/path validation, line, numeric
        /// parses, date/datetime restamp), returning the normalized value
        /// with error lines on failures (null when clean). LIST kinds
        /// (folders/files/paths/lines) audit each visible line as the
        /// singular item kind, rebuilding the value from the passing
        /// lines. Ids without an audit rule return empty.
        /// </summary>
        public static Chars GetAuditedValue(EnumCodes theCodes, int theIndex, Chars theValue, out string returnErrLines)
        {
            returnErrLines = null;
            if (theIndex < 0 || theCodes == null || theIndex >= theCodes.Count)
                return Chars.Nothing;
            uint iFlag32 = theCodes.Flag32(theIndex);
            Bits.ValId iValId = Bits.ValId_from_flags(iFlag32);
            Bits.ValKindId iKind = Bits.ValKindId_from_flags(iFlag32);

            if (iKind == Bits.ValKindId.List)
            {
                // FIX(D13, G 2026-07-17): rebuilt - the old branch switched
                // the USER's enum against ValFlag constants (never matched),
                // recursed on the same index (masked non-termination), and
                // swallowed item errors. Each visible line is now audited
                // as the singular ITEM kind.
                Bits.ValId iItemValId;

                switch (iValId)
                {
                    case Bits.ValId.folders: iItemValId = Bits.ValId.folder; break;
                    case Bits.ValId.files: iItemValId = Bits.ValId.file; break;
                    case Bits.ValId.paths: iItemValId = Bits.ValId.path; break;
                    case Bits.ValId.lines: iItemValId = Bits.ValId.line; break;
                    default:
                        return Chars.Nothing; // list kind without an item audit rule (urls, ...)
                }

                string sLines = string.Empty;
                string sErrs = string.Empty;

                while (theValue.PluckedLine(out Chars vLine))
                {
                    vLine.Trim();

                    if (vLine.IsEmpty) continue;

                    Chars vItem = _AuditedValue(theCodes, theIndex, iItemValId, vLine, out string sItemErr);

                    if (sItemErr != null) sErrs += sItemErr + FS.LSep;

                    if (vItem.NotEmpty)
                    {
                        if (sLines.Length > 0) sLines += FS.LSep;

                        sLines += vItem.ToString();
                    }
                }

                if (sErrs.Length > 0) returnErrLines = sErrs;

                return new Chars(sLines);
            }

            return _AuditedValue(theCodes, theIndex, iValId, theValue, out returnErrLines);
        }

        private static Chars _AuditedValue(EnumCodes theCodes, int theIndex, Bits.ValId iValId, Chars theValue, out string returnErrLines)
        {
            string errLines(Chars oldVal, object xMsg)
            {
                return "Issue: " + theCodes.Name(theIndex) + AS.SP
                    + Bits.ValId_from_flags(theCodes.Flag32(theIndex))
                    + FS.LSep + "Value: " + AS.Quoted(oldVal.ToString())
                    + FS.LSep + xMsg.ToString();
            }

            returnErrLines = null;
            Issue xIssue;
            Chars vNew = Chars.Nothing;
            bool bNumberDone = false;
            bool bDateDone = false;
            bool bNumberSuccess = false;

            theValue.Trim();

            switch (iValId)
            {
                case Bits.ValId.truth:
                    vNew = theValue.IsEmpty ? Chars.Nothing
                        : new Chars(AS.IsTrue(theValue[0]) ? AS.True : null);
                    return vNew;

                case Bits.ValId.folder:
                    vNew = new Chars(FS.ValidFolderPath_or_null(theValue.ToString(), out xIssue, false));
                    if (xIssue != null) returnErrLines = errLines(theValue, xIssue);
                    return vNew;

                case Bits.ValId.file:
                    vNew = new Chars(FS.ValidFilePath_or_null(theValue.ToString(), out xIssue, false));
                    if (xIssue != null) returnErrLines = errLines(theValue, xIssue);
                    return vNew;

                case Bits.ValId.path:
                    vNew = new Chars(FS.ValidPath_or_null(theValue.ToString(), out xIssue));
                    if (xIssue != null) returnErrLines = errLines(theValue, xIssue);
                    return vNew;

                case Bits.ValId.line:
                    vNew = theValue.PluckFirstVisibleLine();
                    return vNew;


                case Bits.ValId.int32:
                    bNumberDone = true;
                    bNumberSuccess = Int32.TryParse(theValue.ToString(), out int i_Int32);
                    vNew = new Chars(i_Int32.ToString());
                    break;

                case Bits.ValId.int64:
                    bNumberDone = true;
                    bNumberSuccess = Int64.TryParse(theValue.ToString(), out long i_Int64);
                    vNew = new Chars(i_Int64.ToString());
                    break;

                case Bits.ValId.uint32:
                    bNumberDone = true;
                    bNumberSuccess = UInt32.TryParse(theValue.ToString(), out uint i_UInt32);
                    vNew = new Chars(i_UInt32.ToString());
                    break;

                case Bits.ValId.uint64:
                    bNumberDone = true;
                    bNumberSuccess = UInt64.TryParse(theValue.ToString(), out ulong i_ULong64);
                    vNew = new Chars(i_ULong64.ToString());
                    break;

                case Bits.ValId.float64:
                    bNumberDone = true;
                    bNumberSuccess = double.TryParse(theValue.ToString(), out double i_float64);
                    vNew = new Chars(i_float64.ToString());
                    break;

                case Bits.ValId.float32:
                    bNumberDone = true;
                    bNumberSuccess = float.TryParse(theValue.ToString(), out float i_float32);
                    vNew = new Chars(i_float32.ToString());
                    break;

                case Bits.ValId.date:
                    bNumberDone = true; bDateDone = true;
                    bNumberSuccess = AS.ParsedDateTime(theValue.ToString(), out DateTime xDate);
                    if (bNumberSuccess)
                    {
                        vNew = new Chars(xDate.ToString(AS.STAMP_yyyy_MM_dd));
                    }
                    break;

                case Bits.ValId.datetime:
                    bNumberDone = true; bDateDone = true;
                    bNumberSuccess = AS.ParsedDateTime(theValue.ToString(), out DateTime xDateTime);
                    if (bNumberSuccess)
                    {
                        vNew = new Chars(xDateTime.ToString(AS.STAMP_date_HHmm_ss_fff));
                    }
                    break;
            }

            if (bNumberDone && !bNumberSuccess)
                returnErrLines += errLines(theValue, bDateDone ? _IssueId.Invalid_date : _IssueId.Invalid_numeric_value);

            return vNew;
        }
    }
}
