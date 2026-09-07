// Copyright (c) GDFrank - 77NW.net. All rights reserved.
// Licensed under the MIT License.
// SPDX-License-Identifier: MIT

using System;
using System.Windows.Forms;
using Source77NW;

namespace Samples77NW
{
    static partial class UI
    {
        /// <summary>
        /// A Button sized to its own text via WinForms' built-in
        /// AutoSize and driven by <see cref="UI.Fonts"/> - a
        /// simplified stand-in for AppLab's custom
        /// Graphics.MeasureString shrink-wrap sizing (same idea;
        /// WinForms does the measuring here instead).
        /// </summary>
        /// <remarks>
        /// ResCode-aware (2026-07-30, per G): when constructed from an
        /// Enum code, the button remembers it (<see cref="Code"/>) and
        /// can re-pull its own caption on demand via
        /// <see cref="RefreshFromResCode"/> - the whole point of
        /// ResCode being a tiny struct (just a cache index + code
        /// index, not stored text): a lingo switch needs nothing more
        /// than walking the visible controls and calling this on each
        /// one, no stored strings to invalidate, no per-control
        /// language state to track.
        /// </remarks>
        public class BaseButton : Button
        {
            /// <summary>The Enum code this button displays, or null when
            /// this instance isn't ResCode-bound (parameterless ctor).</summary>
            public Enum Code { get; private set; }

            /// <summary>Turns on AutoSize and sets the initial zoomed
            /// font. Not ResCode-bound - use the (Enum) overload for
            /// that.</summary>
            public BaseButton()
            {
                AutoSize = true;
                AutoSizeMode = AutoSizeMode.GrowAndShrink;

                Font = UI.Fonts.GetFont();
            }

            /// <summary>As the parameterless constructor, then binds to
            /// theCode (Tag = theCode too, so existing Tag-keyed click
            /// dispatch - e.g. MainForm's _OnCmd - needs no changes) and
            /// pulls the initial caption via RefreshFromResCode.</summary>
            public BaseButton(Enum theCode) : this()
            {
                Code = theCode;
                Tag = theCode;

                RefreshFromResCode();
            }

            /// <summary>Re-pulls this button's caption from ResCode - the
            /// entire lingo-switch story for a bound button: call this
            /// again after the active lingo changes (ResCode.ActiveLingoNum),
            /// nothing else needs invalidating. No-op when this instance
            /// isn't bound (Code is null).</summary>
            public void RefreshFromResCode()
            {
                if (Code == null) return;

                Text = ResCode.For(Code).Val(CodeValId.but).ToString();
            }
        }
    }
}
