// Copyright (c) GDFrank
// SPDX-License-Identifier: MIT

#if NETCOREAPP && !NO_CORE
#define USE_CORE
#endif

using System;
using System.IO;

namespace Source77NW
{
    #region // ====== CharsCat ENUM ======

    /// <summary>
    /// Character-category flags. Each ASCII char (0..127) owns a unique lower-bit
    /// flag; upper bits carry a conflated UnicodeCategory for all other chars.
    /// Named sums (Ascii_Visible, Any_Letter, ...) combine flags for mask testing.
    /// Zero means unset/none.
    /// </summary>
    [Flags]
    public enum CharsCat : ulong  // UNIQUE Ascii FLAGS (0 implies unset/none/novalue)
    {
        // CONTROL
        NUL = 1 << 0,
        ESC = 1 << 1,
        DEL = 1 << 2,
        BS = 1 << 3,
        FF = 1 << 4,
        VT = 1 << 5,
        TAB = 1 << 6,
        CR = 1 << 7,
        LF = 1 << 8,
        CAN = 1 << 9,
        OTHER = 1 << 10,
        // VISIBLE
        SP = 1 << 11,
        DOT = 1 << 12,
        UNDER = 1 << 13,
        SLASH = 1 << 14,
        BSLASH = 1 << 15,
        COLON = 1 << 16,
        QUOTE = 1 << 17,
        LT = 1 << 18,
        GT = 1 << 19,
        PAR1 = 1 << 20,
        PAR2 = 1 << 21,
        BOX1 = 1 << 22,
        BOX2 = 1 << 23,
        CURLY1 = 1 << 24,
        CURLY2 = 1 << 25,
        EQ = 1 << 26,
        STAR = 1 << 27,
        QUEST = 1 << 28,
        COMMA = 1 << 29,
        SEMI = 1 << 30,
        DOL = 1UL << 31,
        PCT = 1UL << 32,
        AT = 1UL << 33,
        AMP = 1UL << 34,
        PLUS = 1UL << 35,
        DASH = 1UL << 36,
        HASH = 1UL << 37,
        BANG = 1UL << 38,
        APOS = 1UL << 39,
        TILDE = 1UL << 40,
        GRAVE = 1UL << 41,
        CARET = 1UL << 42,
        VBAR = 1UL << 43,

        DIGIT = 1UL << 44,
        LOWER = 1UL << 45,
        UPPER = 1UL << 46,

        // "conflated" Unicode, see _cat_UnicodeCategory[]

        Unicode_Upper = 1UL << 55,
        Unicode_Lower = 1UL << 56,
        Unicode_Other = 1UL << 57,
        Unicode_Mark = 1UL << 58,
        Unicode_Number = 1UL << 59,
        Unicode_Sep = 1UL << 60,
        Unicode_Punct = 1UL << 61,
        Unicode_Symbol = 1UL << 62,
        Unicode_ControlEtcetera = 1UL << 63, // or Nothing

        // CatSums Ascii chars 0 to 127 

        Ascii_Letter = UPPER | LOWER,

        Ascii_CR_LF = CR | LF,

        Ascii_CR_LF_TAB = Ascii_CR_LF | TAB,

        Ascii_Letter_Digit = Ascii_Letter | DIGIT,

        Ascii_Punct = DOT | UNDER | SLASH | BSLASH | COLON | QUOTE | LT | GT
            | PAR1 | BOX1 | CURLY1 | PAR2 | BOX2 | CURLY2
            | EQ | STAR | QUEST | COMMA | SEMI | DOL | PCT | AT | AMP
            | PLUS | DASH | HASH | BANG | APOS | TILDE | GRAVE | CARET | VBAR
            ,

        Ascii_Control = NUL | ESC | DEL | BS | FF | VT | TAB | CR | LF | CAN | OTHER,

        Ascii_Invisible = Ascii_Control | SP,

        Ascii_Visible = Ascii_Letter_Digit | Ascii_Punct,

        Ascii_Quoter = QUOTE | APOS,
        Ascii_PAR1_BOX1 = PAR1 | BOX1,
        Ascii_PAR2_BOX2 = PAR2 | BOX2,
        Ascii_CURLY1_LT = CURLY1 | LT,
        Ascii_CURLY2_GT = CURLY2 | GT,

        Ascii_Letter_Digit_or_UNDER = Ascii_Letter_Digit | UNDER,

        Ascii_Letter_Digit_or_DOT = Ascii_Letter_Digit | DOT,

        Ascii_Letter_Digit_UNDER_or_DOT = Ascii_Letter_Digit_or_UNDER | DOT,

        Ascii_Opener = Ascii_PAR1_BOX1 | Ascii_CURLY1_LT,

        Ascii_Closer = Ascii_PAR2_BOX2 | Ascii_CURLY2_GT,

        Ascii_Visible_SP = Ascii_Visible | SP,

        Ascii_Visible_SP_TAB = Ascii_Visible_SP | TAB,

        // CatSums Unicode for chars > 127

        Unicode_Letter = Unicode_Upper | Unicode_Lower,

        Unicode_Letter_Other = Unicode_Letter | Unicode_Other,

        Unicode_Letter_Other_Mark = Unicode_Letter_Other | Unicode_Mark,

        Unicode_Invisible = Unicode_Sep | Unicode_ControlEtcetera,

        Unicode_Visible = Unicode_Upper | Unicode_Lower | Unicode_Other | Unicode_Mark
            | Unicode_Number | Unicode_Punct | Unicode_Symbol,


        // CatSums Ascii / Unicode

        Any_Ascii = Ascii_Invisible | Ascii_Visible,

        Any_Unicode = Unicode_Invisible | Unicode_Visible,

        Any_Letter = Ascii_Letter | Unicode_Letter,
        
        Any_Letter_Other = Any_Letter | Unicode_Letter_Other,
        
        Any_Letter_Other_Mark = Any_Letter_Other | Unicode_Mark,

        Any_Invisible = Ascii_Invisible | Unicode_Invisible,

        Any_Visible = Ascii_Visible | Unicode_Visible,

        Any_Punct = Ascii_Punct | Unicode_Punct,

        Any_Control = Ascii_Control | Unicode_ControlEtcetera,
    }

    #endregion

    /// <summary>
    /// A lightweight view (bot..top span) over a base string, for scanning,
    /// factoring, and validating text with minimal string allocation: file paths,
    /// URLs, command lines, CSV, HTML, settings. Acts like a string for compares,
    /// Contains, Length, IndexOf, indexing; usable as a dictionary key.
    /// </summary>
    /// <remarks>
    /// Vocabulary: "index" addresses TextBase[index]; "offset" addresses
    /// TextBase[BotIndex + offset]; -1 means none. Pluck*/Pop* members consume
    /// from the bot/top of the view; Found*/Got*/Plucked* follow the soft
    /// Try-pattern (bool + out). The default value behaves as an empty string;
    /// null strings are treated as string.Empty. Invariants: 0 &lt;= BotIndex
    /// &lt;= TopIndex &lt;= TextBase.Length. WARNING: GetHashCode does not match
    /// string.GetHashCode; Chars keys and string keys hash differently.
    /// The struct is one string reference plus two ints (12 or 16 bytes),
    /// cheap to copy and valid as a dictionary key among other Chars.
    /// </remarks>
    public struct Chars : IComparable<Chars>, IComparable<string>
    {
        #region // ====== ASCII CHAR CONSTANTS (NUL QUOTE etc) ======

        public const char DIGIT0 = (char)48;
        public const char DIGIT9 = (char)57;
        public const char UPPERA = (char)65;
        public const char UPPERZ = (char)90;
        public const char LOWERA = (char)97;
        public const char LOWERZ = (char)122;

