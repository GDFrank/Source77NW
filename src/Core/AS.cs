// Copyright (c) GDFrank
// SPDX-License-Identifier: MIT

#if NETCOREAPP && !NO_CORE
#define _USE_CORE
#endif

using System;

namespace Source77NW
{
    /// <summary>
    /// Ascii Strings - named ASCII control/punctuation strings, frequent
    /// combined strings, DOT file-extension strings, timestamp format strings,
    /// and general string parsing helpers - used everywhere in Source77NW.
    /// Named constants read better than embedded literals: easy recognition,
    /// consistent spelling, and the eyeball never has to verify the "stuff".
    /// See Chars for the char counterparts; nested Url for url schemes.
    /// </summary>
    public static class AS
    {
        #region // ASCII STRING CHARACTERS
        public const string NUL = "\0";         // 000
        public const string BS = "\b";          // 008
        public const string TAB = "\t";         // 009
        public const string LF = "\n";          // 010
        public const string VT = "\x0B";        // 011
        public const string FF = "\f";          // 012
        public const string CR = "\r";          // 013
        public const string CAN = "\x18";       // 024
        public const string ESC = "\x1B";       // 027
        public const string SP = " ";           // 032
        public const string BANG = "!";         // 033
        public const string QUOTE = "\"";       // 034
        public const string HASH = "#";         // 035
        public const string DOL = "$";          // 036
        public const string PCT = "%";          // 037
        public const string AMP = "&";          // 038
        public const string APOS = "'";         // 039
        public const string PAR1 = "(";         // 040
        public const string PAR2 = ")";         // 041
        public const string STAR = "*";         // 042
        public const string PLUS = "+";         // 043
        public const string COMMA = ",";        // 044
        public const string DASH = "-";         // 045
        public const string DOT = ".";          // 046
        public const string SLASH = "/";        // 047
        public const string DIGIT0 = "0";       // 048
        public const string DIGIT9 = "9";       // 057
        public const string COLON = ":";        // 058
        public const string SEMI = ";";         // 059
        public const string LT = "<";           // 060
        public const string EQ = "=";           // 061
        public const string GT = ">";           // 062
        public const string QUEST = "?";        // 063
        public const string AT = "@";           // 064
        public const string BOX1 = "[";         // 091
        public const string BSLASH = @"\";      // 092
        public const string BOX2 = "]";         // 093
        public const string CARET = "^";       // 094
        public const string UNDER = "_";        // 095
        public const string GRAVE = "`";        // 096
        public const string CURLY1 = "{";       // 123
        public const string VBAR = "|";         // 124
        public const string CURLY2 = "}";       // 125
        public const string TILDE = "~";        // 126
        public const string DEL = "\x7F";       // 127
        #endregion

        #region // COMMON GENERIC STRINGS
        public const string CRLF = CR + LF;
        public const string SP_2 = SP + SP;
        public const string SLASH2 = SLASH + SLASH;
        public const string BSLASH2 = BSLASH + BSLASH;
        public const string COLON_SP = COLON + SP;
        public const string COLON_BSLASH = COLON + BSLASH;
        public const string COLON_SLASH2 = COLON + SLASH2;
        public const string DOT_DOT = DOT + DOT;
        public const string STAR_DOT = STAR + DOT;
        public const string STAR_DOT_STAR = STAR_DOT + STAR;
        public const string LSEP_ntfs = CRLF;
        public const string LSEP_unix = LF;
        public const string DSEP_ntfs = BSLASH;
        public const string DSEP_unix = SLASH;
        public const string DSEP_url = SLASH;
        public const string DOT_bak = DOT + "bak";
        public const string DOT_bin = DOT + "bin";
        public const string DOT_csv = DOT + "csv";
        public const string DOT_exe = DOT + "exe";
        public const string DOT_html = DOT + "html";
        public const string DOT_ico = DOT + "ico";
        public const string DOT_jpeg = DOT + "jpeg";
        public const string DOT_jpg = DOT + "jpg";
        public const string DOT_lnk = DOT + "lnk";
        public const string DOT_log = DOT + "log";
        public const string DOT_png = DOT + "png";
        public const string DOT_tsv = DOT + "tsv";
        public const string DOT_txt = DOT + "txt";
        public const string DOT_url = DOT + "url";
        public const string DOT_wav = DOT + "wav";
        public const string DOT_zip = DOT + "zip";
        public const string DOT_com = DOT + "com";
        public const string DOT_net = DOT + "net";
        public const string DOT_org = DOT + "org";

