// Copyright (c) GDFrank - 77NW.net. All rights reserved.
// Licensed under the MIT License.
// SPDX-License-Identifier: MIT

using Source77NW;

namespace Samples77NW
{
    /// <summary>
    /// ViewOP - reserved pool for View-menu operations (see JOB.33.txt).
    /// PLACEHOLDER ONLY - one dummy member reserving the name/shape;
    /// not yet designed. G's one-line intent: "ops in view menu."
    /// </summary>
    [EnumCodes("Reserved: View-menu operations (placeholder)", null, 1)]
    public enum ViewOP : byte
    {
        [EnumInfo("|tip Placeholder - View-menu ops land here|", CodeDefId.action)]
        Placeholder = 0,
    }
}
