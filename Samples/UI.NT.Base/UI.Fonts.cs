// Copyright (c) GDFrank - 77NW.net. All rights reserved.
// Licensed under the MIT License.
// SPDX-License-Identifier: MIT

using System;
using System.Drawing;

namespace Samples77NW
{
    static partial class UI
    {
        /// <summary>
        /// A user-adjustable font zoom factor, applied on top of the
        /// system default font size and persisted via
        /// <see cref="Config.SettingId.ZoomFont"/>. A simplified
        /// illustration of AppLab's own UI.Fonts.cs pattern - no
        /// per-context font table, no OS accessibility multiplier, no
        /// font-family lookup - just the zoom mechanics this sample
        /// needs.
        /// </summary>
        public static class Fonts
        {
            /// <summary>Largest allowed zoom factor.</summary>
            public const float ZoomMax = 3f;
            /// <summary>Smallest allowed zoom factor.</summary>
            public const float ZoomMin = 0.7f;
            /// <summary>Zoom factor representing 100%.</summary>
            public const float ZoomDefault = 1.0f;
            /// <summary>Step size applied by <see cref="DidBumpUserZoomFactor"/>.</summary>
            public const float ZoomBump = 0.1f;

            private static float _UserZoomFactor = ZoomDefault;

            /// <summary>The current zoom factor (1.0 = 100%).</summary>
            public static float UserZoomFactor => _UserZoomFactor;

            static Fonts()
            {
                string sStored = Config.Setting(Config.SettingId.ZoomFont);

                if (!float.TryParse(sStored, out _UserZoomFactor))
                    _UserZoomFactor = ZoomDefault;

                _Clamp(ref _UserZoomFactor);
            }

            /// <summary>A new Font sized off SystemFonts.DefaultFont, scaled
            /// by the current zoom factor. Caller disposes.</summary>
            public static Font GetFont(FontStyle theStyle = FontStyle.Regular)
            {
                float fSize = SystemFonts.DefaultFont.Size * _UserZoomFactor;

                return new Font(SystemFonts.DefaultFont.FontFamily, fSize, theStyle);
            }

            /// <summary>Bumps the zoom factor one <see cref="ZoomBump"/> step
            /// (clamped to ZoomMin/ZoomMax) and persists it. False when
            /// already at the limit in that direction.</summary>
            public static bool DidBumpUserZoomFactor(bool bigger_else_smaller)
            {
                float fNew = _UserZoomFactor + (bigger_else_smaller ? ZoomBump : -ZoomBump);

                _Clamp(ref fNew);

                if (fNew == _UserZoomFactor) return false;

                _UserZoomFactor = fNew;

                _Save();

                return true;
            }

            /// <summary>Resets the zoom factor to ZoomDefault and persists.</summary>
            public static void SetDefaultUserZoomFactor()
            {
                _UserZoomFactor = ZoomDefault;

                _Save();
            }

            private static void _Clamp(ref float theFactor)
            {
                theFactor = (float)Math.Round(theFactor, 1);

                if (theFactor < ZoomMin) theFactor = ZoomMin;
                else if (theFactor > ZoomMax) theFactor = ZoomMax;
            }

            private static void _Save()
            {
                // Omit the setting entirely at default, so an untouched
                // Settings.txt implies "default zoom" (AppLab convention).
                string sVal = _UserZoomFactor == ZoomDefault ? null : _UserZoomFactor.ToString();

                Config.Set(Config.SettingId.ZoomFont, sVal);
            }
        }
    }
}