        public const char NUL = '\0';
        public const char BS = '\b';
        public const char TAB = '\t';
        public const char LF = '\n';
        public const char VT = '\x0B';
        public const char CR = '\r';
        public const char FF = '\f';
        public const char CAN = (char)24;
        public const char ESC = (char)27;
        public const char SP = (char)32;
        public const char BANG = (char)33;
        public const char QUOTE = (char)34;
        public const char HASH = (char)35;
        public const char DOL = (char)36;
        public const char PCT = (char)37;
        public const char AMP = (char)38;
        public const char APOS = (char)39;
        public const char PAR1 = (char)40;
        public const char PAR2 = (char)41;
        public const char STAR = (char)42;
        public const char PLUS = (char)43;
        public const char COMMA = (char)44;
        public const char DASH = (char)45;
        public const char DOT = (char)46;
        public const char AT = (char)64;
        public const char SLASH = (char)47;
        public const char COLON = (char)58;
        public const char SEMI = (char)59;
        public const char LT = (char)60;
        public const char EQ = (char)61;
        public const char GT = (char)62;
        public const char QUEST = (char)63;
        public const char BOX1 = (char)91;
        public const char BSLASH = (char)92;
        public const char BOX2 = (char)93;
        public const char CARET = (char)94;
        public const char UNDER = (char)95;
        public const char GRAVE = (char)96;
        public const char CURLY1 = (char)123;
        public const char VBAR = (char)124;
        public const char CURLY2 = (char)125;
        public const char TILDE = (char)126;
        public const char DEL = (char)127;

        public const char DSEP_ntfs = BSLASH;
        public const char DSEP_unix = SLASH;
        public const char DSEP_url = SLASH;

        #endregion

        
        #region // ====== STATIC Cat INITIALIZATION ======

        private const int _cat_7bit_Count = 128; // ONLY ascii

        private static readonly CharsCat[] _cat_7bit = new CharsCat[_cat_7bit_Count];

        static Chars()
        {
            void _set_7bit(char theChar, CharsCat theCat) => _cat_7bit[theChar] = theCat;

            // control
            _set_7bit(NUL, CharsCat.NUL);
            _set_7bit(ESC, CharsCat.ESC);
            _set_7bit(DEL, CharsCat.DEL);
            _set_7bit(BS, CharsCat.BS);
            _set_7bit(FF, CharsCat.FF);
            _set_7bit(VT, CharsCat.VT);
            _set_7bit(TAB, CharsCat.TAB);
            _set_7bit(CR, CharsCat.CR);
            _set_7bit(LF, CharsCat.LF);
            _set_7bit(CAN, CharsCat.CAN);

            _set_7bit(SP, CharsCat.SP);
            _set_7bit(DOT, CharsCat.DOT);
            _set_7bit(UNDER, CharsCat.UNDER);
            _set_7bit(SLASH, CharsCat.SLASH);
            _set_7bit(BSLASH, CharsCat.BSLASH);
            _set_7bit(COLON, CharsCat.COLON);
            _set_7bit(QUOTE, CharsCat.QUOTE);
            _set_7bit(LT, CharsCat.LT);
            _set_7bit(GT, CharsCat.GT);

            _set_7bit(PAR1, CharsCat.PAR1);
            _set_7bit(BOX1, CharsCat.BOX1);
            _set_7bit(CURLY1, CharsCat.CURLY1);

            _set_7bit(PAR2, CharsCat.PAR2);
            _set_7bit(BOX2, CharsCat.BOX2);
            _set_7bit(CURLY2, CharsCat.CURLY2);

            _set_7bit(STAR, CharsCat.STAR);
            _set_7bit(QUEST, CharsCat.QUEST);
            _set_7bit(EQ, CharsCat.EQ);

            _set_7bit(COMMA, CharsCat.COMMA);
            _set_7bit(SEMI, CharsCat.SEMI);

            _set_7bit(DOL, CharsCat.DOL);
            _set_7bit(PCT, CharsCat.PCT);
            _set_7bit(AT, CharsCat.AT);
            _set_7bit(AMP, CharsCat.AMP);

            _set_7bit(PLUS, CharsCat.PLUS);
            _set_7bit(DASH, CharsCat.DASH);
            _set_7bit(HASH, CharsCat.HASH);
            _set_7bit(BANG, CharsCat.BANG);

            _set_7bit(APOS, CharsCat.APOS);
            _set_7bit(TILDE, CharsCat.TILDE);
            _set_7bit(GRAVE, CharsCat.GRAVE);
            _set_7bit(CARET, CharsCat.CARET);
            _set_7bit(VBAR, CharsCat.VBAR);

            int i = -1;

            while (++i < _cat_7bit_Count)
            {
                // fill in the uncategorized

                if (_cat_7bit[i] == 0)
                {
                    switch (_cat_UnicodeCategory[(int)char.GetUnicodeCategory((char)i)])
                    {
                        case CharsCat.Unicode_Lower:
                            _cat_7bit[i] = CharsCat.LOWER;
                            break;
                        case CharsCat.Unicode_Upper:
                            _cat_7bit[i] = CharsCat.UPPER;
                            break;
                        case CharsCat.Unicode_Number:
                            _cat_7bit[i] = CharsCat.DIGIT;
                            break;
                        case CharsCat.Unicode_ControlEtcetera:
                            _cat_7bit[i] = CharsCat.OTHER;
                            break;
                    }
                }
            }
        }

        // https://docs.microsoft.com/en-us/dotnet/api/system.globalization.unicodecategory?view=NETCOREAPP-4.8

        private static readonly CharsCat[] _cat_UnicodeCategory = new CharsCat[]  // for unicode range
        {
CharsCat.Unicode_Upper,       // 00_Lu_UppercaseLetter
CharsCat.Unicode_Lower,       // 01_Ll_LowercaseLetter
CharsCat.Unicode_Other,       // 02_Lt_TitlecaseLetter
CharsCat.Unicode_Other,       // 03_Lm_ModifierLetter
CharsCat.Unicode_Other,       // 04_Lo_OtherLetter
CharsCat.Unicode_Mark,        // 05_Mn_NonSpacingMark
CharsCat.Unicode_Mark,        // 06_Mc_SpacingCombiningMark
CharsCat.Unicode_Mark,        // 07_Me_EnclosingMark
CharsCat.Unicode_Number,      // 08_Nd_DecimalDigitNumber
CharsCat.Unicode_Number,      // 09_Nl_LetterNumber
CharsCat.Unicode_Number,      // 10_No_OtherNumber
CharsCat.Unicode_Sep,         // 11_Zs_SpaceSeparator
CharsCat.Unicode_Sep,         // 12_Zl_LineSeparator
CharsCat.Unicode_Sep,         // 13_Zp_ParagraphSeparator
CharsCat.Unicode_ControlEtcetera,  // 14_Cc_Control
CharsCat.Unicode_ControlEtcetera,  // 15_Cf_Format
CharsCat.Unicode_ControlEtcetera,  // 16_Cs_Surrogate
CharsCat.Unicode_ControlEtcetera,  // 17_Co_PrivateUse
CharsCat.Unicode_Punct,       // 18_Pc_ConnectorPunctuation
CharsCat.Unicode_Punct,       // 19_Pd_DashPunctuation
CharsCat.Unicode_Punct,       // 20_Ps_OpenPunctuation
CharsCat.Unicode_Punct,       // 21_Pe_ClosePunctuation
CharsCat.Unicode_Punct,       // 22_Pi_InitialQuotePunctuation
CharsCat.Unicode_Punct,       // 23_Pf_FinalQuotePunctuation
CharsCat.Unicode_Punct,       // 24_Po_OtherPunctuation
CharsCat.Unicode_Symbol,      // 25_Sm_MathSymbol
CharsCat.Unicode_Symbol,      // 26_Sc_CurrencySymbol
CharsCat.Unicode_Symbol,      // 27_Sk_ModifierSymbol
CharsCat.Unicode_Symbol,      // 28_So_OtherSymbol
CharsCat.Unicode_ControlEtcetera,  // 29_Cn_OtherNotAssigned
        };

