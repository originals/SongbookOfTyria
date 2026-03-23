using System;
using System.Linq;
using System.Net;

using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;

using Microsoft.Xna.Framework;

using SongbookOfTyria.Models;
using SongbookOfTyria.Services;
using SongbookOfTyria.UI.Controls;
using SongbookOfTyria.UI.Controls.Notation;

namespace SongbookOfTyria.UI.Windows
{
    public class TabDetailWindow : StandardWindow
    {
        private const int WindowWidth = 935;
        private const int WindowHeight = 920;
        private const int WindowMaxWidth = 1920;
        private const int WindowMaxHeight = 1440;
        private const int LeftPanelWidth = 260;
        private const int LeftPanelCollapsedWidth = 45;
        private const int DefaultSpacing = 10;
        private const float ScrollSpeedMultiplier = 0.21f;
        private const int WindowBackgroundAssetId = 155985;
        private const string TabDetailWindowIdPrefix = "SongbookOfTyria_TabDetail_";
        private const int AudioSectionExpandedHeight = 105;
        private const int PianoKeybindsSectionExpandedHeight = 120;
        private const int CollapsedSectionHeight = 35;
        private const int NotationSectionHeaderHeight = 28;
        private const int SectionWidthPadding = 15;
        private const int NotationPanelPadding = 3;

        private static readonly Logger Logger = Logger.GetLogger<TabDetailWindow>();

        private readonly MusicTab _musicTab;
        private readonly TextureService _textureService;
        private readonly AudioService _audioService;
        private readonly UserSettingsService _userSettingsService;

        private FlowPanel _leftPanel;
        private FlowPanel _rightPanel;
        private TabDetailsPanel _detailsSection;
        private ViewOptionsPanel _controlsSection;
        private FlowPanel _audioSection;
        private PianoKeybindsPanel _pianoKeybindsSection;
        private FlowPanel _notationSection;
        private Panel _notationContentPanel;
        private PianoKeybinds _pianoKeybinds;
        private NotationFontSize _currentFontSize = NotationFontSize.Size20;
        private bool _detailsCollapsed;
        private bool _viewOptionsCollapsed;
        private bool _audioPlayerCollapsed;
        private bool _pianoKeybindsCollapsed;
        private bool _autoScrollEnabled;
        private float _scrollSpeed = 30f;
        private float _accumulatedScrollOffset;
        private Scrollbar _cachedScrollbar;
        private NotationRenderer _notationRenderer;

        public TabDetailWindow(
            MusicTab musicTab,
            TextureService textureService,
            AudioService audioService,
            UserSettingsService userSettingsService)
            : base(
                    AsyncTexture2D.FromAssetId(WindowBackgroundAssetId),
                    new Rectangle(45, 25, WindowWidth - 35, WindowHeight - 220),
                    new Rectangle(40, 25, 890, 650))
        {
            _musicTab = musicTab;
            _textureService = textureService;
            _audioService = audioService;
            _userSettingsService = userSettingsService;
            Emblem = _textureService.GetEmblem();

            RestoreSavedState();
            InitializeWindow();
            BuildLeftPanel();
            BuildRightPanel();
            ForceLayoutRefresh();
        }

        private void ForceLayoutRefresh()
        {
            Size = new Point(Size.X, Size.Y + 1);
            Size = new Point(Size.X, Size.Y - 1);
        }

        private void RestoreSavedState()
        {
            _detailsCollapsed = _userSettingsService?.GetGlobalDetailsCollapsed() ?? false;
            _viewOptionsCollapsed = _userSettingsService?.GetGlobalViewOptionsCollapsed() ?? false;
            _audioPlayerCollapsed = _userSettingsService?.GetGlobalAudioPlayerCollapsed() ?? false;
            _pianoKeybindsCollapsed = _userSettingsService?.GetGlobalPianoKeybindsCollapsed() ?? false;

            _pianoKeybinds = _userSettingsService?.GetPianoKeybinds() ?? new PianoKeybinds();

            var savedState = _userSettingsService?.GetTabWindowState(_musicTab.Id);
            if (savedState != null)
            {
                _currentFontSize = savedState.FontSize;
                _autoScrollEnabled = savedState.AutoScrollEnabled;
                _scrollSpeed = savedState.ScrollSpeed;
            }
        }

        private void SaveWindowState()
        {
            var windowState = new TabWindowState
            {
                FontSize = _currentFontSize,
                AutoScrollEnabled = _autoScrollEnabled,
                ScrollSpeed = _scrollSpeed
            };

            windowState.SetLocation(Location);
            windowState.SetSize(Size);

            _userSettingsService?.SaveTabWindowState(_musicTab.Id, windowState);
        }

