// CopyrightCopyright (c) GDFrank
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Reflection;
using System.Security;

namespace Source77NW
{
    /// <summary>
    /// Overall outcome of an operation or program run, in increasing
    /// order of severity.
    /// </summary>
    [Obfuscation(Exclude = true)]
    public enum ExitId : byte
    {
        /// <summary>Completed with no problems; all good.</summary>
        Completed = 0,

        /// <summary>Canceled by the user or by user-defined parameters.</summary>
        Canceled = 1,

        /// <summary>Canceled by the system due to a timeout.</summary>
        TimedOut = 2,

        /// <summary>Failed for a processing reason.</summary>
        Failed = 3,

        /// <summary>Failed due to a program failure.</summary>
        Critical = 4
    }

    /// <summary>
    /// Categorizes an <see cref="Issue"/> in approximate order of increasing
    /// severity - from user, access, and resource problems up to system and
    /// programming faults - to simplify follow-up actions and to serve as a
    /// display tag.
    /// </summary>
    /// <remarks>
    /// CONTRACT: values are ordered by severity and pack into 5 bits of
    /// <see cref="Exception.HResult"/> (maximum value 31; currently 29).
    /// Members at or above <see cref="ProgramIssue"/> are programming
    /// faults - see <see cref="Issue.KindIsProgramIssue"/>. Severity-based
    /// comparisons depend on this ordering, so new members must respect it.
    /// </remarks>
    [Obfuscation(Exclude = true)]
    public enum IssueKind : byte
    {
        /// <summary>No issue; the unset value.</summary>
        NoIssue = 0,

        /// <summary>Advisory only; not an error.</summary>
        Warning = 1,

        /// <summary>An issue whose kind was not determined.</summary>
        Unspecified = 2,

        /// <summary>General user or application error.</summary>
        App = 3,

        /// <summary>The requested item does not exist.</summary>
        NoSuch = 4,

        /// <summary>No more items; the end of the data was reached.</summary>
        NoMore = 5,

        /// <summary>No room; capacity is exhausted.</summary>
        NoRoom = 6,

        /// <summary>The item already exists.</summary>
        AlreadyExists = 7,

        /// <summary>The resource is locked by another user or process.</summary>
        LockedAccess = 8,

        /// <summary>More information is needed to proceed.</summary>
        NeedMoreInfo = 9,

        /// <summary>Permission or authorization is required.</summary>
        NeedPermit = 10,

        /// <summary>The supplied permission or credential is wrong.</summary>
        WrongPermit = 11,

        /// <summary>A required resource is missing.</summary>
        MissingResource = 12,

        /// <summary>The operation is not supported.</summary>
        NotSupported = 13,

        /// <summary>The operation was canceled.</summary>
        OperationCanceled = 14,

        /// <summary>The operation timed out.</summary>
        OperationTimedOut = 15,

        /// <summary>The connection was canceled.</summary>
        ConnectionCanceled = 16,

        /// <summary>The connection timed out.</summary>
        ConnectionTimedOut = 17,

        /// <summary>The connection was lost.</summary>
        ConnectionLost = 18,

        /// <summary>General I/O failure.</summary>
        IOException = 19,

        /// <summary>The operation is invalid in the current state.</summary>
        BadOperation = 20,

        /// <summary>The entered value is invalid.</summary>
        BadEntry = 21,

        /// <summary>The data is invalid or corrupt.</summary>
        BadData = 22,

        /// <summary>Programming-fault threshold: this member and all above
        /// it indicate a bug rather than an operational condition.</summary>
        ProgramIssue = 23,

        /// <summary>An index was out of range (programming fault).</summary>
        BadIndex = 24,

        /// <summary>An argument was invalid (programming fault).</summary>
        BadParam = 25,

        /// <summary>A null reference was used (programming fault).</summary>
        NullReference = 26,

        /// <summary>A disposed object was used (programming fault).</summary>
        DisposedReference = 27,

        /// <summary>An assembly or program-image fault.</summary>
        AssemblyIssue = 28,

        /// <summary>A system-level fault (for example, out of memory).</summary>
        SystemIssue = 29
    }