        public const string Yes = "Yes";
        public const string No = "No";
        /// <summary>Boolean.TrueString ("True").</summary>
        public static string True => Boolean.TrueString;
        /// <summary>Boolean.FalseString ("False").</summary>
        public static string False => Boolean.FalseString;

        public const string STAMP_yyyy = "yyyy";
        public const string STAMP_yyyy_MM = STAMP_yyyy + "-MM";
        public const string STAMP_yyyy_MM_dd = STAMP_yyyy_MM + "-dd";
        public const string STAMP_date_HHmm = STAMP_yyyy_MM_dd + "-HHmm";
        public const string STAMP_date_HHmm_ss = STAMP_date_HHmm + "-ss";
        public const string STAMP_date_HHmm_ss_fff = STAMP_date_HHmm_ss + "-fff";

        public const string STAMP_yyyyMMdd = "yyyyMMdd";
        public const string STAMP_yyyyMMddHHmm = STAMP_yyyyMMdd + "HHmm";
        public const string STAMP_yyyyMMddHHmmss = STAMP_yyyyMMddHHmm + "ss";
        public const string STAMP_yyyyMMddHHmmssfff = STAMP_yyyyMMddHHmmss + "fff";

        #endregion

        // COMMON METHODS

        /// <summary>StringComparison.OrdinalIgnoreCase (the house standard for case-insensitive compares).</summary>
        public static StringComparison IgnoreCase => StringComparison.OrdinalIgnoreCase;

        /// <summary>True when theText starts with thePrefix; false when either is null/empty.</summary>
        public static bool IsPrefixedWith(string theText, string thePrefix, bool ignoreCase)
        {
            if (string.IsNullOrEmpty(thePrefix)) return false;

            if (string.IsNullOrEmpty(theText)) return false;

            return ignoreCase ? theText.StartsWith(thePrefix, IgnoreCase) : theText.StartsWith(thePrefix);
        }

        private const string _GuidFmt = @"D";

        /// <summary>theGuid in the uniform house form: "D" format (dashed), upper case.</summary>
        public static string UniformGuidText(Guid theGuid) => theGuid.ToString(_GuidFmt).ToUpperInvariant();

        /// <summary>Parses theText as a Guid and returns it in the uniform house form, else null.</summary>
        public static string UniformGuidText_or_null(string theText)
        {
            if (!string.IsNullOrWhiteSpace(theText))
                return Guid.TryParse(theText, out Guid vGuid)
                    ? vGuid.ToString(_GuidFmt).ToUpperInvariant()
                    : null;
            return null;
        }

        /// <summary>theString wrapped in theQuote (default double quote); null theString yields the quotes alone.</summary>
        public static string Quoted(string theString, string theQuote = QUOTE) => theQuote + theString + theQuote; // null will return ""

        /// <summary>Removes one pair of surrounding double quotes when present; otherwise returns theString unchanged.</summary>
        public static string Unquote(string theString)
        {
            if (theString == null || theString.Length < 2) return theString;
            if (theString[0] == QUOTE[0] && theString[theString.Length - 1] == QUOTE[0])
            {
                return theString.Substring(1, theString.Length - 2);
            }
            return theString;
        }

        /// <summary>Combined hash of a and b (HashCode.Combine on core; 17/31 rollup on framework). Nulls hash as 0.</summary>
        public static int HashCode(object a, object b)
        {
#if _USE_CORE
            return System.HashCode.Combine(a, b);
#else
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (a?.GetHashCode() ?? 0);
                hash = hash * 31 + (b?.GetHashCode() ?? 0);
                return hash;
            }
#endif
        }

        private static readonly char[] _bool_chars = new char[] { 'T', 'F', 't', 'f', 'Y', 'N', 'y', 'n', '1', '0' }; // TrueFalse pairs

        /// <summary>True when theChar is a boolean char (T/F, Y/N, 1/0, any case), with its value in returnTrue.</summary>
        public static bool IsTrueOrFalse(char theChar, out bool returnTrue)
        {
            // for determining if theChar is one of the _bool_chars or not.
            if (theChar >= '0')
            {
                int i2 = 0;
                while (i2 < _bool_chars.Length) // TrueFalse PAIRS
                {
                    if (theChar == _bool_chars[i2++]) { returnTrue = true; return true; }
                    if (theChar == _bool_chars[i2++]) { returnTrue = false; return true; }
                }
            }
            // not a _bool_chars char
            returnTrue = false;
            return false;
        }

