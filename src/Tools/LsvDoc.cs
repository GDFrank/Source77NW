// CopyrightCopyright (c) GDFrank
// SPDX-License-Identifier: MIT

using System;

namespace Source77NW
{
    /// <summary>
    /// Views LsvRecords text as a document: the first record carries the
    /// document name/type and context/identity info; later records are
    /// plucked one at a time.
    /// </summary>
    /// <remarks>
    /// Load once via <see cref="LoadedDoc"/> or
    /// <see cref="LoadedDocFromFile"/> (false when already loaded);
    /// <see cref="Clear"/> readies for a new load. Use
    /// <see cref="DocName"/> to determine how to read context and
    /// identity from <see cref="DocContext"/> and
    /// DocRecord.GotNextFieldNameAndValue().
    /// </remarks>
    public sealed class LsvDoc
    {
        private LsvRecord _DocRecord;

        private Chars _Unparsed;
        private Chars _DocName;
        private Chars _DocContext;
        private string _DocText;
        private string _FilePath;

        /// <summary>Clears the loaded state for a new load.</summary>
        public void Clear()
        {
            _DocRecord = default;
            _Unparsed = default;
            _DocName = default;
            _DocContext = default;
            _DocText = default;
            _FilePath = default;
        }

        /// <summary>The parsed first record (the document header).</summary>
        public LsvRecord DocRecord => _DocRecord;
        /// <summary>The not-yet-plucked remainder of the document text.</summary>
        public Chars Unparsed => _Unparsed;
        /// <summary>The document name: first visible/quoted token of the header context.</summary>
        public Chars DocName => _DocName;
        /// <summary>The header context following the document name, trimmed.</summary>
        public Chars DocContext => _DocContext;
        /// <summary>The source file path (null when loaded from text).</summary>
        public string FilePath => _FilePath;
        /// <summary>The full loaded document text (null when not loaded).</summary>
        public string DocText => _DocText;

        /// <summary>True when a document is loaded.</summary>
        public bool IsLoaded => _DocText != null;

        /// <summary>True when loaded and the name matches (ignore-case for a Chars).</summary>
        public bool IsDocName(object theName)
        {
            if (!IsLoaded) return false;

            if (_DocName.Length == 0) return false;

            if (theName is Chars v1)
            {
                return _DocName.Equals(v1, true);
            }

            return _DocName.Equals(theName.ToString());
        }

        /// <summary>
        /// Parses the text's first record as the document header, setting
        /// name/context; false when already loaded or no record parses.
        /// </summary>
        public bool LoadedDoc(string theDocText)
        {
            if (IsLoaded) return false;

            _Unparsed = new Chars(theDocText);

            if (LsvRecord.Parsed(ref _Unparsed, out _DocRecord))
            {
                _DocText = theDocText;
                _DocContext = _DocRecord.Context;
                _DocContext.PluckedVisible_or_QuotedValue(out Chars vName);
                _DocName = vName.Trim();
                _DocContext.Trim();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Reads the file and parses its first record as the document
        /// header; false (with Issue) on an invalid/unreadable path, or
        /// false when already loaded or no record parses.
        /// </summary>
        public bool LoadedDocFromFile(string theFilePath, out Issue returnIssue)
        {
            // parses the 1st LsvRecord of the file to DocHeader
            returnIssue = null;

            if (IsLoaded) return false;

            string sFile = FS.ValidFilePath_or_null(theFilePath, out returnIssue);

            if (sFile == null)
            {
                return false;
            }

            string sText = FS.GetText_or_null(sFile, out returnIssue);

            if (sText == null)
            {
                return false;
            }

            _FilePath = theFilePath;

            return LoadedDoc(sText);
        }

        /// <summary>Parses the next record from the remaining document text; false when none remain.</summary>
        public bool PluckedItemRecord(out LsvRecord returnRecord)
        {
            return LsvRecord.Parsed(ref _Unparsed, out returnRecord);
        }

    }
}
