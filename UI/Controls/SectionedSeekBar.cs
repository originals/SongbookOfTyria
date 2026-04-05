using System;
using System.Collections.Generic;
using System.Linq;

using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Input;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using MonoGame.Extended.BitmapFonts;

namespace SongbookOfTyria.UI.Controls
{
    public class SectionInfo
    {
        public string Label { get; set; }
        public double StartTime { get; set; }
        public double EndTime { get; set; }
    }

    public class MarkerInfo
    {
        public double Time { get; set; }
        public Color Color { get; set; }
        public int Id { get; set; }
    }

    public class SectionedSeekBar : Panel
    {
        private const int SeekBarControlHeight = 62;
        private const int TopMargin = 4;

        private static readonly Color[] MarkerColors = new[]
        {
            new Color(80, 180, 80),
            new Color(180, 180, 80),
            new Color(180, 80, 180),
            new Color(180, 120, 60),
        };

        private readonly List<SectionInfo> _sections = new List<SectionInfo>();
        private readonly List<MarkerInfo> _markers = new List<MarkerInfo>();
        private double _duration;
        private double _currentPosition;
        private int _nextMarkerId = 1;

        private SeekBarControl _seekBarControl;

        public event EventHandler<double> SeekRequested;
        public event EventHandler<MarkerInfo> MarkerAdded;
        public event EventHandler<MarkerInfo> MarkerRemoved;
        public event EventHandler<MarkerInfo> MarkerMoved;

        public double Duration
        {
            get => _duration;
            set
            {
                if (Math.Abs(_duration - value) > 0.001)
                {
                    _duration = value;
                    if (_seekBarControl != null) _seekBarControl.Duration = value;
                }
            }
        }

        public double CurrentPosition
        {
            get => _currentPosition;
            set
            {
                if (Math.Abs(_currentPosition - value) > 0.001)
                {
                    _currentPosition = value;
                    if (_seekBarControl != null) _seekBarControl.CurrentPosition = value;
                }
            }
        }

        public SectionedSeekBar()
        {
            Height = TopMargin + SeekBarControlHeight;
            BuildLayout();
        }

        private void BuildLayout()
        {
            _seekBarControl = new SeekBarControl
            {
                Width = Math.Max(100, Width - 10),
                Height = SeekBarControlHeight,
                Location = new Point(0, TopMargin),
                Parent = this
            };
            _seekBarControl.SeekRequested += OnSeekBarSeekRequested;
            _seekBarControl.MarkerRemoved += OnSeekBarMarkerRemoved;
            _seekBarControl.MarkerMoved += OnSeekBarMarkerMoved;

            Resized += OnResized;
        }

        private void OnSeekBarSeekRequested(object sender, double time)
        {
            SeekRequested?.Invoke(this, time);
        }

        private void OnSeekBarMarkerRemoved(object sender, int markerIndex)
        {
            RemoveMarker(markerIndex);
        }

        private void OnSeekBarMarkerMoved(object sender, int markerIndex)
        {
            if (markerIndex >= 0 && markerIndex < _markers.Count)
            {
                MarkerMoved?.Invoke(this, _markers[markerIndex]);
            }
        }

        private void OnResized(object sender, ResizedEventArgs e)
        {
            UpdateLayout();
        }

        private void UpdateLayout()
        {
            if (_seekBarControl != null)
            {
                _seekBarControl.Width = Math.Max(100, Width - 10);
            }
        }

        public void SetSections(IEnumerable<SectionInfo> sections)
        {
            _sections.Clear();
            if (sections != null)
            {
                _sections.AddRange(sections);
            }
            _seekBarControl?.SetSections(_sections);
        }

        public void SetMarkers(IEnumerable<MarkerInfo> markers)
        {
            _markers.Clear();
            if (markers != null)
            {
                _markers.AddRange(markers);
                _nextMarkerId = _markers.Count > 0 ? _markers.Max(m => m.Id) + 1 : 1;
            }
            _seekBarControl?.SetMarkers(_markers);
        }

        public IReadOnlyList<MarkerInfo> GetMarkers() => _markers.AsReadOnly();
        public IReadOnlyList<SectionInfo> GetSections() => _sections.AsReadOnly();