        /// <summary>True when theChar is a TRUE boolean char (T/Y/1, any case); false for all others.</summary>
        public static bool IsTrue(char theChar)
        {
            if (theChar >= '0')
            {
                int i2 = 0;

                while (i2 < _bool_chars.Length) // TrueFalse pairs
                {
                    if (theChar == _bool_chars[i2++]) return true;

                    if (theChar == _bool_chars[i2++]) return false;
                }
            }

            return false;
        }

        /// <summary>True when theString's first non-whitespace char is a TRUE boolean char (T/Y/1, any case).</summary>
        public static bool IsTrue(string theString)
        {
            if (theString == null) return false;

            int i1 = 0;

            while (i1 < theString.Length)
            {
                char c1 = theString[i1++];

                if (char.IsWhiteSpace(c1)) continue;

                return IsTrue(c1);
            }

            return false;
        }

        /// <summary>"True" or "False" for value.</summary>
        public static string True_or_False(bool value) => value ? True : False;

        /// <summary>"True" or "False" for value (parsed via IsTrue).</summary>
        public static string True_or_False(string value) => IsTrue(value) ? True : False;

        /// <summary>"Yes" or "No" for value.</summary>
        public static string Yes_or_No(bool value) => value ? Yes : No;

        /// <summary>"Yes" or "No" for value (parsed via IsTrue).</summary>
        public static string Yes_or_No(string value) => IsTrue(value) ? Yes : No;

        public const uint OneKB = 1024;
        public const uint OneMB = OneKB * OneKB;
        public const uint OneGB = OneMB * OneKB;
        public const ulong OneTB = (ulong)OneGB * OneKB;
        public const ulong OnePB = OneTB * OneKB;

        /// <summary>Scales theValue to the largest fitting unit, returning the scaled number with its unit suffix ("KB".."PB"; empty below 1KB).</summary>
        public static double Get_KB_MB_GB_TB_PB(ulong theValue, out string return_KB_MB_GB_TB_PB)
        {
            return_KB_MB_GB_TB_PB = string.Empty;
            if (theValue < OneKB)
                return theValue;
            if (theValue < OneMB)
            {
                return_KB_MB_GB_TB_PB = "KB";
                return (double)theValue / OneKB;
            }
            if (theValue < OneGB)
            {
                return_KB_MB_GB_TB_PB = "MB";
                return (double)theValue / OneMB;
            }
            if (theValue < OneTB)
            {
                return_KB_MB_GB_TB_PB = "GB";
                return (double)theValue / OneGB;
            }
            if (theValue < OnePB)
            {
                return_KB_MB_GB_TB_PB = "TB";
                return (double)theValue / OneTB;
            }
            return_KB_MB_GB_TB_PB = "PB";
            return (double)theValue / OnePB;
        }

        /// <summary>theValue as a human-readable size string, e.g. "12MB" (withTenths: "12.3 MB").</summary>
        public static string Format_KB_MB_GB_TB_PB(ulong theValue, bool withTenths = false)
        {
            double dResult = Get_KB_MB_GB_TB_PB(theValue, out string sSuffix);
            string sDigits;
            if (withTenths)
            {
                dResult = Math.Round(dResult, 1);
                sDigits = dResult.ToString("0.0");
                return sDigits + SP + sSuffix;
            }
            sDigits = dResult.ToString("0");
            return sDigits + sSuffix;
        }

        /// <summary>Parses theUInt64Text then formats as a human-readable size; returns the input unchanged when it does not parse.</summary>
        public static string Format_KB_MB_GB_TB_PB(string theUInt64Text, bool withTenths = false)
        {
            ulong iValue = ParseUInt64(theUInt64Text, out bool bSuccess);
            if (bSuccess)
                return Format_KB_MB_GB_TB_PB(iValue, withTenths);
            return theUInt64Text;
        }

