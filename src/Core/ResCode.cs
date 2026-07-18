// Copyright (c) GDFrank
// SPDX-License-Identifier: MIT

using System;

namespace Source77NW
{
    /// <summary>
    /// Identifies which resource value of a ResCode is being requested:
    /// caption, menu caption, button caption, tip, ctrl-key sequence, or a
    /// named icon/echo/blob stream reference.
    /// </summary>
    /// <remarks>
    /// Values are a storage contract - MUST remain byte, MAY BE PERSISTED,
    /// and are used as indexes into internal name tables; do not renumber.
    /// Each value's NameAsToken is the name key inside EnumInfoAttribute
    /// VBAR text, e.g. "|icon edit.cut.ico|ctrl ctrl-X|tip Cut selected|".
    /// </remarks>
    public enum CodeValId : byte
    {
        /// <summary>Caption/phrase/cmd text; when absent the enum member's NameAsCaption is used.</summary>
        cap = 0,
        /// <summary>Menu caption; defaults to "&amp;cap" (a single-letter value marks that letter of cap as the accelerator).</summary>
        mnu = 1,
        /// <summary>Button caption; defaults to mnu behavior.</summary>
        but = 2,
        /// <summary>Menu/button/input tip (brief help text).</summary>
        tip = 3,
        /// <summary>Associated ctrl-key sequence (shift/letter).</summary>
        ctrl = 4,
        /// <summary>Icon NAME reference (resolved via a registered stream provider).</summary>
        icon = 5,
        /// <summary>Echo NAME reference (short sound).</summary>
        echo = 6,
        /// <summary>Blob NAME reference (embedded blob stream).</summary>
        blob = 7,
    }

    /// <summary>
    /// Classifies what an enum member DEFINES in an EnumCodes-driven UI
    /// structure: a menu, action, toggle, group, radio list, or value.
    /// </summary>
    /// <remarks>
    /// Structural conventions: a menu member is followed by its
    /// actions/toggles/groups/radios/menus; a group is followed by
    /// actions/toggles/radios; a radio list is followed by toggles.
    /// Groups/radios inside menus display between menu separators; menus
    /// inside menus display as sub-menus. A value member is optionally
    /// defined by Bits.cs ValFlag/Id. VALUES MAY BE PERSISTED - do not
    /// renumber.
    /// </remarks>
    public enum CodeDefId : byte
    {
        /// <summary>Unspecified definition kind.</summary>
        unspecified = 0,
        /// <summary>A menu, followed by its actions/toggles/groups/radios/sub-menus.</summary>
        menu = 1,
        /// <summary>An action (command).</summary>
        action = 2,
        /// <summary>A toggle (check on/off).</summary>
        toggle = 3,
        /// <summary>A group list, followed by actions/toggles/radios.</summary>
        group = 4,
        /// <summary>A radio list, followed by toggles.</summary>
        radio = 5,
        /// <summary>A value, optionally defined by Bits.cs ValFlag/Id.</summary>
        value = 6,
    }

    /// <summary>
    /// Identifies a resource pack kind for ResCode stream and lingo lookups:
    /// named streams (icon/echo/blob) or a lingo override pack. Lingo values
    /// carry bit 7 set (see ResCode.IsLingo); the low bits are the lingo
    /// number.
    /// </summary>
    public enum ResKind : byte
    {
        /// <summary>No resource kind.</summary>
        none = 0,
        /// <summary>Named icon streams of any image type.</summary>
        icon = 1,
        /// <summary>Named short sound streams used to echo actions.</summary>
        echo = 2,
        /// <summary>Other named streams.</summary>
        blob = 3,

        /// <summary>Lingo pack 0 (en) - extends the built-in CodeValId values.</summary>
        lingo0 = 0 | 1 << 7,
        /// <summary>Lingo pack 1 (sp) - overrides.</summary>
        lingo1 = 1 | 1 << 7,
        /// <summary>Lingo pack 2 (fr) - overrides.</summary>
        lingo2 = 2 | 1 << 7,
        /// <summary>Lingo pack 3 (de) - overrides.</summary>
        lingo3 = 3 | 1 << 7,
    }