        public void AddMarker(double time)
        {
            var colorIndex = _markers.Count % MarkerColors.Length;
            var marker = new MarkerInfo
            {
                Time = time,
                Color = MarkerColors[colorIndex],
                Id = _nextMarkerId++
            };
            _markers.Add(marker);
            _seekBarControl?.SetMarkers(_markers);
            MarkerAdded?.Invoke(this, marker);
        }

        public void RemoveMarker(int index)
        {
            if (index >= 0 && index < _markers.Count)
            {
                var marker = _markers[index];
                _markers.RemoveAt(index);
                _seekBarControl?.SetMarkers(_markers);
                MarkerRemoved?.Invoke(this, marker);
            }
        }

        protected override void DisposeControl()
        {
            Resized -= OnResized;

            if (_seekBarControl != null)
            {
                _seekBarControl.SeekRequested -= OnSeekBarSeekRequested;
                _seekBarControl.MarkerRemoved -= OnSeekBarMarkerRemoved;
                _seekBarControl.MarkerMoved -= OnSeekBarMarkerMoved;
            }

            base.DisposeControl();
        }
    }

    internal class SeekBarControl : Control
    {
        private const int MarkerPinHeight = 20;
        private const int TrackBarHeight = 32;
        private const int TrackMarginVertical = 5;
        private const int TimeLabelWidth = 80;
        private const int SectionDividerWidth = 1;
        private const int MarkerPinWidth = 14;
        private const int SectionLabelHeight = 16;

        private static readonly Color TrackBackgroundColor = new Color(40, 40, 40);
        private static readonly Color TrackBorderColor = new Color(80, 80, 80);
        private static readonly Color ProgressColor = new Color(100, 100, 100, 180);
        private static readonly Color SectionDividerColor = new Color(60, 60, 60);
        private static readonly Color SectionLabelBackgroundColor = new Color(50, 50, 50);
        private static readonly Color SectionLabelTextColor = Color.White;
        private static readonly Color PositionLineColor = Color.White;
        private static readonly Color TimeLabelColor = new Color(180, 180, 180);

        private List<SectionInfo> _sections = new List<SectionInfo>();
        private List<MarkerInfo> _markers = new List<MarkerInfo>();
        private double _duration;
        private double _currentPosition;
        private bool _isDragging;
        private bool _isSeekingTrack;
        private int? _draggingMarkerIndex;

        public event EventHandler<double> SeekRequested;
        public event EventHandler<int> MarkerRemoved;
        public event EventHandler<int> MarkerMoved;

        public double Duration
        {
            get => _duration;
            set => _duration = value;
        }

        public double CurrentPosition
        {
            get => _currentPosition;
            set => _currentPosition = value;
        }

        private int MarkerTop => 0;
        private int TrackTop => MarkerPinHeight + TrackMarginVertical;

        public SeekBarControl()
        {
            Height = MarkerPinHeight + TrackMarginVertical + TrackBarHeight + TrackMarginVertical;
        }

        public void SetSections(IEnumerable<SectionInfo> sections)
        {
            _sections = sections?.ToList() ?? new List<SectionInfo>();
        }

        public void SetMarkers(IEnumerable<MarkerInfo> markers)
        {
            _markers = markers?.ToList() ?? new List<MarkerInfo>();
        }

        private Rectangle GetTrackBounds()
        {
            int trackLeft = 0;
            int trackWidth = Width - TimeLabelWidth - 10;
            return new Rectangle(trackLeft, TrackTop, trackWidth, TrackBarHeight);
        }

        private float TimeToX(double time, Rectangle trackBounds)
        {
            if (_duration <= 0) return trackBounds.X;
            var ratio = (float)(time / _duration);
            return trackBounds.X + ratio * trackBounds.Width;
        }

        private double XToTime(float x, Rectangle trackBounds)
        {
            if (trackBounds.Width <= 0) return 0;
            var ratio = MathHelper.Clamp((x - trackBounds.X) / trackBounds.Width, 0f, 1f);
            return ratio * _duration;
        }

        protected override void OnLeftMouseButtonPressed(MouseEventArgs e)
        {
            base.OnLeftMouseButtonPressed(e);

            var relativePos = e.MousePosition - AbsoluteBounds.Location;
            var trackBounds = GetTrackBounds();

            var markerIndex = GetMarkerAtPosition(relativePos, trackBounds);
            if (markerIndex.HasValue)
            {
                _isDragging = true;
                _draggingMarkerIndex = markerIndex.Value;
                return;
            }

            if (IsPositionInTrack(relativePos, trackBounds))
            {
                _isSeekingTrack = true;
                var seekTime = XToTime(relativePos.X, trackBounds);
                SeekRequested?.Invoke(this, seekTime);
            }
        }

