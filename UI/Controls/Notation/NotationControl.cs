using System;
using System.Collections.Generic;

using Blish_HUD;
using Blish_HUD.Controls;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using MonoGame.Extended.BitmapFonts;

namespace SongbookOfTyria.UI.Controls.Notation
{
    public enum NoteFeedbackType
    {
        None,
        Correct,
        Wrong,
        Missed
    }

    public class NotationControl : Control
    {
        private const float BoldOffset = 0.5f;
        private const double HighlightTimeoutMs = 500.0;
        private const double FeedbackTimeoutMs = 600.0;
        private const int FeedbackIndicatorOffsetY = -4;
        private const int FeedbackIndicatorSize = 8;
        private static readonly Color HighlightColor = new Color(255, 255, 100) * 0.35f;
        private static readonly Color CorrectIndicatorColor = new Color(100, 255, 100);
        private static readonly Color WrongIndicatorColor = new Color(255, 80, 80);
        private static readonly Color MissedIndicatorColor = new Color(255, 180, 50);

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
        private readonly Dictionary<int, int> _noteIndexToSegment = new Dictionary<int, int>();
        private HashSet<int> _highlightedNoteIndices;
        private HashSet<int> _lastHighlightedNoteIndices;
        private DateTime _lastHighlightTime;
        private int _noteCounter;
        private Dictionary<int, NoteFeedbackInfo> _noteFeedback = new Dictionary<int, NoteFeedbackInfo>();
        private readonly List<FeedbackIndicator> _feedbackIndicators = new List<FeedbackIndicator>();
        private List<NotationMarker> _markers = new List<NotationMarker>();

        public bool SmoothScrolling { get; set; }

        public void AddSegment(string text, BitmapFont font, Color color, int x, int y, int charWidth = 0, int lineHeight = 0, Color? backgroundColor = null)
        {
            int segmentIndex = _segments.Count;
            _segments.Add(new TextSegment(text, font, color, x, y, charWidth, lineHeight, backgroundColor));

            if (text.Length == 1 && IsNoteCharacter(text[0]))
            {
                _noteIndexToSegment[_noteCounter] = segmentIndex;
                _noteCounter++;
            }
        }

        public void ClearSegments()
        {
            _segments.Clear();
            _noteIndexToSegment.Clear();
            _noteCounter = 0;
            _highlightedNoteIndices = null;
            _lastHighlightedNoteIndices = null;
            _noteFeedback.Clear();
            _feedbackIndicators.Clear();
            _markers.Clear();
        }

        public void SetMarkers(List<NotationMarker> markers)
        {
            _markers = markers ?? new List<NotationMarker>();
            Invalidate();
        }

        public void SetHighlightedNoteIndices(HashSet<int> noteIndices)
        {
            if (noteIndices != null && noteIndices.Count > 0)
            {
                _highlightedNoteIndices = noteIndices;
                _lastHighlightedNoteIndices = new HashSet<int>(noteIndices);
            }
            else
            {
                if (_highlightedNoteIndices != null && _highlightedNoteIndices.Count > 0)
                {
                    _lastHighlightTime = DateTime.UtcNow;
                }
                _highlightedNoteIndices = null;
            }
            Invalidate();
        }

        public void SetNoteFeedback(Dictionary<int, NoteFeedbackType> feedback)
        {
            var now = DateTime.UtcNow;
            var newFeedback = new Dictionary<int, NoteFeedbackInfo>();

            if (feedback != null)
            {
                foreach (var kvp in feedback)
                {
                    if (_noteFeedback.TryGetValue(kvp.Key, out var existingInfo) && existingInfo.Type == kvp.Value)
                    {
                        newFeedback[kvp.Key] = existingInfo;
                    }
                    else
                    {
                        newFeedback[kvp.Key] = new NoteFeedbackInfo(kvp.Value, now);

                        if (_noteIndexToSegment.TryGetValue(kvp.Key, out var segIdx) && segIdx < _segments.Count)
                        {
                            var segment = _segments[segIdx];
                            _feedbackIndicators.Add(new FeedbackIndicator(
                                kvp.Value,
                                segment.X + (segment.CharWidth / 2) - (FeedbackIndicatorSize / 2),
                                segment.Y + FeedbackIndicatorOffsetY,
                                now));
                        }
                    }
                }
            }

            _noteFeedback = newFeedback;
            Invalidate();
        }