    /// <summary>
    /// The single application exception of Source77NW: carries a user-facing
    /// <see cref="Exception.Message"/> plus a forensic trail - an
    /// <see cref="IssueKind"/>, the raising module's <see cref="Source"/>
    /// number, and a <see cref="Spot"/> byte pinning the exact raise site -
    /// so dialogs stay humane while logs identify the origin precisely.
    /// </summary>
    /// <remarks>
    /// Created only through <see cref="Create"/>; constructors are private.
    /// The type is sealed by design: handling dispatches on <see cref="Kind"/>
    /// (data) rather than on exception subtype (hierarchy) - one exception
    /// type, many kinds - enabling friendly user-facing responses and
    /// hard-core forensic analysis from the same object.
    /// By convention each source file declares <c>const ushort issueSource</c>
    /// identifying that module, and each raise site within it uses a distinct
    /// Spot byte. Source77NW core tiers reserve issueSource 65,000-65,535.
    /// <para>
    /// Kind, Source, and Spot pack into <see cref="Exception.HResult"/> as
    /// bits 0-7 Spot, bits 8-23 Source, bits 24-28 Kind. This is a private
    /// scheme: the value is not a COM HRESULT (severity bit 31 is never set),
    /// so it must not be handed to HRESULT-interpreting APIs.
    /// </para>
    /// </remarks>
    public sealed class Issue : ApplicationException
    {
        /// <summary>
        /// Creates an <see cref="Issue"/> for the given source module and
        /// spot, composing its message, kind, and inner exception from
        /// <paramref name="theParams"/>.
        /// </summary>
        /// <param name="theSource">The raising module's issueSource number.</param>
        /// <param name="theSpot">The raise site within that module.</param>
        /// <param name="theParams">
        /// Items interpreted by type, in order: an <see cref="IssueKind"/>
        /// ranks into <see cref="Kind"/>/<see cref="Kind2"/> by severity
        /// (most severe wins Kind; runner-up becomes Kind2); an
        /// <see cref="Exception"/> becomes the <see cref="Exception.InnerException"/>,
        /// and a further exception CHAINS: it becomes the new inner with the
        /// prior inner preserved beneath it (mirrored by a bridge Issue when
        /// needed, since a built exception cannot adopt an inner); each
        /// contributes its message, and - if it
        /// is an <see cref="Issue"/> - its Kind/Kind2 replace any accumulated
        /// so far; any other enum appends its name with underscores as
        /// spaces; nested object arrays are flattened; anything else appends
        /// its <c>ToString()</c> as a message line (nulls and blanks skipped).
        /// </param>
        /// <returns>The composed Issue, ready to throw.</returns>
        public static Issue Create(ushort theSource, byte theSpot, params object[] theParams)
        {
            IssueKind iRefKind = IssueKind.NoIssue;

            IssueKind iRefKind2 = IssueKind.NoIssue;

            Exception xRefInner = null;

            string sRefMessage = string.Empty;

            _build(ref sRefMessage
                , ref xRefInner
                , ref iRefKind
                , ref iRefKind2
                , theParams);

            Issue xIssue = xRefInner != null
                ? new Issue(sRefMessage, xRefInner)
                : new Issue(sRefMessage);

            xIssue.Kind = iRefKind;

            xIssue.Kind2 = iRefKind2;

            xIssue.Source = theSource;

            xIssue.Spot = theSpot;

            xIssue.HResult = (int)xIssue.Kind << _shift_kind
                | theSource << _shift_source
                | theSpot;

            return xIssue;
        }