        private void InitializeWindow()
        {
            Parent = GameService.Graphics.SpriteScreen;
            Title = WebUtility.HtmlDecode(_musicTab.Name) ?? "Tab Details";
            Subtitle = string.Empty;
            CanResize = true;
            Id = $"{TabDetailWindowIdPrefix}{_musicTab.Id}";

            var savedState = _userSettingsService?.GetTabWindowState(_musicTab.Id);
            if (savedState != null && savedState.Width > 0 && savedState.Height > 0)
            {
                Location = savedState.GetLocation();
                Size = savedState.GetSize();
                SavesPosition = false; // We manage position ourselves
            }
            else
            {
                Location = new Point(200, 150);
                Size = new Point(WindowWidth, WindowHeight);
                SavesPosition = true;
            }

            Resized += OnWindowResized;
            Hidden += OnWindowHidden;
        }

        private void OnWindowHidden(object sender, EventArgs e)
        {
            _audioService?.Stop();
            SaveWindowState();
        }

        private void BuildLeftPanel()
        {
            var initialWidth = _detailsCollapsed ? LeftPanelCollapsedWidth : LeftPanelWidth;

            _leftPanel = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                Width = initialWidth,
                HeightSizingMode = SizingMode.AutoSize,
                Location = new Point(DefaultSpacing, DefaultSpacing),
                ControlPadding = new Vector2(0, 5),
                OuterControlPadding = new Vector2(0, 5),
                Parent = this
            };

            BuildDetailsSection();
            BuildControlsSection();
        }

        private void BuildRightPanel()
        {
            var contentWidth = ContentRegion.Width;
            var contentHeight = ContentRegion.Height;
            var currentLeftWidth = _detailsCollapsed ? LeftPanelCollapsedWidth : LeftPanelWidth;

            _rightPanel = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                Width = contentWidth - currentLeftWidth - DefaultSpacing * 3,
                Height = contentHeight - DefaultSpacing,
                Location = new Point(currentLeftWidth + DefaultSpacing * 2, DefaultSpacing),
                ControlPadding = new Vector2(0, 0),
                OuterControlPadding = new Vector2(5, 0),
                Parent = this
            };

