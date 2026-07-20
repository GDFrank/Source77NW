// Copyright (c) GDFrank
// SPDX-License-Identifier: MIT

using System;

namespace Source77NW
{
    /// <summary>
    /// Bit-section layout and helpers for uint/ushort EnumCodes flag
    /// values: Grp (1 nibble, grouping), Cmd (1 nibble, runtime command
    /// kind), Etc (1 byte, local use), Val (1 byte, value identification),
    /// over the low ByteCode byte required by EnumCodes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Flag layout (hi to lo): GxxxCxxxEEEEEEEEVVVVVVVVBBBBBBBB -
    /// Grp bits 31-28, Cmd bits 27-24, Etc bits 23-16, Val bits 15-8,
    /// ByteCode bits 7-0.
    /// </para>
    /// <para>
    /// Vocabulary: xFlag = the shifted-up section inside the uint/ushort;
    /// xByte = that section shifted back down to byte/nibble; xId = an
    /// EnumCodes enum naming the items. The <see cref="ValFlag"/>,
    /// <see cref="CmdFlag"/>, and <see cref="GrpFlag"/> enums carry
    /// pre-shifted values so local enums can compose flags without
    /// writing shifts. Sections not given a global meaning (Etc always;
    /// Grp per local convention) are free for local flags/indexes.
    /// </para>
    /// </remarks>
    public static class Bits
    {
        /// <summary>Bit shift of the Grp nibble (28).</summary>
        public const byte Grp_Shift = Cmd_Shift + Cmd_Width;
        /// <summary>Bit width of the Grp section (4).</summary>
        public const byte Grp_Width = 4;
        /// <summary>Unshifted Grp bits (0xF).</summary>
        public const byte Grp_Bits = 0xF;
        /// <summary>Shifted Grp mask.</summary>
        public const uint Grp_Mask = (uint)Grp_Bits << Grp_Shift;

        /// <summary>Bit shift of the Cmd nibble (24).</summary>
        public const byte Cmd_Shift = Etc_Shift + Etc_Width;
        /// <summary>Bit width of the Cmd section (4).</summary>
        public const byte Cmd_Width = 4;
        /// <summary>Unshifted Cmd bits (0xF).</summary>
        public const byte Cmd_Bits = 0xF;
        /// <summary>Shifted Cmd mask.</summary>
        public const uint Cmd_Mask = Cmd_Bits << Cmd_Shift;

        /// <summary>Bit shift of the Etc byte (16).</summary>
        public const byte Etc_Shift = Val_Shift + Val_Width;
        /// <summary>Bit width of the Etc section (8).</summary>
        public const byte Etc_Width = 8;
        /// <summary>Unshifted Etc bits (0xFF).</summary>
        public const byte Etc_Bits = 0xFF;
        /// <summary>Shifted Etc mask.</summary>
        public const uint Etc_Mask = Etc_Bits << Etc_Shift;

        /// <summary>Bit shift of the Val byte (8).</summary>
        public const byte Val_Shift = 8;
        /// <summary>Bit width of the Val section (8).</summary>
        public const byte Val_Width = 8;
        /// <summary>Unshifted Val bits (0xFF).</summary>
        public const byte Val_Bits = 0xFF;
        /// <summary>Shifted Val mask.</summary>
        public const uint Val_Mask = Val_Bits << Val_Shift;

        /// <summary>The Grp section of the flags, still shifted.</summary>
        public static uint GrpFlag_from_flags(uint theFlag) => theFlag & Grp_Mask;
        /// <summary>The Cmd section of the flags, still shifted.</summary>
        public static uint CmdFlag_from_flags(uint theFlag) => theFlag & Cmd_Mask;
        /// <summary>The Etc section of the flags, still shifted.</summary>
        public static uint EtcFlag_from_flags(uint theFlag) => theFlag & Etc_Mask;
        /// <summary>The Val section of the flags, still shifted.</summary>
        public static uint ValFlag_from_flags(uint theFlag) => theFlag & Val_Mask;

        /// <summary>The byte shifted up into the Grp section (excess bits masked off).</summary>
        public static uint GrpFlag_from_byte(byte theByte) => ((uint)theByte << Grp_Shift) & Grp_Mask;
        /// <summary>The byte shifted up into the Cmd section (excess bits masked off).</summary>
        public static uint CmdFlag_from_byte(byte theByte) => ((uint)theByte << Cmd_Shift) & Cmd_Mask;
        /// <summary>The byte shifted up into the Etc section.</summary>
        public static uint EtcFlag_from_byte(byte theByte) => ((uint)theByte << Etc_Shift) & Etc_Mask;
        /// <summary>The byte shifted up into the Val section.</summary>
        public static uint ValFlag_from_byte(byte theByte) => (((uint)theByte << Val_Shift) & Val_Mask);