        /// <summary>
        /// True if <paramref name="theKind"/> equals any of
        /// <paramref name="theKinds"/>.
        /// </summary>
        public static bool KindIsAny(IssueKind theKind, params IssueKind[] theKinds)
        {
            for (int i = 0; i < theKinds.Length; i++)
            {
                if (theKind == theKinds[i])
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// True if <paramref name="theKind"/> indicates a programming fault
        /// (at or above <see cref="IssueKind.ProgramIssue"/>).
        /// </summary>
        public static bool KindIsProgramIssue(IssueKind theKind) => theKind >= IssueKind.ProgramIssue;

        /// <summary>
        /// Maps any exception to its closest <see cref="IssueKind"/>:
        /// an <see cref="Issue"/> reports its own Kind; well-known BCL
        /// exception types map to their equivalents; anything unrecognized
        /// (or null) maps to <see cref="IssueKind.Unspecified"/>.
        /// </summary>
        /// <remarks>
        /// Order matters where BCL types derive from one another (for
        /// example <see cref="ArgumentOutOfRangeException"/> is tested before
        /// <see cref="ArgumentException"/>).
        /// </remarks>
        public static IssueKind KindOf(Exception theException)
        {
            if (theException == null) return IssueKind.Unspecified;

            if (theException is Issue xIssue) return xIssue.Kind;

            if (theException is ArgumentOutOfRangeException) return IssueKind.BadIndex;

            if (theException is DuplicateWaitObjectException) return IssueKind.ProgramIssue;

            if (theException is ArgumentException) return IssueKind.BadParam;

            if (theException is PathTooLongException) return IssueKind.BadParam;

            if (theException is IndexOutOfRangeException) return IssueKind.BadIndex;

            if (theException is NullReferenceException) return IssueKind.ProgramIssue;

            if (theException is NotSupportedException) return IssueKind.NotSupported;

            if (theException is FileNotFoundException) return IssueKind.NoSuch;

            if (theException is DirectoryNotFoundException) return IssueKind.NoSuch;

            if (theException is EndOfStreamException) return IssueKind.NoMore;

            if (theException is ObjectDisposedException) return IssueKind.DisposedReference;

            if (theException is InvalidOperationException) return IssueKind.BadOperation;

            if (theException is InvalidDataException) return IssueKind.BadData;

            if (theException is UnauthorizedAccessException) return IssueKind.NeedPermit;

            if (theException is AccessViolationException) return IssueKind.NeedPermit;

            if (theException is SecurityException) return IssueKind.NeedPermit;

            if (theException is IOException)
            {
                // Win32 code lives in the low 16 bits of an IOException
                // HResult: 32 = ERROR_SHARING_VIOLATION, 33 =
                // ERROR_LOCK_VIOLATION - both mean "locked by someone".
                int i = theException.HResult & ((1 << 16) - 1);

                if (i == 32 || i == 33)
                    return IssueKind.LockedAccess;

                return IssueKind.IOException;
            }

            if (theException is OutOfMemoryException) return IssueKind.SystemIssue;

            if (theException is InvalidProgramException) return IssueKind.AssemblyIssue;

            return IssueKind.Unspecified;
        }

        /// <summary>
        /// Optional caller-attached payload; rendered as leading message
        /// lines by <see cref="DetailLines"/> and the Detail_* message
        /// properties.
        /// </summary>
        public object Detail { get; set; }

        /// <summary>
        /// True if this Issue's <see cref="Kind"/> equals any of the given
        /// kinds.
        /// </summary>
        public bool IsAny(params IssueKind[] one_of_kinds) => KindIsAny(Kind, one_of_kinds);

        /// <summary>The primary (most severe) kind of this Issue.</summary>
        public IssueKind Kind { get; private set; }

        /// <summary>
        /// True if <see cref="Kind"/> indicates a programming fault.
        /// </summary>
        public bool IsProgrammingIssue => KindIsProgramIssue(Kind);

        /// <summary>True if a secondary kind is present.</summary>
        public bool HasSubKind => Kind2 > IssueKind.NoIssue;

        /// <summary>
        /// The secondary (less severe) kind, when two kinds competed during
        /// creation; otherwise <see cref="IssueKind.NoIssue"/>.
        /// </summary>
        public IssueKind Kind2 { get; private set; }

        /// <summary>True if an inner exception is attached.</summary>
        public bool HasInnerException => InnerException != null;

        /// <summary>
        /// The <see cref="IssueKind"/> of the inner exception via
        /// <see cref="KindOf"/> (<see cref="IssueKind.Unspecified"/> if none).
        /// </summary>
        public IssueKind InnerExceptionKind => KindOf(InnerException);

        /// <summary>
        /// The raising module's issueSource number.
        /// HIDES <see cref="Exception.Source"/> (the BCL string property);
        /// cast to <see cref="Exception"/> to reach the original.
        /// </summary>
        public new ushort Source { get; private set; }

        /// <summary>The raise site within the source module.</summary>
        public byte Spot { get; private set; }

        /// <summary>
        /// Builds a one-line forensic header for any exception. For an
        /// <see cref="Issue"/>: "Issue: Kind[/Kind2] (kind.source.spot)";
        /// for other exceptions: "Issue: MappedKind TypeName". Returns
        /// an empty string for null.
        /// </summary>
        /// <param name="forException">The exception to describe.</param>
        /// <param name="appendMessage">True to append the exception's
        /// message after the header.</param>
        public static string GetHeader(Exception forException, bool appendMessage = false)
        {
            void build(ref string s2, Exception x1)
            {
                s2 += s2.Length > 0 ? "Inner: " : "Issue: ";

                if (x1 is Issue xIssue)
                {
                    string sKind2 = xIssue.Kind2 > IssueKind.NoIssue
                        ? '/' + xIssue.Kind2.ToString()
                        : string.Empty;

                    s2 += xIssue.Kind.ToString()
                        + sKind2
                        + SP + '(' + (int)xIssue.Kind
                        + DOT + xIssue.Source.ToString()
                        + DOT + xIssue.Spot.ToString()
                        + ')'
                        + LSep;
                }
                else if (x1 is Exception xException)
                {
                    s2 += KindOf(xException).ToString()
                        + SP + xException.GetType().Name
                        + LSep;
                }
            }

            string sResult = string.Empty;

            if (forException != null)
            {
                build(ref sResult, forException);

                if (appendMessage)
                {
                    sResult += forException.Message;
                }
            }

            return sResult;
        }

        /// <summary>This Issue's forensic header line (see <see cref="GetHeader"/>).</summary>
        public string Header => GetHeader(this);

        private static string _Lines(object obj)
        {
            string sReturn = obj == null ? string.Empty : obj.ToString().Trim();
            if (string.IsNullOrEmpty(sReturn)) return string.Empty;
            if (!sReturn.EndsWith(LF)) sReturn += LSep;
            return sReturn;
        }

        /// <summary><see cref="Detail"/> rendered as message lines (empty if no Detail).</summary>
        public string DetailLines => _Lines(Detail);

        /// <summary>Header line followed by the message.</summary>
        public string Header_Message => Header + Message;

        /// <summary>Detail lines followed by the message.</summary>
        public string Detail_Message => _Lines(Detail) + Message;

        /// <summary>Header, detail lines, then the message.</summary>
        public string Header_Detail_Message => Header + Detail_Message;

        /// <summary>Header, detail lines, message, then the inner exception's
        /// header and message.</summary>
        public string Header_Detail_Message_Inner => Header_Detail_Message + GetHeader(InnerException, true);

        /// <summary>Returns <see cref="Header_Message"/>.</summary>
        public override string ToString() => Header_Message;

        private Issue(string sMessage, Exception xException) : base(sMessage, xException) { }

        private Issue(string sMessage) : base(sMessage) { }

        private static void _Set_Kind_Kind2(IssueKind iKind, ref IssueKind iRefKind, ref IssueKind iRefKind2)
        {
            // when kinds compete: Kind is the more severe,
            // Kind2 the runner-up

            if (iKind == iRefKind) return;

            if (iKind > iRefKind)
            {
                iRefKind2 = iRefKind;
                iRefKind = iKind;
                return;
            }

            if (iKind > iRefKind2)
                iRefKind2 = iKind;
        }

        private static void _build
            ( ref string sRefMessage
            , ref Exception xRefInner
            , ref IssueKind iRefKind
            , ref IssueKind iRefKind2
            , object obj)
        {
            if (obj is object[] xList)
            {
                foreach (object x1 in xList)
                    _build(ref sRefMessage, ref xRefInner
                    , ref iRefKind, ref iRefKind2, x1);

                return;
            }

            if (obj is Exception xException)
            {
                sRefMessage += xException.Message + LSep;

                if (xRefInner == null)
                {
                    xRefInner = xException;
                }
                else
                {
                    // CHAINING (G 2026-07-17, per the original header's
                    // intent): a further exception becomes the new inner
                    // with the previously held inner preserved beneath it.
                    // A built exception cannot adopt an InnerException, so
                    // the incoming one is mirrored by a bridge Issue (its
                    // forensic header + message; ids copied when it is an
                    // Issue) that carries the prior chain as ITS inner.
                    Issue xBridge = new Issue(GetHeader(xException, true), xRefInner);

                    xBridge.Kind = KindOf(xException);

                    if (xException is Issue xAsIssue)
                    {
                        xBridge.Kind2 = xAsIssue.Kind2;
                        xBridge.Source = xAsIssue.Source;
                        xBridge.Spot = xAsIssue.Spot;
                        xBridge.HResult = xAsIssue.HResult;
                    }

                    xRefInner = xBridge;
                }

                if (xException is Issue xIssue)
                {
                    // an inner Issue's kinds replace whatever accumulated
                    iRefKind = xIssue.Kind;
                    iRefKind2 = xIssue.Kind2;
                }
                else
                {
                    _Set_Kind_Kind2(KindOf(xException), ref iRefKind, ref iRefKind2);
                }

                return;
            }

            if (obj != null)
            {
                if (obj is Enum nEnum)
                {
                    if (nEnum is IssueKind iKind)
                        _Set_Kind_Kind2(iKind, ref iRefKind, ref iRefKind2);
                    else
                        sRefMessage += nEnum.ToString().Replace('_', ' ') + LSep;

                    return;
                }
                else
                {
                    string s1 = obj.ToString().Trim();

                    if (!string.IsNullOrEmpty(s1))
                        sRefMessage += s1 + LSep;
                }
            }
        }

        private static readonly string LSep = Environment.NewLine;

        private const string DOT = ".";
        private const string SP = " ";
        private const string LF = "\n";
        private const byte _shift_kind = 24;
        private const byte _shift_source = 8;
    }
}
