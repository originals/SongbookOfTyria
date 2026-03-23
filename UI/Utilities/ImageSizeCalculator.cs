using Microsoft.Xna.Framework;

namespace SongbookOfTyria.UI.Utilities
{
    public static class ImageSizeCalculator
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