        /// <summary>Int32.TryParse with the value returned and success as out (0 on failure).</summary>
        public static int ParseInt32(string theText, out bool succeeded) { succeeded = Int32.TryParse(theText, out int iValue); return iValue; }
        /// <summary>UInt32.TryParse with the value returned and success as out (0 on failure).</summary>
        public static uint ParseUInt32(string theText, out bool succeeded) { succeeded = UInt32.TryParse(theText, out uint iValue); return iValue; }
        /// <summary>Int64.TryParse with the value returned and success as out (0 on failure).</summary>
        public static long ParseInt64(string theText, out bool succeeded) { succeeded = Int64.TryParse(theText, out long iValue); return iValue; }
        /// <summary>UInt64.TryParse with the value returned and success as out (0 on failure).</summary>
        public static ulong ParseUInt64(string theText, out bool succeeded) { succeeded = UInt64.TryParse(theText, out ulong iValue); return iValue; }
        /// <summary>Single.TryParse with the value returned and success as out (0 on failure).</summary>
        public static float ParseSingle(string theText, out bool succeeded) { succeeded = Single.TryParse(theText, out float iValue); return iValue; }
        /// <summary>Double.TryParse with the value returned and success as out (0 on failure).</summary>
        public static double ParseDouble(string theText, out bool succeeded) { succeeded = Double.TryParse(theText, out double iValue); return iValue; }
        /// <summary>Decimal.TryParse with the value returned and success as out (0 on failure).</summary>
        public static decimal ParseDecimal(string theText, out bool succeeded) { succeeded = Decimal.TryParse(theText, out decimal vValue); return vValue; }

        /// <summary>theChar '0'..'9' as its digit value 0..9; 0 with succeeded false otherwise.</summary>
        public static byte ParseDigit(char theChar, out bool succeeded)
        {
            if (theChar >= '0' && theChar <= '9')
            {
                succeeded = true;
                return (byte)(theChar - '0');
            }
            succeeded = false;
            return 0;
        }


        #region // ============= DATETIME ================

        /// <summary>DateTime.TryParse, falling back to ParsedStamp for house STAMP forms.</summary>
        public static bool ParsedDateTime(string theText, out DateTime returnDateTime)
        {
            if (DateTime.TryParse(theText, out returnDateTime))
                return true;
            return ParsedStamp(theText, out returnDateTime, out _);
        }

        private static bool _gotDigits(ref int theCursor, string theText, bool bSkipDash, byte forCount, out int iValue)
        {
            // theCursor advances only on success (scanning uses a local) - by design

            iValue = 0;
            int iDigitCount = 0;
            int iCursor = theCursor - 1; // only update theCursor if successful
            while (++iCursor < theText.Length && iDigitCount < forCount)
            {
                char c1 = theText[iCursor];
                if (!char.IsDigit(c1))
                {
                    if (c1 == Chars.DASH)
                        if (bSkipDash && iCursor == theCursor) // first char of parse
                        {
                            bSkipDash = false;
                            continue;
                        }

                    break;
                }
                iValue = ((iValue * 10) + (c1 - '0'));
                iDigitCount++;
            }
            if (iDigitCount == forCount)
            {
                theCursor = iCursor;
                return true;
            }
            iValue = 0;
            return false;
        }

        /// <summary>Parses a house STAMP timestamp (yyyy[-MM[-dd[-HHmm[-ss[-fff]]]]], dashes optional; see STAMP_* consts) starting at theStartingIndex. Partial stamps default missing parts (month/day to 1). Returns the chars consumed.</summary>
        public static bool ParsedStamp(string theText
            , out DateTime returnDateTime
            , out int returnLengthParsed
            , bool asUtc = false
            , int theStartingIndex = 0)
        {
            returnDateTime = default; returnLengthParsed = 0;
            if (theText == null)
                return false;
            int i_yyyy = 0; int i_MM = 0; int i_dd = 0; int i_HH = 0; int i_mm = 0; int i_ss = 0;int i_fff = 0;
            int iCursor = theStartingIndex;
            bool bGotYYYY = _gotDigits(ref iCursor, theText, false, 4, out i_yyyy);
            if (bGotYYYY)
                if (_gotDigits(ref iCursor, theText, true, 2, out i_MM))
                    if (_gotDigits(ref iCursor, theText, true, 2, out i_dd))
                        if (_gotDigits(ref iCursor, theText, true, 2, out i_HH))
                            if (_gotDigits(ref iCursor, theText, false, 2, out i_mm)) // HHmm is never HH-mm
                                if (_gotDigits(ref iCursor, theText, true, 2, out i_ss))
                                    _gotDigits(ref iCursor, theText, true, 3, out i_fff);
            try
            {
                if (bGotYYYY)
                {
                    if (i_MM < 1) i_MM = 1; // if yyyy only
                    if (i_dd < 1) i_dd = 1; // if yyyy MM only
                    returnDateTime = new DateTime(i_yyyy, i_MM, i_dd, i_HH, i_mm, i_ss, i_fff, asUtc ? DateTimeKind.Utc : DateTimeKind.Local);
                    returnLengthParsed = iCursor - theStartingIndex;
                    return true;
                }
            }
            catch (ArgumentOutOfRangeException) { }
            returnDateTime = default;
            returnLengthParsed = 0;
            return false;
        }