        /// <summary>The Grp section shifted down to its nibble.</summary>
        public static byte GrpByte_from_flags(uint theFlag) => (byte)((theFlag & Grp_Mask) >> Grp_Shift);
        /// <summary>The Cmd section shifted down to its nibble.</summary>
        public static byte CmdByte_from_flags(uint theFlag) => (byte)((theFlag & Cmd_Mask) >> Cmd_Shift);
        /// <summary>The Etc section shifted down to its byte.</summary>
        public static byte EtcByte_from_flags(uint theFlag) => (byte)((theFlag & Etc_Mask) >> Etc_Shift);
        /// <summary>The Val section shifted down to its byte.</summary>
        public static byte ValByte_from_flags(uint theFlag) => (byte)((theFlag & Val_Mask) >> Val_Shift);

        private const byte _ValId_Kind_MaxValue = 0x3;
        private const byte _ValId_Mask_Kind = _ValId_Kind_MaxValue << 6;

        /// <summary>The Cmd section of the flags as a CmdId.</summary>
        public static CmdId CmdId_from_flags(uint theFlag) => (CmdId)CmdByte_from_flags(theFlag);
        /// <summary>The ValKindId carried in the Val section of the flags.</summary>
        public static ValKindId ValKindId_from_flags(uint theFlag) => (ValKindId)ValByte_from_flags(theFlag);
        /// <summary>The Val section of the flags as a ValId.</summary>
        public static ValId ValId_from_flags(uint theFlag) => (ValId)ValByte_from_flags(theFlag);
        /// <summary>The ValKindId encoded in the hi 2 bits of the ValId.</summary>
        public static ValKindId ValIdKind(ValId theId) => (ValKindId)((byte)theId & _ValId_Mask_Kind);

        /// <summary>
        /// Value-handling kind encoded in the hi 2 bits of a
        /// <see cref="ValId"/>, leaving 6 bits for 0..63 ids.
        /// </summary>
        public enum ValKindId : byte
        {
            /// <summary>Single value: first visible line; a quoted line is dequoted and trimmed.</summary>
            Value  = 0 << 6,
            /// <summary>List: rebuilt lines of Value, empty lines ignored.</summary>
            List = 1 << 6,
            /// <summary>Text: trimmed but otherwise AS IS.</summary>
            Text  = 2 << 6,
            /// <summary>Special Text (user-defined handling).</summary>
            User = 3 << 6,
        }

        /// <summary>
        /// Globally defined value tokens for the Val section; each value
        /// composes a 6-bit id with its <see cref="ValKindId"/> in the hi
        /// 2 bits. Member names ARE the tokens.
        /// </summary>
        /// <remarks>
        /// WARNING: MAY BE PERSISTED - do not renumber; MUST stay synced
        /// with <see cref="ValFlag"/>.
        /// </remarks>
        public enum ValId : byte
        {
            none = 0,
            word = 1,
            line = 2,
            lines = 3 | ValKindId.List,
            text = 4 | ValKindId.Text,
            folder = 5,
            folders = 6 | ValKindId.List,
            subpath = 7,
            path = 8,
            paths = 9 | ValKindId.List,
            file = 10,
            files = 11 | ValKindId.List,
            url = 12,
            urls = 13 | ValKindId.List,
            truth = 14,
            int32 = 15,
            int64 = 16,
            uint32 = 17,
            uint64 = 18,
            float32 = 19,
            float64 = 20,
            date = 21,
            datetime = 22,
            cmd  = 23 | ValKindId.User,
            menu = 24 | ValKindId.User,
            args = 25 | ValKindId.User,
        }

        /// <summary>EnumCodes singleton for <see cref="ValId"/>.</summary>
        public static readonly EnumCodes ValCodes = EnumCodes.ForType(typeof(ValId));

        /// <summary>The ValFlag (shifted ByteCode) of the ValCodes entry at the index.</summary>
        public static ushort ValFlag_from_ValCodesIndex(int theValIndex)
        {
            return (ushort)(ValCodes.ByteCode(theValIndex) << Val_Shift);
        }

        /// <summary>Gets the ValId whose token matches; false (none) when no match.</summary>
        public static bool GotValId(string theToken, out ValId returnId)
        {
            int i = ValCodes.IndexOf(theToken);
            if (i >= 0)
            {
                returnId = (ValId)ValCodes.Code(i);
                return true;
            }
            returnId = 0;
            return false;
        }

        /// <summary>The token name of the ValId.</summary>
        public static string ValToken(ValId theVal) => ValCodes.NameAsToken(theVal);