            BuildAudioSection();
            BuildPianoKeybindsSection();
            BuildNotationSection();
            BuildNotationContent();
        }

        private void BuildAudioSection()
        {
            if (string.IsNullOrEmpty(_musicTab.SongMp3)) return;

            _audioSection = new FlowPanel
            {
                ShowBorder = true,
                Title = "Audio Player",
                CanCollapse = true,
                Width = _rightPanel.Width - SectionWidthPadding,
                Height = AudioSectionExpandedHeight,
                HeightSizingMode = SizingMode.Standard,
                Parent = _rightPanel,
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                ControlPadding = new Vector2(0, 0),
                OuterControlPadding = new Vector2(10, 20),
                Collapsed = _audioPlayerCollapsed
            };

            new Mp3PlayerControl(_audioService, _textureService, _musicTab.SongMp3) { Parent = _audioSection };
            _audioSection.Resized += OnAudioSectionResized;
        }

        private void BuildPianoKeybindsSection()
        {
            if (!_musicTab.Piano) return;

            _pianoKeybindsSection = new PianoKeybindsPanel(
                _rightPanel.Width - SectionWidthPadding,
                _pianoKeybindsCollapsed,
                _pianoKeybinds,
                _userSettingsService,
                RefreshNotationContent)
            {
                Parent = _rightPanel
            };
            _pianoKeybindsSection.CollapsedChanged += OnPianoKeybindsSectionCollapsedChanged;
        }

        private void OnPianoKeybindsSectionCollapsedChanged(object sender, bool isCollapsed)
        {
            if (isCollapsed != _pianoKeybindsCollapsed)
            {
                _pianoKeybindsCollapsed = isCollapsed;
                _userSettingsService?.SaveGlobalPianoKeybindsCollapsed(_pianoKeybindsCollapsed);
            }

            _pianoKeybinds = _pianoKeybindsSection?.Keybinds ?? _pianoKeybinds;
            UpdatePanelSizes();
        }

        private int GetPianoKeybindsSectionHeight() =>
            _pianoKeybindsSection == null ? 0 : (_pianoKeybindsCollapsed ? CollapsedSectionHeight : PianoKeybindsSectionExpandedHeight);

        private void BuildNotationSection()
        {
            var notationWidth = _rightPanel.Width - SectionWidthPadding;
            var notationHeight = _rightPanel.Height - GetAudioSectionHeight() - GetPianoKeybindsSectionHeight();

            _notationSection = new FlowPanel
            {
                ShowBorder = true,
                Title = "Notation",
                Width = notationWidth,
                Height = notationHeight,
                Parent = _rightPanel,
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                ControlPadding = new Vector2(0, 0),
                OuterControlPadding = new Vector2(0, 0)
            };

            _notationContentPanel = new Panel
            {
                Width = notationWidth - NotationPanelPadding,
                Height = notationHeight - NotationSectionHeaderHeight,
                CanScroll = true,
                BackgroundColor = Color.Black * 0.3f,
                Parent = _notationSection
            };
        }

        private void RefreshNotationContent(int? explicitWidth = null, int? explicitHeight = null)
        {
            _pianoKeybinds = _pianoKeybindsSection?.Keybinds ?? _pianoKeybinds;

            var savedScrollOffset = _autoScrollEnabled 
                ? _accumulatedScrollOffset 
                : (_notationContentPanel?.VerticalScrollOffset ?? 0);

            _cachedScrollbar = null;
            ClearNotationPanel();
            BuildNotationContent(explicitWidth, explicitHeight);
            _notationContentPanel.Invalidate();

            RestoreScrollPosition(savedScrollOffset);
        }

        private void ClearNotationPanel()
        {
            var children = _notationContentPanel.Children.ToArray();
            foreach (var child in children)
            {
                child.Parent = null;
                child.Dispose();
            }
            _notationRenderer = null;
        }

        private void RestoreScrollPosition(float savedScrollOffset)
        {
            if (savedScrollOffset <= 0) return;

            if (_autoScrollEnabled)
            {
                _accumulatedScrollOffset = savedScrollOffset;
            }

            _cachedScrollbar = PanelScrollHelper.GetScrollbar(_notationContentPanel);
            if (_cachedScrollbar == null) return;

            var scrollableHeight = GetScrollableHeight();
            if (scrollableHeight > 0)
            {
                PanelScrollHelper.SetScrollPosition(_cachedScrollbar, savedScrollOffset / scrollableHeight);
            }
        }

        private void RefreshNotationContent() => RefreshNotationContent(null, null);

        private void UpdateLayoutForDetailsState()
        {
            var currentLeftWidth = _detailsCollapsed ? LeftPanelCollapsedWidth : LeftPanelWidth;
            var contentWidth = GetCurrentLeftPanelContentWidth();

            _leftPanel.Width = currentLeftWidth;

            if (_detailsSection != null)
            {
                _detailsSection.Width = contentWidth;
                if (!_detailsCollapsed) _detailsSection.RebuildContent();
            }

            if (_controlsSection != null)
            {
                _controlsSection.Width = contentWidth;
                if (!_detailsCollapsed) _controlsSection.RebuildContent();
            }

            _leftPanel.Invalidate();
            UpdatePanelSizes();
        }

        private void OnAudioSectionResized(object sender, ResizedEventArgs e)
        {
            var isNowCollapsed = _audioSection.Collapsed;
            if (isNowCollapsed != _audioPlayerCollapsed)
            {
                _audioPlayerCollapsed = isNowCollapsed;
                _userSettingsService?.SaveGlobalAudioPlayerCollapsed(_audioPlayerCollapsed);
            }

            UpdatePanelSizes();
        }

        private int GetAudioSectionHeight() =>
            _audioSection == null ? 0 : (_audioPlayerCollapsed ? CollapsedSectionHeight : AudioSectionExpandedHeight);

        private void BuildDetailsSection()
        {
            _detailsSection = new TabDetailsPanel(
                _musicTab,
                _textureService,
                GetCurrentLeftPanelContentWidth(),
                _detailsCollapsed)
            {
                Parent = _leftPanel
            };
            _detailsSection.CollapsedChanged += OnDetailsSectionCollapsedChanged;
        }

        private int GetCurrentLeftPanelContentWidth() =>
            (_detailsCollapsed ? LeftPanelCollapsedWidth : LeftPanelWidth) - DefaultSpacing;

        private void OnDetailsSectionCollapsedChanged(object sender, bool isCollapsed)
        {
            if (isCollapsed == _detailsCollapsed) return;

            _detailsCollapsed = isCollapsed;
            _userSettingsService?.SaveGlobalDetailsCollapsed(_detailsCollapsed);

            if (_controlsSection != null)
            {
                _controlsSection.Visible = !_detailsCollapsed;
            }

            UpdateLayoutForDetailsState();
        }

        private void BuildControlsSection()
        {
            _controlsSection = new ViewOptionsPanel(
                GetCurrentLeftPanelContentWidth(),
                _viewOptionsCollapsed,
                _currentFontSize,
                _autoScrollEnabled,
                _scrollSpeed,
                _textureService)
            {
                Parent = _leftPanel,
                Visible = !_detailsCollapsed
            };

            _controlsSection.FontSizeChanged += OnFontSizeChanged;
            _controlsSection.AutoScrollToggled += OnAutoScrollToggled;
            _controlsSection.ScrollSpeedChanged += OnScrollSpeedChanged;
            _controlsSection.CollapsedChanged += OnControlsSectionCollapsedChanged;
        }

        private void OnFontSizeChanged(object sender, NotationFontSize fontSize)
        {
            _currentFontSize = fontSize;
            RefreshNotationContent();
        }

        private void OnAutoScrollToggled(object sender, bool enabled)
        {
            _autoScrollEnabled = enabled;

            if (_notationRenderer?.Control != null)
            {
                _notationRenderer.Control.SmoothScrolling = _autoScrollEnabled;
            }

            if (_autoScrollEnabled && _notationContentPanel != null)
            {
                _accumulatedScrollOffset = _notationContentPanel.VerticalScrollOffset;
                _cachedScrollbar = PanelScrollHelper.GetScrollbar(_notationContentPanel);
            }
        }

        private void OnScrollSpeedChanged(object sender, float speed)
        {
            _scrollSpeed = speed;
        }

        private void OnControlsSectionCollapsedChanged(object sender, bool isCollapsed)
        {
            if (isCollapsed != _viewOptionsCollapsed)
            {
                _viewOptionsCollapsed = isCollapsed;
                _userSettingsService?.SaveGlobalViewOptionsCollapsed(_viewOptionsCollapsed);
            }
        }

        private void BuildNotationContent(int? explicitWidth = null, int? explicitHeight = null)
        {
            var notationText = _musicTab.NotationBlishhud;

            if (string.IsNullOrEmpty(notationText))
            {
                CreateNoNotationMessage();
                return;
            }

            RenderNotation(notationText, explicitWidth, explicitHeight);
        }

        private void CreateNoNotationMessage()
        {
            new Label
            {
                Text = "No notation available for this tab.",
                Font = GameService.Content.DefaultFont14,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(10, 10),
                Parent = _notationContentPanel
            };
        }

        private void RenderNotation(string notation, int? explicitWidth = null, int? explicitHeight = null)
        {
            var width = explicitWidth ?? _notationContentPanel.Width;
            var height = explicitHeight ?? _notationContentPanel.Height;

            if (_musicTab.Piano && _pianoKeybinds != null)
            {
                notation = _pianoKeybinds.ApplyToNotation(notation);
            }

            _notationRenderer = new NotationRenderer(_notationContentPanel, _currentFontSize, width, height);
            _notationRenderer.Render(notation);

            if (_notationRenderer.Control != null)
            {
                _notationRenderer.Control.SmoothScrolling = _autoScrollEnabled;
                _notationContentPanel.Invalidate();
            }
        }

        private void OnWindowResized(object sender, ResizedEventArgs e)
        {
            UpdatePanelSizes();
        }

        private void UpdatePanelSizes()
        {
            var contentWidth = ContentRegion.Width;
            var contentHeight = ContentRegion.Height;
            if (contentWidth <= 0 || contentHeight <= 0) return;

            var currentLeftWidth = _detailsCollapsed ? LeftPanelCollapsedWidth : LeftPanelWidth;
            var rightPanelWidth = contentWidth - currentLeftWidth - DefaultSpacing * 3;
            var rightPanelHeight = contentHeight - DefaultSpacing;

            UpdateRightPanelLayout(currentLeftWidth, rightPanelWidth, rightPanelHeight);
            UpdateSectionSizes(rightPanelWidth, rightPanelHeight);
        }

        private void UpdateRightPanelLayout(int currentLeftWidth, int rightPanelWidth, int rightPanelHeight)
        {
            if (_rightPanel == null) return;

            _rightPanel.Width = rightPanelWidth;
            _rightPanel.Height = rightPanelHeight;
            _rightPanel.Location = new Point(currentLeftWidth + DefaultSpacing * 2, DefaultSpacing);
        }

        private void UpdateSectionSizes(int rightPanelWidth, int rightPanelHeight)
        {
            var sectionWidth = rightPanelWidth - SectionWidthPadding;
            var notationSectionHeight = rightPanelHeight - GetAudioSectionHeight() - GetPianoKeybindsSectionHeight();

            if (_audioSection != null) _audioSection.Width = sectionWidth;
            if (_pianoKeybindsSection != null) _pianoKeybindsSection.Width = sectionWidth;

            UpdateNotationSection(sectionWidth, notationSectionHeight);
        }

        private void UpdateNotationSection(int sectionWidth, int notationSectionHeight)
        {
            if (_notationSection == null) return;

            _notationSection.Width = sectionWidth;
            _notationSection.Height = notationSectionHeight;

            if (_notationContentPanel == null) return;

            var notationPanelWidth = sectionWidth - NotationPanelPadding;
            var notationPanelHeight = notationSectionHeight - NotationSectionHeaderHeight;

            _notationContentPanel.Width = notationPanelWidth;
            _notationContentPanel.Height = notationPanelHeight;
            RefreshNotationContent(notationPanelWidth, notationPanelHeight);
        }

        protected override Point HandleWindowResize(Point newSize)
        {
            return new Point(
                MathHelper.Clamp(newSize.X, WindowWidth, WindowMaxWidth),
                MathHelper.Clamp(newSize.Y, WindowHeight, WindowMaxHeight));
        }

        public override void UpdateContainer(GameTime gameTime)
        {
            base.UpdateContainer(gameTime);

            SyncAudioSectionCollapseState();

            if (_autoScrollEnabled && _notationContentPanel != null)
            {
                UpdateAutoScroll(gameTime);
            }
        }

        private void SyncAudioSectionCollapseState()
        {
            if (_audioSection == null || _audioSection.Collapsed == _audioPlayerCollapsed) return;

            _audioPlayerCollapsed = _audioSection.Collapsed;
            _userSettingsService?.SaveGlobalAudioPlayerCollapsed(_audioPlayerCollapsed);
            UpdatePanelSizes();
        }

        private int GetScrollableHeight()
        {
            var maxChildBottom = 0;
            foreach (var child in _notationContentPanel.Children)
            {
                if (child.Visible)
                {
                    maxChildBottom = Math.Max(maxChildBottom, child.Bottom);
                }
            }
            return maxChildBottom - _notationContentPanel.Height;
        }

        private void UpdateAutoScroll(GameTime gameTime)
        {
            var scrollableHeight = GetScrollableHeight();

            if (scrollableHeight <= 0)
            {
                return;
            }

            _accumulatedScrollOffset += _scrollSpeed * ScrollSpeedMultiplier * (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (_accumulatedScrollOffset >= scrollableHeight)
            {
                _accumulatedScrollOffset = 0;
            }

            _cachedScrollbar ??= PanelScrollHelper.GetScrollbar(_notationContentPanel);

            if (_cachedScrollbar != null)
            {
                var scrollPercent = _accumulatedScrollOffset / scrollableHeight;
                PanelScrollHelper.SetScrollPosition(_cachedScrollbar, scrollPercent);
            }
        }

        protected override void DisposeControl()
        {
            Resized -= OnWindowResized;
            Hidden -= OnWindowHidden;

            if (_detailsSection != null)
            {
                _detailsSection.CollapsedChanged -= OnDetailsSectionCollapsedChanged;
            }

            if (_controlsSection != null)
            {
                _controlsSection.FontSizeChanged -= OnFontSizeChanged;
                _controlsSection.AutoScrollToggled -= OnAutoScrollToggled;
                _controlsSection.ScrollSpeedChanged -= OnScrollSpeedChanged;
                _controlsSection.CollapsedChanged -= OnControlsSectionCollapsedChanged;
            }

            if (_audioSection != null)
            {
                _audioSection.Resized -= OnAudioSectionResized;
            }

            if (_pianoKeybindsSection != null)
            {
                _pianoKeybindsSection.CollapsedChanged -= OnPianoKeybindsSectionCollapsedChanged;
            }

            _audioService?.Stop();

            _leftPanel?.Dispose();
            _rightPanel?.Dispose();

            base.DisposeControl();
        }

        private static class PanelScrollHelper
        {
            private static readonly System.Reflection.FieldInfo ScrollbarField =
                typeof(Panel).GetField("_panelScrollbar", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            private static readonly System.Reflection.FieldInfo ScrollDistanceField =
                typeof(Scrollbar).GetField("_scrollDistance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            private static readonly System.Reflection.FieldInfo TargetScrollDistanceField =
                typeof(Scrollbar).GetField("_targetScrollDistance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            public static Scrollbar GetScrollbar(Panel panel)
            {
                return ScrollbarField?.GetValue(panel) as Scrollbar;
            }

            public static void SetScrollPosition(Scrollbar scrollbar, float scrollPercent)
            {
                if (scrollbar == null) return;
                ScrollDistanceField?.SetValue(scrollbar, scrollPercent);
                TargetScrollDistanceField?.SetValue(scrollbar, scrollPercent);
            }
        }
    }
}
