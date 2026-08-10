using System;
using System.Drawing;
using System.Windows.Forms;

namespace GhostShell
{
    public static class GhostColorDialog
    {
        // ---- Persist custom colors across picks ----
        private static Color[] _customColors = new Color[16];

        /// <summary>
        /// Opens a color picker dialog. If the user confirms,
        /// invokes the callback with the chosen Color.
        /// </summary>
        public static void Pick(IWin32Window owner, Color initial, Action<Color> onPicked)
        {
            using var dialog = new ColorDialog
            {
                Color = initial,
                FullOpen = true,
                CustomColors = ToIntArray(_customColors),
            };

            if (dialog.ShowDialog(owner) == DialogResult.OK)
            {
                // Persist custom colors for next open
                _customColors = FromIntArray(dialog.CustomColors);
                onPicked(dialog.Color);
            }
        }

        /// <summary>
        /// Convenience overload — returns the color as normalized
        /// floats (r, g, b) for direct use with GhostEngine_SetClearColor.
        /// </summary>
        public static void PickFloat(IWin32Window owner, Color initial,
            Action<float, float, float> onPicked)
        {
            Pick(owner, initial, color =>
            {
                onPicked(
                    color.R / 255f,
                    color.G / 255f,
                    color.B / 255f);
            });
        }

        // -------------------------------------------------------
        // Helpers — ColorDialog stores custom colors as int[]
        // -------------------------------------------------------
        private static int[] ToIntArray(Color[] colors)
        {
            var result = new int[colors.Length];
            for (int i = 0; i < colors.Length; i++)
                result[i] = colors[i].ToArgb();
            return result;
        }

        private static Color[] FromIntArray(int[] ints)
        {
            var result = new Color[ints.Length];
            for (int i = 0; i < ints.Length; i++)
                result[i] = Color.FromArgb(ints[i]);
            return result;
        }
    }
}