using System;
using System.Collections.Generic;

using Blish_HUD;
using Blish_HUD.Controls;

using Microsoft.Xna.Framework;

using SongbookOfTyria.UI.Controls.Notation;

namespace SongbookOfTyria.UI.Controls
{
    public sealed class ViewOptionsPanel : FlowPanel
    {
        private const int AutoScrollButtonSize = 24;
        private const float DefaultScrollSpeed = 30f;
        private const float MinScrollSpeed = 10f;
        private const float MaxScrollSpeed = 100f;

        private static readonly Dictionary<string, NotationFontSize> FontSizeFromString = new Dictionary<string, NotationFontSize>
        {
            { "16", NotationFontSize.Size16 },
            { "18", NotationFontSize.Size18 },
            { "20", NotationFontSize.Size20 },
            { "22", NotationFontSize.Size22 },
            { "24", NotationFontSize.Size24 },
            { "26", NotationFontSize.Size26 },
            { "28", NotationFontSize.Size28 }
        };

        private static readonly Dictionary<NotationFontSize, string> FontSizeToString = new Dictionary<NotationFontSize, string>
        {
            { NotationFontSize.Size16, "16" },
            { NotationFontSize.Size18, "18" },
            { NotationFontSize.Size20, "20" },
            { NotationFontSize.Size22, "22" },
            { NotationFontSize.Size24, "24" },
            { NotationFontSize.Size26, "26" },
            { NotationFontSize.Size28, "28" }
        };

        private readonly Services.TextureService _textureService;

        private Dropdown _fontSizeDropdown;
        private GlowButton _autoScrollButton;
        private TrackBar _speedTrackBar;
        private Label _speedLabel;
        private Checkbox _hitDetectionCheckbox;
        private FlowPanel _hitDetectionRow;
        private Panel _hitDetectionSpacer;

        private NotationFontSize _currentFontSize;
        private bool _autoScrollEnabled;
        private float _scrollSpeed;
        private bool _hitDetectionEnabled;
        private bool _lastCollapsedState;
        private bool _isHandlingResize;

        public NotationFontSize FontSize
        {
            get => _currentFontSize;
            set
            {
                _currentFontSize = value;
                if (_fontSizeDropdown != null)
                {
                    _fontSizeDropdown.SelectedItem = GetFontSizeString(value);
                }
            }
        }

        public bool AutoScrollEnabled
        {
            get => _autoScrollEnabled;
            set
            {
                _autoScrollEnabled = value;
                if (_autoScrollButton != null)
                {
                    _autoScrollButton.Checked = value;
                }
            }
        }

        public float ScrollSpeed
        {
            get => _scrollSpeed;
            set
            {
                _scrollSpeed = value;
                if (_speedTrackBar != null)
                {
                    _speedTrackBar.Value = value;
                }
                UpdateSpeedLabel();
            }
        }

        public bool HitDetectionEnabled
        {
            get => _hitDetectionEnabled;
            set
            {
                _hitDetectionEnabled = value;
                if (_hitDetectionCheckbox != null)
                {
                    _hitDetectionCheckbox.Checked = value;
                }
            }
        }

        public event EventHandler<NotationFontSize> FontSizeChanged;

        public event EventHandler<bool> AutoScrollToggled;

        public event EventHandler<float> ScrollSpeedChanged;

        public event EventHandler<bool> HitDetectionToggled;

        public event EventHandler<bool> CollapsedChanged;

        public ViewOptionsPanel(
            int panelWidth,
            bool collapsed,
            NotationFontSize initialFontSize,
            bool initialAutoScroll,
            float initialScrollSpeed,
            bool initialHitDetection,
            Services.TextureService textureService)
        {
            _textureService = textureService;
            _currentFontSize = initialFontSize;
            _autoScrollEnabled = initialAutoScroll;
            _scrollSpeed = initialScrollSpeed > 0 ? initialScrollSpeed : DefaultScrollSpeed;
            _hitDetectionEnabled = initialHitDetection;
            _lastCollapsedState = collapsed;

            ShowBorder = true;
            Title = "View Options";
            CanCollapse = true;
            Width = panelWidth;
            HeightSizingMode = SizingMode.AutoSize;
            FlowDirection = ControlFlowDirection.SingleTopToBottom;
            ControlPadding = new Vector2(0, 8);
            OuterControlPadding = new Vector2(10, 10);

            BuildContent(panelWidth);

            Collapsed = collapsed;

            Resized += OnResized;
        }

