using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;

using Microsoft.Xna.Framework;

using SongbookOfTyria.Models;
using SongbookOfTyria.Services;
using SongbookOfTyria.UI.Controls;
using SongbookOfTyria.UI.Controls.Notation;

using System.Threading.Tasks;

namespace SongbookOfTyria.UI.Windows
{
    public enum TabViewMode
    {
        Normal,
        Practice
    }

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
        private readonly MidiPlaybackService _midiPlaybackService;
        private readonly MidiFileParser _midiFileParser;
        private readonly Settings.ModuleSettings _moduleSettings;
        private PracticeFeedbackService _practiceFeedbackService;

        private FlowPanel _leftPanel;
        private FlowPanel _rightPanel;
        private TabDetailsPanel _detailsSection;
        private ViewOptionsPanel _controlsSection;
        private Dropdown _modeDropdown;
        private FlowPanel _audioSection;
        private PracticeModePanel _practiceModePanel;
        private PianoKeybindsPanel _pianoKeybindsSection;
        private Panel _notationSection;
        private Panel _notationHeaderPanel;
        private Panel _notationContentPanel;
        private TrackSelectionPanel _trackSelectionPanel;
        private PianoKeybinds _pianoKeybinds;
        private NotationFontSize _currentFontSize = NotationFontSize.Size20;
        private TabViewMode _currentMode = TabViewMode.Normal;
        private bool _detailsCollapsed;
        private bool _viewOptionsCollapsed;
        private bool _audioPlayerCollapsed;
        private bool _pianoKeybindsCollapsed;
        private bool _autoScrollEnabled;
        private float _scrollSpeed = 30f;
        private bool _hitDetectionEnabled = false;
        private float _accumulatedScrollOffset;
        private Scrollbar _cachedScrollbar;
        private NotationRenderer _notationRenderer;
        private List<ActiveNoteInfo> _pendingActiveNotes;
        private volatile bool _activeNotesDirty;
        private readonly Dictionary<int, DateTime> _noteHighlightStartTimes = new Dictionary<int, DateTime>();
        private const double HighlightMaxDurationMs = 500.0;
        private Dictionary<int, NoteFeedbackType> _pendingFeedback;
        private volatile bool _feedbackDirty;
        private volatile bool _markersDirty;

        public TabDetailWindow(
            MusicTab musicTab,
            TextureService textureService,
            AudioService audioService,
            UserSettingsService userSettingsService,
            Settings.ModuleSettings moduleSettings,
            MidiPlaybackService midiPlaybackService = null,
            string cacheDirectory = null)
            : base(
                    AsyncTexture2D.FromAssetId(TextureService.WindowBackgroundAssetId),
                    new Rectangle(45, 25, WindowWidth - 35, WindowHeight - 220),
                    new Rectangle(40, 25, 890, 650))
        {
            _musicTab = musicTab;
            _textureService = textureService;
            _audioService = audioService;
            _userSettingsService = userSettingsService;
            _moduleSettings = moduleSettings;
            _midiPlaybackService = midiPlaybackService;
            if (cacheDirectory != null)
                _midiFileParser = new MidiFileParser(cacheDirectory);
            Emblem = _textureService.GetEmblem();

            RestoreSavedState();
            InitializeWindow();
            BuildLeftPanel();
            BuildRightPanel();
            ForceLayoutRefresh();

            if (_musicTab.HasPracticeMode && _midiPlaybackService != null)
            {
                _ = InitializePracticeModeAsync();
            }
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
            _hitDetectionEnabled = false;

            _pianoKeybinds = _userSettingsService?.GetPianoKeybinds() ?? new PianoKeybinds();

            var savedState = _userSettingsService?.GetTabWindowState(_musicTab.Id);
            if (savedState != null)
            {
                _currentFontSize = savedState.FontSize;
                _autoScrollEnabled = savedState.AutoScrollEnabled;
                _scrollSpeed = savedState.ScrollSpeed;

                if (savedState.IsPracticeMode && _musicTab.HasPracticeMode)
                {
                    _currentMode = TabViewMode.Practice;
                }
            }
        }

