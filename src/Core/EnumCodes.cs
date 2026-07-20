// CopyrightCopyright (c) GDFrank
// SPDX-License-Identifier: MIT

using System;
using System.Reflection;
using System.Collections.Generic;

namespace Source77NW
{
    /// <summary>
    /// Type-level attribute for an enum registered with EnumCodes: optional Text,
    /// Tag object, and a Version byte for managing ByteCode changes in persisted
    /// storage (bump Version to signal stored data must be invalidated/refactored).
    /// </summary>
    public sealed class EnumCodesAttribute : Attribute
    {
        /// <summary>Free-form text for the enum type (null when none).</summary>
        public readonly string Text; 
        /// <summary>Arbitrary tag object for the enum type (null when none).</summary>
        public readonly object Tag; 
        /// <summary>Persistence version of the enum's ByteCodes (0 = unmanaged).</summary>
        public readonly byte Version;
        
        /// <summary>True when Text is present.</summary>
        public bool HasText => Text != null;
        /// <summary>True when Tag is present.</summary>
        public bool HasTag => Tag != null;

        /// <summary>Creates the attribute with text (trimmed), optional tag, and optional version.</summary>
        public EnumCodesAttribute(string theText, object theTag = null, byte theVersion = 0)
        {
            Text = theText?.Trim();
            Tag = theTag;
            Version = theVersion; 
        }

        /// <summary>Creates the attribute with a tag and optional version (no text).</summary>
        public EnumCodesAttribute(object theTag, byte theVersion = 0)
        {
            Text = null;
            Tag = theTag;
            Version = theVersion;
        }

        /// <summary>Creates an empty attribute (no text, no tag, version 0).</summary>
        public EnumCodesAttribute()
        {
            Text = null;
            Tag = null;
            Version = 0;
        }

    }

    /// <summary>
    /// Item-level attribute for an enum member: optional Text (usually VBAR format
    /// "|name value|..." for FoundDelimitedNameAndText, e.g. "|icon edit.cut.ico|tip Cut selected|")
    /// and/or Tag objects for building operational items and enum relationships.
    /// </summary>
    public sealed class EnumInfoAttribute : Attribute
    {
        /// <summary>Free-form text, usually VBAR-delimited name/value pairs (null when none).</summary>
        public readonly string Text; 
        /// <summary>Arbitrary tag objects (null when none).</summary>
        public readonly object[] Tags;

        /// <summary>True when Text is present.</summary>
        public bool HasText => Text != null;
        /// <summary>True when Tags are present.</summary>
        public bool HasTags => Tags != null;

        /// <summary>Creates the attribute with text only (trimmed).</summary>
        public EnumInfoAttribute(string theResCodeIdVals)
        {
            Text = theResCodeIdVals?.Trim();
            Tags = null;
        }

        /// <summary>Creates the attribute with tag objects only (empty array becomes null).</summary>
        public EnumInfoAttribute(params object[] theTags)
        {
            Text = null;
            Tags = theTags;
            if (theTags.Length == 0)
                Tags = null;
        }

        /// <summary>Creates the attribute with text (trimmed) and tag objects (empty array becomes null).</summary>
        public EnumInfoAttribute(string theResCodeIdVals, params object[] theTags)
        {
            Text = theResCodeIdVals?.Trim();
            Tags = theTags;
            if (theTags.Length == 0)
                Tags = null;
        }

    }

    /// <summary>
    /// Views an enum type as an indexed, validated list of codes, flags, and text
    /// values - a centralized truth table for processing behaviors, message ints,
    /// and resource access (captions, menus, tips, icons). One validated singleton
    /// per enum type, created on first ForType/ForCode call and cached. Thread safe.
    /// </summary>
    /// <remarks>
    /// Creation throws (as Issue) when the enum has more than 256 values, values
    /// are unsorted or not unique, low bytes (ByteCodes) are not unique, or names
    /// are not unique ignoring case. ByteCode is safe for PERSISTED references
    /// providing it never changes; manage changes via EnumCodesAttribute.Version.
    /// Indexes and names are runtime-only, never for persisting. Enums should be
    /// byte/ushort/uint/ulong. See also ResCode.cs, EnumVals.cs, Bits.cs.
    /// </remarks>
    public sealed class EnumCodes : IComparable<EnumCodes>
    {
        // STATIC ====================================

