// Copyright (c) GDFrank - 77NW.net. All rights reserved.
// Licensed under the MIT License.
// SPDX-License-Identifier: MIT

using Source77NW;

namespace Samples77NW
{
    /// <summary>
    /// EditOP - reusable Edit-menu operations, drawn from the same pool
    /// any App can pull from (see JOB.33.txt). Split out of
    /// EnumCodingEG's CmdOP.Edit_* members 2026-07-30. No cap field is
    /// given - the member name IS the caption via NameAsCaption; just
    /// mnu (accelerator letter) and tip. Word_Wrap stays a toggle
    /// (CodeDefId.toggle), matching its original CmdOP behavior.
    /// </summary>
    [EnumCodes("Reusable Edit-menu operations", null, 1)]
    public enum EditOP : byte
    {
        [EnumInfo("|mnu t|tip Cut selected text|", CodeDefId.action)]
        Cut = 0,

        [EnumInfo("|mnu W|tip Toggle word wrap|", CodeDefId.toggle)]
        Word_Wrap = 1,
    }
}
