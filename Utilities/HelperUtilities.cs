using Microsoft.Xna.Framework;

namespace SongbookOfTyria.Utilities
{
    internal static class HashUtility
    {
        public static uint GetStableHashCode(string str)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char c in str)
                {
                    hash = (hash ^ c) * 16777619;
                }
                return hash;
            }
        }
    }

    internal static class ImageSizeCalculator
    {
        public static Point CalculateAspectRatioSize(
            int originalWidth,
            int originalHeight,
            int maxWidth,
            int maxHeight)
        {
            if (originalWidth == 0 || originalHeight == 0)
            {
                return new Point(maxWidth, maxHeight);
            }

            var aspectRatio = (float)originalWidth / originalHeight;
            var targetWidth = maxWidth;
            var targetHeight = (int)(targetWidth / aspectRatio);

            if (targetHeight > maxHeight)
            {
                targetHeight = maxHeight;
                targetWidth = (int)(targetHeight * aspectRatio);
            }

            return new Point(targetWidth, targetHeight);
        }
    }
}
