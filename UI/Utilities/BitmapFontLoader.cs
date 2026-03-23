using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

using Blish_HUD;
using Blish_HUD.Modules.Managers;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using MonoGame.Extended.BitmapFonts;
using MonoGame.Extended.TextureAtlases;

namespace SongbookOfTyria.UI.Utilities
{
    public static class BitmapFontLoader
    {
        private static readonly Logger Logger = Logger.GetLogger(typeof(BitmapFontLoader));
        private static readonly ConcurrentDictionary<string, BitmapFont> _loadedFonts = new ConcurrentDictionary<string, BitmapFont>();

        public static BitmapFont Load(ContentsManager contentsManager, string fontPath, string texturePath, int letterSpacing = 0)
        {
            if (_loadedFonts.TryGetValue(fontPath, out var cachedFont))
            {
                return cachedFont;
            }

            try
            {
                Texture2D fontTexture;
                using (var textureStream = contentsManager.GetFileStream(texturePath))
                {
                    fontTexture = TextureUtil.FromStreamPremultiplied(textureStream);
                }

                string fontContent;
                using (var fontStream = contentsManager.GetFileStream(fontPath))
                using (var reader = new StreamReader(fontStream))
                {
                    fontContent = reader.ReadToEnd();
                }

                var fontData = ParseFontFile(fontContent);
                var font = CreateBitmapFont(fontData, fontTexture);
                font.LetterSpacing = letterSpacing;

                _loadedFonts.TryAdd(fontPath, font);
                Logger.Debug("Loaded and cached bitmap font: {FontPath}", fontPath);

                return font;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to load bitmap font from {Path}", fontPath);
                throw;
            }
        }

        public static void ClearCache()
        {
            _loadedFonts.Clear();
            Logger.Debug("Bitmap font cache cleared.");
        }

        private static FontData ParseFontFile(string content)
        {
            var fontData = new FontData();
            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                if (line.StartsWith("info "))
                {
                    fontData.Face = GetStringValue(line, "face");
                    fontData.Size = GetIntValue(line, "size");
                }
                else if (line.StartsWith("common "))
                {
                    fontData.LineHeight = GetIntValue(line, "lineHeight");
                    fontData.Base = GetIntValue(line, "base");
                    fontData.ScaleW = GetIntValue(line, "scaleW");
                    fontData.ScaleH = GetIntValue(line, "scaleH");
                }
                else if (line.StartsWith("char "))
                {
                    var charData = new CharData
                    {
                        Id = GetIntValue(line, "id"),
                        X = GetIntValue(line, "x"),
                        Y = GetIntValue(line, "y"),
                        Width = GetIntValue(line, "width"),
                        Height = GetIntValue(line, "height"),
                        XOffset = GetIntValue(line, "xoffset"),
                        YOffset = GetIntValue(line, "yoffset"),
                        XAdvance = GetIntValue(line, "xadvance"),
                        Page = GetIntValue(line, "page")
                    };
                    fontData.Characters.Add(charData);
                }
            }

            return fontData;
        }

        private static BitmapFont CreateBitmapFont(FontData fontData, Texture2D texture)
        {
            var regions = new List<BitmapFontRegion>();

            foreach (var charData in fontData.Characters)
            {
                var bounds = new Rectangle(charData.X, charData.Y, charData.Width, charData.Height);
                var textureRegion = new TextureRegion2D(texture, bounds);

                var region = new BitmapFontRegion(
                    textureRegion,
                    charData.Id,
                    charData.XOffset,
                    charData.YOffset,
                    charData.XAdvance);

                regions.Add(region);
            }

            return new BitmapFont(fontData.Face, regions, fontData.LineHeight);
        }

        private static int GetIntValue(string line, string key)
        {
            var pattern = $@"{key}=(-?\d+)";
            var match = Regex.Match(line, pattern);
            if (match.Success && InvariantUtil.TryParseInt(match.Groups[1].Value, out int result))
            {
                return result;
            }
            return 0;
        }

        private static string GetStringValue(string line, string key)
        {
            var pattern = $@"{key}=""([^""]*)""";
            var match = Regex.Match(line, pattern);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private class FontData
        {
            public string Face { get; set; } = string.Empty;
            public int Size { get; set; }
            public int LineHeight { get; set; }
            public int Base { get; set; }
            public int ScaleW { get; set; }
            public int ScaleH { get; set; }
            public List<CharData> Characters { get; } = new List<CharData>();
        }

        private class CharData
        {
            public int Id { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public int XOffset { get; set; }
            public int YOffset { get; set; }
            public int XAdvance { get; set; }
            public int Page { get; set; }
        }
    }
}