        #endregion

       
        // ====== STATIC METHODS / PROPERTIES / CONST ======

        private const ushort issueSource = 65010;

        private const StringComparison _CompareIgnoreCase = StringComparison.OrdinalIgnoreCase;

        private const StringComparison _CompareCaseSensitive = StringComparison.Ordinal;

        private static readonly char[] _openers = new char[] { LT, PAR1, CURLY1, BOX1 };

        private static readonly char[] _closers = new char[] { GT, PAR2, CURLY2, BOX2 };

        /// <summary>The empty Chars (no base text, zero length).</summary>
        public static Chars Nothing => default;

        /// <summary>Returns the complement of a category mask (all categories NOT in theCat).</summary>
        public static CharsCat Not(CharsCat theCat) => ~theCat;

        /// <summary>Returns a copy of theString with every char matching the category mask replaced by theWithChar.</summary>
        public static string ReplaceWith(string theString, char theWithChar, CharsCat forCharsMatching)
        {
            return new Chars(theString).ReplaceWith(theWithChar, forCharsMatching).ToString();
        }

        /// <summary>Returns a copy of theString with every char in forCharsMatching replaced by theWithChar.</summary>
        public static string ReplaceWith(string theString, char theWithChar, params char[] forCharsMatching)
        {
            return new Chars(theString).ReplaceWith(theWithChar, forCharsMatching).ToString();
        }

        /// <summary>True if theString contains at least one char matching the category mask.</summary>
        public static bool Contains(string theString, CharsCat theCat) => new Chars(theString).Contains(theCat);

        /// <summary>True if the two category masks share any flag.</summary>
        public static bool CatTrue(CharsCat theCatSum, CharsCat theAnyCat) => 0 != (theCatSum & theAnyCat);

        /// <summary>True if the two category masks share no flag.</summary>
        public static bool CatFalse(CharsCat theCatSum, CharsCat theAnyCat) => 0 == (theCatSum & theAnyCat);

        /// <summary>Returns the CharsCat category of a char: unique flag for ASCII, conflated Unicode flag otherwise.</summary>
        public static CharsCat Cat(char theChar) => theChar < _cat_7bit_Count ? _cat_7bit[theChar] : _cat_UnicodeCategory[(int)char.GetUnicodeCategory(theChar)];

        /// <summary>Returns the OR of the categories of every char in theText (0 for null/empty).</summary>
        public static CharsCat CatSum(string theText)
        {
            CharsCat iCatSum = 0;
            if (theText != null) 
            {
                int i = -1;
                while (++i < theText.Length)
                    iCatSum |= Cat(theText[i]);
            }
            return iCatSum;
        }

        /// <summary>True if theChar's category matches any flag in theAny.</summary>
        public static bool CharIsAny(char theChar, CharsCat theAny) => 0 != (Cat(theChar) & theAny);

        /// <summary>True if theChar equals any char in theAnyChars (false for null/empty array).</summary>
        public static bool CharIsAny(char theChar, params char[] theAnyChars)
        {
            if (theAnyChars == null || theAnyChars.Length == 0) return false;
            int i = -1;
            while (++i < theAnyChars.Length)
                if (theChar == theAnyChars[i])
                    return true;
            return false;
        }

        /// <summary>True if theChar opens an enclosure pair ( [ { or &lt;; returns the matching closer, else NUL.</summary>
        public static bool IsEncloserBegin(char theChar, out char returnEndEncloser_or_NUL)
        {
            int i = -1;
            while (++i < _openers.Length)
            {
                if (theChar == _openers[i])
                {
                    returnEndEncloser_or_NUL = _closers[i];
                    return true;
                }
            }
            returnEndEncloser_or_NUL = NUL;
            return false;
        }

        /// <summary>True if theChar closes an enclosure pair ) ] } or &gt;; returns the matching opener, else NUL.</summary>
        public static bool IsEncloserEnd(char theChar, out char returnBeginEncloser_or_NUL)
        {
            int i = -1;
            while (++i < _closers.Length)
            {
                if (theChar == _closers[i])
                {
                    returnBeginEncloser_or_NUL = _openers[i];
                    return true;
                }
            }
            returnBeginEncloser_or_NUL = NUL;
            return false;
        }

        /// <summary>True if every char of theString is visible (letters, digits, punctuation - no controls or separators).</summary>
        public static bool IsOnlyVisible(string theString) => new Chars(theString).ContainsOnly(CharsCat.Any_Visible);

        /// <summary>True if every char of theString matches the category mask.</summary>
        public static bool IsOnly(string theString, CharsCat theCat) => new Chars(theString).ContainsOnly(theCat);

        /// <summary>True if at least one char of theString matches the category mask (false for null/empty).</summary>
        public static bool IsAny(string theString, CharsCat theAny)
        {
            if (string.IsNullOrEmpty(theString)) return false;
            int i = -1;
            while (++i < theString.Length)
            {
                if (0 != (Cat(theString[i]) & theAny))
                    return true;
            }
            return false;
        }

        /// <summary>Counts the lines in theText and reports the widest line's char count.</summary>
        public static int GetLineCount(string theText, out int returnMaxLineCharWidth) => new Chars(theText).LineCount(out returnMaxLineCharWidth);

        /// <summary>Returns the first line of theText containing any visible char, as a new string (empty if none).</summary>
        public static string GetFirstVisibleLine(string theText) => new Chars(theText).PluckFirstVisibleLine().ToString();


        // ====== INSTANCE METHODS / PROPERTIES ======

        private const string s_Null_or_empty_markers = @"Null or empty markers";
        private string __issue_this_info(string sMsg) { return "Chars " + sMsg + "\nBotIndex: " + _BotIndex.ToString() + "\nTopIndex: " + _TopIndex.ToString() + "\nLength: " + Length.ToString(); }
        private Issue _issue_BadIndex(byte iSpot, int index) { return Issue.Create(issueSource, iSpot, __issue_this_info("index: " + index + " out of range"), IssueKind.BadIndex); }
        private Issue _issue_BadParam(byte iSpot, string msg) { return Issue.Create(issueSource, iSpot, __issue_this_info(msg), IssueKind.BadParam); }

        private readonly string _TextBase; // 4 or 8 bytes

        private int _BotIndex; // 4 bytes

        private int _TopIndex; // 4 bytes

        /// <summary>Creates a view over the whole of theText (null treated as empty).</summary>
        public Chars(string theText)
        {
            _TextBase = theText ?? string.Empty;
            _BotIndex = 0;
            _TopIndex = _TextBase.Length;
        }

        /// <summary>Creates a view over theTextBase from theBotIndex (inclusive) to theTopIndex (exclusive). Throws on invalid indices.</summary>
        public Chars(int theBotIndex, int theTopIndex, string theTextBase)
        {
            _TextBase = theTextBase ?? string.Empty;
            _BotIndex = theBotIndex;
            _TopIndex = theTopIndex;

            if (theBotIndex < 0
            || theBotIndex > theTopIndex
            || theTopIndex > _TextBase.Length)
                throw _issue_BadIndex(11, theBotIndex);
        }

        /// <summary>Gets the char at the given offset within the view (0 to Length-1). Throws on out-of-range offset.</summary>
        public char this[int theOffset_to_BotIndex] // INDEXER
        {
            get
            {
                // the span offset = 0 to Length - 1
                int index = _BotIndex + theOffset_to_BotIndex;
                if (index >= _BotIndex && index < _TopIndex)
                    return _TextBase[index];
                // arrives here if null/Empty _TextBase
                throw _issue_BadIndex(12, theOffset_to_BotIndex);
            }
        }

        /// <summary>Converts a base-string index to a view offset, or -1 if outside the view.</summary>
        public int OffsetOfIndex(int theIndex) => theIndex >= BotIndex && theIndex < TopIndex ? theIndex - BotIndex : -1;