        public void ClearNoteFeedback()
        {
            _noteFeedback.Clear();
            _feedbackIndicators.Clear();
            Invalidate();
        }

        private static bool IsNoteCharacter(char c)
        {
            return (c >= '1' && c <= '8') || (c >= '\u2460' && c <= '\u24FF');
        }

        private static bool IsAdjacentSymbol(char c)
        {
            return c == '/' || c == '[' || c == ']' || c == '(' || c == ')';
        }

        public override void DoUpdate(GameTime gameTime)
        {
            base.DoUpdate(gameTime);

            if (_lastHighlightedNoteIndices != null && _highlightedNoteIndices == null)
            {
                var elapsed = (DateTime.UtcNow - _lastHighlightTime).TotalMilliseconds;
                if (elapsed >= HighlightTimeoutMs)
                {
                    _lastHighlightedNoteIndices = null;
                    Invalidate();
                }
            }

            CleanupExpiredFeedbackIndicators();
        }

        private void CleanupExpiredFeedbackIndicators()
        {
            if (_feedbackIndicators.Count == 0) return;

            var now = DateTime.UtcNow;
            bool anyRemoved = false;

            for (int i = _feedbackIndicators.Count - 1; i >= 0; i--)
            {
                var elapsed = (now - _feedbackIndicators[i].Timestamp).TotalMilliseconds;
                if (elapsed >= FeedbackTimeoutMs)
                {
                    _feedbackIndicators.RemoveAt(i);
                    anyRemoved = true;
                }
            }

            if (anyRemoved)
            {
                Invalidate();
            }
        }

        protected override void Paint(SpriteBatch spriteBatch, Rectangle bounds)
        {
            float uiScale = Graphics.UIScaleMultiplier;
            var absoluteBounds = this.AbsoluteBounds;
            float opacity = this.AbsoluteOpacity();

            var highlightedSegments = BuildHighlightedSegmentSet();
            var highlightGroups = BuildContiguousHighlightGroups(highlightedSegments);

            int colorIdx = 0;
            for (int segIdx = 0; segIdx < _segments.Count; segIdx++)
            {
                var segment = _segments[segIdx];
                float absX = segment.X + absoluteBounds.X;
                float absY = segment.Y + absoluteBounds.Y;

                float alignedX = (int)(absX * uiScale) / uiScale;
                float finalY = SmoothScrolling ? absY : (int)(absY * uiScale) / uiScale;

                if (highlightedSegments != null && highlightedSegments.Contains(segIdx) && segment.CharWidth > 0 && segment.LineHeight > 0)
                {
                    bool prevHighlighted = segIdx > 0 && highlightedSegments.Contains(segIdx - 1);
                    bool nextHighlighted = segIdx < _segments.Count - 1 && highlightedSegments.Contains(segIdx + 1);

                    int hlX = prevHighlighted ? segment.X : segment.X - 1;
                    int hlWidth = segment.CharWidth + (prevHighlighted ? 0 : 1) + (nextHighlighted ? 0 : 1);

                    int hlY = segment.Y;
                    int hlHeight = segment.LineHeight;
                    if (highlightGroups.TryGetValue(segIdx, out var groupInfo))
                    {
                        hlY = groupInfo.MinY;
                        hlHeight = groupInfo.Height;
                    }

                    var hlRect = new Rectangle(hlX, hlY, hlWidth, hlHeight);
                    spriteBatch.DrawOnCtrl(this, ContentService.Textures.Pixel, hlRect, HighlightColor * opacity);
                }

                if (segment.BackgroundColor.HasValue && segment.CharWidth > 0 && segment.LineHeight > 0)
                {
                    var bgRect = new Rectangle(segment.X, segment.Y, segment.CharWidth, segment.LineHeight);
                    spriteBatch.DrawOnCtrl(this, ContentService.Textures.Pixel, bgRect, segment.BackgroundColor.Value * opacity);
                }

                if (ShowDebugBounds && segment.CharWidth > 0 && segment.LineHeight > 0)
                {
                    var debugColor = DebugColors[colorIdx % DebugColors.Length];
                    var debugRect = new Rectangle(segment.X, segment.Y, segment.CharWidth, segment.LineHeight);
                    spriteBatch.DrawOnCtrl(this, ContentService.Textures.Pixel, debugRect, debugColor);
                    colorIdx++;
                }

                var position = new Vector2(alignedX, finalY);
                var color = segment.Color * opacity;

                spriteBatch.DrawString(segment.Font, segment.Text, position + new Vector2(BoldOffset, 0), color);
                spriteBatch.DrawString(segment.Font, segment.Text, position, color);
            }

            DrawMarkers(spriteBatch, opacity);
            DrawFeedbackIndicators(spriteBatch, opacity);
        }