        private void SaveWindowState()
        {
            var windowState = new TabWindowState
            {
                FontSize = _currentFontSize,
                AutoScrollEnabled = _autoScrollEnabled,
                ScrollSpeed = _scrollSpeed,
                IsPracticeMode = _currentMode == TabViewMode.Practice
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
            _midiPlaybackService?.Stop();
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
            BuildPracticeModeSection();
            BuildPianoKeybindsSection();
            BuildNotationSection();
            BuildNotationContent();
            UpdateModeVisibility();
        }

        private void BuildModeDropdown()
        {
            if (!_musicTab.HasPracticeMode || _notationHeaderPanel == null) return;

            _modeDropdown = new Dropdown
            {
                Width = 110,
                Location = new Point(75, 1),
                Parent = _notationHeaderPanel
            };

            _modeDropdown.Items.Add("Normal");
            _modeDropdown.Items.Add("Practice");
            _modeDropdown.SelectedItem = _currentMode == TabViewMode.Normal ? "Normal" : "Practice";

            _modeDropdown.ValueChanged += OnModeDropdownChanged;
        }

        private void OnModeDropdownChanged(object sender, ValueChangedEventArgs e)
        {
            var newMode = e.CurrentValue == "Practice" ? TabViewMode.Practice : TabViewMode.Normal;
            if (newMode == _currentMode) return;

            _currentMode = newMode;

            if (_currentMode == TabViewMode.Normal)
            {
                _midiPlaybackService?.Stop();
            }
            else
            {
                _audioService?.Stop();
            }

            _accumulatedScrollOffset = 0;
            _cachedScrollbar = null;
            UpdateModeVisibility();
            ForceLayoutRefresh();
        }

        private void UpdateModeVisibility()
        {
            bool isNormalMode = _currentMode == TabViewMode.Normal;
            bool isPracticeMode = !isNormalMode;

            SetControlVisible(_audioSection, isNormalMode);
            SetControlVisible(_practiceModePanel, isPracticeMode);

            _controlsSection?.SetPracticeModeActive(isPracticeMode);
            _leftPanel?.Invalidate();

            if (_practiceFeedbackService != null)
            {
                _practiceFeedbackService.IsEnabled = isPracticeMode && _hitDetectionEnabled;
                if (isNormalMode)
                {
                    _practiceFeedbackService.ClearFeedback();
                    _notationRenderer?.Control?.ClearNoteFeedback();
                }
            }

            if (_trackSelectionPanel != null)
            {
                if (isPracticeMode)
                {
                    AddTrackSelectionToNotationSection();
                }
                else
                {
                    _trackSelectionPanel.Parent = null;
                    UpdateNotationContentPanelHeight();
                }
            }

            ReorderSectionsForMode(isNormalMode);
            UpdatePanelSizes();
        }

        private static void SetControlVisible(Control control, bool visible)
        {
            if (control != null) control.Visible = visible;
        }

        private void ReorderSectionsForMode(bool isNormalMode)
        {
            if (_pianoKeybindsSection == null || _practiceModePanel == null) return;

            _pianoKeybindsSection.Parent = null;
            _practiceModePanel.Parent = null;
            _notationSection.Parent = null;

            if (isNormalMode)
            {
                _practiceModePanel.Parent = _rightPanel;
                _pianoKeybindsSection.Parent = _rightPanel;
            }
            else
            {
                _pianoKeybindsSection.Parent = _rightPanel;
                _practiceModePanel.Parent = _rightPanel;
            }

            _notationSection.Parent = _rightPanel;
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

        private void BuildPracticeModeSection()
        {
            if (!_musicTab.HasPracticeMode || _midiPlaybackService == null) return;
            if (_musicTab.MidiData?.Tracks?.Count > 0)
            {
                CreatePracticeModePanel(_musicTab.MidiData);
            }
        }

        private async Task InitializePracticeModeAsync()
        {
            try
            {
                if (_musicTab.MidiData?.Tracks?.Count > 0) return;

                if (!string.IsNullOrEmpty(_musicTab.MidiFile) && _midiFileParser != null)
                {
                    Logger.Debug("Downloading and parsing MIDI file: {0}", _musicTab.MidiFile);
                    var midiData = await _midiFileParser.ParseFromUrlAsync(_musicTab.MidiFile).ConfigureAwait(false);

                    if (midiData?.Tracks?.Count > 0)
                    {
                        _musicTab.MidiData = midiData;
                        CreatePracticeModePanel(midiData);
                        UpdateModeVisibility();
                        ForceLayoutRefresh();
                    }
                    else
                    {
                        Logger.Warn("MIDI file parsed but contained no tracks");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to initialize practice mode");
            }
        }

        private void CreatePracticeModePanel(MidiData midiData)
        {
            _practiceModePanel = new PracticeModePanel(
                _midiPlaybackService,
                midiData,
                _textureService,
                _rightPanel.Width - SectionWidthPadding,
                _userSettingsService,
                _musicTab.Id,
                _musicTab.PracticeSections)
            {
                Parent = _rightPanel,
                Visible = _currentMode == TabViewMode.Practice
            };
            _midiPlaybackService.ActiveNotesChanged += OnActiveNotesChanged;
            _practiceModePanel.CollapsedChanged += OnPlaybackCollapsedChanged;
            _practiceModePanel.MarkersChanged += OnMarkersChanged;

            _practiceFeedbackService = new PracticeFeedbackService(_midiPlaybackService, _moduleSettings);
            _practiceFeedbackService.FeedbackChanged += OnPracticeFeedbackChanged;
            _practiceFeedbackService.IsEnabled = _currentMode == TabViewMode.Practice && _hitDetectionEnabled;

            var savedTrackIndex = _practiceModePanel.SelectedTrackIndex;
            if (midiData?.Tracks?.Count > 0)
            {
                var trackIndex = Math.Min(savedTrackIndex, midiData.Tracks.Count - 1);
                var track = midiData.Tracks.FirstOrDefault(t => t.Index == trackIndex) ?? midiData.Tracks[0];
                _practiceFeedbackService.SetActiveTrack(track);
            }

            CreateTrackSelectionPanel(midiData);

            if (_currentMode == TabViewMode.Practice && _trackSelectionPanel != null)
            {
                AddTrackSelectionToNotationSection();
            }

            if (_notationSection != null)
            {
                _notationSection.Parent = null;
                _notationSection.Parent = _rightPanel;
            }
        }

        private void OnPlaybackCollapsedChanged(object sender, bool collapsed)
        {
            _userSettingsService?.SaveGlobalPlaybackCollapsed(collapsed);
        }

        private void CreateTrackSelectionPanel(MidiData midiData)
        {
            if (midiData?.Tracks == null || midiData.Tracks.Count == 0) return;

            _trackSelectionPanel = new TrackSelectionPanel(midiData, _notationSection?.Width ?? (_rightPanel.Width - SectionWidthPadding - NotationPanelPadding));
            _trackSelectionPanel.TrackChanged += OnTrackSelectionChanged;

            var savedTrackIndex = _practiceModePanel?.SelectedTrackIndex ?? 0;
            if (savedTrackIndex > 0 && savedTrackIndex < midiData.Tracks.Count)
            {
                _trackSelectionPanel.SelectTrack(savedTrackIndex);
            }
        }

        private void OnActiveNotesChanged(object sender, ActiveNoteEventArgs e)
        {
            _pendingActiveNotes = e.ActiveNotes;
            _activeNotesDirty = true;
        }

        private void OnMarkersChanged(object sender, EventArgs e)
        {
            _markersDirty = true;
        }

        private void OnPracticeFeedbackChanged(object sender, PracticeFeedbackEventArgs e)
        {
            _pendingFeedback = e.NoteFeedback?.ToDictionary(
                kvp => kvp.Key,
                kvp => ConvertFeedbackState(kvp.Value));
            _feedbackDirty = true;
        }

        private static NoteFeedbackType ConvertFeedbackState(NoteFeedbackState state)
        {
            switch (state)
            {
                case NoteFeedbackState.Correct:
                    return NoteFeedbackType.Correct;
                case NoteFeedbackState.Wrong:
                    return NoteFeedbackType.Wrong;
                case NoteFeedbackState.Missed:
                    return NoteFeedbackType.Missed;
                default:
                    return NoteFeedbackType.None;
            }
        }

        private void OnTrackSelectionChanged(object sender, int trackIndex)
        {
            _practiceModePanel?.SetSelectedTrackIndex(trackIndex);

            if (_practiceFeedbackService != null && _musicTab.MidiData?.Tracks != null)
            {
                var track = _musicTab.MidiData.Tracks.FirstOrDefault(t => t.Index == trackIndex);
                _practiceFeedbackService.SetActiveTrack(track);
            }
            RefreshNotationContent();
        }

        private int GetPracticeModeSectionHeight()
        {
            if (_currentMode != TabViewMode.Practice) return 0;
            return _practiceModePanel?.Height ?? 0;
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

        private int GetTrackSelectionHeight() => 0;

        private void BuildNotationSection()
        {
            var notationWidth = _rightPanel.Width - SectionWidthPadding;
            var notationHeight = _rightPanel.Height - GetAudioSectionHeight() - GetPracticeModeSectionHeight() - GetPianoKeybindsSectionHeight();

            _notationSection = new Panel
            {
                ShowBorder = true,
                Width = notationWidth,
                Height = notationHeight,
                Parent = _rightPanel
            };

            _notationHeaderPanel = new Panel
            {
                Width = notationWidth - NotationPanelPadding,
                Height = NotationSectionHeaderHeight,
                Location = new Point(0, 0),
                Parent = _notationSection
            };

            new Label
            {
                Text = "Notation",
                Font = GameService.Content.DefaultFont16,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(10, 5),
                Parent = _notationHeaderPanel
            };

            BuildModeDropdown();

            _notationContentPanel = new Panel
            {
                Width = notationWidth - NotationPanelPadding,
                Height = notationHeight - NotationSectionHeaderHeight,
                Location = new Point(0, NotationSectionHeaderHeight),
                CanScroll = true,
                BackgroundColor = Color.Black * 0.3f,
                Parent = _notationSection
            };
        }

        private void AddTrackSelectionToNotationSection()
        {
            if (_trackSelectionPanel == null || _notationHeaderPanel == null) return;

            _trackSelectionPanel.Parent = null;
            _trackSelectionPanel.Resized -= OnTrackSelectionPanelResized;
            _trackSelectionPanel.HeightSizingMode = SizingMode.Standard;
            _trackSelectionPanel.Height = NotationSectionHeaderHeight - 4;
            _trackSelectionPanel.WidthSizingMode = SizingMode.AutoSize;
            _trackSelectionPanel.Resized += OnTrackSelectionPanelResized;
            _trackSelectionPanel.Parent = _notationHeaderPanel;

            RepositionTrackSelectionPanel();
        }

        private void OnTrackSelectionPanelResized(object sender, ResizedEventArgs e)
        {
            RepositionTrackSelectionPanel();
        }

        private void RepositionTrackSelectionPanel()
        {
            if (_trackSelectionPanel == null || _notationHeaderPanel == null) return;

            var rightX = _notationHeaderPanel.Width - _trackSelectionPanel.Width - 5;
            _trackSelectionPanel.Location = new Point(Math.Max(80, rightX), 2);
        }

        private void UpdateNotationContentPanelHeight()
        {
            if (_notationContentPanel == null || _notationSection == null) return;

            _notationContentPanel.Height = _notationSection.Height - NotationSectionHeaderHeight;
            _notationContentPanel.Location = new Point(0, NotationSectionHeaderHeight);
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
            _notationContentPanel?.Invalidate();

            RestoreScrollPosition(savedScrollOffset);
        }

        private void ClearNotationPanel()
        {
            if (_notationContentPanel == null) return;

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

        private int GetAudioSectionHeight()
        {
            if (_currentMode != TabViewMode.Normal) return 0;
            return _audioSection == null ? 0 : (_audioPlayerCollapsed ? CollapsedSectionHeight : AudioSectionExpandedHeight);
        }

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
                _hitDetectionEnabled,
                _textureService)
            {
                Parent = _leftPanel,
                Visible = !_detailsCollapsed
            };

            _controlsSection.FontSizeChanged += OnFontSizeChanged;
            _controlsSection.AutoScrollToggled += OnAutoScrollToggled;
            _controlsSection.ScrollSpeedChanged += OnScrollSpeedChanged;
            _controlsSection.HitDetectionToggled += OnHitDetectionToggled;
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

        private void OnHitDetectionToggled(object sender, bool enabled)
        {
            _hitDetectionEnabled = enabled;
            _userSettingsService?.SaveHitDetectionFeedbackEnabled(enabled);

            if (_practiceFeedbackService != null)
            {
                bool shouldBeEnabled = _currentMode == TabViewMode.Practice && enabled;
                _practiceFeedbackService.IsEnabled = shouldBeEnabled;

                if (!shouldBeEnabled)
                {
                    _practiceFeedbackService.ClearFeedback();
                    _notationRenderer?.Control?.ClearNoteFeedback();
                }
            }
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
            if (_notationContentPanel == null) return;

            var notationText = GetActiveNotation();

            if (string.IsNullOrEmpty(notationText))
            {
                CreateNoNotationMessage();
                return;
            }

            RenderNotation(notationText, explicitWidth, explicitHeight);
        }

        private string GetActiveNotation()
        {
            if (_currentMode == TabViewMode.Practice && _trackSelectionPanel != null)
            {
                var trackNotation = _trackSelectionPanel.GetSelectedTrackNotation();
                if (!string.IsNullOrEmpty(trackNotation))
                {
                    return ConvertTrackNotationToBlishHud(trackNotation, _musicTab.PracticeSections);
                }
            }

            return _musicTab.NotationBlishhud;
        }

        private void CreateNoNotationMessage()
        {
            if (_notationContentPanel == null) return;

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
            if (_notationContentPanel == null) return;

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

                if (_currentMode == TabViewMode.Practice)
                {
                    UpdateNotationMarkers();
                }

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
            var notationSectionHeight = rightPanelHeight - GetAudioSectionHeight() - GetPracticeModeSectionHeight() - GetPianoKeybindsSectionHeight();
            if (_audioSection != null) _audioSection.Width = sectionWidth;
            if (_practiceModePanel != null) _practiceModePanel.Width = sectionWidth;
            if (_pianoKeybindsSection != null) _pianoKeybindsSection.Width = sectionWidth;

            UpdateNotationSection(sectionWidth, notationSectionHeight);
        }

        private void UpdateNotationSection(int sectionWidth, int notationSectionHeight)
        {
            if (_notationSection == null) return;

            _notationSection.Width = sectionWidth;
            _notationSection.Height = notationSectionHeight;

            var notationPanelWidth = sectionWidth - NotationPanelPadding;

            if (_notationHeaderPanel != null)
            {
                _notationHeaderPanel.Width = notationPanelWidth;
            }

            if (_notationContentPanel == null) return;

            var notationPanelHeight = notationSectionHeight - NotationSectionHeaderHeight;

            _notationContentPanel.Width = notationPanelWidth;
            _notationContentPanel.Height = notationPanelHeight;

            if (_trackSelectionPanel != null && _currentMode == TabViewMode.Practice)
            {
                RepositionTrackSelectionPanel();
            }

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

            if (_currentMode == TabViewMode.Practice)
            {
                _practiceFeedbackService?.Update();
            }

            if (_feedbackDirty && _notationRenderer?.Control != null && _currentMode == TabViewMode.Practice)
            {
                _feedbackDirty = false;
                _notationRenderer.Control.SetNoteFeedback(_pendingFeedback);
            }

            if (_markersDirty && _notationRenderer?.Control != null && _currentMode == TabViewMode.Practice)
            {
                _markersDirty = false;
                UpdateNotationMarkers();
            }

            if (_activeNotesDirty && _notationRenderer?.Control != null && _currentMode == TabViewMode.Practice)
            {
                _activeNotesDirty = false;
                var notes = _pendingActiveNotes;
                var now = DateTime.UtcNow;
                var filteredNoteIndices = new HashSet<int>();

                if (notes != null)
                {
                    var selectedTrack = _trackSelectionPanel?.SelectedTrackIndex ?? 0;
                    var currentTrackNotes = new HashSet<int>();

                    foreach (var note in notes)
                    {
                        if (note.TrackIndex == selectedTrack)
                        {
                            currentTrackNotes.Add(note.NoteIndex);
                        }
                    }

                    foreach (var noteIdx in currentTrackNotes)
                    {
                        if (!_noteHighlightStartTimes.TryGetValue(noteIdx, out var startTime))
                        {
                            _noteHighlightStartTimes[noteIdx] = now;
                            startTime = now;
                        }

                        var elapsed = (now - startTime).TotalMilliseconds;
                        if (elapsed < HighlightMaxDurationMs)
                        {
                            filteredNoteIndices.Add(noteIdx);
                        }
                    }

                    var expiredKeys = new List<int>();
                    foreach (var kvp in _noteHighlightStartTimes)
                    {
                        if (!currentTrackNotes.Contains(kvp.Key))
                        {
                            expiredKeys.Add(kvp.Key);
                        }
                    }
                    foreach (var key in expiredKeys)
                    {
                        _noteHighlightStartTimes.Remove(key);
                    }
                }
                else
                {
                    _noteHighlightStartTimes.Clear();
                }

                _notationRenderer.Control.SetHighlightedNoteIndices(filteredNoteIndices);
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

        private void UpdateNotationMarkers()
        {
            if (_notationRenderer?.Control == null || _practiceModePanel == null) return;

            var markers = _practiceModePanel.GetMarkers();
            if (markers == null || markers.Count == 0)
            {
                _notationRenderer.Control.SetMarkers(null);
                return;
            }

            var selectedTrackIndex = _trackSelectionPanel?.SelectedTrackIndex ?? -1;
            var notationMarkers = new List<NotationMarker>();
            foreach (var marker in markers)
            {
                var noteIndex = _practiceModePanel.GetNoteIndexForTime(marker.Time, selectedTrackIndex);
                notationMarkers.Add(new NotationMarker
                {
                    NoteIndex = noteIndex,
                    Color = marker.Color
                });
            }

            _notationRenderer.Control.SetMarkers(notationMarkers);
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
                _controlsSection.HitDetectionToggled -= OnHitDetectionToggled;
                _controlsSection.CollapsedChanged -= OnControlsSectionCollapsedChanged;
            }

            if (_audioSection != null)
            {
                _audioSection.Resized -= OnAudioSectionResized;
            }

            if (_modeDropdown != null)
            {
                _modeDropdown.ValueChanged -= OnModeDropdownChanged;
            }

            if (_pianoKeybindsSection != null)
            {
                _pianoKeybindsSection.CollapsedChanged -= OnPianoKeybindsSectionCollapsedChanged;
            }

            if (_trackSelectionPanel != null)
            {
                _trackSelectionPanel.TrackChanged -= OnTrackSelectionChanged;
                _trackSelectionPanel.Resized -= OnTrackSelectionPanelResized;
            }

            if (_midiPlaybackService != null)
            {
                _midiPlaybackService.ActiveNotesChanged -= OnActiveNotesChanged;
            }

            if (_practiceFeedbackService != null)
            {
                _practiceFeedbackService.FeedbackChanged -= OnPracticeFeedbackChanged;
                _practiceFeedbackService.Dispose();
            }

            _audioService?.Stop();
            _midiPlaybackService?.Stop();

            _leftPanel?.Dispose();
            _rightPanel?.Dispose();

            base.DisposeControl();
        }

        private static string ConvertTrackNotationToBlishHud(string trackNotation, PracticeSections practiceSections)
        {
            var cleaned = trackNotation.Replace("\u200b", "");
            var lines = cleaned.Split('\n');
            var contentLines = new List<string>();
            bool skippedHeader = false;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (!skippedHeader && (trimmed.EndsWith(":") || trimmed.Length == 0))
                {
                    if (trimmed.EndsWith(":")) skippedHeader = true;
                    continue;
                }

                skippedHeader = true;
                if (!string.IsNullOrEmpty(trimmed))
                {
                    contentLines.Add(trimmed);
                }
            }

            var allContent = string.Join(" ", contentLines).Trim();
            allContent = Regex.Replace(allContent, @"\|\s*\|", "|");

            if (!allContent.StartsWith("|")) allContent = "|" + allContent;
            if (!allContent.EndsWith("|")) allContent = allContent + "|";

            var bars = SplitIntoBars(allContent);

            if (bars.Count == 0)
            {
                return string.Empty;
            }

            var barsPerRow = practiceSections?.BarsPerRow > 0 ? practiceSections.BarsPerRow : 4;
            var sectionLookup = BuildSectionLookup(practiceSections);

            var sb = new StringBuilder();
            int barsInCurrentRow = 0;
            for (int i = 0; i < bars.Count; i++)
            {
                bool isNewSection = sectionLookup.TryGetValue(i, out var label);
                bool isLastBar = i == bars.Count - 1;
                bool nextIsNewSection = !isLastBar && sectionLookup.ContainsKey(i + 1);

                if (isNewSection)
                {
                    if (i > 0)
                    {
                        sb.AppendLine();
                    }
                    sb.AppendLine(FormatSectionLabel(label));
                    barsInCurrentRow = 0;
                }
                else if (barsInCurrentRow == barsPerRow)
                {
                    sb.AppendLine();
                    barsInCurrentRow = 0;
                }

                sb.Append("<c=#6bff6b>|</c>");
                sb.Append(ColorizeNotationLine(bars[i]));
                barsInCurrentRow++;

                bool isEndOfRow = barsInCurrentRow == barsPerRow;
                if (isLastBar || isEndOfRow || nextIsNewSection)
                {
                    sb.Append("<c=#6bff6b>|</c>");
                }
            }

            return sb.ToString().TrimEnd();
        }

        private static List<string> SplitIntoBars(string content)
        {
            var bars = new List<string>();
            var parts = content.Split(new[] { '|' }, StringSplitOptions.None);

            for (int i = 0; i < parts.Length; i++)
            {
                if (!string.IsNullOrEmpty(parts[i]))
                {
                    bars.Add(parts[i]);
                }
            }

            return bars;
        }

        private static Dictionary<int, string> BuildSectionLookup(PracticeSections practiceSections)
        {
            var lookup = new Dictionary<int, string>();
            if (practiceSections?.Sections == null) return lookup;

            foreach (var section in practiceSections.Sections)
            {
                var barIndex = section.Bar;
                if (!lookup.ContainsKey(barIndex))
                {
                    lookup[barIndex] = section.Label;
                }
            }

            return lookup;
        }

        private static string FormatSectionLabel(string label)
        {
            return $"<c=#ffffff>{label}</c>";
        }

        private static string ColorizeNotationLine(string line)
        {
            var sb = new StringBuilder(line.Length * 2);
            int i = 0;

            while (i < line.Length)
            {
                char c = line[i];

                if (c == '|')
                {
                    sb.Append("<c=#6bff6b>|</c>");
                    i++;
                }
                else if (c == '[')
                {
                    int end = line.IndexOf(']', i);
                    if (end > i)
                    {
                        var content = line.Substring(i, end - i + 1);
                        sb.Append("<c=#6bb5ff>").Append(content).Append("</c>");
                        i = end + 1;
                    }
                    else
                    {
                        sb.Append(c);
                        i++;
                    }
                }
                else if (c == '(')
                {
                    int end = line.IndexOf(')', i);
                    if (end > i)
                    {
                        var content = line.Substring(i, end - i + 1);
                        sb.Append("<c=#ff6b6b>").Append(content).Append("</c>");
                        i = end + 1;
                    }
                    else
                    {
                        sb.Append(c);
                        i++;
                    }
                }
                else
                {
                    sb.Append(c);
                    i++;
                }
            }

            return sb.ToString();
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