        /// <summary>Maximum enum values per type (256; ByteCodes must fit a byte).</summary>
        public const int MaxCodeCount = 256;
        private const string LF = "\n";
        private const string SP = " ";
        private const string s_Index_ = @"Index: ";
        private const string s_CacheCount_ = @"CacheCount: ";
        private const string s_Count_ = @"Count: ";
        private const string s_ByteCode_ = @"ByteCode: ";
        private const string s_SimpleName_ = @"SimpleName: ";
        private const string s_This_ = @"This: ";
        private const string s_Last_ = @"Last: ";
        private const string s_ForType_ = @"ForType: ";
        private const string s_EnumType_ = @"EnumType: ";
        private const string s_EnumCodes_class = @"EnumCodes class";

        private const ushort issueSource = 65111;
        private const int _CodesCache_OpenSize = 32;
        private const char DOTchar = '.';
        private const char UNDERchar = '_';
        private const char SPchar = ' ';

        private static volatile ItemStack<EnumCodes> _CodesCache 
            = new ItemStack<EnumCodes>(_CodesCache_OpenSize);
        private static volatile Dictionary<long, ushort> _TypeHandles_to_CodesCacheIndex 
            = new Dictionary<long, ushort>(_CodesCache_OpenSize);
        /// <summary>Count of EnumCodes singletons currently cached.</summary>
        public static int CacheCount => _CodesCache.Count;

        /// <summary>Returns the singleton for theCode's enum type plus the code's index (-1 if not found). Throws on null.</summary>
        public static EnumCodes ForCode(Enum theCode, out int returnCodeIndex)
        {
            if (theCode == null)
                throw _Issue(issueId.Null_argument, s_ForType_ + nameof(theCode));

            Type xType = theCode.GetType();
            EnumCodes xCodes = ForType(xType);
            returnCodeIndex = xCodes.IndexOf(theCode);
            return xCodes;
        }

        /// <summary>Returns the cached singleton at theIndex (see CacheIndex). Throws on invalid index.</summary>
        public static EnumCodes CacheItem(ushort theIndex)
        {
            if (theIndex < _CodesCache.Count)
                return _CodesCache[theIndex];
            throw _Issue(issueId.EnumCodes_invalid_cache_index, s_Index_ + theIndex.ToString());
        }

        /// <summary>Returns the singleton for theEnumType, creating and validating it on first call. Throws (Issue) on null, non-enum, or validation failure.</summary>
        public static EnumCodes ForType(Type theEnumType)
        {
            if (theEnumType == null)
                throw _Issue(issueId.Null_argument, s_ForType_ + nameof(theEnumType));
            if (!theEnumType.IsEnum)
                throw _Issue(issueId.Type_not_enum_type, theEnumType.Name);

            long iTypeHandle = theEnumType.TypeHandle.Value.ToInt64(); // Runtime handle

            if (_TypeHandles_to_CodesCacheIndex.TryGetValue(iTypeHandle, out ushort iCacheIndex))
                return _CodesCache[iCacheIndex];

            lock (_CodesCache)
            {
                // repeat in case another thread created
                if (_TypeHandles_to_CodesCacheIndex.TryGetValue(iTypeHandle, out iCacheIndex))
                    return _CodesCache[iCacheIndex];

                // ushort.MaxValue is ResCode's IsEmpty sentinel, never a valid index
                if (_CodesCache.Count >= ushort.MaxValue)
                    throw _Issue(issueId.EnumCodes_cache_limit_exceeded
                        , s_CacheCount_ + ushort.MaxValue.ToString()
                        , null);

                EnumCodes xCodes = new EnumCodes() { EnumType = theEnumType };

                xCodes.Info = theEnumType.GetCustomAttribute<EnumCodesAttribute>();

                // CLR returns GetValues items in sorted order (even in framework) 
                xCodes._Values = Enum.GetValues(xCodes.EnumType);
                xCodes._Names = Enum.GetNames(xCodes.EnumType);
                xCodes.Count = xCodes._Values.Length;

                if (xCodes.Count > MaxCodeCount)
                    throw _Issue(issueId.Enum_item_count_exceeds_256
                        , s_Count_ + xCodes.Count.ToString()
                        , xCodes.EnumType);

                // validate FULLY before publication.
                xCodes._ForType_initialization();

                // publish only after successful validation
                iCacheIndex = (ushort)_CodesCache.Push(xCodes);
                xCodes.CacheIndex = iCacheIndex;

                // copy-on-write swap so the unlocked fast path never observes a mutating Add
                var xMap = new Dictionary<long, ushort>(_TypeHandles_to_CodesCacheIndex);
                xMap.Add(iTypeHandle, iCacheIndex);
                _TypeHandles_to_CodesCacheIndex = xMap;

                return xCodes;
            }
        }

