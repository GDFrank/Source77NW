// Copyright (c) GDFrank - 77NW.net. All rights reserved.
// Licensed under the MIT License.
// SPDX-License-Identifier: MIT

using Source77NW;

namespace Samples77NW
{
    /// <summary>
    /// ToolsOP - reserved pool for Tools-menu operations (see JOB.33.txt).
    /// PLACEHOLDER ONLY - one dummy member reserving the name/shape;
    /// not yet designed. G's one-line intent: "tools menu."
    /// </summary>
    [EnumCodes("Reserved: Tools-menu operations (placeholder)", null, 1)]
    public enum ToolsOP : byte
    {
        [EnumInfo("|tip Placeholder - Tools-menu ops land here|", CodeDefId.action)]
        Placeholder = 0,
    }
}
