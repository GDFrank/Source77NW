// Copyright (c) GDFrank - 77NW.net. All rights reserved.
// Licensed under the MIT License.
// SPDX-License-Identifier: MIT

using Source77NW;

namespace Samples77NW
{
    /// <summary>
    /// AppOP - reserved pool for domain-level operations, shared across
    /// the whole Samples77NW domain (see JOB.33.txt). PLACEHOLDER ONLY -
    /// one dummy member reserving the name/shape; not yet designed.
    /// </summary>
    /// <remarks>
    /// NAMING SETTLED 2026-07-30 (G): "AppOP" is domain-level - PUBLIC,
    /// shared, lives here in Core\. "ExeOP" is the opposite - project-
    /// PRIVATE (one per exe, internal), living IN each project's own
    /// folder (e.g. samples\App.EnumCodingEG\ExeOP.cs), never in Core\.
    /// The earlier name-overlap flag on this file is resolved by this
    /// split - see done\JOB.33.txt for the history.
    /// </remarks>
    [EnumCodes("Reserved: domain-level operations (placeholder)", null, 1)]
    public enum AppOP : byte
    {
        [EnumInfo("|tip Placeholder - domain-level ops land here|", CodeDefId.action)]
        Placeholder = 0,
    }
}