        /// <summary>Soft ForCode: returns null with the Issue instead of throwing.</summary>
        public static EnumCodes ForCode_or_null(Enum theCode, out int returnCodeIndex, out Issue returnIssue)
        {
            try
            {
                returnIssue = null;
                return ForCode(theCode, out returnCodeIndex);
            }
            catch (Exception e) // e is always Issue
            {
                returnCodeIndex = -1;
                if (e is Issue xIssue)
                    returnIssue = xIssue;
                else
                    returnIssue = _Issue(issueId.Unexpected_issue, e.ToString(), null);
                return null;
            }
        }

        /// <summary>Soft ForType: returns null with the Issue instead of throwing.</summary>
        public static EnumCodes ForType_or_null(Type theEnumType, out Issue returnIssue)
        {
            try
            {
                returnIssue = null;
                return ForType(theEnumType);
            }
            catch (Exception e) // e is always Issue
            {
                if (e is Issue xIssue)
                    returnIssue = xIssue;
                else
                    returnIssue = _Issue(issueId.Unexpected_issue, e.ToString(), null);
                return null;
            }
        }

        /// <summary>Returns the enum's name with '_' converted to '.' (token form).</summary>
        public static string EnumAsToken(Enum theEnum)
        {
            string sName = theEnum.ToString();
            if (sName.IndexOf(UNDERchar) >= 0)
                return sName.Replace(UNDERchar, DOTchar);
            return sName;
        }

        /// <summary>Returns the enum's name with '_' converted to ' ' (caption form).</summary>
        public static string EnumAsCaption(Enum theEnum)
        {
            string sName = theEnum.ToString();
            if (sName.IndexOf(UNDERchar) >= 0)
                return sName.Replace(UNDERchar, SPchar);
            return sName;
        }

        private static void _normalizeName(ref string refName, char c_new, char c_old1, char c_old2)
        {
            /* Normalizes the sName for consistent name reference.
             * Only creates and updates to xNew if necessary, 
             *    converting c_old1 or c_old2 to c_new.
             */

            int cursor = -1;
            while (++cursor < refName.Length)
            {
                char c_old = refName[cursor];
                if (c_old == c_old1 || c_old == c_old2)
                {
                    int i = -1;
                    var xNew = new char[refName.Length];
                    while (++i < cursor) xNew[i] = refName[i]; // back fill xNew
                    xNew[cursor] = c_new;
                    while (++cursor < xNew.Length) // build the rest to xNew
                    {
                        c_old = refName[cursor];
                        if (c_old == c_old1 || c_old == c_old2)
                            xNew[cursor] = c_new;
                        else
                            xNew[cursor] = c_old;
                    }
                    refName = new string(xNew);
                }
            }
        }

        
        // INSTANCE =================================

        private Array _Values;
        private string[] _Names;
        private byteCodeToIndex[] _ByteCodeToIndex_Array;
        private nameToIndex[] _NameToIndex_Array;
        private volatile bool _IsByteCodeToIndex_Sorted = false;
        private volatile bool _IsNameToIndex_Sorted = false;
        private bool _no_need_for_ByteCode_BinarySearch = false;

