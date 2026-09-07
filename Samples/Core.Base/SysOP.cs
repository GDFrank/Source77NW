// Copyright (c) GDFrank - 77NW.net. All rights reserved.
// Licensed under the MIT License.
// SPDX-License-Identifier: MIT

using Source77NW;

namespace Samples77NW
{
    /// <summary>
    /// SysOP - reserved pool for system operations (see JOB.33.txt).
    /// PLACEHOLDER ONLY - one dummy member reserving the name/shape;
    /// not yet designed. G's one-line intent: "system ops (whatever
    /// they would be)." G separately flagged SysOP as a candidate for
    /// Grp/GrpId bit-flag sub-grouping (Ã  la AppLab's OpsZZ.CodeGrp)
    /// if/when it grows enough internal variety to warrant it - not
    /// yet decided either way.
    /// </summary>
    [EnumCodes("Reserved: system operations (placeholder)", null, 1)]
    public enum SysOP : byte
    {
        [EnumInfo("|tip Placeholder - system ops land here|", CodeDefId.action)]
        Placeholder = 0,
    }
}
