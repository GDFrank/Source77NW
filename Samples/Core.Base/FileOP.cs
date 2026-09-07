// Copyright (c) GDFrank - 77NW.net. All rights reserved.
// Licensed under the MIT License.
// SPDX-License-Identifier: MIT

using Source77NW;

namespace Samples77NW
{
    /// <summary>
    /// FileOP - reusable File-menu operations, drawn from the same pool
    /// any App can pull from (see JOB.33.txt). Split out of
    /// EnumCodingEG's CmdOP.File_* members 2026-07-30. No cap field is
    /// given - the member name IS the caption via NameAsCaption, since
    /// each name is already a single clean word (Open/Save/Exit); just
    /// mnu (accelerator letter) and tip. App-specific bindings (icon,
    /// ctrl shortcut, but caption) stay out of the shared pool and get
    /// layered on by the consuming App at the point of use, not baked
    /// in here - this is a resource of ops, not a UI-binding document.
    /// </summary>
    [EnumCodes("Reusable File-menu operations", null, 1)]
    public enum FileOP : byte
    {
        [EnumInfo("|mnu O|tip Open a document|", CodeDefId.action)]
        Open = 0,

        [EnumInfo("|mnu S|tip Save the document|", CodeDefId.action)]
        Save = 1,

        [EnumInfo("|mnu x|tip Exit the app|", CodeDefId.action)]
        Exit = 2,
    }
}