        /// <summary>The type-level EnumCodesAttribute, or null when absent.</summary>
        public EnumCodesAttribute Info { get; private set; }
        /// <summary>This singleton's index in the EnumCodes cache (see CacheItem).</summary>
        public ushort CacheIndex { get; private set; }
        /// <summary>The enum type this instance views.</summary>
        public Type EnumType { get; private set; }
        /// <summary>Number of values in the enum (Enum.GetValues().Length).</summary>
        public int Count { get; private set; }
        /// <summary>Smallest ByteCode (low byte) among the enum's values.</summary>
        public byte MinByteCode { get; private set; }
        /// <summary>Largest ByteCode (low byte) among the enum's values.</summary>
        public byte MaxByteCode { get; private set; }
        /// <summary>True when a type-level EnumCodesAttribute is present.</summary>
        public bool HasInfo => Info != null;
        /// <summary>The attribute's Version, or 0 when absent.</summary>
        public byte CodesVersion => HasInfo ? Info.Version : byte.MinValue;
        /// <summary>True when CodesVersion is above 0 (ByteCodes are persistence-managed).</summary>
        public bool IsVersionManaged => CodesVersion > 0;

        /// <summary>The enum value at index (sorted order). Throws on invalid index.</summary>
        public Enum Code(int index)
        {
            if (index >= 0 && index < Count)
                return (Enum)_Values.GetValue(index);

            throw _Issue(issueId.Invalid_index, index.ToString(), EnumType);
        }
        
        /// <summary>The enum value's name at index. Throws on invalid index.</summary>
        public string Name(int index)
        {
            if (index >= 0 && index < Count)
                return _Names[index];

            throw _Issue(issueId.Invalid_index, index.ToString(), EnumType);
        }

        /// <summary>Caption form ('_' to ' ') of theCode's name. Throws if theCode is not of this enum type.</summary>
        public string NameAsCaption(Enum theCode) => NameAsCaption(IndexOf(theCode));
        
        /// <summary>Caption form ('_' to ' ') of the name at index. Throws on invalid index.</summary>
        public string NameAsCaption(int index)
        {
            string sName = Name(index); // will throw Issue if bad index
            if (sName.IndexOf(UNDERchar) >= 0)
                return sName.Replace(UNDERchar, SPchar);
            return sName;
        }

        /// <summary>Token form ('_' to '.') of theCode's name. Throws if theCode is not of this enum type.</summary>
        public string NameAsToken(Enum theCode) => NameAsToken(IndexOf(theCode));

        /// <summary>Token form ('_' to '.') of the name at index. Throws on invalid index.</summary>
        public string NameAsToken(int index)
        {
            string sName = Name(index); // will throw Issue if bad index
            if (sName.IndexOf(UNDERchar) >= 0)
                return sName.Replace(UNDERchar, DOTchar);
            return sName;
        }

        /// <summary>The enum value at index as a 64-bit flag. Throws on invalid index.</summary>
        public ulong Flag64(int index) => Convert.ToUInt64(Code(index));

        /// <summary>The enum value at index as a 32-bit flag (low 32 bits). Throws on invalid index.</summary>
        public uint Flag32(int index) => (uint)Convert.ToUInt64(Code(index));

        /// <summary>The enum value at index as its low byte (persistable reference). Throws on invalid index.</summary>
        public byte ByteCode(int index) => (byte)Convert.ToUInt64(Code(index));

        /// <summary>Binary-searches for theCode; returns its index or -1 (also -1 for null or wrong enum type).</summary>
        public int IndexOf(Enum theCode)
        {
            if (theCode != null && _IsMyEnumType(theCode.GetType()))
            {
                int i = Array.BinarySearch(_Values, theCode);
                if (i >= 0) return i;
            }
            return -1;
        }