        protected override void OnLeftMouseButtonReleased(MouseEventArgs e)
        {
            base.OnLeftMouseButtonReleased(e);
            _isDragging = false;
            _isSeekingTrack = false;
            _draggingMarkerIndex = null;
        }

        protected override void OnMouseMoved(MouseEventArgs e)
        {
            base.OnMouseMoved(e);

            var relativePos = e.MousePosition - AbsoluteBounds.Location;
            var trackBounds = GetTrackBounds();

            if (_isDragging && _draggingMarkerIndex.HasValue && _draggingMarkerIndex.Value < _markers.Count)
            {
                var newTime = XToTime(relativePos.X, trackBounds);
                _markers[_draggingMarkerIndex.Value].Time = MathHelper.Clamp((float)newTime, 0f, (float)_duration);
                MarkerMoved?.Invoke(this, _draggingMarkerIndex.Value);
            }
            else if (_isSeekingTrack)
            {
                var seekTime = XToTime(relativePos.X, trackBounds);
                SeekRequested?.Invoke(this, seekTime);
            }
        }

        protected override void OnRightMouseButtonPressed(MouseEventArgs e)
        {
            base.OnRightMouseButtonPressed(e);

            var relativePos = e.MousePosition - AbsoluteBounds.Location;
            var trackBounds = GetTrackBounds();

            var markerIndex = GetMarkerAtPosition(relativePos, trackBounds);
            if (markerIndex.HasValue)
            {
                MarkerRemoved?.Invoke(this, markerIndex.Value);
            }
        }

        private int? GetMarkerAtPosition(Point relativePos, Rectangle trackBounds)
        {
            int pinTop = MarkerTop;
            int pinBottom = pinTop + MarkerPinHeight + 5;

            for (int i = 0; i < _markers.Count; i++)
            {
                var marker = _markers[i];
                var markerX = TimeToX(marker.Time, trackBounds);
                var pinLeft = markerX - MarkerPinWidth / 2;
                var pinRight = markerX + MarkerPinWidth / 2;

                if (relativePos.X >= pinLeft && relativePos.X <= pinRight &&
                    relativePos.Y >= pinTop && relativePos.Y <= pinBottom)
                {
                    return i;
                }
            }

            return null;
        }

        private bool IsPositionInTrack(Point relativePos, Rectangle trackBounds)
        {
            return relativePos.X >= trackBounds.X && relativePos.X <= trackBounds.Right &&
                   relativePos.Y >= trackBounds.Y && relativePos.Y <= trackBounds.Bottom;
        }

        protected override void Paint(SpriteBatch spriteBatch, Rectangle bounds)
        {
            var trackBounds = GetTrackBounds();
            var pixel = ContentService.Textures.Pixel;

            DrawTrackBackground(spriteBatch, pixel, trackBounds);
            DrawProgress(spriteBatch, pixel, trackBounds);
            DrawSectionDividers(spriteBatch, pixel, trackBounds);
            DrawSectionLabels(spriteBatch, pixel, trackBounds);
            DrawMarkers(spriteBatch, pixel, trackBounds);
            DrawTimeLabel(spriteBatch, trackBounds);
        }

        private void DrawSectionLabels(SpriteBatch spriteBatch, Texture2D pixel, Rectangle trackBounds)
        {
            var font = GameService.Content.DefaultFont12;

            foreach (var section in _sections)
            {
                var sectionX = TimeToX(section.StartTime, trackBounds);
                var labelText = section.Label;
                var textSize = font.MeasureString(labelText);

                var labelWidth = (int)textSize.Width + 8;
                var labelX = Math.Max(0, (int)sectionX - 8);
                var labelY = MarkerTop;

                var bgRect = new Rectangle(labelX, labelY, labelWidth, SectionLabelHeight);
                spriteBatch.DrawOnCtrl(this, pixel, bgRect, SectionLabelBackgroundColor);
                DrawBorder(spriteBatch, pixel, bgRect, TrackBorderColor);

                var textY = labelY + (SectionLabelHeight - (int)textSize.Height) / 2 - 3;
                spriteBatch.DrawStringOnCtrl(this, labelText, font, new Rectangle(labelX + 4, textY, labelWidth, SectionLabelHeight), SectionLabelTextColor);
            }
        }

