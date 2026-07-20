// Copyright (c) GDFrank
// SPDX-License-Identifier: MIT

using System;
using System.IO;

namespace Source77NW
{
    /// <summary>
    /// The extended-selection twin of FileAttr: Directory, Device,
    /// Offline, ReparsePoint, SparseFile, Temporary, NotContentIndexed
    /// bits with parse/format as the seven-char "DVORSTN" dot-style
    /// string and selection tests.
    /// </summary>
    /// <remarks>
    /// <see cref="ToString"/> formats one char per bit in DVORSTN order,
    /// '.' when off. <see cref="Get(Chars, out Issue)"/> parses those
    /// letters (case-insensitive, dots ignored); any other char is a
    /// BadEntry Issue.
    /// </remarks>
    public struct FileAttrX
    {
        private const ushort issueSource = 65236;

        /// <summary>The cared-about extended FileAttributes bits, letter-prefixed for the DVORSTN format.</summary>
        [Flags]
        public enum Bits
        {
            D_Directory = FileAttributes.Directory,
            V_Device = FileAttributes.Device,
            O_Offline = FileAttributes.Offline,
            R_ReparsePoint = FileAttributes.ReparsePoint,
            S_SparseFile = FileAttributes.SparseFile,
            T_Temporary = FileAttributes.Temporary,
            N_NotContentIndexed = FileAttributes.NotContentIndexed
        }

        private const Bits _Mask
            = Bits.D_Directory
            | Bits.V_Device
            | Bits.T_Temporary
            | Bits.S_SparseFile
            | Bits.R_ReparsePoint
            | Bits.O_Offline
            | Bits.N_NotContentIndexed;

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

        /// <summary>The seven-char DVORSTN dot-style form (e.g. "D......").</summary>
        public override string ToString()
        {
            char[] x = new char[7];
            x[0] = 0 != (Value & Bits.D_Directory) ? 'D' : DOT;
            x[1] = 0 != (Value & Bits.V_Device) ? 'V' : DOT;
            x[2] = 0 != (Value & Bits.O_Offline) ? 'O' : DOT;
            x[3] = 0 != (Value & Bits.R_ReparsePoint) ? 'R' : DOT;
            x[4] = 0 != (Value & Bits.S_SparseFile) ? 'S' : DOT;
            x[5] = 0 != (Value & Bits.T_Temporary) ? 'T' : DOT;
            x[6] = 0 != (Value & Bits.N_NotContentIndexed) ? 'N' : DOT;
            return new string(x);
        }

        /// <summary>A FileAttrX selecting the bits.</summary>
        public static FileAttrX Get(Bits theBits) => new FileAttrX() { Value = theBits };

        /// <summary>A FileAttrX selecting the attributes' cared-about bits.</summary>
        public static FileAttrX Get(FileAttributes theAttributes) => Get((Bits)theAttributes & _Mask);

        /// <summary>A FileAttrX selecting the file/folder's cared-about bits.</summary>
        public static FileAttrX Get(FileSystemInfo theInfo) => Get((Bits)theInfo.Attributes & _Mask);

        /// <summary>
        /// Parses DVORSTN letters (case-insensitive, dots ignored); any
        /// other char returns default with a BadEntry Issue.
        /// </summary>
        public static FileAttrX Get(Chars theCS, out Issue returnIssue)
        {
            returnIssue = null;

            Bits iBits = 0;

            for (int i = 0; i < theCS.Length; i++)
            {
                switch (char.ToUpper(theCS[i]))
                {
                    case 'D': iBits |= Bits.D_Directory; continue;
                    case 'V': iBits |= Bits.V_Device; continue;
                    case 'T': iBits |= Bits.T_Temporary; continue;
                    case 'O': iBits |= Bits.O_Offline; continue;
                    case 'S': iBits |= Bits.S_SparseFile; continue;
                    case 'N': iBits |= Bits.N_NotContentIndexed; continue;
                    case 'R': iBits |= Bits.R_ReparsePoint; continue;
                    case DOT: continue;
                    default:
                        // FIX: symmetry with FileAttr - include the offending text in the Issue
                        returnIssue = Issue.Create(issueSource, 2, typeof(FileAttrX), theCS.ToString(), IssueKind.BadEntry);
                        return default;
                }
            }

            return new FileAttrX() { Value = iBits };
        }

        /// <summary>Parses DVORSTN letters from the string (see <see cref="Get(Chars, out Issue)"/>).</summary>
        public static FileAttrX Get(string theText, out Issue returnIssue) => Get(new Chars(theText), out returnIssue);

    }
}