        /// <summary>Binary-searches for a name in symbol, token, or caption form (case-insensitive); returns its index or -1.</summary>
        public int IndexOf(string theName_Token_or_Caption)
        {
            if (!string.IsNullOrEmpty(theName_Token_or_Caption))
            {
                // normalize to symbol name.
                _normalizeName(ref theName_Token_or_Caption, UNDERchar, DOTchar, SPchar);

                // ensure normalized to symbol name

                if (!_IsNameToIndex_Sorted)
                    lock (_NameToIndex_Array)
                    {
                        if (!_IsNameToIndex_Sorted) // double-check (matches ByteCode path); avoid redundant re-sort under race
                            _NamesToIndex_Sort_and_validate();
                    }

                int iFound = Array.BinarySearch(_NameToIndex_Array, theName_Token_or_Caption);

                if (iFound >= 0)
                    return _NameToIndex_Array[iFound].CodeIndex;
            }

            return -1;
        }

        /// <summary>Finds the value index whose low byte equals theByteCode; -1 if none. Direct lookup when ByteCodes are dense, else binary search.</summary>
        public int IndexOfByteCode(byte theByteCode)
        {
            if (!_IsByteCodeToIndex_Sorted)
            {
                lock (_ByteCodeToIndex_Array)
                {
                    // check again if another thread sorted before we sort
                    if (!_IsByteCodeToIndex_Sorted)
                        _ByteCodeToIndex_Sort_and_validate();
                }
            }

            if (_no_need_for_ByteCode_BinarySearch)
            {
                // theByteCode is the index to the Array element
                if (theByteCode < _ByteCodeToIndex_Array.Length)
                    return _ByteCodeToIndex_Array[theByteCode].CodeIndex;
                return -1; // out-of-range ByteCode threw IndexOutOfRange; contract says -1
            }

            // OTHERWISE we have to BinarySearch

            byteCodeToIndex v1 = byteCodeToIndex.New(theByteCode, 0);

            int i = Array.BinarySearch(_ByteCodeToIndex_Array, v1);

            if (i >= 0)
                return _ByteCodeToIndex_Array[i].CodeIndex;

            return -1;
        }

        /// <summary>True with the member's EnumInfoAttribute if present at index. Throws on invalid index.</summary>
        public bool GotCodeInfo(int index, out EnumInfoAttribute returnInfo)
        {
            var fieldInfo = EnumType.GetField(Name(index));
            returnInfo = fieldInfo?.GetCustomAttribute<EnumInfoAttribute>();
            return returnInfo != null;
        }

        /// <summary>Gets the theIndex-th (ByteCode, value-index) pair in ByteCode order; false when theIndex is out of range.</summary>
        public bool GotByteCodesItem(int theIndex, out byte returnByteCode, out int returnValueIndex)
        {
            if (!_IsByteCodeToIndex_Sorted)
            {
                lock (_ByteCodeToIndex_Array)
                {
                    // check again if another thread
                    // sorted before we sort
                    if (!_IsByteCodeToIndex_Sorted)
                    {
                        _ByteCodeToIndex_Sort_and_validate();
                    }
                }
            }
            if (theIndex >= 0 && theIndex < _ByteCodeToIndex_Array.Length)
            {
                byteCodeToIndex vItem = _ByteCodeToIndex_Array[theIndex];
                returnByteCode = vItem.ByteCode;
                returnValueIndex = vItem.CodeIndex;
                return true;
            }
            returnByteCode = default;
            returnValueIndex = default;
            return false;
        }

        /// <summary>Cursor enumeration of (ByteCode, value-index) pairs: int cursor = 0; while (GotNextByteCode(ref cursor, out b, out i)) ...</summary>
        public bool GotNextByteCode(ref int cursor, out byte returnByteCode, out int returnValueIndex)
        {
            return GotByteCodesItem(cursor++, out returnByteCode, out returnValueIndex);
        }

        /// <summary>The enum type's full name and value count.</summary>
        public sealed override string ToString() => EnumType.FullName + SP + s_Count_ + Count.ToString();

        /// <summary>Hash of the enum type's runtime handle.</summary>
        public sealed override int GetHashCode() => EnumType.TypeHandle.GetHashCode();