        private void BuildContent(int panelWidth)
        {
            var contentWidth = panelWidth - 20;

            var fontSizeRow = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.SingleLeftToRight,
                Width = contentWidth,
                Height = 26,
                ControlPadding = new Vector2(5, 0),
                Parent = this
            };

            var textSizeLabelContainer = new Panel
            {
                Width = 70,
                Height = 26,
                Parent = fontSizeRow
            };

            new Label
            {
                Text = "Text Size:",
                Font = GameService.Content.DefaultFont14,
                Width = 70,
                AutoSizeHeight = true,
                Location = new Point(0, 2),
                Parent = textSizeLabelContainer
            };

            _fontSizeDropdown = new Dropdown
            {
                Width = 100,
                Parent = fontSizeRow
            };

            foreach (var size in FontSizeFromString.Keys)
            {
                _fontSizeDropdown.Items.Add(size);
            }

            _fontSizeDropdown.SelectedItem = GetFontSizeString(_currentFontSize);
            _fontSizeDropdown.ValueChanged += OnFontSizeChanged;

            var autoscrollRow = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.SingleLeftToRight,
                Width = contentWidth,
                Height = 26,
                ControlPadding = new Vector2(3, 0),
                Parent = this
            };

            new Label
            {
                Text = "Scroll:",
                Font = GameService.Content.DefaultFont14,
                Width = 45,
                AutoSizeHeight = true,
                Parent = autoscrollRow
            };

            var buttonContainer = new Panel
            {
                Width = AutoScrollButtonSize,
                Height = 26,
                Parent = autoscrollRow
            };

            _autoScrollButton = new GlowButton
            {
                Icon = _textureService.GetPlayIcon(),
                ActiveIcon = _textureService.GetPauseIcon(),
                BasicTooltipText = "Toggle Autoscroll",
                ToggleGlow = true,
                Checked = _autoScrollEnabled,
                Size = new Point(AutoScrollButtonSize, AutoScrollButtonSize),
                Location = new Point(0, -1),
                Parent = buttonContainer
            };
            _autoScrollButton.Click += OnAutoScrollButtonClicked;

            var trackBarContainer = new Panel
            {
                Width = 100,
                Height = 26,
                Parent = autoscrollRow
            };

            _speedTrackBar = new TrackBar
            {
                MinValue = MinScrollSpeed,
                MaxValue = MaxScrollSpeed,
                Value = _scrollSpeed,
                Width = 100,
                Location = new Point(0, 3),
                Parent = trackBarContainer
            };
            _speedTrackBar.ValueChanged += OnSpeedTrackBarChanged;

            var speedLabelContainer = new Panel
            {
                Width = 30,
                Height = 26,
                Parent = autoscrollRow
            };

            _speedLabel = new Label
            {
                Text = $"{(int)_scrollSpeed}",
                Font = GameService.Content.DefaultFont14,
                Width = 30,
                AutoSizeHeight = true,
                HorizontalAlignment = HorizontalAlignment.Center,
                Location = new Point(0, 3),
                Parent = speedLabelContainer
            };

            //_hitDetectionRow = new FlowPanel
            //{
            //    FlowDirection = ControlFlowDirection.SingleLeftToRight,
            //    Width = contentWidth,
            //    Height = 26,
            //    ControlPadding = new Vector2(5, 0),
            //    Visible = false,
            //    Parent = this
            //};

            //var hitDetectionLabelContainer = new Panel
            //{
            //    Width = 90,
            //    Height = 26,
            //    Parent = _hitDetectionRow
            //};

            //new Label
            //{
            //    Text = "Hit Detection:",
            //    Font = GameService.Content.DefaultFont14,
            //    Width = 90,
            //    AutoSizeHeight = true,
            //    Location = new Point(0, 2),
            //    Parent = hitDetectionLabelContainer
            //};

            //_hitDetectionCheckbox = new Checkbox
            //{
            //    Text = "",
            //    Checked = _hitDetectionEnabled,
            //    Enabled = false,
            //    BasicTooltipText = "Coming soon! Hit detection will show visual feedback for notes played during practice mode.",
            //    Height = 26,
            //    Parent = _hitDetectionRow
            //};
            //_hitDetectionCheckbox.CheckedChanged += OnHitDetectionCheckboxChanged;