        /// <summary>Month numbers 1..12 as three-letter names.</summary>
        public enum MonthId : byte
        {
            JAN = 1, FEB = 2, MAR = 3, APR = 4, MAY = 5,  JUN = 6,
            JUL = 7, AUG = 8, SEP = 9, OCT = 10, NOV = 11, DEC = 12
        }

        /// <summary>Three-letter alternative names for DayOfWeek (same values).</summary>
        public enum WeekDayId : byte // alt names for DayOfWeek
        {
            SUN = DayOfWeek.Sunday,   MON = DayOfWeek.Monday,
            TUE = DayOfWeek.Tuesday,  WED = DayOfWeek.Wednesday,
            THU = DayOfWeek.Thursday, FRI = DayOfWeek.Friday,
            SAT = DayOfWeek.Saturday,
        }

        /// <summary>theDate's day of week as a WeekDayId.</summary>
        public static WeekDayId WeekDay(DateTime theDate) => WeekDay(theDate, out _);

        /// <summary>theDate's day of week as a WeekDayId, with its month as a MonthId.</summary>
        public static WeekDayId WeekDay(DateTime theDate, out MonthId returnMonthId)
        {
            returnMonthId = (MonthId)theDate.Month;
            return (WeekDayId)theDate.DayOfWeek;
        }

        /// <summary>Parses a WeekDayId, accepting both three-letter (WED) and full DayOfWeek (Wednesday) names, any case.</summary>
        public static bool ParsedWeekDayId(string forWeekDayId, out WeekDayId returnDayId)
        {
            if (!Enum.TryParse(forWeekDayId, true, out returnDayId))
            {
                if (Enum.TryParse(forWeekDayId, true, out DayOfWeek iDay))
                {
                    returnDayId = (WeekDayId)iDay;
                    return true;
                }
                return false;
            }
            return true;
        }

        /// <summary>Parses a MonthId three-letter name, any case.</summary>
        public static bool ParsedMonthId(string forMonthId, out MonthId returnMonId)
        {
            return Enum.TryParse(forMonthId, true, out returnMonId);
        }

        /// <summary>The first day of theDate's month.</summary>
        public static DateTime AsMonthOf(DateTime theDate) => new DateTime(theDate.Year, theDate.Month, 1);

        /// <summary>The first day of theDate's year.</summary>
        public static DateTime AsYearOf(DateTime theDate) => new DateTime(theDate.Year, 1, 1);

        /// <summary>The date (time zeroed) of theDate's week start, where weeks begin on theFirstDayOfWeek.</summary>
        public static DateTime AsWeekOf(DateTime theDate, WeekDayId theFirstDayOfWeek)
        {
            WeekDayId iDay = WeekDay(theDate, out _);

            int iDays = 0;

            if (iDay < theFirstDayOfWeek)
            {
                iDays = (int)iDay + (7 - (int)theFirstDayOfWeek);
            }
            else if (iDay > theFirstDayOfWeek)
            {
                iDays = iDay - theFirstDayOfWeek;
            }

            if (iDays > 0)
            {
                TimeSpan v = new TimeSpan(iDays, 0, 0, 0);

                theDate = theDate.Subtract(v);
            }

            return theDate.Date;
        }

        #endregion

        /// <summary>
        /// Url scheme strings and helpers: recognized prefixes (file, http(s),
        /// ftp(s), mailto) plus the Source77NW "exe:" scheme for naming runtime
        /// resources (exe:clipboard, exe:output). See FS.GetPathKind.
        /// </summary>
        public static class Url
        {
            /// <summary>Recognized url scheme prefixes; unknown covers unprefixed and unrecognized (if changed, update FS.GetPathKind).</summary>
            public enum PrefixId : byte
            {
                none = 0,
                file = 1,
                http = 2,
                https = 3,
                ftp = 4,
                ftps = 5,
                mailto = 6,
                exe = 7, // exe:clipboard/output
                unknown = 8,