        /// <summary>Converts a view offset to a base-string index, or -1 if outside the view.</summary>
        public int IndexOfOffset(int theOffset) => theOffset >= 0 && theOffset < Length ? BotIndex + theOffset : -1;

        /// <summary>The base string being viewed (string.Empty for the default value).</summary>
        public string TextBase => _TextBase ?? string.Empty; // default/Nothing same as string.Empty

        /// <summary>Base-string index of the first char of the view.</summary>
        public int BotIndex => _BotIndex;

        /// <summary>Base-string index one past the last char of the view.</summary>
        public int TopIndex => _TopIndex;

        /// <summary>Base-string index of the last char of the view (TopIndex - 1).</summary>
        public int EndIndex => _TopIndex - 1;

        /// <summary>Char count of the view (TopIndex - BotIndex).</summary>
        public int Length => _TopIndex - _BotIndex;

        /// <summary>True when the view is empty (Length == 0).</summary>
        public bool IsEmpty => _BotIndex == _TopIndex;

        /// <summary>True when the view has at least one char.</summary>
        public bool NotEmpty => _BotIndex != _TopIndex;

        /// <summary>First char of the view, or NUL when empty.</summary>
        public char BotChar_or_NUL => IsEmpty ? NUL : _TextBase[_BotIndex];

        /// <summary>Last char of the view, or NUL when empty.</summary>
        public char EndChar_or_NUL => IsEmpty ? NUL : _TextBase[_TopIndex - 1];

        /// <summary>Char of the base string just past the view's top, or NUL at end of base.</summary>
        public char TopChar_or_NUL => _TopIndex >= TextBase.Length ? NUL : _TextBase[_TopIndex];

        /// <summary>Char at the given base-string index, or NUL if outside the view.</summary>
        public char Char_or_NUL(int theIndex) => theIndex >= _BotIndex && theIndex < TopIndex ? TextBase[theIndex] : NUL;

        /// <summary>Category of the first char, or 0 when empty.</summary>
        public CharsCat BotCat_or_zero => IsEmpty ? 0 : Cat(_TextBase[_BotIndex]);

        /// <summary>Category of the last char, or 0 when empty.</summary>
        public CharsCat EndCat_or_zero => IsEmpty ? 0 : Cat(_TextBase[_TopIndex - 1]);

#if USE_CORE
        /// <summary>Returns the view as a ReadOnlySpan over the base string (no allocation).</summary>
        public ReadOnlySpan<char> AsSpan()
        {
            return _TextBase.AsSpan(_BotIndex, Length);
        }
#endif

        /// <summary>Returns the viewed text as a string (the base string itself when the view covers all of it; string.Empty when empty).</summary>
        public override string ToString()
        {
            if (_BotIndex == _TopIndex)
                return String.Empty;
            if (_BotIndex == 0 && _TopIndex == _TextBase.Length)
                return _TextBase;
            return _TextBase.Substring(_BotIndex, Length);
        }

        /// <summary>Writes the viewed chars to theWriter without creating a string.</summary>
        public void Write(TextWriter theWriter)
        {
#if USE_CORE
            theWriter.Write(AsSpan());
#else
            int i = _BotIndex - 1;
            while (++i < _TopIndex)
                theWriter.Write(_TextBase[i]);
#endif
        }

        /// <summary>Returns the viewed text enclosed in theQuote chars.</summary>
        public string ToQuoted(char theQuote = QUOTE) => theQuote + ToString() + theQuote;

        /// <summary>True if the viewed text parses as a decimal; returns the value.</summary>
        public bool IsDecimalValue(out decimal returnDecimalValue)
        {
#if USE_CORE
            return decimal.TryParse(AsSpan(), out returnDecimalValue);
#else
            return decimal.TryParse(ToString(), out returnDecimalValue);
#endif
        }

        /// <summary>True if the view starts with theBotChar and ends with theEndChar (Length &gt; 1).</summary>
        public bool IsEnclosed(char theBotChar, char theEndChar) => Length > 1 && BotChar_or_NUL == theBotChar && EndChar_or_NUL == theEndChar;

        /// <summary>True if the view starts and ends with theChar (default double-quote).</summary>
        public bool IsQuoted(char theChar = QUOTE) => IsEnclosed(theChar, theChar);

        /// <summary>Ordinal equality with another Chars (optionally case-insensitive).</summary>
        public bool Equals(Chars that, bool ignoreCase = false)
        {
            if (Length != that.Length) return false;
#if USE_CORE
            if (Length >= 16)
                return ignoreCase
                    ? MemoryExtensions.Equals(AsSpan(), that.AsSpan(), StringComparison.OrdinalIgnoreCase)
                    : MemoryExtensions.Equals(AsSpan(), that.AsSpan(), StringComparison.Ordinal);
#endif
            return 0 == string.Compare(TextBase, _BotIndex, that.TextBase, that._BotIndex, Length, ignoreCase ? _CompareIgnoreCase : _CompareCaseSensitive);
        }

        /// <summary>Ordinal equality with a string (optionally case-insensitive; null equals empty).</summary>
        public bool Equals(string that, bool ignoreCase = false)
        {
            if (that == null) return IsEmpty;
            if (Length != that.Length) return false;
#if USE_CORE
            if (Length >= 16)
                return ignoreCase
                    ? MemoryExtensions.Equals(AsSpan(), that.AsSpan(), StringComparison.OrdinalIgnoreCase)
                    : MemoryExtensions.Equals(AsSpan(), that.AsSpan(), StringComparison.Ordinal);
#endif
            return 0 == string.Compare(TextBase, _BotIndex, that, 0, Length, ignoreCase ? _CompareIgnoreCase : _CompareCaseSensitive);
        }

        /// <summary>Equality against Chars or string only; any other type returns false.</summary>
        public override bool Equals(object obj)
        {
            if (obj is string s1)
                return Equals(s1);
            if (obj is Chars v1)
                return Equals(v1);
            return false; // Equals is ONLY FOR CS or string ... nothing else
        }

        /// <summary>FNV-1a hash of the viewed chars. Does NOT match string.GetHashCode.</summary>
        public override int GetHashCode()
        {
            const uint fnvOffset = 2166136261u;
            const uint fnvPrime = 16777619u;
            uint hash = fnvOffset;
            if (_BotIndex < _TopIndex)
            {
                int i = _BotIndex - 1;
                while (++i < _TopIndex)
                {
                    hash ^= (uint)_TextBase[i];
                    hash *= fnvPrime;
                }
            }
            return (int)hash;
        }

        /// <summary>Ordinal comparison with another Chars (optionally case-insensitive).</summary>
        public int CompareTo(Chars that, bool ignoreCase) => CompareTo(that, ignoreCase ? _CompareIgnoreCase : _CompareCaseSensitive);

        /// <summary>Case-sensitive ordinal comparison with another Chars.</summary>
        public int CompareTo(Chars that) => CompareTo(that, _CompareCaseSensitive);

        /// <summary>Comparison with another Chars using the given StringComparison.</summary>
        public int CompareTo(Chars that, StringComparison compareHow)
        {
            int iLen_this = Length;
            int iLen_that = that.Length;
            int iLen_shorter = (iLen_that < iLen_this) ? iLen_that : iLen_this;

            int iCompare = string.Compare
                ( _TextBase ?? string.Empty
                , _BotIndex
                , that._TextBase ?? string.Empty
                , that._BotIndex
                , iLen_shorter
                , compareHow);

            if (iCompare == 0)
            {
                if (iLen_this > iLen_shorter) return 1;
                if (iLen_that > iLen_shorter) return -1;
                return 0;
            }

            return iCompare;
        }

        /// <summary>Ordinal comparison with a string (optionally case-insensitive; null treated as empty).</summary>
        public int CompareTo(string that, bool ignoreCase) => CompareTo(that, ignoreCase ? _CompareIgnoreCase : _CompareCaseSensitive);