        private void DrawMarkers(SpriteBatch spriteBatch, float opacity)
        {
            if (_markers == null || _markers.Count == 0) return;

            const int markerWidth = 4;
            const int markerPadding = 2;
            const int markerVerticalInset = 2;

            foreach (var marker in _markers)
            {
                if (!_noteIndexToSegment.TryGetValue(marker.NoteIndex, out var segIdx)) continue;
                if (segIdx >= _segments.Count) continue;

                var segment = _segments[segIdx];
                if (segment.LineHeight <= 0) continue;

                var markerRect = new Rectangle(
                    segment.X - markerWidth - markerPadding,
                    segment.Y + markerVerticalInset,
                    markerWidth,
                    segment.LineHeight - markerVerticalInset * 2);

                spriteBatch.DrawOnCtrl(this, ContentService.Textures.Pixel, markerRect, marker.Color * opacity);
            }
        }

        private void DrawFeedbackIndicators(SpriteBatch spriteBatch, float opacity)
        {
            if (_feedbackIndicators.Count == 0) return;

            var now = DateTime.UtcNow;
            const int dotSize = 8;

            foreach (var indicator in _feedbackIndicators)
            {
                var elapsed = (now - indicator.Timestamp).TotalMilliseconds;
                if (elapsed >= FeedbackTimeoutMs) continue;

                float fadeProgress = (float)(elapsed / FeedbackTimeoutMs);
                float alpha = 1.0f - (fadeProgress * fadeProgress);

                float floatOffset = (float)(elapsed / FeedbackTimeoutMs) * -10f;

                Color indicatorColor;

                switch (indicator.Type)
                {
                    case NoteFeedbackType.Correct:
                        indicatorColor = CorrectIndicatorColor;
                        break;
                    case NoteFeedbackType.Wrong:
                        indicatorColor = WrongIndicatorColor;
                        break;
                    case NoteFeedbackType.Missed:
                        indicatorColor = MissedIndicatorColor;
                        break;
                    default:
                        continue;
                }

                var finalColor = indicatorColor * opacity * alpha;
                int dotY = Math.Max(2, (int)(indicator.Y + floatOffset));
                var dotRect = new Rectangle(indicator.X, dotY, dotSize, dotSize);

                DrawFilledCircle(spriteBatch, dotRect, finalColor);
            }
        }