    /// <summary>
    /// Contract for ResCode-style value access: retrieve a CodeValId value
    /// directly or via the Got pattern, and test for emptiness. Implemented
    /// by ResCode and ResCode.Override.
    /// </summary>
    public interface IResCode
    {
        /// <summary>Returns the value for the given id (empty Chars when absent).</summary>
        Chars Val(CodeValId theValId);
        /// <summary>Gets the value for the given id; false (with empty Chars) when absent.</summary>
        bool GotVal(CodeValId theValId, out Chars returnChars);
        /// <summary>True when this instance represents no code.</summary>
        bool IsEmpty { get; }
    }

    /// <summary>
    /// Lightweight stub interface binding an enum member (via EnumCodes) to
    /// its runtime resources: captions, menu/button text, tips, ctrl keys,
    /// and named icon/echo/blob streams.
    /// </summary>
    /// <remarks>
    /// <para>
    /// ResCode.Register(delegates) binds resource managers into ResCode
    /// processing, allowing all modules to safely call ResCode members even
    /// when no resource managers are in use: value lookups fall back to the
    /// member's EnumInfoAttribute text, and captions ultimately fall back to
    /// the enum member's NameAsCaption, so a bare enum always yields usable
    /// UI text.
    /// </para>
    /// <para>
    /// Especially useful during construction of forms, windows, and menus:
    /// establishing click points, state-changing associated display items,
    /// structures for interaction, and retrieving associated tips, ctrl
    /// keys, icons, echos, and more.
    /// </para>
    /// <para>
    /// Using EnumInfoAttribute on enum members embeds all required runtime
    /// info without resource managers; managers need only be registered for
    /// alternate lingo overrides or stream (icon/echo/blob) resolution.
    /// Registered value/stream providers are invoked first-found-wins and
    /// are exception-isolated (a throwing provider is treated as
    /// not-found).
    /// </para>
    /// </remarks>
    public struct ResCode : IResCode, IComparable<ResCode>
    {
        private const byte _IsLingo = 1 << 7; // MUST MATCH ResPackId 1 << 7

        /// <summary>True when the ResKind is a lingo pack (bit 7 set).</summary>
        public static bool IsLingo(ResKind theId) => 0 != ((int)theId & _IsLingo);

        /// <summary>EnumCodes singleton for CodeValId.</summary>
        public static readonly EnumCodes ValIds = EnumCodes.ForType_or_null(typeof(CodeValId), out _);
        /// <summary>EnumCodes singleton for CodeDefId.</summary>
        public static readonly EnumCodes KindIds = EnumCodes.ForType_or_null(typeof(CodeDefId), out _);
        /// <summary>EnumCodes singleton for ResKind.</summary>
        public static readonly EnumCodes ResKinds = EnumCodes.ForType_or_null(typeof(ResKind), out _);

        /// <summary>Provider delegate resolving a CodeValId value for a ResCode; false when not provided.</summary>
        public delegate bool GotValueDO(ResCode theResCode, CodeValId theValId, out Chars returnValue);
        /// <summary>Provider delegate resolving a named stream of a ResKind; false when not provided.</summary>
        public delegate bool GotStreamDO(ResKind thePackId, string theStreamName, out BytesReader returnReader);

        private static readonly object _RegisterGate = new object();
        private static volatile ItemStack<GotValueDO> _GotValueDO_stack = new ItemStack<GotValueDO>(3);
        private static volatile ItemStack<GotStreamDO> _GotStreamDO_stack = new ItemStack<GotStreamDO>(3);

