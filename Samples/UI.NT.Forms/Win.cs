// Copyright (c) GDFrank - 77NW.net. All rights reserved.
// Licensed under the MIT License.
// SPDX-License-Identifier: MIT

using System.Windows.Forms;

namespace Samples77NW
{
    static partial class UI
    {
        /// <summary>
        /// A Form whose font, and every child control's font, is
        /// driven by <see cref="UI.Fonts"/> instead of WinForms' own
        /// DPI auto-scaling. Call <see cref="ApplyZoom"/> after the
        /// zoom factor changes to re-apply it to every control
        /// currently on the form.
        /// </summary>
        public class Win : Form
        {
            /// <summary>Sets AutoScaleMode.None (UI.Fonts drives sizing
            /// explicitly instead) and the initial zoomed font.</summary>
            public Win()
            {
                AutoScaleMode = AutoScaleMode.None;

                Font = UI.Fonts.GetFont();
            }

            /// <summary>Re-applies the current zoom font to this form and
            /// every control it contains, recursively.</summary>
            public void ApplyZoom()
            {
                Font = UI.Fonts.GetFont();

                _ApplyZoom(Controls);
            }

            private static void _ApplyZoom(Control.ControlCollection theControls)
            {
                foreach (Control xControl in theControls)
                {
                    xControl.Font = UI.Fonts.GetFont(xControl.Font.Style);

                    if (xControl.Controls.Count > 0)
                        _ApplyZoom(xControl.Controls);
                }
            }
        }
    }
}
