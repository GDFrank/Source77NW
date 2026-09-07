// Copyright (c) GDFrank - 77NW.net. All rights reserved.
// Licensed under the MIT License.
// SPDX-License-Identifier: MIT

namespace Samples77NW
{
    /// <summary>
    /// Root partial for the Samples77NW UI grouping class - generic,
    /// toolkit-agnostic UI helpers plus wrapped controls (UIC was
    /// retired 2026-07-30, JOB.77 - UI absorbed its role, so there is
    /// no separate Controls class anymore). This is the ONE
    /// identifying declaration for the whole UI type (public static
    /// partial class UI); every other .cs file across UI's tiers
    /// (UI.NT.Base\, UI.NT.Forms\, future optional subsets, etc.)
    /// declares only `static partial class UI` - accessibility lives
    /// here alone. A simplified illustration scoped to what
    /// EnumCodingEG actually needs, not a full curation - see
    /// src\README.md and samples\README.md for the tier scheme,
    /// REPO.txt OPEN: UI TIER for the eventual full library curation.
    /// </summary>
    public static partial class UI
    {
    }
}
