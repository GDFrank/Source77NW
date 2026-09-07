// Copyright (c) GDFrank - 77NW.net. All rights reserved.
// Licensed under the MIT License.
// SPDX-License-Identifier: MIT

using Source77NW;

namespace Samples77NW
{
    /// <summary>
    /// WinOP - reusable window-level operations, drawn from the same
    /// pool any App can pull from (see JOB.33.txt). Split out of
    /// EnumCodingEG's CmdOP.FontSize_Zoom_* members 2026-07-30. No cap
    /// field is given - the member name IS the caption via
    /// NameAsCaption; just mnu (accelerator letter) and tip.
    /// </summary>
    [EnumCodes("Reusable window-level operations", null, 1)]
    public enum WinOP : byte
    {
        [EnumInfo("|mnu B|tip Increase font size|", CodeDefId.action)]
        Bigger_Font = 0,

        [EnumInfo("|mnu S|tip Decrease font size|", CodeDefId.action)]
        Smaller_Font = 1,

        [EnumInfo("|mnu D|tip Reset font size to default|", CodeDefId.action)]
        Default_Font = 2,
    }
}
