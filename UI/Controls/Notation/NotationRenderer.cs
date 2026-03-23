using System;
using System.Text.RegularExpressions;

using Blish_HUD;
using Blish_HUD.Controls;

using Microsoft.Xna.Framework;

using MonoGame.Extended.BitmapFonts;

using SongbookOfTyria.UI.Utilities;

namespace SongbookOfTyria.UI.Controls.Notation
{
    public enum NotationFontSize
    {
        Size16,
        Size18,
        Size20,
        Size22,
        Size24,
        Size26,
        Size28
    }

    public class NotationRenderer
    {
        private static readonly Logger Logger = Logger.GetLogger<NotationRenderer>();
        private static readonly Regex ColorTagRegex = new Regex(@"^<c[=:](#?[\w]+)>(.*?)</c>", RegexOptions.Compiled);
        private static readonly Regex ColorTagNormalizeRegex = new Regex(@"(<c[=:][^>]+>)(.*?)(</c>)", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex BackgroundColorTagRegex = new Regex(@"^<bc[=:](#?[\w]+)>(.*?)</bc>", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex BackgroundColorNormalizeRegex = new Regex(@"(<bc[=:][^>]+>)(.*?)(</bc>)", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex EmptyColorTagRegex = new Regex(@"<c[=:][^>]+></c>", RegexOptions.Compiled);
        private static readonly Regex EmptyBackgroundColorTagRegex = new Regex(@"<bc[=:][^>]+></bc>", RegexOptions.Compiled);
        private static readonly Regex ColorTagOpenRegex = new Regex(@"^<c[=:](#?[\w]+)>", RegexOptions.Compiled);
        private static readonly Regex StripInnerColorTagsRegex = new Regex(@"<c[=:][^>]+>|</c>", RegexOptions.Compiled);

        private static BitmapFont _font16;
        private static BitmapFont _font18;
        private static BitmapFont _font20;
        private static BitmapFont _font22;
        private static BitmapFont _font24;
        private static BitmapFont _font26;
        private static BitmapFont _font28;
        private static BitmapFont _font30;
        private static BitmapFont _font32;

        // Monospace character widths from font files (xadvance value)
        private const int CharWidth16 = 9;   // EversonMono-16.fnt xadvance
        private const int CharWidth18 = 10;  // EversonMono-18.fnt xadvance
        private const int CharWidth20 = 11;  // EversonMono-20.fnt xadvance
        private const int CharWidth22 = 12;  // EversonMono-22.fnt xadvance
        private const int CharWidth24 = 14;  // EversonMono-24.fnt xadvance
        private const int CharWidth26 = 15;  // EversonMono-26.fnt xadvance
        private const int CharWidth28 = 16;  // EversonMono-28.fnt xadvance
        private const int CharWidth30 = 17;  // EversonMono-30.fnt xadvance
        private const int CharWidth32 = 18;  // EversonMono-32.fnt xadvance (for enclosed alphanumerics)

        // Notation layout constants (from font lineHeight values)
        private const int LineHeight16 = 22;  // EversonMono-16.fnt lineHeight
        private const int LineHeight18 = 25;  // EversonMono-18.fnt lineHeight
        private const int LineHeight20 = 27;  // EversonMono-20.fnt lineHeight
        private const int LineHeight22 = 30;  // EversonMono-22.fnt lineHeight
        private const int LineHeight24 = 33;  // EversonMono-24.fnt lineHeight
        private const int LineHeight26 = 35;  // EversonMono-26.fnt lineHeight
        private const int LineHeight28 = 38;  // EversonMono-28.fnt lineHeight
        private const int LinePadding = 0;
        private const int InitialYOffset = 10;
        private const int InitialXOffset = 15;
        private const int EnclosedAlphanumericExtraSpacing = 4;
        private const int ExtraCharacterSpacing = 1;  // Extra pixels between each character

        private readonly NotationControl _notationControl;
        private readonly BitmapFont _currentFont;
        private readonly BitmapFont _largerFont;
        private readonly int _currentCharWidth;
        private readonly int _largerCharWidth;
        private readonly int _currentLineHeight;
        private readonly int _explicitWidth;

        public static void InitializeFonts(Blish_HUD.Modules.Managers.ContentsManager contentsManager)
        {
            try
            {
                _font16 = BitmapFontLoader.Load(contentsManager, "fonts/EversonMono-16.fnt", "fonts/EversonMono-16_0.png");
                _font18 = BitmapFontLoader.Load(contentsManager, "fonts/EversonMono-18.fnt", "fonts/EversonMono-18_0.png");
                _font20 = BitmapFontLoader.Load(contentsManager, "fonts/EversonMono-20.fnt", "fonts/EversonMono-20_0.png");
                _font22 = BitmapFontLoader.Load(contentsManager, "fonts/EversonMono-22.fnt", "fonts/EversonMono-22_0.png");
                _font24 = BitmapFontLoader.Load(contentsManager, "fonts/EversonMono-24.fnt", "fonts/EversonMono-24_0.png");
                _font26 = BitmapFontLoader.Load(contentsManager, "fonts/EversonMono-26.fnt", "fonts/EversonMono-26_0.png");
                _font28 = BitmapFontLoader.Load(contentsManager, "fonts/EversonMono-28.fnt", "fonts/EversonMono-28_0.png");
                _font30 = BitmapFontLoader.Load(contentsManager, "fonts/EversonMono-30.fnt", "fonts/EversonMono-30_0.png");
                _font32 = BitmapFontLoader.Load(contentsManager, "fonts/EversonMono-32.fnt", "fonts/EversonMono-32_0.png");
                Logger.Info("Successfully loaded EversonMono fonts (16, 18, 20, 22, 24, 26, 28, 30, 32).");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to load EversonMono fonts: {Message}, rendering will be skipped.", ex.Message);
            }
        }

        public NotationControl Control => _notationControl;

        public NotationRenderer(Panel parentPanel, NotationFontSize fontSize, int width, int height)
        {
            _explicitWidth = width;

            _notationControl = new NotationControl
            {
                Parent = parentPanel,
                Location = Point.Zero,
                Size = new Point(width, height)
            };

            switch (fontSize)
            {
                case NotationFontSize.Size16:
                    _currentFont = _font16;
                    _currentCharWidth = CharWidth16;
                    _currentLineHeight = LineHeight16;
                    _largerFont = _font20;      // +4
                    _largerCharWidth = CharWidth20;
                    break;
                case NotationFontSize.Size18:
                    _currentFont = _font18;
                    _currentCharWidth = CharWidth18;
                    _currentLineHeight = LineHeight18;
                    _largerFont = _font22;      // +4
                    _largerCharWidth = CharWidth22;
                    break;
                case NotationFontSize.Size20:
                    _currentFont = _font20;
                    _currentCharWidth = CharWidth20;
                    _currentLineHeight = LineHeight20;
                    _largerFont = _font24;      // +4
                    _largerCharWidth = CharWidth24;
                    break;
                case NotationFontSize.Size22:
                    _currentFont = _font22;
                    _currentCharWidth = CharWidth22;
                    _currentLineHeight = LineHeight22;
                    _largerFont = _font26;      // +4
                    _largerCharWidth = CharWidth26;
                    break;
                case NotationFontSize.Size26:
                    _currentFont = _font26;
                    _currentCharWidth = CharWidth26;
                    _currentLineHeight = LineHeight26;
                    _largerFont = _font30;      // +4
                    _largerCharWidth = CharWidth30;
                    break;
                case NotationFontSize.Size28:
                    _currentFont = _font28;
                    _currentCharWidth = CharWidth28;
                    _currentLineHeight = LineHeight28;
                    _largerFont = _font32;      // +4
                    _largerCharWidth = CharWidth32;
                    break;
                case NotationFontSize.Size24:
                default:
                    _currentFont = _font24;
                    _currentCharWidth = CharWidth24;
                    _currentLineHeight = LineHeight24;
                    _largerFont = _font28;      // +4
                    _largerCharWidth = CharWidth28;
                    break;
            }
        }

        public void Render(string notation)
        {
            if (string.IsNullOrEmpty(notation) || _currentFont == null || _largerFont == null)
            {
                return;
            }

            // Replace heavy box drawing character with light version that exists in the font
            notation = notation.Replace('\u2501', '\u2500');

            // Normalize color tags: remove all line breaks inside <c>...</c> tags
            // This handles cases like "<c=#ff4500>content\n</c>" -> "<c=#ff4500>content</c>"
            notation = ColorTagNormalizeRegex.Replace(notation, match =>
            {
                var openTag = match.Groups[1].Value;
                var content = match.Groups[2].Value;
                var closeTag = match.Groups[3].Value;

                // Remove all line breaks from content within color tags
                content = content.Replace("\r\n", "").Replace("\r", "").Replace("\n", "");

                return openTag + content + closeTag;
            });

            // Normalize background color tags: remove all line breaks inside <bc>...</bc> tags
            notation = BackgroundColorNormalizeRegex.Replace(notation, match =>
            {
                var openTag = match.Groups[1].Value;
                var content = match.Groups[2].Value;
                var closeTag = match.Groups[3].Value;

                content = content.Replace("\r\n", "").Replace("\r", "").Replace("\n", "");

                return openTag + content + closeTag;
            });

            // Remove empty tags (loop handles nested empty tags like <c=x><bc=y></bc></c>)
            string previousNotation;
            do
            {
                previousNotation = notation;
                notation = EmptyColorTagRegex.Replace(notation, "");
                notation = EmptyBackgroundColorTagRegex.Replace(notation, "");
            } while (notation != previousNotation);

            // Split on all line ending types (\r\n, \r, \n) while preserving blank lines
            var lines = notation.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var currentY = InitialYOffset;

            foreach (var line in lines)
            {
                // Preserve blank lines by advancing Y position
                if (string.IsNullOrWhiteSpace(line))
                {
                    currentY += _currentLineHeight + LinePadding;
                    continue;
                }

                currentY = RenderLine(line, currentY);
                currentY += LinePadding;
            }

            _notationControl.Width = _explicitWidth;
            _notationControl.Height = currentY + _currentLineHeight;
            _notationControl.Invalidate();
        }

        private int RenderLine(string line, int startY)
        {
            var currentX = InitialXOffset;
            var currentY = startY;
            var remainingText = line;

            while (!string.IsNullOrEmpty(remainingText))
            {
                if (remainingText.Length > 0 && remainingText[0] == '<')
                {
                    if (TryRenderBackgroundColorTag(ref remainingText, ref currentX, ref currentY))
                    {
                        continue;
                    }

                    if (TryRenderColorTag(ref remainingText, ref currentX, ref currentY, null))
                    {
                        continue;
                    }
                }

                RenderPlainText(ref remainingText, ref currentX, ref currentY, null);
            }

            return currentY + _currentLineHeight;
        }

        private bool TryRenderBackgroundColorTag(
            ref string remainingText,
            ref int currentX,
            ref int currentY)
        {
            var match = BackgroundColorTagRegex.Match(remainingText);
            if (!match.Success)
            {
                return false;
            }

            var bgColorName = match.Groups[1].Value;
            var innerContent = match.Groups[2].Value;
            var backgroundColor = NotationColorHelper.GetColorFromName(bgColorName);

            // Process inner content which may contain color tags
            RenderContentWithBackground(innerContent, backgroundColor, ref currentX, ref currentY);
            remainingText = remainingText.Substring(match.Length);

            return true;
        }

        private void RenderContentWithBackground(string content, Color backgroundColor, ref int currentX, ref int currentY)
        {
            var remainingContent = content;

            while (!string.IsNullOrEmpty(remainingContent))
            {
                if (remainingContent.Length > 0 && remainingContent[0] == '<')
                {
                    if (TryRenderColorTag(ref remainingContent, ref currentX, ref currentY, backgroundColor))
                    {
                        continue;
                    }
                }

                RenderPlainText(ref remainingContent, ref currentX, ref currentY, backgroundColor);
            }
        }

        private bool TryRenderColorTag(
            ref string remainingText,
            ref int currentX,
            ref int currentY,
            Color? backgroundColor)
        {
            if (!TryExtractBalancedColorTag(remainingText, out var colorName, out var content, out var totalLength))
            {
                return false;
            }

            // Strip inner color tags - outer color has priority
            content = StripInnerColorTagsRegex.Replace(content, "");

            var color = NotationColorHelper.GetColorFromName(colorName);

            RenderTextSegment(content, color, ref currentX, ref currentY, backgroundColor);
            remainingText = remainingText.Substring(totalLength);

            return true;
        }

        private static bool TryExtractBalancedColorTag(string text, out string colorName, out string content, out int totalLength)
        {
            colorName = null;
            content = null;
            totalLength = 0;

            var openMatch = ColorTagOpenRegex.Match(text);
            if (!openMatch.Success)
            {
                return false;
            }

            colorName = openMatch.Groups[1].Value;
            var searchStart = openMatch.Length;
            var depth = 1;
            var pos = searchStart;

            while (pos < text.Length && depth > 0)
            {
                var nextOpen = text.IndexOf("<c", pos, StringComparison.Ordinal);
                var nextClose = text.IndexOf("</c>", pos, StringComparison.Ordinal);

                if (nextClose == -1)
                {
                    return false;
                }

                if (nextOpen != -1 && nextOpen < nextClose)
                {
                    var potentialTag = text.Substring(nextOpen);
                    if (ColorTagOpenRegex.IsMatch(potentialTag))
                    {
                        depth++;
                    }
                    pos = nextOpen + 2;
                }
                else
                {
                    depth--;
                    if (depth == 0)
                    {
                        content = text.Substring(searchStart, nextClose - searchStart);
                        totalLength = nextClose + 4;
                        return true;
                    }
                    pos = nextClose + 4;
                }
            }

            return false;
        }

        private void RenderPlainText(
            ref string remainingText,
            ref int currentX,
            ref int currentY,
            Color? backgroundColor)
        {
            var plainText = ExtractPlainText(ref remainingText);

            if (string.IsNullOrEmpty(plainText))
            {
                return;
            }

            // For whitespace without background color, just advance position
            // For whitespace with background color, render it to show the background
            if (string.IsNullOrWhiteSpace(plainText) && !backgroundColor.HasValue)
            {
                currentX += plainText.Length * _currentCharWidth;
                return;
            }

            RenderTextSegment(plainText, Color.White, ref currentX, ref currentY, backgroundColor);
        }

        private void RenderTextSegment(string text, Color color, ref int currentX, ref int currentY, Color? backgroundColor = null)
        {
            var maxLineWidth = GetMaxLineWidth();

            // Split text into words, preserving spaces as separate tokens
            var tokens = SplitIntoTokens(text);

            foreach (var token in tokens)
            {
                var tokenWidth = CalculateTokenWidth(token);

                // Check if we need to wrap before this token (but not for spaces at line start)
                if (currentX + tokenWidth > maxLineWidth && currentX > InitialXOffset)
                {
                    // If this is just a space and we're wrapping, skip it
                    if (token == " ")
                    {
                        continue;
                    }

                    currentY += _currentLineHeight + LinePadding;
                    currentX = InitialXOffset;
                }

                // Render each character in the token
                foreach (var c in token)
                {
                    RenderCharacter(c, color, ref currentX, currentY, backgroundColor);
                }
            }
        }

        private static string[] SplitIntoTokens(string text)
        {
            var tokens = new System.Collections.Generic.List<string>();
            var currentWord = new System.Text.StringBuilder();

            foreach (var c in text)
            {
                if (c == ' ')
                {
                    if (currentWord.Length > 0)
                    {
                        tokens.Add(currentWord.ToString());
                        currentWord.Clear();
                    }
                    tokens.Add(" ");
                }
                else
                {
                    currentWord.Append(c);
                }
            }

            if (currentWord.Length > 0)
            {
                tokens.Add(currentWord.ToString());
            }

            return tokens.ToArray();
        }

        private int CalculateTokenWidth(string token)
        {
            var width = 0;
            foreach (var c in token)
            {
                if (IsEnclosedAlphanumeric(c))
                {
                    width += _largerCharWidth + EnclosedAlphanumericExtraSpacing;
                }
                else
                {
                    width += _currentCharWidth + ExtraCharacterSpacing;
                }
            }
            return width;
        }

        private void RenderCharacter(char c, Color color, ref int currentX, int currentY, Color? backgroundColor)
        {
            var charStr = c.ToString();

            if (IsEnclosedAlphanumeric(c))
            {
                var verticalOffset = -2;
                _notationControl.AddSegment(charStr, _largerFont, color, currentX, currentY + verticalOffset, _largerCharWidth + EnclosedAlphanumericExtraSpacing, _currentLineHeight, backgroundColor);
                currentX += _largerCharWidth + EnclosedAlphanumericExtraSpacing;
            }
            else
            {
                _notationControl.AddSegment(charStr, _currentFont, color, currentX, currentY, _currentCharWidth + ExtraCharacterSpacing, _currentLineHeight, backgroundColor);
                currentX += _currentCharWidth + ExtraCharacterSpacing;
            }
        }

        private static bool IsEnclosedAlphanumeric(char c)
        {
            return c >= '\u2460' && c <= '\u24FF';
        }

        private string ExtractPlainText(ref string remainingText)
        {
            var nextTagIndex = remainingText.IndexOf('<');

            if (nextTagIndex > 0)
            {
                var plainText = remainingText.Substring(0, nextTagIndex);
                remainingText = remainingText.Substring(nextTagIndex);
                return plainText;
            }

            if (nextTagIndex == -1)
            {
                var plainText = remainingText;
                remainingText = string.Empty;
                return plainText;
            }

            var singleChar = remainingText.Substring(0, 1);
            remainingText = remainingText.Substring(1);
            return singleChar;
        }

        private int GetMaxLineWidth()
        {
            const int scrollbarMargin = 20;
            return _explicitWidth - InitialXOffset - scrollbarMargin;
        }
    }
}