        private void DrawFilledCircle(SpriteBatch spriteBatch, Rectangle bounds, Color color)
        {
            int centerX = bounds.X + bounds.Width / 2;
            int centerY = bounds.Y + bounds.Height / 2;
            int radius = bounds.Width / 2;

            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    if (x * x + y * y <= radius * radius)
                    {
                        var pixelRect = new Rectangle(centerX + x, centerY + y, 1, 1);
                        spriteBatch.DrawOnCtrl(this, ContentService.Textures.Pixel, pixelRect, color);
                    }
                }
            }
        }

        private HashSet<int> BuildHighlightedSegmentSet()
        {
            var activeIndices = _highlightedNoteIndices;

            if (activeIndices == null || activeIndices.Count == 0)
            {
                if (_lastHighlightedNoteIndices != null && _lastHighlightedNoteIndices.Count > 0)
                {
                    var elapsed = (DateTime.UtcNow - _lastHighlightTime).TotalMilliseconds;
                    if (elapsed < HighlightTimeoutMs)
                    {
                        activeIndices = _lastHighlightedNoteIndices;
                    }
                    else
                    {
                        _lastHighlightedNoteIndices = null;
                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }

            var result = new HashSet<int>();
            foreach (var noteIdx in activeIndices)
            {
                if (_noteIndexToSegment.TryGetValue(noteIdx, out var segIdx))
                {
                    result.Add(segIdx);
                    AddAdjacentSymbolSegments(segIdx, result);
                }
            }
            return result.Count > 0 ? result : null;
        }

        private Dictionary<int, HighlightGroupInfo> BuildContiguousHighlightGroups(HashSet<int> highlightedSegments)
        {
            var result = new Dictionary<int, HighlightGroupInfo>();
            if (highlightedSegments == null || highlightedSegments.Count == 0)
                return result;

            var sortedIndices = new List<int>(highlightedSegments);
            sortedIndices.Sort();

            var currentGroup = new List<int>();
            int minY = int.MaxValue;
            int maxBottom = 0;

            foreach (var segIdx in sortedIndices)
            {
                var segment = _segments[segIdx];
                bool isContiguous = currentGroup.Count == 0 || segIdx == currentGroup[currentGroup.Count - 1] + 1;

                int firstGroupY = currentGroup.Count == 0 ? segment.Y : _segments[currentGroup[0]].Y;
                bool sameLine = currentGroup.Count == 0 || Math.Abs(segment.Y - firstGroupY) <= 10;

                if (isContiguous && sameLine)
                {
                    currentGroup.Add(segIdx);
                    if (segment.Y < minY)
                        minY = segment.Y;
                    int bottom = segment.Y + segment.LineHeight;
                    if (bottom > maxBottom)
                        maxBottom = bottom;
                }
                else
                {
                    var info = new HighlightGroupInfo(minY, maxBottom - minY);
                    foreach (var idx in currentGroup)
                        result[idx] = info;

                    currentGroup.Clear();
                    currentGroup.Add(segIdx);
                    minY = segment.Y;
                    maxBottom = segment.Y + segment.LineHeight;
                }
            }

            if (currentGroup.Count > 0)
            {
                var info = new HighlightGroupInfo(minY, maxBottom - minY);
                foreach (var idx in currentGroup)
                    result[idx] = info;
            }

            return result;
        }

        private void AddAdjacentSymbolSegments(int noteSegmentIndex, HashSet<int> result)
        {
            for (int i = noteSegmentIndex - 1; i >= 0; i--)
            {
                var seg = _segments[i];
                if (seg.Text.Length == 1 && IsAdjacentSymbol(seg.Text[0]))
                {
                    result.Add(i);
                }
                else
                {
                    break;
                }
            }

            for (int i = noteSegmentIndex + 1; i < _segments.Count; i++)
            {
                var seg = _segments[i];
                if (seg.Text.Length == 1 && IsAdjacentSymbol(seg.Text[0]))
                {
                    result.Add(i);
                }
                else
                {
                    break;
                }
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

        private class NoteFeedbackInfo
        {
            public NoteFeedbackType Type { get; }
            public DateTime Timestamp { get; }

            public NoteFeedbackInfo(NoteFeedbackType type, DateTime timestamp)
            {
                Type = type;
                Timestamp = timestamp;
            }
        }

        private readonly struct FeedbackIndicator
        {
            public readonly NoteFeedbackType Type;
            public readonly int X;
            public readonly int Y;
            public readonly DateTime Timestamp;

            public FeedbackIndicator(NoteFeedbackType type, int x, int y, DateTime timestamp)
            {
                Type = type;
                X = x;
                Y = y;
                Timestamp = timestamp;
            }
        }

        private readonly struct HighlightGroupInfo
        {
            public readonly int MinY;
            public readonly int Height;

            public HighlightGroupInfo(int minY, int height)
            {
                MinY = minY;
                Height = height;
            }
        }
    }

    public class NotationMarker
    {
        public int NoteIndex { get; set; }
        public Color Color { get; set; }
    }
}