        private void DrawTrackBackground(SpriteBatch spriteBatch, Texture2D pixel, Rectangle trackBounds)
        {
            spriteBatch.DrawOnCtrl(this, pixel, trackBounds, TrackBackgroundColor);
            DrawBorder(spriteBatch, pixel, trackBounds, TrackBorderColor);
        }

        private void DrawBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color color)
        {
            spriteBatch.DrawOnCtrl(this, pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), color);
            spriteBatch.DrawOnCtrl(this, pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), color);
            spriteBatch.DrawOnCtrl(this, pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), color);
            spriteBatch.DrawOnCtrl(this, pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), color);
        }

        private void DrawProgress(SpriteBatch spriteBatch, Texture2D pixel, Rectangle trackBounds)
        {
            if (_duration <= 0) return;

            var progressRatio = (float)(_currentPosition / _duration);
            var progressWidth = (int)(trackBounds.Width * progressRatio);

            if (progressWidth > 0)
            {
                var progressRect = new Rectangle(trackBounds.X + 1, trackBounds.Y + 1, progressWidth - 1, trackBounds.Height - 2);
                spriteBatch.DrawOnCtrl(this, pixel, progressRect, ProgressColor);
            }

            var positionX = (int)TimeToX(_currentPosition, trackBounds);
            var lineRect = new Rectangle(positionX, trackBounds.Y, 2, trackBounds.Height);
            spriteBatch.DrawOnCtrl(this, pixel, lineRect, PositionLineColor);
        }

        private void DrawSectionDividers(SpriteBatch spriteBatch, Texture2D pixel, Rectangle trackBounds)
        {
            foreach (var section in _sections)
            {
                if (section.StartTime <= 0) continue;

                var dividerX = (int)TimeToX(section.StartTime, trackBounds);
                var dividerRect = new Rectangle(dividerX, trackBounds.Y, SectionDividerWidth, trackBounds.Height);
                spriteBatch.DrawOnCtrl(this, pixel, dividerRect, SectionDividerColor);
            }
        }

        private void DrawMarkers(SpriteBatch spriteBatch, Texture2D pixel, Rectangle trackBounds)
        {
            foreach (var marker in _markers)
            {
                var markerX = TimeToX(marker.Time, trackBounds);
                DrawMarkerPin(spriteBatch, pixel, markerX, marker.Color);
            }
        }

        private void DrawMarkerPin(SpriteBatch spriteBatch, Texture2D pixel, float centerX, Color pinColor)
        {
            int pinBodyHeight = MarkerPinHeight - 6;
            int pinLeft = (int)centerX - MarkerPinWidth / 2;

            var bodyRect = new Rectangle(pinLeft + 2, MarkerTop, MarkerPinWidth - 4, pinBodyHeight);
            spriteBatch.DrawOnCtrl(this, pixel, bodyRect, pinColor);

            var outlineColor = pinColor * 0.5f;
            DrawBorder(spriteBatch, pixel, bodyRect, outlineColor);

            for (int row = 0; row < 6; row++)
            {
                int y = MarkerTop + pinBodyHeight + row;
                int halfWidth = (6 - row) / 2;
                if (halfWidth > 0)
                {
                    var triangleRow = new Rectangle((int)centerX - halfWidth, y, halfWidth * 2, 1);
                    spriteBatch.DrawOnCtrl(this, pixel, triangleRow, pinColor);
                }
            }
        }

        private void DrawTimeLabel(SpriteBatch spriteBatch, Rectangle trackBounds)
        {
            var font = GameService.Content.DefaultFont12;
            var timeText = $"{FormatTime(_currentPosition)} / {FormatTime(_duration)}";

            var labelX = trackBounds.Right + 10;
            var labelY = trackBounds.Y + (trackBounds.Height - font.LineHeight) / 2 - 5;

            spriteBatch.DrawStringOnCtrl(this, timeText, font, new Rectangle(labelX, labelY, TimeLabelWidth, 20), TimeLabelColor);
        }

        private static string FormatTime(double seconds)
        {
            var ts = TimeSpan.FromSeconds(Math.Max(0, seconds));
            return ts.TotalMinutes >= 1
                ? $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}"
                : $"0:{ts.Seconds:D2}";
        }
    }
}