        /// <summary>Equality by enum type (runtime handle).</summary>
        public override bool Equals(object obj)
        {
            if (obj is EnumCodes xCodes)
                return Equals(xCodes);

            return false;
        }

        /// <summary>Equality by enum type (runtime handle).</summary>
        public bool Equals(EnumCodes theCodes) => 0 == CompareTo(theCodes);

        /// <summary>Orders instances by enum type runtime handle (null sorts first).</summary>
        public int CompareTo(EnumCodes theCodes)
        {
            if (theCodes == null) return 1;
            long i1 = EnumType.TypeHandle.Value.ToInt64();
            long i2 = theCodes.EnumType.TypeHandle.Value.ToInt64();
            return i1.CompareTo(i2);
        }

        private bool _IsMyEnumType(Type theType)
        {
            if (theType == null) return false;
            long i1 = EnumType.TypeHandle.Value.ToInt64();
            long i2 = theType.TypeHandle.Value.ToInt64();
            return i1 == i2;
        }


        // PRIVATE INITIALIZATION METHODS/CONST =====================

        private EnumCodes() { }

        private void _ForType_initialization()
        {
            // _Names, _Values, Count are set; runs BEFORE cache publication (see ForType FIX)

            MinByteCode = byte.MaxValue;
            MaxByteCode = 0;
            _ByteCodeToIndex_Array = new byteCodeToIndex[Count];
            _NameToIndex_Array = new nameToIndex[Count];

            ulong iLastFlag = 0;

            int i = -1;
            while (++i < Count)
            {
                string sName = _Names[i];
                ulong iFlag = Flag64(i);
                byte iByteCode = (byte)iFlag;

                if (iByteCode < MinByteCode) MinByteCode = iByteCode;
                if (iByteCode > MaxByteCode) MaxByteCode = iByteCode;

                _NameToIndex_Array[i] = nameToIndex.New(sName, (byte)i);
                _ByteCodeToIndex_Array[i] = byteCodeToIndex.New(iByteCode, (byte)i);

                if (i == 0)
                {
                    iLastFlag = iFlag;
                }
                else
                {
                    if (iFlag <= iLastFlag)
                    {
                        if (iFlag == iLastFlag)
                        {
                            throw _Issue(issueId.Enum_values_are_not_unique
                            , s_This_ + sName + LF + s_Last_ + Name(i - 1)
                            , EnumType);
                        }

                        throw _Issue(issueId.Unsorted_Enum_values_not_supported
                        , s_This_ + sName + LF + s_Last_ + Name(i - 1)
                        , EnumType);
                    }
                }
            }

            _no_need_for_ByteCode_BinarySearch = MaxByteCode == Count - 1;
            // 1. all ByteCode must be unique (see _ByteCodeToIndex_Sort)
            // 2. all ByteCode are present since MaxByteCode is at top
            // 3. thus _no_need_for_ByteCode_BinarySearch (no sparse entries)
            if (Exe.IsDebug || Exe.IsAlpha)
            {
                _ByteCodeToIndex_Sort_and_validate();
                _NamesToIndex_Sort_and_validate();
            }
        }

        private void _NamesToIndex_Sort_and_validate()
        {
            Array.Sort(_NameToIndex_Array);

            if (_NameToIndex_Array.Length == 0)
            {
                _IsNameToIndex_Sorted = true;
                return;
            }

            int i = 0;
            nameToIndex vLast = _NameToIndex_Array[i];
            while (++i < _Values.Length)
            {
                nameToIndex vThis = _NameToIndex_Array[i];

                if (string.Equals(vThis.CodeName, vLast.CodeName, StringComparison.OrdinalIgnoreCase))
                    throw _Issue(issueId.Enum_names_not_unique
                        , s_This_ + vThis.CodeName + LF + s_Last_ + vLast.CodeName
                        , EnumType);

                vLast = vThis;
            }
            _IsNameToIndex_Sorted = true;
        }