        /// <summary>Case-sensitive ordinal comparison with a string (null treated as empty).</summary>
        public int CompareTo(string that) => CompareTo(that, _CompareCaseSensitive);

        /// <summary>Comparison with a string using the given StringComparison (null treated as empty).</summary>
        public int CompareTo(string that, StringComparison compareHow)
        {
            if (that == null) that = string.Empty;
            int iLen_this = Length;
            int iLen_that = that.Length;
            int iLen_shorter = (iLen_that < iLen_this) ? iLen_that : iLen_this;
            int iCompare = string.Compare
                (_TextBase ?? string.Empty
                , _BotIndex
                , that ?? string.Empty
                , 0
                , iLen_shorter
                , compareHow);
            if (iCompare == 0)
            {
                if (iLen_this > iLen_shorter) return 1;
                if (iLen_that > iLen_shorter) return -1;
                return 0;
            }
            return iCompare;
        }

        /// <summary>True if the view starts with thePrefix (optionally case-insensitive; false for null/empty prefix).</summary>
        public bool HasPrefix(string thePrefix, bool ignoreCase)
        {
            int iLen = thePrefix == null ? 0 : thePrefix.Length;
            if (iLen > Length || IsEmpty || iLen == 0) return false;
#if USE_CORE
            if (iLen >= 16)
                return ignoreCase
                    ? MemoryExtensions.StartsWith(AsSpan(), thePrefix.AsSpan(), StringComparison.OrdinalIgnoreCase)
                    : MemoryExtensions.StartsWith(AsSpan(), thePrefix.AsSpan(), StringComparison.Ordinal);
#endif
            return 0 == string.Compare(_TextBase, _BotIndex, thePrefix, 0, iLen, ignoreCase ? _CompareIgnoreCase : _CompareCaseSensitive);
        }

        /// <summary>True if the view contains theChar (optionally case-insensitive).</summary>
        public bool Contains(char theChar, bool ignoreCase = false) => 0 <= IndexOf(theChar, ignoreCase);

        /// <summary>True if the view contains any char of theCharArray.</summary>
        public bool ContainsAny(char[] theCharArray) => 0 <= IndexOfAny(theCharArray);

        /// <summary>True if the view contains theText (optionally case-insensitive).</summary>
        public bool Contains(string theText, bool ignoreCase = false) => 0 <= IndexOf(theText, ignoreCase);

        /// <summary>True if the view contains any char matching the category mask.</summary>
        public bool Contains(CharsCat theCat) => 0 <= IndexOf(theCat);

        /// <summary>True if every char of the view matches the category mask (true when empty).</summary>
        public bool ContainsOnly(CharsCat theCat)
        {
            CharsCat iNotCat = Not(theCat);
            int iCursor = _BotIndex - 1;
            while (++iCursor < _TopIndex)
            {
                if (CharIsAny(_TextBase[iCursor], iNotCat))
                    return false;
            }
            return true;
        }

        private void _TrimBot()
        {
            while (_BotIndex < _TopIndex && 0 == (CharsCat.Any_Visible & Cat(_TextBase[_BotIndex]))) _BotIndex++;
        }

        private void _TrimTop()
        {
            while (_BotIndex < _TopIndex && 0 == (CharsCat.Any_Visible & Cat(_TextBase[_TopIndex - 1]))) _TopIndex--;
        }

        /// <summary>Advances the bot past leading invisible chars; returns this view.</summary>
        public Chars TrimBot() { _TrimBot(); return this; }

        /// <summary>Retreats the top past trailing invisible chars; returns this view.</summary>
        public Chars TrimTop() { _TrimTop(); return this; }

        /// <summary>Trims invisible chars from both ends; returns this view.</summary>
        public Chars Trim() { _TrimBot(); _TrimTop(); return this; }

        /// <summary>Empties the view by moving the bot to the top; returns this view.</summary>
        public Chars TrimToTopIndex() => TrimToIndex(_TopIndex);

        /// <summary>Moves the bot to the given base-string index; returns this view. Throws on invalid index.</summary>
        public Chars TrimToIndex(int theBaseIndex)
        {
            if (theBaseIndex >= _BotIndex && theBaseIndex <= _TopIndex)
            {
                _BotIndex = theBaseIndex;
                return this;
            }
            throw _issue_BadIndex(31, theBaseIndex);
        }

        /// <summary>Returns the OR of the categories of every char in the view.</summary>
        public CharsCat CatSum()
        {
            CharsCat iCatSum = 0;
            int iBot = BotIndex;
            while (iBot < TopIndex) iCatSum |= Cat(_TextBase[iBot++]);
            return iCatSum;
        }

        /// <summary>Base-string index of the first occurrence of any given char, or -1.</summary>
        public int IndexOfAny(params char[] theChars)
        {
            if (IsEmpty || theChars == null || theChars.Length == 0) return -1;
            return _TextBase.IndexOfAny(theChars, _BotIndex, Length);
        }

        /// <summary>Base-string index of the last occurrence of any given char, or -1.</summary>
        public int LastIndexOfAny(params char[] theChars)
        {
            if (IsEmpty || theChars == null || theChars.Length == 0) return -1; 
            return _TextBase.LastIndexOfAny(theChars, EndIndex, Length);
        }

        /// <summary>Base-string index of the first occurrence of theChar (optionally case-insensitive), or -1.</summary>
        public int IndexOf(char theChar, bool ignoreCase = false)
        {
            if (IsEmpty) return -1;
            if (ignoreCase && char.IsLetter(theChar))
            {
                char c_lower = char.ToLowerInvariant(theChar);
                char c_upper = char.ToUpperInvariant(theChar);
                int i = _BotIndex - 1;
                while (++i < _TopIndex)
                {
                    char c = _TextBase[i];
                    if (c == c_lower || c == c_upper) return i;
                }
                return -1; 
            }
#if USE_CORE
            if (Length >= 32)
            {
                int iOffset = MemoryExtensions.IndexOf(AsSpan(), theChar);
                return iOffset < 0 ? -1 : _BotIndex + iOffset;
            }
#endif
            return _TextBase.IndexOf(theChar, _BotIndex, Length);
        }

        /// <summary>Base-string index of the last occurrence of theChar (optionally case-insensitive), or -1.</summary>
        public int LastIndexOf(char theChar, bool ignoreCase = false)
        {
            if (IsEmpty) return -1;
            if (ignoreCase && char.IsLetter(theChar))
            {
                char c_lower = char.ToLowerInvariant(theChar);
                char c_upper = char.ToUpperInvariant(theChar);
                int i = _TopIndex;
                while (--i >= _BotIndex)
                {
                    char c = _TextBase[i];
                    if (c == c_lower || c == c_upper) return i;
                }
                return -1;
            }
#if USE_CORE
            if (Length >= 32)
            {
                int iOffset = MemoryExtensions.LastIndexOf(AsSpan(), theChar);
                return iOffset < 0 ? -1 : _BotIndex + iOffset;
            }
#endif
            return _TextBase.LastIndexOf(theChar, EndIndex, Length);
        }

        /// <summary>Base-string index of the first char matching the category mask, or -1.</summary>
        public int IndexOf(CharsCat theCat)
        {
            int i = _BotIndex - 1;
            while (++i < _TopIndex)
                if (0 != (theCat & Cat(_TextBase[i])))
                    return i;
            return -1;
        }

        /// <summary>Base-string index of the last char matching the category mask, or -1.</summary>
        public int LastIndexOf(CharsCat theCat)
        {
            int i = _TopIndex;
            while (--i >= _BotIndex)
                if (0 != (theCat & Cat(_TextBase[i])))
                    return i;
            return -1;
        }