        /// <summary>
        /// Gets a reader over the named stream of the given kind by querying
        /// registered stream providers first-found-wins; false (null reader)
        /// when the name is null, no provider resolves it, or providers
        /// throw (provider exceptions are absorbed as not-found).
        /// </summary>
        public static bool GotReader(ResKind theKind, string theStreamName, out BytesReader returnReader)
        {
            returnReader = null;

            if (theStreamName == null)
                return false;

            ItemStack<GotStreamDO> xStack = _GotStreamDO_stack;
            int cursor = 0;

            while (xStack.GotNext(ref cursor, out GotStreamDO xDO, out _))
            {
                if (xDO != null)
                {
                    bool bDid = false;
                    returnReader = null;

                    try
                    {
                        bDid = xDO.Invoke(theKind, theStreamName, out returnReader);
                    }
                    catch
                    {
                        bDid = false;
                    }

                    if (bDid && returnReader != null)
                        return true;
                }
            }

            returnReader = null;
            return false;
        }

        /// <summary>Returns the ResCode for the enum member (empty on any resolution failure).</summary>
        public static ResCode For(Enum theEnum) => For(theEnum, out _);

        /// <summary>
        /// Returns the ResCode for the enum member, surfacing any EnumCodes
        /// resolution Issue; an absent/unresolvable code (including
        /// composed-flags values) yields an empty ResCode, never a throw.
        /// </summary>
        public static ResCode For(Enum theEnum, out Issue returnIssue)
        {
            EnumCodes xCodes = EnumCodes.ForCode_or_null(theEnum, out int iCodeIndex, out returnIssue);

            // Treat IndexOf miss (iCodeIndex < 0, e.g. a composed-flags value or
            // out-of-range cast) as empty. ForCode returns -1 with NO issue for such values;
            // (byte)-1 became CodeIndex 255 with a valid CacheIndex, and Val/GotVal later
            // threw Invalid_index from deep inside. Soft rule: absent code -> empty ResCode.
            if (returnIssue != null || iCodeIndex < 0)
                return new ResCode() { _CacheIndex = ushort.MaxValue }; // is empty

            xCodes.GotCodeInfo(iCodeIndex, out EnumInfoAttribute xInfo);

            return new ResCode()
            {
                _CodeInfo = xInfo,
                _CacheIndex = xCodes.CacheIndex,
                _CodeIndex = (byte)iCodeIndex,
            };
        }