        private void _ByteCodeToIndex_Sort_and_validate()
        {
            // ONLY SORT WHEN first call to IndexOfByteCode() occurs

            Array.Sort(_ByteCodeToIndex_Array);

            if (_ByteCodeToIndex_Array.Length == 0)
            {
                _IsByteCodeToIndex_Sorted = true;
                return;
            }

            int i = 0;
            byteCodeToIndex vLast = _ByteCodeToIndex_Array[i];
            while (++i < _Values.Length)
            {
                byteCodeToIndex vThis = _ByteCodeToIndex_Array[i];

                if (vThis.ByteCode <= vLast.ByteCode)
                {
                    throw _Issue(issueId.Enum_value_lobytes_are_not_unique
                        , s_Last_ + vLast.ToString() + LF + s_This_ + vThis.ToString()
                        , EnumType);
                }

                vLast = vThis;
            }

            _IsByteCodeToIndex_Sorted = true;
        }

        private static Issue _Issue(issueId theId, string details, Type reType = null)
        {
            byte iId = (byte)theId;
            string sCaption = theId.ToString().Replace(UNDERchar, SPchar);
            if (reType != null)
                return Issue.Create(issueSource, iId, sCaption, details, s_EnumType_ + reType.FullName, IssueKind.ProgramIssue);

            return Issue.Create(issueSource, iId, sCaption, details, s_EnumCodes_class, IssueKind.ProgramIssue);
        }

        private struct nameToIndex : IComparable, IComparable<nameToIndex>
        {
            public string CodeName;
            
            public byte CodeIndex;

            public static nameToIndex New(string theCodeName, byte theCodeIndex)
            {
                return new nameToIndex
                {
                    CodeName = theCodeName,
                    CodeIndex = theCodeIndex
                };
            }

            public int CompareTo(nameToIndex that)
            {
                return string.Compare(CodeName, that.CodeName, StringComparison.OrdinalIgnoreCase);
            }

            public int CompareTo(object obj)
            {
                if (obj is string sText)
                    return string.Compare(CodeName, sText, StringComparison.OrdinalIgnoreCase);

                if (obj is nameToIndex that)
                    return string.Compare(CodeName, that.CodeName, StringComparison.OrdinalIgnoreCase);

                throw _Issue(issueId.Invalid_CompareTo, obj.GetType().FullName, null);
            }

            public override bool Equals(object obj)
            {
                if (obj is nameToIndex that)
                    return string.Equals(CodeName, that.CodeName, StringComparison.OrdinalIgnoreCase);

                if (obj is string sText)
                    return string.Equals(CodeName, sText, StringComparison.OrdinalIgnoreCase);

                return false;
            }

            public override int GetHashCode() => CodeName.GetHashCode();

            public override string ToString() => s_SimpleName_ + CodeName + SP + s_Index_ + CodeIndex.ToString();
        }

        private struct byteCodeToIndex : IComparable<byteCodeToIndex>
        {
            public byte ByteCode;

            public byte CodeIndex;

            public static byteCodeToIndex New(byte theByteCode, byte theCodeIndex)
            {
                return new byteCodeToIndex
                {
                    ByteCode = theByteCode,
                    CodeIndex = theCodeIndex
                };
            }

            public int CompareTo(byteCodeToIndex that) => ByteCode.CompareTo(that.ByteCode);

            public override bool Equals(object obj) => obj is byteCodeToIndex index && ByteCode.Equals(index.ByteCode);

            public override int GetHashCode() => ByteCode.GetHashCode();

            public override string ToString() => s_ByteCode_ + ByteCode.ToString() + SP + s_Index_ + CodeIndex.ToString();
        }

        private enum issueId : byte
        {
            NoIssue = 0,
            Null_argument = 1,
            Type_not_enum_type = 2,
            Invalid_index = 3,
            Invalid_CompareTo = 4,
            Enum_values_are_not_unique = 5,
            Unsorted_Enum_values_not_supported = 6,
            Enum_item_count_exceeds_256 = 7,
            Enum_value_lobytes_are_not_unique = 8,
            Enum_names_not_unique = 9,
            EnumCodes_cache_limit_exceeded = 10,
            EnumCodes_invalid_cache_index = 11,
            Unexpected_issue = 12,
        }
    }
}

