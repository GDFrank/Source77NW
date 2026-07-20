// Copyright (c) GDFrank
// SPDX-License-Identifier: MIT

using System;
using System.IO;

namespace Source77NW
{
    /// <summary>
    /// A curated selection of FileAttributes bits (Archive, ReadOnly,
    /// Hidden, System, Compressed, Encrypted) with parse/format as the
    /// six-char "ARHSCE" dot-style string and selection tests.
    /// </summary>
    /// <remarks>
    /// <see cref="ToString"/> formats as one char per bit in ARHSCE order,
    /// '.' when off (e.g. "A.H..."). <see cref="Get(Chars, out Issue)"/>
    /// parses those letters (case-insensitive, dots ignored); any other
    /// char is a BadEntry Issue.
    /// </remarks>
    public struct FileAttr
    {
        private const ushort issueSource = 65237;

        /// <summary>The cared-about FileAttributes bits, letter-prefixed for the ARHSCE format.</summary>
        [Flags]
        public enum Bits
        {
            A_Archive = FileAttributes.Archive,
            R_ReadOnly = FileAttributes.ReadOnly,
            H_Hidden = FileAttributes.Hidden,
            S_System = FileAttributes.System,
            C_Compressed = FileAttributes.Compressed,
            E_Encrypted = FileAttributes.Encrypted,
        }

        private const Bits _Mask // THE ONLY BITS WE CARE ABOUT
            = Bits.A_Archive
            | Bits.C_Compressed
            | Bits.E_Encrypted
            | Bits.H_Hidden
            | Bits.R_ReadOnly
            | Bits.S_System;

        /// <summary>The selected bits.</summary>
        public Bits Value { get; private set; }

        /// <summary>True when any cared-about bit is selected.</summary>
        public bool HasBits => 0 != (Value & _Mask);

        /// <summary>True when the attributes share any selected bit.</summary>
        public bool Selected(FileAttributes theAttributes) => 0 != (Value & (Bits)theAttributes);

        /// <summary>True when the file/folder's attributes share any selected bit.</summary>
        public bool Selected(FileSystemInfo theInfo) => Selected(theInfo.Attributes);

        /// <summary>True when the bits share any selected bit.</summary>
        public bool Selected(Bits theBits) => 0 != (Value & theBits);

        private const char DOT = '.';

        /// <summary>The six-char ARHSCE dot-style form (e.g. "A.H...").</summary>
        public override string ToString()
        {
            char[] x = new char[6];
            x[0] = 0 != (Value & Bits.A_Archive) ? 'A' : DOT;
            x[1] = 0 != (Value & Bits.R_ReadOnly) ? 'R' : DOT;
            x[2] = 0 != (Value & Bits.H_Hidden) ? 'H' : DOT;
            x[3] = 0 != (Value & Bits.S_System) ? 'S' : DOT;
            x[4] = 0 != (Value & Bits.C_Compressed) ? 'C' : DOT;
            x[5] = 0 != (Value & Bits.E_Encrypted) ? 'E' : DOT;
            return new string(x);
        }

        /// <summary>Turns the bits ON in the attributes.</summary>
        public static void TurnOn(ref FileAttributes theAttributes, Bits theBits)
        {
            theAttributes = theAttributes | (FileAttributes)theBits;
        }

        /// <summary>Turns the bits OFF in the attributes.</summary>
        public static void TurnOff(ref FileAttributes theAttributes, Bits theBits)
        {
            theAttributes &= ~(FileAttributes)theBits;
        }

        /// <summary>A FileAttr selecting the bits.</summary>
        public static FileAttr Get(Bits theBits) => new FileAttr() { Value = theBits };

        /// <summary>A FileAttr selecting the attributes' cared-about bits.</summary>
        public static FileAttr Get(FileAttributes theAttributes) => Get((Bits)theAttributes & _Mask);

        /// <summary>A FileAttr selecting the file/folder's cared-about bits.</summary>
        public static FileAttr Get(FileSystemInfo theInfo) => Get((Bits)theInfo.Attributes & _Mask);

        /// <summary>
        /// Parses ARHSCE letters (case-insensitive, dots ignored); any
        /// other char returns default with a BadEntry Issue.
        /// </summary>
        public static FileAttr Get(Chars theCS, out Issue returnIssue)
        {
            returnIssue = null;

            Bits iBits = 0;

            for (int i = 0; i < theCS.Length; i++)
            {
                switch (char.ToUpper(theCS[i]))
                {
                    case 'A': iBits |= Bits.A_Archive; continue;
                    case 'R': iBits |= Bits.R_ReadOnly; continue;
                    case 'H': iBits |= Bits.H_Hidden; continue;
                    case 'S': iBits |= Bits.S_System; continue;
                    case 'C': iBits |= Bits.C_Compressed; continue;
                    case 'E': iBits |= Bits.E_Encrypted; continue;
                    case DOT: continue;
                    default:
                        returnIssue = Issue.Create(issueSource, 2, typeof(FileAttr), theCS.ToString(), IssueKind.BadEntry);
                        return default;
                }
            }

            return new FileAttr() { Value = iBits };
        }

        /// <summary>Parses ARHSCE letters from the string (see <see cref="Get(Chars, out Issue)"/>).</summary>
        public static FileAttr Get(string theText, out Issue returnIssue) => Get(new Chars(theText), out returnIssue);

    }
}