        /// <summary>Base-string index of the first occurrence of theString, or -1.</summary>
        public int IndexOf(string theString, bool ignoreCase)
        {
            if (string.IsNullOrEmpty(theString) || Length < theString.Length) return -1;
#if USE_CORE
            if (Length >= 32)
            {
                int iOffset = ignoreCase
                    ? MemoryExtensions.IndexOf(AsSpan(), theString.AsSpan(), StringComparison.OrdinalIgnoreCase)
                    : MemoryExtensions.IndexOf(AsSpan(), theString.AsSpan(), StringComparison.Ordinal);
                return iOffset < 0 ? -1 : _BotIndex + iOffset;
            }
#endif
            return _TextBase.IndexOf(theString, _BotIndex, Length, ignoreCase ? _CompareIgnoreCase : _CompareCaseSensitive);
        }

        /// <summary>Base-string index of the last occurrence of theString, or -1.</summary>
        public int LastIndexOf(string theString, bool ignoreCase)
        {
            if (string.IsNullOrEmpty(theString) || Length < theString.Length) return -1;
#if USE_CORE
            if (Length >= 32)
            {
                int iOffset = ignoreCase
                    ? MemoryExtensions.LastIndexOf(AsSpan(), theString.AsSpan(), StringComparison.OrdinalIgnoreCase)
                    : MemoryExtensions.LastIndexOf(AsSpan(), theString.AsSpan(), StringComparison.Ordinal);
                return iOffset < 0 ? -1 : _BotIndex + iOffset;
            }
#endif
            return _TextBase.LastIndexOf(theString, EndIndex, Length, ignoreCase ? _CompareIgnoreCase : _CompareCaseSensitive);
        }

        /// <summary>Returns the view of the whole line containing theIndex, optionally including its trailing CR/LF. Throws on invalid index.</summary>
        public Chars LineContainingIndex(int theIndex, bool include_trailing_LSep_if_any) // NO DEFAULT include_, FORCE CHOICE
        {
            if (theIndex >= _BotIndex && theIndex <= _TopIndex)
            {
                char c1 = theIndex < _TopIndex ? _TextBase[theIndex] : NUL;
                if (c1 == LF && theIndex > _BotIndex)
                {
                    if (CR == _TextBase[theIndex - 1])
                    {
                        theIndex--;
                        c1 = _TextBase[theIndex]; // NOW ALIGNED ON CR belonging to the LF
                    }
                }
                int iBot = theIndex;
                int iTop = theIndex;
                if (c1 == CR || c1 == LF)
                {
                    if (iBot > _BotIndex)
                    {
                        c1 = _TextBase[iBot - 1];
                        if (c1 != LF) // THE PREVIOUS LINE LF
                            iBot--;  // iBot now on line value char
                    }
                }
                // iBot is on a line value char OR the line value's CR/LF char OR _BotIndex;
                while (iBot > _BotIndex)
                {
                    // SEARCH DOWN TO LF (if any)
                    c1 = _TextBase[--iBot];
                    if (c1 == LF)
                    {
                        iBot++; // BACK OFF THE DISCOVERED LF
                        break;
                    }
                }
                // iTop is on line value char OR line value's CR/LF char OR _TopIndex
                while (iTop < _TopIndex)
                {
                    // SCAN UP TO NEXT CR/LF
                    c1 = _TextBase[iTop];
                    if (c1 == CR || c1 == LF)
                        break;
                    iTop++;
                }

                if (include_trailing_LSep_if_any)
                {
                    if (iTop < _TopIndex)
                        if (_TextBase[iTop] == CR)
                            iTop++;
                    if (iTop < _TopIndex)
                        if (_TextBase[iTop] == LF)
                            iTop++;
                }
                return new Chars(iBot, iTop, _TextBase);
            }
            throw _issue_BadIndex(91, theIndex);
        }

        /// <summary>Consumes and returns the first char of the view, or NUL when empty.</summary>
        public char PluckChar_or_NUL() => (_BotIndex < _TopIndex) ? _TextBase[_BotIndex++] : NUL;

        /// <summary>Consumes and returns the last char of the view, or NUL when empty.</summary>
        public char PopChar_or_NUL() => (_BotIndex < _TopIndex) ? _TextBase[--_TopIndex] : NUL;

        /// <summary>Consumes from the bot up to the given base-string index and returns it as a view. Throws on invalid index.</summary>
        public Chars PluckToIndex(int theBaseIndex_max_TopIndex)
        {
            // theBaseIndex can EQ _TopIndex as _BotIndex of next span or EOF
            if (theBaseIndex_max_TopIndex >= _BotIndex && theBaseIndex_max_TopIndex <= _TopIndex)
            {
                int iBot = _BotIndex;
                _BotIndex = theBaseIndex_max_TopIndex;
                return new Chars(iBot, theBaseIndex_max_TopIndex, _TextBase);
            }
            throw _issue_BadIndex(33, theBaseIndex_max_TopIndex);
        }

        /// <summary>Consumes from the given base-string index to the top and returns it as a view. Throws on invalid index.</summary>
        public Chars PopFromIndex(int theBaseIndex)
        {
            if (theBaseIndex >= _BotIndex && theBaseIndex < _TopIndex)
            {
                int iTop = _TopIndex;
                _TopIndex = theBaseIndex;
                return new Chars(theBaseIndex, iTop, _TextBase);
            }
            throw _issue_BadIndex(32, theBaseIndex);
        }

        /// <summary>Consumes up to theMaxLength chars from the bot and returns them as a view (Nothing if theMaxLength &lt;= 0).</summary>
        public Chars PluckLength(int theMaxLength)
        {
            if (theMaxLength > 0)
            {
                int iBot = _BotIndex;
                int iTop = _BotIndex + theMaxLength;
                if (iTop > _TopIndex)
                    iTop = _TopIndex;
                _BotIndex = iTop;
                return new Chars(iBot, iTop, _TextBase);
            }
            return Nothing;
        }

        /// <summary>Consumes up to theMaxLength chars from the top and returns them as a view (Nothing if theMaxLength &lt;= 0).</summary>
        public Chars PopLength(int theMaxLength)
        {
            if (theMaxLength > 0)
            {
                int iBot = _TopIndex - theMaxLength;
                int iTop = _TopIndex;
                if (iBot < _BotIndex)
                    iBot = _BotIndex;
                _TopIndex = iBot;
                return new Chars(iBot, iTop, _TextBase);
            }
            return Nothing;
        }

        /// <summary>Consumes chars from the bot until one matches the category mask; returns the consumed view and the OR of its categories.</summary>
        public Chars PluckUntil(CharsCat theCat, out CharsCat returnCatSum)
        {
            returnCatSum = 0;
            if (_BotIndex >= _TopIndex) return Nothing;
            int iBotIndex = _BotIndex;
            while (_BotIndex < _TopIndex)
            {
                CharsCat iCat = Cat(_TextBase[_BotIndex]);
                if (0 != (theCat & iCat))
                    break;
                returnCatSum |= iCat;
                _BotIndex++;
            }
            return new Chars(iBotIndex, _BotIndex, _TextBase);
        }

        /// <summary>Consumes chars from the bot while they match the category mask; returns the consumed view and the OR of its categories.</summary>
        public Chars PluckWhile(CharsCat theCat, out CharsCat returnCatSum)
        {
            returnCatSum = 0;
            if (_BotIndex >= _TopIndex) return Nothing;
            int iBotIndex = _BotIndex;
            while (_BotIndex < _TopIndex)
            {
                CharsCat iCat = Cat(_TextBase[_BotIndex]);
                if (0 == (theCat & iCat))
                    break;
                returnCatSum |= iCat;
                _BotIndex++;
            }
            return new Chars(iBotIndex, _BotIndex, _TextBase);
        }

        /// <summary>Consumes the next visible run or quoted value and returns it (Nothing if none); also reports the OR of its categories.</summary>
        public Chars PluckVisible_or_QuotedValue(out CharsCat returnCatSum)
        {
            PluckedVisible_or_QuotedValue(out Chars vReturn, out returnCatSum, out _, out _);
            return vReturn;
        }