                // if changed, update FS.GetPathKind
            }

            public const string SP = "%20";
            public const string file = "file";
            public const string file_COLON_SLASH2 = file + COLON_SLASH2;
            public const string file_COLON_SLASH3 = file_COLON_SLASH2 + SLASH;
            public const string ftp = "ftp";
            public const string ftp_COLON_SLASH2 = ftp + COLON_SLASH2;
            public const string ftps = "ftps";
            public const string ftps_COLON_SLASH2 = ftps + COLON_SLASH2;
            public const string http = "http";
            public const string http_COLON_SLASH2 = http + COLON_SLASH2;
            public const string https = "https";
            public const string https_COLON_SLASH2 = https + COLON_SLASH2;
            public const string mailto = "mailto";
            public const string mailto_COLON = mailto + COLON;
            public const string exe = "exe"; // Source77NW naming runtime resources
            public const string clipboard = "clipboard";
            public const string output = "output";
            public const string exe_COLON = exe + COLON;
            public const string exe_COLON_clipboard = exe_COLON + clipboard;
            public const string exe_COLON_output = exe_COLON + output;

            private const char COLON_char = ':'; // for UrlSchemeOf

            /// <summary>Identifies thePath's scheme, splitting it into the normalized prefix and the remaining value. Drive paths ("C:...") and unprefixed text return unknown; null/whitespace returns none.</summary>
            public static PrefixId GetPrefixId(string thePath, out string returnPrefix, out string returnValue)
            {
                returnPrefix = null;
                returnValue = null;

                PrefixId iPrefixId = PrefixId.none;

                if (string.IsNullOrWhiteSpace(thePath))
                {
                    return PrefixId.none;
                }

                int i = thePath.IndexOf(COLON_char);

                if (i < 0)
                {
                    returnPrefix = string.Empty;
                    returnValue = thePath;
                    return PrefixId.unknown;
                }

                if (thePath.Length > 1 && thePath[1] == COLON_char)
                {
                    return PrefixId.unknown; // quick analysis of "C:...")
                }

                // if known prefix, normalize prefix

                if (thePath.StartsWith(file_COLON_SLASH2, IgnoreCase))
                {
                    if (thePath.StartsWith(file_COLON_SLASH3))
                    {
                        returnPrefix = file_COLON_SLASH3;
                    }
                    else
                    {
                        returnPrefix = file_COLON_SLASH2;
                    }
                    iPrefixId = PrefixId.file;
                }
                else if(thePath.StartsWith(http_COLON_SLASH2, IgnoreCase))
                {
                    returnPrefix = http_COLON_SLASH2;
                    iPrefixId = PrefixId.http;
                }
                else if (thePath.StartsWith(https_COLON_SLASH2, IgnoreCase))
                {
                    returnPrefix = https_COLON_SLASH2;
                    iPrefixId = PrefixId.https;
                }
                else if (thePath.StartsWith(ftp_COLON_SLASH2, IgnoreCase))
                {
                    returnPrefix = ftp_COLON_SLASH2;
                    iPrefixId = PrefixId.ftp;
                }
                else if (thePath.StartsWith(ftps_COLON_SLASH2, IgnoreCase))
                {
                    returnPrefix = ftps_COLON_SLASH2;
                    iPrefixId = PrefixId.ftps;
                }
                else if (thePath.StartsWith(mailto_COLON, IgnoreCase))
                {
                    returnPrefix = mailto_COLON;
                    iPrefixId = PrefixId.mailto;
                }
                else if (thePath.StartsWith(exe_COLON, IgnoreCase))
                {
                    returnPrefix = exe_COLON;
                    iPrefixId = PrefixId.exe;
                }
                else
                {
                    returnPrefix = string.Empty;
                    iPrefixId = PrefixId.unknown;
                }

                if (returnPrefix.Length < thePath.Length)
                {
                    returnValue = thePath.Substring(returnPrefix.Length);
                }
                else
                {
                    returnValue = string.Empty;
                }

                return iPrefixId;
            }

            /// <summary>True when thePath is a file:// url, with its prefix and remaining value split out.</summary>
            public static bool IsFileScheme(string thePath, out string returnSchemePrefix, out string returnValue)
                => PrefixId.file == GetPrefixId(thePath, out returnSchemePrefix, out returnValue);
        }
    }
}