        /// <summary>
        /// Registers a stream provider (once; duplicate registrations are
        /// ignored); false only when the delegate is null. Thread safe via
        /// copy-on-write.
        /// </summary>
        public static bool Register(GotStreamDO theDO)
        {
            if (theDO != null)
            {
                lock (_RegisterGate)
                {
                    if (_GotStreamDO_stack.IndexOf(theDO) < 0)
                    {
                        var xNew = new ItemStack<GotStreamDO>(_GotStreamDO_stack.Count + 1);
                        int cursor = 0;
                        while (_GotStreamDO_stack.GotNext(ref cursor, out GotStreamDO xDO, out _))
                            xNew.Push(xDO);
                        xNew.Push(theDO);
                        _GotStreamDO_stack = xNew;
                    }
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Registers a value provider (once; duplicate registrations are
        /// ignored); false only when the delegate is null. Thread safe via
        /// copy-on-write.
        /// </summary>
        public static bool Register(GotValueDO theDO)
        {
            if (theDO != null)
            {
                lock (_RegisterGate)
                {
                    if (_GotValueDO_stack.IndexOf(theDO) < 0)
                    {
                        var xNew = new ItemStack<GotValueDO>(_GotValueDO_stack.Count + 1);
                        int cursor = 0;
                        while (_GotValueDO_stack.GotNext(ref cursor, out GotValueDO xDO, out _))
                            xNew.Push(xDO);
                        xNew.Push(theDO);
                        _GotValueDO_stack = xNew;
                    }
                }
                return true;
            }
            return false;
        }

        /// <summary>The built-in (default) lingo number.</summary>
        public const byte DefaultLingoNum = 0;

        private static volatile byte _ActiveLingoNum = 0;

        /// <summary>The currently active lingo number.</summary>
        public static byte ActiveLingoNum => _ActiveLingoNum;

        /// <summary>True when the active lingo is the built-in default.</summary>
        public static bool ActiveIsDefault => _ActiveLingoNum == DefaultLingoNum;

        /// <summary>
        /// Returns the caption (or menu caption) for the enum member; a
        /// member with no ResCode resolution falls back to
        /// EnumCodes.EnumAsCaption.
        /// </summary>
        public static string Caption(Enum theEnum, bool asMenuItem = false)
        {
            ResCode vResCode = For(theEnum);
            if (vResCode.IsEmpty)
                return EnumCodes.EnumAsCaption(theEnum);
            return vResCode.Val(asMenuItem ? CodeValId.mnu : CodeValId.cap).ToString();
        }

        /// <summary>Returns the caption for the enum member, or an empty value when the code is absent (no name fallback).</summary>
        public static string Caption_or_empty(Enum theEnum)
        {
            return For(theEnum).Val(CodeValId.cap).ToString();
        }

        // ================= INSTANCE =================

        private EnumInfoAttribute _CodeInfo;
        private ushort _CacheIndex;
        private byte _CodeIndex;

        /// <summary>True when the enum member carries an EnumInfoAttribute.</summary>
        public bool HasInfo => _CodeInfo != null;
        /// <summary>The enum member's EnumInfoAttribute (null when none).</summary>
        public EnumInfoAttribute CodeInfo => _CodeInfo;
        /// <summary>The EnumCodes cache index of the member's enum type.</summary>
        public ushort CacheIndex => _CacheIndex;
        /// <summary>The member's index within its EnumCodes.</summary>
        public byte CodeIndex => _CodeIndex;
        /// <summary>True when this ResCode represents no code.</summary>
        public bool IsEmpty => _CacheIndex == ushort.MaxValue;
        private int _CompareInt => IsEmpty ? -1 : (_CacheIndex << 8) | _CodeIndex;

        /// <summary>The EnumCodes singleton for the member's enum type (null when empty).</summary>
        public EnumCodes Codes => IsEmpty ? null : EnumCodes.CacheItem(_CacheIndex);

        /// <summary>Returns the value for the given id (empty Chars when absent).</summary>
        public Chars Val(CodeValId theId) { GotVal(theId, out Chars v2); return v2; }

        /// <summary>
        /// Gets the value for the given id: registered value providers are
        /// tried first (first non-empty wins; provider exceptions absorbed),
        /// then the member's EnumInfoAttribute VBAR text. cap falls back to
        /// the member's NameAsCaption (so cap always resolves on a non-empty
        /// ResCode); mnu/but ensure an accelerator '&amp;' - a single-letter
        /// value marks that letter of cap, a longer value gets a leading
        /// '&amp;', no value defaults to "&amp;cap". False only when empty or the
        /// id has no value and no defaulting applies.
        /// </summary>
        public bool GotVal(CodeValId theId, out Chars returnValue)
        {
            if (IsEmpty)
            {
                returnValue = Chars.Nothing;
                return false;
            }
            bool bDid = false;
            Chars vValue = Chars.Nothing;

            ItemStack<GotValueDO> xStack = _GotValueDO_stack;
            int cursor = 0; // loop Registered for first found
            while (xStack.GotNext(ref cursor, out GotValueDO xDO, out _))
            {
                if (xDO != null)
                {
                    try
                    {
                        if (xDO.Invoke(this, theId, out vValue))
                        {
                            vValue.Trim(); // insurance
                            if (vValue.NotEmpty)
                            {
                                bDid = true;
                                break;
                            }
                        }
                    }
                    catch{}
                }
            }

            if (!bDid)
            {
                vValue = CodeInfoValue(theId);
                if (vValue.NotEmpty)
                    bDid = true;
            }

            bool bEnsure_AMP = false;

            switch (theId)
            {
                case CodeValId.cap:
                    if (!bDid)
                    {
                        vValue = new Chars(Codes.NameAsCaption(CodeIndex));
                        bDid = true;
                    }
                    break;
                case CodeValId.mnu:
                    bEnsure_AMP = true;
                    break;
                case CodeValId.but:
                    bEnsure_AMP = true;
                    break;
            }

            if (bEnsure_AMP)
            {
                if (!vValue.Contains(Chars.AMP))
                {
                    string sVal = vValue.ToString();
                    GotVal(CodeValId.cap, out Chars vCap); // always gets
                    string sCap = vCap.ToString();

                    if (sVal.Length == 1 && char.IsLetter(sVal[0]))
                    {
                        // single-letter hint: AMP before that letter of cap
                        int i = vCap.IndexOf(sVal[0], true);
                        if (i > 0)
                            vValue = new Chars(sCap.Substring(0, i) + Chars.AMP + sCap.Substring(i));
                        else
                            vValue = new Chars(Chars.AMP + sCap);
                    }
                    else if (sVal.Length > 1)
                    {
                        vValue = new Chars(Chars.AMP + sVal); // given caption, ensure AMP
                    }
                    else
                    {
                        vValue = new Chars(Chars.AMP + sCap); // default "&cap"
                    }
                    bDid = true;
                }
            }

            returnValue = vValue;
            return bDid;
        }

        /// <summary>
        /// Returns the raw value for the given id from the member's
        /// EnumInfoAttribute VBAR text only (no providers, no defaulting);
        /// empty Chars when the ResCode is empty, has no info, or the id has
        /// no entry.
        /// </summary>
        public Chars CodeInfoValue(CodeValId theId)
        {
            if (IsEmpty || !HasInfo) return Chars.Nothing;
            int i = (int)theId;
            Chars v1 = new Chars(_CodeInfo.Text);
            v1.FoundDelimitedNameAndText(ValIds.NameAsToken(i), out Chars v2);
            return v2;
        }

        /// <summary>Orders by (CacheIndex, CodeIndex); empty sorts first.</summary>
        public int CompareTo(ResCode other) => _CompareInt.CompareTo(other._CompareInt);

        /// <summary>True when the other object is a ResCode for the same enum member.</summary>
        public override bool Equals(object obj) => obj is ResCode v1 ? 0 == CompareTo(v1) : false;

        /// <summary>Hash of the (CacheIndex, CodeIndex) identity.</summary>
        public override int GetHashCode() => _CompareInt.GetHashCode();

        /// <summary>The enum member's name (null when empty).</summary>
        public override string ToString() => IsEmpty ? null : EnumCodes.CacheItem(_CacheIndex).Name(_CodeIndex);

        /// <summary>
        /// A ResCode wrapper carrying per-instance VBAR-delimited value
        /// overrides: GotVal consults the override text first, then falls
        /// through to the wrapped ResCode.
        /// </summary>
        public struct Override : IResCode
        {
            private ResCode _ResCodes;

            private Chars _TextVals;

            /// <summary>Creates an Override over the ResCode with VBAR-delimited name/value pairs (ignored when the ResCode is empty).</summary>
            public static Override For(ResCode theResCode, Chars theVBarDelimitedNameValuePairs)
            {
                return new Override()
                {
                    _ResCodes = theResCode,
                    _TextVals = theResCode.IsEmpty ? default : theVBarDelimitedNameValuePairs
                };
            }

            /// <summary>True when the wrapped ResCode is empty.</summary>
            public bool IsEmpty => _ResCodes.IsEmpty;

            /// <summary>Returns the value for the given id (empty Chars when absent).</summary>
            public Chars Val(CodeValId theId) { GotVal(theId, out Chars v2); return v2; }

            /// <summary>Gets the value for the given id from the override text first, else from the wrapped ResCode.</summary>
            public bool GotVal(CodeValId theId, out Chars returnValue)
            {
                if (IsEmpty)
                {
                    returnValue = Chars.Nothing;
                    return false;
                }

                string sName = ValIds.NameAsToken(theId);

                bool bGot = _TextVals.FoundDelimitedNameAndText(sName, out returnValue);

                if (bGot)
                    return true;

                return _ResCodes.GotVal(theId, out returnValue);
            }
        }
    }
}
