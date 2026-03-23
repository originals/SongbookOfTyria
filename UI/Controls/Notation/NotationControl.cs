using System.Collections.Generic;

using Blish_HUD;
using Blish_HUD.Controls;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using MonoGame.Extended.BitmapFonts;

namespace SongbookOfTyria.UI.Controls.Notation
{
    public class NotationControl : Control
    {
        private const float BoldOffset = 0.5f;

        // Debug: Toggle to show character bounds (set to true to visualize character cells)
        private const bool ShowDebugBounds = false;
        private static readonly Color[] DebugColors = new[]
        {
            Color.Red * 0.3f,
            Color.Green * 0.3f,
            Color.Blue * 0.3f,
            Color.Yellow * 0.3f,
            Color.Cyan * 0.3f,
            Color.Magenta * 0.3f
        };

        private readonly List<TextSegment> _segments = new List<TextSegment>();

        public bool SmoothScrolling { get; set; }

        public void AddSegment(string text, BitmapFont font, Color color, int x, int y, int charWidth = 0, int lineHeight = 0, Color? backgroundColor = null)
        {
            _segments.Add(new TextSegment(text, font, color, x, y, charWidth, lineHeight, backgroundColor));
        }

        public void ClearSegments()
        {
            _segments.Clear();
        }

        protected override void Paint(SpriteBatch spriteBatch, Rectangle bounds)
        {
            float uiScale = Graphics.UIScaleMultiplier;
            var absoluteBounds = this.AbsoluteBounds;
            float opacity = this.AbsoluteOpacity();

            int colorIdx = 0;
            foreach (var segment in _segments)
            {
                float absX = segment.X + absoluteBounds.X;
                float absY = segment.Y + absoluteBounds.Y;

                // Always align X to pixels for text sharpness
                float alignedX = (int)(absX * uiScale) / uiScale;

                // Only align Y to pixels when not smooth scrolling (sharper text when static)
                float finalY = SmoothScrolling ? absY : (int)(absY * uiScale) / uiScale;

                // Draw background color if specified
                if (segment.BackgroundColor.HasValue && segment.CharWidth > 0 && segment.LineHeight > 0)
                {
                    var bgRect = new Rectangle(
                        segment.X,
                        segment.Y,
                        segment.CharWidth,
                        segment.LineHeight);
                    spriteBatch.DrawOnCtrl(this, ContentService.Textures.Pixel, bgRect, segment.BackgroundColor.Value * opacity);
                }

                // Debug: Draw background box showing character cell bounds (use relative coords for DrawOnCtrl)
                if (ShowDebugBounds && segment.CharWidth > 0 && segment.LineHeight > 0)
                {
                    var debugColor = DebugColors[colorIdx % DebugColors.Length];
                    var debugRect = new Rectangle(
                        segment.X,
                        segment.Y,
                        segment.CharWidth,
                        segment.LineHeight);
                    spriteBatch.DrawOnCtrl(this, ContentService.Textures.Pixel, debugRect, debugColor);
                    colorIdx++;
                }

                var position = new Vector2(alignedX, finalY);
                var color = segment.Color * opacity;

                // Draw text with slight horizontal offset first (creates pseudo-bold effect)
                spriteBatch.DrawString(segment.Font, segment.Text, position + new Vector2(BoldOffset, 0), color);

                // Draw main text on top
                spriteBatch.DrawString(segment.Font, segment.Text, position, color);
            }
        }

        private readonly struct TextSegment
        {
            public readonly string Text;
            public readonly BitmapFont Font;
            public readonly Color Color;
            public readonly int X;
            public readonly int Y;
            public readonly int CharWidth;
            public readonly int LineHeight;
            public readonly Color? BackgroundColor;

            public TextSegment(string text, BitmapFont font, Color color, int x, int y, int charWidth = 0, int lineHeight = 0, Color? backgroundColor = null)
            {
                Text = text;
                Font = font;
                Color = color;
                X = x;
                Y = y;
                CharWidth = charWidth;
                LineHeight = lineHeight;
                BackgroundColor = backgroundColor;
            }
        }
    }
}