        /// <summary>Consumes lines until one contains a visible char and returns that line (Nothing if none).</summary>
        public Chars PluckFirstVisibleLine()
        {
            while (PluckedLine(out Chars vLine))
                if (vLine.Contains(CharsCat.Any_Visible))
                    return vLine;
            return Nothing;
        }

        /// <summary>Consumes the next line (CR/LF excluded from the value) and returns true, or false when the view is empty.</summary>
        public bool PluckedLine(out Chars returnLineValue)
        {
            if (_BotIndex == _TopIndex)
            {
                returnLineValue = Nothing;
                return false;
            }

            int iReturnBot = _BotIndex;
            int iReturnTop = _TextBase.IndexOf(LF, _BotIndex, _TopIndex - _BotIndex);

            if (iReturnTop >= 0)
            {
                _BotIndex = iReturnTop + 1; // SKIP LF
                // currently LF is iReturnTop
                if (iReturnTop > 0 && _TextBase[iReturnTop - 1] == CR)
                    iReturnTop--; // back off of the CR
            }
            else // NO LF, Value is rest of text
            {
                _BotIndex = _TopIndex; // plucked to empty
                iReturnTop = _TopIndex;
                // NO LF, then assume NO CR possible
            }

            // plucks empty line value when CRLFCRLF or LFLF
            returnLineValue = new Chars(iReturnBot, iReturnTop, _TextBase);
            return true;
        }

        /// <summary>Consumes whole lines (CR/LF included) until a line starts with theChar or the view empties; returns the consumed lines.</summary>
        public bool PluckedLinesUntil_LF_char(char theChar, out Chars returnLines)
        {
            if (_BotIndex < _TopIndex)
            {
                // we are not processing ... simply searching for the next LF<char>
                int iBot = _BotIndex;
                while (_BotIndex < _TopIndex) // LINE BY LINE
                {
                    int iFound = _TextBase.IndexOf(LF, _BotIndex, _TopIndex - _BotIndex);
                    _BotIndex = iFound < 0 ? _TopIndex : iFound + 1; // +1 skips the LF
                    if (_BotIndex == _TopIndex || _TextBase[_BotIndex] == theChar)
                        break; // _BotIndex NOW POSITIONED ON theChar OR _TopIndex
                }
                returnLines = new Chars(iBot, _BotIndex, _TextBase); // INCLUDES THE CR LF (if any)
                return true;
            }
            returnLines = Nothing;
            return false;
        }

        /// <summary>Trims the bot then consumes the next visible run; true if any chars were consumed.</summary>
        public bool PluckedVisible(out Chars returnVisible, out CharsCat returnValueCatSum)
        {
            _TrimBot();

            returnVisible = PluckWhile(CharsCat.Any_Visible, out returnValueCatSum);

            return returnVisible.Length > 0;
        }

        /// <summary>Consumes a quoted value at the bot (after trimming); true if an opening quote was found. Value ends at the closing quote, CR/LF, or end of view.</summary>
        public bool PluckedQuotedValue(out Chars returnValue, out CharsCat returnValueCatSum, out bool returnHasEndingQuote, char theQuote = QUOTE)
        {
            int iBot = _BotIndex; // original bot if not quoted
            int iTop = _TopIndex; // non-QUOTE above all chars
            _TrimBot();
            returnValueCatSum = 0;
            returnHasEndingQuote = false;
            if (BotChar_or_NUL != theQuote)
            {
                _BotIndex = iBot; // reset to original bot if trimmed
                returnValue = Nothing;
                return false;
            }
            iBot = ++_BotIndex;  // SKIP theQuote, set to 1st char of Quoted value
            while (true)
            {
                char c1 = BotChar_or_NUL;
                if (_BotIndex >= _TopIndex)
                {
                    returnHasEndingQuote = false;
                    _BotIndex = _TopIndex;
                    iTop = _BotIndex;
                    break;
                }

                if (c1 == CR || c1 == LF)
                {
                    returnHasEndingQuote = false;
                    iTop = _BotIndex;
                    break;
                }

                if (c1 == theQuote)
                {
                    iTop = _BotIndex;
                    returnHasEndingQuote = true;
                    _BotIndex++; // SKIP THE QUOTE
                    break;
                }
                returnValueCatSum |= Cat(c1);
                _BotIndex++;
            }

            returnValue = new Chars(iBot, iTop, _TextBase);

            return true;
        }

        /// <summary>Consumes the next visible run or quoted value; true if a value was consumed.</summary>
        public bool PluckedVisible_or_QuotedValue(out Chars returnValue, char theQuoteChar = QUOTE)
                 => PluckedVisible_or_QuotedValue(out returnValue, out _, out _, out _, theQuoteChar);

        /// <summary>Consumes the next visible run or quoted value; reports categories, whether quoted, and whether the end quote was present.</summary>
        public bool PluckedVisible_or_QuotedValue(out Chars returnValue, out CharsCat returnValueCatSum, out bool returnIsQuoted, out bool returnHasEndQuote, char theQuoteChar = QUOTE)
        {
            _TrimBot();
            if (BotChar_or_NUL == theQuoteChar)
            {
                returnIsQuoted = true;
                return PluckedQuotedValue(out returnValue, out returnValueCatSum, out returnHasEndQuote, theQuoteChar);
            }
            returnIsQuoted = false;
            returnHasEndQuote = false;
            returnValue = PluckWhile(CharsCat.Any_Visible, out returnValueCatSum);
            return !returnValue.IsEmpty;
        }

        /// <summary>Consumes the next digit run (after trimming) as a view; true if at least one digit.</summary>
        public bool PluckedDigits(out Chars returnDigits)
        {
            _TrimBot();
            returnDigits = PluckWhile(CharsCat.DIGIT, out _);
            return returnDigits.Length > 0;
        }

        /// <summary>Consumes the next digit run (after trimming) as a ulong; false (nothing consumed) on no digits or overflow. Primary digit plucker.</summary>
        public bool PluckedDigits(out ulong returnUInt64_0_to_MaxValue)
        {
            // THIS IS THE PRIMARY DIGIT PLUCKER
            _TrimBot();
            int iBotOriginal = _BotIndex;
            returnUInt64_0_to_MaxValue = 0; // starting value
            char c = BotChar_or_NUL;
            if (char.IsDigit(c))
            {
                do
                {
                    uint iDigit = (uint)(c - DIGIT0);
                    if (returnUInt64_0_to_MaxValue > (ulong.MaxValue - iDigit) / 10)
                    {
                        // OVERFLOW: reset to no value found and STOP
                        _BotIndex = iBotOriginal;

                        returnUInt64_0_to_MaxValue = 0;

                        return false;
                    }
                    returnUInt64_0_to_MaxValue = returnUInt64_0_to_MaxValue * 10 + iDigit;
                    _BotIndex++;
                    c = BotChar_or_NUL;
                } while (char.IsDigit(c));
                return true; // got all digits and at least 1 digit
            }
            _BotIndex = iBotOriginal;
            return false;
        }

        /// <summary>Consumes the next digit run as a byte; false (nothing consumed) if absent or over 255.</summary>
        public bool PluckedDigits(out byte returnByte_0_to_255)
        {
            int iBotOriginal = _BotIndex;
            if (PluckedDigits(out ulong iValue))
            {
                if (iValue <= byte.MaxValue)
                {
                    returnByte_0_to_255 = (byte)iValue;
                    return true;
                }
            }
            _BotIndex = iBotOriginal;
            returnByte_0_to_255 = 0;
            return false;
        }

        /// <summary>Consumes the next digit run as a non-negative int; false (nothing consumed) if absent or over int.MaxValue.</summary>
        public bool PluckedDigits(out int returnInt32_0_to_MaxValue)
        {
            int iBotOriginal = _BotIndex;
            if (PluckedDigits(out ulong iValue))
            {
                if (iValue <= int.MaxValue)
                {
                    returnInt32_0_to_MaxValue = (int)iValue;
                    return true;
                }
            }
            _BotIndex = iBotOriginal;
            returnInt32_0_to_MaxValue = 0;
            return false;
        }

