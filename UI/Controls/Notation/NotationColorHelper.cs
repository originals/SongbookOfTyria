using Blish_HUD;

using Microsoft.Xna.Framework;

namespace SongbookOfTyria.UI.Controls.Notation
{
    public static class NotationColorHelper
    {
        private static readonly Color BlueColor = new Color(74, 144, 226);
        private static readonly Color RedColor = new Color(226, 74, 74);
        private static readonly Color GreenColor = new Color(74, 226, 144);

        public static Color GetColorFromName(string colorName)
        {
            if (string.IsNullOrEmpty(colorName))
            {
                return Color.White;
            }

            // Support hex color strings (e.g., "#FF0000" or "FF0000")
            if (colorName.StartsWith("#") || (colorName.Length == 6 && IsHexString(colorName)))
            {
                if (ColorUtil.TryParseHex(colorName, out Color hexColor))
                {
                    return hexColor;
                }
            }

            switch (colorName.ToLowerInvariant())
            {
                case "blue":
                    return BlueColor;
                case "red":
                    return RedColor;
                case "green":
                    return GreenColor;
                case "yellow":
                    return Color.Yellow;
                case "orange":
                    return Color.Orange;
                case "purple":
                    return Color.Purple;
                default:
                    return Color.White;
            }
        }

        private static bool IsHexString(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f')))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