            //_hitDetectionSpacer = new Panel
            //{
            //    Width = contentWidth,
            //    Height = 1,
            //    Visible = false,
            //    Parent = this
            //};
        }

        private void OnFontSizeChanged(object sender, ValueChangedEventArgs e)
        {
            if (FontSizeFromString.TryGetValue(e.CurrentValue, out var fontSize))
            {
                _currentFontSize = fontSize;
                FontSizeChanged?.Invoke(this, fontSize);
            }
        }

        private void OnAutoScrollButtonClicked(object sender, Blish_HUD.Input.MouseEventArgs e)
        {
            _autoScrollEnabled = !_autoScrollEnabled;
            _autoScrollButton.Checked = _autoScrollEnabled;
            AutoScrollToggled?.Invoke(this, _autoScrollEnabled);
        }

        private void OnSpeedTrackBarChanged(object sender, ValueEventArgs<float> e)
        {
            _scrollSpeed = e.Value;
            UpdateSpeedLabel();
            ScrollSpeedChanged?.Invoke(this, _scrollSpeed);
        }

        private void OnHitDetectionCheckboxChanged(object sender, CheckChangedEvent e)
        {
            _hitDetectionEnabled = e.Checked;
            HitDetectionToggled?.Invoke(this, _hitDetectionEnabled);
        }

        public void SetPracticeModeActive(bool isPracticeMode)
        {
            if (_hitDetectionRow != null)
            {
                _hitDetectionRow.Parent = null;
            }

            if (_hitDetectionSpacer != null)
            {
                _hitDetectionSpacer.Parent = null;
            }

            if (isPracticeMode)
            {
                if (_hitDetectionRow != null)
                {
                    _hitDetectionRow.Visible = true;
                    _hitDetectionRow.Parent = this;
                }

                if (_hitDetectionSpacer != null)
                {
                    _hitDetectionSpacer.Visible = true;
                    _hitDetectionSpacer.Parent = this;
                }
            }

            Invalidate();
        }

        private void UpdateSpeedLabel()
        {
            if (_speedLabel != null)
            {
                _speedLabel.Text = $"{(int)_scrollSpeed}";
            }
        }

        private static string GetFontSizeString(NotationFontSize fontSize)
        {
            return FontSizeToString.TryGetValue(fontSize, out var sizeString) ? sizeString : "20";
        }

        public void RebuildContent()
        {
            if (_fontSizeDropdown != null)
            {
                _fontSizeDropdown.ValueChanged -= OnFontSizeChanged;
            }

            if (_autoScrollButton != null)
            {
                _autoScrollButton.Click -= OnAutoScrollButtonClicked;
            }

            if (_speedTrackBar != null)
            {
                _speedTrackBar.ValueChanged -= OnSpeedTrackBarChanged;
            }

            if (_hitDetectionCheckbox != null)
            {
                _hitDetectionCheckbox.CheckedChanged -= OnHitDetectionCheckboxChanged;
            }

            DisposeOrphanedControls();

            var children = Children.ToArray();
            foreach (var child in children)
            {
                child.Parent = null;
                child.Dispose();
            }

            _fontSizeDropdown = null;
            _autoScrollButton = null;
            _speedTrackBar = null;
            _speedLabel = null;
            _hitDetectionCheckbox = null;
            _hitDetectionRow = null;
            _hitDetectionSpacer = null;

            BuildContent(Width);

            Invalidate();
        }

        private void OnResized(object sender, ResizedEventArgs e)
        {
            if (_isHandlingResize)
            {
                return;
            }

            _isHandlingResize = true;

            try
            {
                if (Collapsed != _lastCollapsedState)
                {
                    _lastCollapsedState = Collapsed;
                    CollapsedChanged?.Invoke(this, Collapsed);
                }
            }
            finally
            {
                _isHandlingResize = false;
            }
        }

        private void DisposeOrphanedControls()
        {
            if (_hitDetectionRow != null && _hitDetectionRow.Parent == null)
            {
                _hitDetectionRow.Dispose();
                _hitDetectionRow = null;
            }

            if (_hitDetectionSpacer != null && _hitDetectionSpacer.Parent == null)
            {
                _hitDetectionSpacer.Dispose();
                _hitDetectionSpacer = null;
            }
        }

        protected override void DisposeControl()
        {
            Resized -= OnResized;

            if (_fontSizeDropdown != null)
            {
                _fontSizeDropdown.ValueChanged -= OnFontSizeChanged;
            }

            if (_autoScrollButton != null)
            {
                _autoScrollButton.Click -= OnAutoScrollButtonClicked;
            }

            if (_speedTrackBar != null)
            {
                _speedTrackBar.ValueChanged -= OnSpeedTrackBarChanged;
            }

            if (_hitDetectionCheckbox != null)
            {
                _hitDetectionCheckbox.CheckedChanged -= OnHitDetectionCheckboxChanged;
            }

            DisposeOrphanedControls();

            base.DisposeControl();
        }
    }
}