        /// <summary>
        /// Runtime command-kind flags for the Cmd nibble (0 = not set).
        /// </summary>
        /// <remarks>
        /// WARNING: MAY BE PERSISTED - do not renumber; MUST stay synced
        /// with <see cref="CmdFlag"/>.
        /// </remarks>
        public enum CmdId : byte // 1 nibble
        {
            /// <summary>Context command.</summary>
            Context = 1 << 0,
            /// <summary>Trigger command.</summary>
            Trigger = 1 << 1,
            /// <summary>User command.</summary>
            User = 1 << 2,
            /// <summary>Command carries an operand.</summary>
            WithOperand = 1 << 3,

            /// <summary>Context command with operand.</summary>
            Context_WithOperand = Context | WithOperand,
            /// <summary>Trigger command with operand.</summary>
            Trigger_WithOperand = Trigger | WithOperand,
        }

        /// <summary>True when the Trigger flag is set.</summary>
        public static bool CmdId_IsTrigger(CmdId theId) => 0 != (theId & CmdId.Trigger);
        /// <summary>True when the Context flag is set.</summary>
        public static bool CmdId_IsContext(CmdId theId) => 0 != (theId & CmdId.Context);
        /// <summary>True when the WithOperand flag is set.</summary>
        public static bool CmdId_HasOperand(CmdId theId) => 0 != (theId & CmdId.WithOperand);
        /// <summary>True when Context without operand (read-only context command).</summary>
        public static bool CmdId_Context_RO(CmdId theId) => CmdId_IsContext(theId) & (0 == (theId & CmdId.WithOperand));
        /// <summary>True when Context with operand (read-write context command).</summary>
        public static bool CmdId_Context_RW(CmdId theId) => CmdId_IsContext(theId) & (0 != (theId & CmdId.WithOperand));


        //============= FOR easy Enum inclusions (no shifting needed)

        /// <summary>
        /// <see cref="ValId"/> values pre-shifted into the Val section, so
        /// local enums compose flags without writing shifts.
        /// </summary>
        /// <remarks>WARNING: MUST stay synced with <see cref="ValId"/>.</remarks>
        public enum ValFlag : ushort // for ValId
        {
            none = ValId.none << Val_Shift,
            word = ValId.word << Val_Shift,
            line = ValId.line << Val_Shift,
            lines = ValId.lines << Val_Shift,
            text = ValId.text << Val_Shift,
            folder = ValId.folder << Val_Shift,
            folders = ValId.folders << Val_Shift,
            subpath = ValId.subpath << Val_Shift,
            path = ValId.path << Val_Shift,
            paths = ValId.paths << Val_Shift,
            file = ValId.file << Val_Shift,
            files = ValId.files << Val_Shift,
            url = ValId.url << Val_Shift,
            urls = ValId.urls << Val_Shift,
            truth = ValId.truth << Val_Shift,
            int32 = ValId.int32 << Val_Shift,
            int64 = ValId.int64 << Val_Shift,
            uint32 = ValId.uint32 << Val_Shift,
            uint64 = ValId.uint64 << Val_Shift,
            float32 = ValId.float32 << Val_Shift,
            float64 = ValId.float64 << Val_Shift,
            date = ValId.date << Val_Shift,
            datetime = ValId.datetime << Val_Shift,
            cmd = ValId.cmd << Val_Shift,
            menu = ValId.menu << Val_Shift,
            args = ValId.args << Val_Shift,
        }

        /// <summary>
        /// <see cref="CmdId"/> values pre-shifted into the Cmd section.
        /// </summary>
        /// <remarks>WARNING: MUST stay synced with <see cref="CmdId"/>.</remarks>
        public enum CmdFlag : uint
        {
            Context = CmdId.Context << Cmd_Shift,
            Trigger = CmdId.Trigger << Cmd_Shift,
            User = CmdId.User << Cmd_Shift,

            WithOperand = CmdId.WithOperand << Cmd_Shift,

            Context_WithOperand = Context | WithOperand,
            Trigger_WithOperand = Trigger | WithOperand,
        }

        /// <summary>
        /// Grp nibble values 0-15 pre-shifted into the Grp section, for
        /// locally defined Grp enums.
        /// </summary>
        public enum GrpFlag : uint
        {
            Grp0 = 0 << Grp_Shift,
            Grp1 = 1 << Grp_Shift,
            Grp2 = 2 << Grp_Shift,
            Grp3 = 3 << Grp_Shift,
            Grp4 = 4 << Grp_Shift,
            Grp5 = 5 << Grp_Shift,
            Grp6 = 6 << Grp_Shift,
            Grp7 = 7 << Grp_Shift,
            Grp8 = 8u << Grp_Shift,
            Grp9 = 9u << Grp_Shift,
            GrpA = 10u << Grp_Shift,
            GrpB = 11u << Grp_Shift,
            GrpC = 12u << Grp_Shift,
            GrpD = 13u << Grp_Shift,
            GrpE = 14u << Grp_Shift,
            GrpF = 15u << Grp_Shift,
        }

    }
}