        /// <summary>Consumes the next digit run as a uint; false (nothing consumed) if absent or over uint.MaxValue.</summary>
        public bool PluckedDigits(out uint returnUInt32_0_to_MaxValue)
        {
            int iBotOriginal = _BotIndex;
            if (PluckedDigits(out ulong iValue))
            {
                if (iValue <= uint.MaxValue)
                {
                    returnUInt32_0_to_MaxValue = (uint)iValue;
                    return true;
                }
            }
            _BotIndex = iBotOriginal;
            returnUInt32_0_to_MaxValue = 0;
            return false;
        }

        /// <summary>Consumes the next delimiter-bounded segment (default VBAR) as a trimmed view; true unless the view is empty.</summary>
        public bool PluckedDelimitedText(out Chars returnText, char theDelimiter = VBAR)
        {
            returnText = Nothing;
            _TrimBot();
            if (IsEmpty) return false;
            if (BotChar_or_NUL == theDelimiter) _BotIndex++; // skip delimiter
            int iIndex = IndexOf(theDelimiter);
            int iValueTop = iIndex >= 0 ? iIndex : _TopIndex;
            returnText = new Chars(_BotIndex, iValueTop, _TextBase).Trim();
            _BotIndex = iValueTop;
            return true;
        }

        /// <summary>Consumes the next delimited segment and splits it into a leading name and remaining text; false (nothing consumed) if none.</summary>
        public bool PluckedDelimitedNameAndText(out Chars returnName, out Chars returnText, char theDelimiter = VBAR)
        {
            int iBotOriginal = _BotIndex;
            if (PluckedDelimitedText(out returnText, theDelimiter))
            {
                returnText.PluckedVisible_or_QuotedValue(out returnName, out _, out _, out _);
                returnText.Trim();
                return true;
            }
            _BotIndex = iBotOriginal;
            returnName = Nothing;
            returnText = Nothing;
            return false;
        }

        /// <summary>Splits the view (unconsumed) into a leading name and the remaining text; true if a name was found.</summary>
        public bool GotNameAndText(out Chars returnName, out Chars returnText)
        {
            returnText = this;
            return returnText.PluckedVisible_or_QuotedValue(out returnName, out _, out _, out _);
        }

        /// <summary>Searches delimited name|text segments for theName (case-insensitive); true with its trimmed text if found.</summary>
        public bool FoundDelimitedNameAndText(string theName, out Chars returnText, char theDelimiter = VBAR)
                 => FoundDelimitedNameAndText(new Chars(theName), out returnText, theDelimiter);

        /// <summary>Searches delimited name|text segments for theName (case-insensitive); true with its trimmed text if found. View is not consumed.</summary>
        public bool FoundDelimitedNameAndText(Chars theName, out Chars returnText, char theDelimiter = VBAR)
        {
            returnText = Nothing;
            if (theName.IsEmpty) return false;
            Chars vThis = this;
            while (vThis.PluckedDelimitedNameAndText(out Chars vCode, out Chars vText, theDelimiter))
            {
                if (vCode.NotEmpty && vCode.Equals(theName, true))
                {
                    returnText = vText.Trim();
                    return true;
                }
            }
            return false;
        }

        /// <summary>Finds the text between the first bot/top marker pair; true with the between view if both found. Throws on null/empty markers.</summary>
        public bool FoundTextBetween(out Chars returnTextBetween, string theBotMarker, string theTopMarker, bool ignoreCase)
        {
            if (string.IsNullOrEmpty(theBotMarker) || string.IsNullOrEmpty(theTopMarker))
                throw _issue_BadParam(35, s_Null_or_empty_markers);

            Chars vWip = this; // plucking will have no effect

            int iBot1 = vWip.IndexOf(theBotMarker, ignoreCase); // find theBotMarker
            if (iBot1 >= 0)
            {
                vWip.PluckToIndex(iBot1); // skip before marker;
                vWip.PluckLength(theBotMarker.Length); // skip marker
                int iBot2 = vWip.IndexOf(theTopMarker, ignoreCase); // find theTopMarker
                if (iBot2 >= 0)
                {
                    returnTextBetween = vWip.PluckToIndex(iBot2); // return between
                    return true;
                }
            }
            returnTextBetween = Nothing;
            return false;
        }

        /// <summary>Finds the whole lines strictly between the line holding theBotLineMarker and the line holding theTopLineMarker; true if both found. Throws on null/empty markers.</summary>
        public bool FoundLinesBetween(out Chars returnLinesBetween, string theBotLineMarker, string theTopLineMarker, bool ignoreCase)
        {
            if (string.IsNullOrEmpty(theBotLineMarker) || string.IsNullOrEmpty(theTopLineMarker))
                throw _issue_BadParam(36, s_Null_or_empty_markers);

            returnLinesBetween = Nothing;
            if (FoundTextBetween(out Chars vText, theBotLineMarker, theTopLineMarker, ignoreCase))
            {
                Chars vLine1 = LineContainingIndex(vText._BotIndex, true);
                Chars vLine2 = LineContainingIndex(vText._TopIndex, true);
                int iBot = vLine1._TopIndex;
                int iTop = vLine2._BotIndex;
                if (iBot > iTop) iBot = iTop;
                returnLinesBetween = new Chars(iBot, iTop, _TextBase);
                return true;
            }
            return false;
        }

        /// <summary>Counts the lines in the view (view unchanged) and reports the widest line's char count.</summary>
        public int LineCount(out int returnMaxLineCharWidth)
        {
            int returnLineCount = 0;
            returnMaxLineCharWidth = 0;
            int iBot = _BotIndex;
            while (PluckedLine(out Chars vLine))
            {
                returnLineCount++;
                if (vLine.Length > returnMaxLineCharWidth)
                    returnMaxLineCharWidth = vLine.Length;
            }
            _BotIndex = iBot;
            return returnLineCount;
        }

        /// <summary>Returns a view with every char matching the category mask replaced by theWithChar (this view unchanged; no allocation when no match).</summary>
        public Chars ReplaceWith(char theWithChar, CharsCat forCharsMatching)
        {
            int iFound = IndexOf(forCharsMatching);
            if (iFound < 0) return this;
            int iLen = Length;
            char[] xChars = new char[iLen];
            int iOffset = iFound - _BotIndex; // iFound is absolute; xChars is offset-relative
            _TextBase.CopyTo(_BotIndex, xChars, 0, iOffset); // unchanged prefix in one shot
            int cursor = iOffset - 1;
            while (++cursor < iLen)
            {
                char cNow = _TextBase[_BotIndex + cursor]; // bypass indexer bounds re-check
                xChars[cursor] = CharIsAny(cNow, forCharsMatching) ? theWithChar : cNow;
            }
            return new Chars(new string(xChars));
        }

        /// <summary>Returns a view with every char in forCharsMatching replaced by theWithChar (this view unchanged; no allocation when no match).</summary>
        public Chars ReplaceWith(char theWithChar, params char[] forCharsMatching)
        {
            if (forCharsMatching == null || forCharsMatching.Length == 0) return this;
            int iFound = IndexOfAny(forCharsMatching);
            if (iFound < 0) return this;
            int iLen = Length;
            char[] xChars = new char[iLen];
            int iOffset = iFound - _BotIndex; // iFound is absolute; xChars is offset-relative
            _TextBase.CopyTo(_BotIndex, xChars, 0, iOffset); // unchanged prefix in one shot
            int cursor = iOffset - 1;
            while (++cursor < iLen)
            {
                char cNow = _TextBase[_BotIndex + cursor]; // bypass indexer bounds re-check
                xChars[cursor] = CharIsAny(cNow, forCharsMatching) ? theWithChar : cNow;
            }
            return new Chars(new string(xChars));
        }

    }
}
