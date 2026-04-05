using System;
using System.Collections.Generic;
using System.Linq;

using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;

using Microsoft.Xna.Framework;

using SongbookOfTyria.Models;
using SongbookOfTyria.Services;

namespace SongbookOfTyria.UI.Controls
{
    public sealed class PracticeModePanel : FlowPanel
    {
        private static readonly Logger Logger = Logger.GetLogger<PracticeModePanel>();

        private const int PlayButtonSize = 32;
        private const int MuteButtonSize = 20;
        private const int VolumeRowHeight = 24;
        private const int VolumeLabelWidth = 55;
        private const int PercentLabelWidth = 32;
        private const int VolumeColumnWidth = 230;
        private const int ColumnGap = 10;
        private const int PlaybackControlsWidth = 160;
        private const int MinSliderWidth = 40;
        private const int MinSeekBarWidth = 50;
        private const int VolumeHeaderHeight = 28;
        private const int PlaybackHeaderHeight = 28;
        private const int SectionHeaderHeight = 28;
        private const int SectionPanelHeight = 85;
        private const int SeekBarHeight = 76;
        private const int ButtonSize = 32;
        private const int ButtonSpacing = 4;
        private const int TopMargin = 4;
        private const int RightMargin = 20;
        private const int PanelSpacing = 5;
        private const int MinPanelWidthForSideBySide = 200;


        private readonly MidiPlaybackService _playbackService;
        private readonly MidiData _midiData;
        private readonly PracticeSections _practiceSections;
        private readonly UserSettingsService _userSettingsService;
        private readonly int _tabId;
        private int _panelWidth;
        private readonly AsyncTexture2D _playTexture;
        private readonly AsyncTexture2D _pauseTexture;
        private readonly AsyncTexture2D _volumeTexture;
        private readonly AsyncTexture2D _volumeMutedTexture;

        private Panel _contentPanel;
        private Panel _playbackColumn;
        private Panel _playbackPanelContainer;
        private Panel _sectionsPanelContainer;
        private Panel _markersPanelContainer;
        private FlowPanel _sectionsPanel;
        private FlowPanel _markersPanel;
        private Panel _volumePanelContainer;
        private FlowPanel _volumePanel;
        private Panel _speedPanelContainer;
        private TrackBar _speedTrackBar;
        private Label _speedPercentLabel;
        private GlowButton _playPauseButton;
        private SectionedSeekBar _sectionedSeekBar;
        private TrackVolumeControl _masterVolumeControl;
        private readonly List<TrackVolumeControl> _trackControls = new List<TrackVolumeControl>();
        private readonly List<StandardButton> _sectionButtons = new List<StandardButton>();
        private readonly List<Panel> _markerButtons = new List<Panel>();
        private StandardButton _addMarkerButton;
        private Label _loadingLabel;
        private bool _disposed;
        private double _pendingPosition;
        private volatile bool _positionDirty;
        private volatile bool _pendingPlayState;
        private volatile bool _pendingStopState;
        private volatile bool _pendingFinished;
        private volatile bool _pendingSoundFontLoaded;
        private PracticeModeState _savedState;
        private bool _lastCollapsedState;
        private bool _isStackedLayout;

        public event EventHandler<double> PositionUpdated;
        public event EventHandler<bool> CollapsedChanged;
        public event EventHandler MarkersChanged;

        public PracticeModePanel(MidiPlaybackService playbackService, MidiData midiData, TextureService textureService, int width, UserSettingsService userSettingsService = null, int tabId = 0, PracticeSections practiceSections = null)
        {
            _playbackService = playbackService ?? throw new ArgumentNullException(nameof(playbackService));
            _midiData = midiData ?? throw new ArgumentNullException(nameof(midiData));
            _practiceSections = practiceSections;
            _userSettingsService = userSettingsService;
            _tabId = tabId;
            _panelWidth = width;
            _playTexture = textureService.GetPlayIcon();
            _pauseTexture = textureService.GetPauseIcon();
            _volumeTexture = textureService.GetVolumeIcon();
            _volumeMutedTexture = textureService.GetVolumeMutedIcon();

            ShowBorder = true;
            CanCollapse = true;
            Title = "Playback";
            Width = width;
            HeightSizingMode = SizingMode.AutoSize;
            FlowDirection = ControlFlowDirection.SingleTopToBottom;
            ControlPadding = new Vector2(0, 0);
            OuterControlPadding = new Vector2(5, 5);

            _savedState = _userSettingsService?.GetPracticeModeState(_tabId);
            RestoreSavedState();

            _playbackService.LoadMidiData(midiData);
            _playbackService.PositionChanged += OnPositionChanged;
            _playbackService.PlaybackStarted += OnPlaybackStarted;
            _playbackService.PlaybackStopped += OnPlaybackStopped;
            _playbackService.PlaybackFinished += OnPlaybackFinished;
            _playbackService.SoundFontLoaded += OnSoundFontLoaded;

            if (_playbackService.IsSoundFontLoaded)
            {
                BuildTwoColumnLayout();
            }
            else
            {
                BuildLoadingMessage();
            }

            Resized += OnPanelResized;

            _lastCollapsedState = _userSettingsService?.GetGlobalPlaybackCollapsed() ?? false;
            Collapsed = _lastCollapsedState;
        }

        public int SelectedTrackIndex => _savedState?.SelectedTrackIndex ?? 0;

        private void RestoreSavedState()
        {
            if (_savedState == null) return;

            _playbackService.SetMasterVolume(_savedState.MasterMuted ? 0 : _savedState.MasterVolume);
            _playbackService.SetPlaybackSpeed(_savedState.PlaybackSpeed);

            foreach (var kvp in _savedState.TrackStates)
            {
                _playbackService.SetTrackVolume(kvp.Key, kvp.Value.Volume);
                _playbackService.SetTrackMuted(kvp.Key, kvp.Value.Muted);
            }
        }

        private void BuildLoadingMessage()
        {
            _loadingLabel = new Label
            {
                Text = "Downloading sound data, please wait...",
                Font = GameService.Content.DefaultFont16,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(10, 5),
                Parent = this
            };
        }

        private void OnSoundFontLoaded(object sender, EventArgs e)
        {
            _pendingSoundFontLoaded = true;
        }

        private void RestoreMarkers()
        {
            if (_savedState?.Markers == null || _sectionedSeekBar == null) return;

            var markers = new List<MarkerInfo>();
            for (int i = 0; i < _savedState.Markers.Count; i++)
            {
                var savedMarker = _savedState.Markers[i];
                markers.Add(new MarkerInfo
                {
                    Time = savedMarker.Time,
                    Color = GetMarkerColor(savedMarker.ColorIndex),
                    Id = i + 1
                });
            }
            _sectionedSeekBar.SetMarkers(markers);
        }

        private static Microsoft.Xna.Framework.Color GetMarkerColor(int index)
        {
            var colors = new[]
            {
                new Microsoft.Xna.Framework.Color(46, 204, 113),   // Emerald green
                new Microsoft.Xna.Framework.Color(52, 152, 219),   // Blue
                new Microsoft.Xna.Framework.Color(231, 76, 60),    // Red
                new Microsoft.Xna.Framework.Color(241, 196, 15),   // Yellow
                new Microsoft.Xna.Framework.Color(155, 89, 182),   // Purple
                new Microsoft.Xna.Framework.Color(230, 126, 34),   // Orange
                new Microsoft.Xna.Framework.Color(26, 188, 156),   // Teal
                new Microsoft.Xna.Framework.Color(236, 100, 165),  // Pink
            };
            return colors[index % colors.Length];
        }

        private void SaveState()
        {
            if (_userSettingsService == null || _tabId == 0) return;

            var state = new PracticeModeState
            {
                MasterVolume = _masterVolumeControl?.LastVolume ?? 1.0f,
                MasterMuted = _masterVolumeControl?.VolumeSlider?.Value == 0,
                SelectedTrackIndex = _savedState?.SelectedTrackIndex ?? 0,
                PlaybackSpeed = _playbackService?.PlaybackSpeed ?? 1.0f,
                TrackStates = new Dictionary<int, TrackVolumeState>(),
                Markers = new List<SavedMarker>()
            };

            foreach (var control in _trackControls)
            {
                state.TrackStates[control.TrackIndex] = new TrackVolumeState
                {
                    Volume = control.LastVolume,
                    Muted = _playbackService.IsTrackMuted(control.TrackIndex)
                };
            }

            if (_sectionedSeekBar != null)
            {
                var markers = _sectionedSeekBar.GetMarkers();
                for (int i = 0; i < markers.Count; i++)
                {
                    state.Markers.Add(new SavedMarker
                    {
                        Time = markers[i].Time,
                        ColorIndex = i % 4
                    });
                }
            }

            _savedState = state;
            _userSettingsService.SavePracticeModeState(_tabId, state);
        }

        public void SetSelectedTrackIndex(int trackIndex)
        {
            if (_savedState == null)
            {
                _savedState = new PracticeModeState();
            }

            _savedState.SelectedTrackIndex = trackIndex;
            SaveState();
        }

        public float GetPlaybackSpeed() => _savedState?.PlaybackSpeed ?? 1.0f;

        public void SetPlaybackSpeed(float speed)
        {
            _playbackService?.SetPlaybackSpeed(speed);
            SaveState();
        }

        private void BuildTwoColumnLayout()
        {
            var trackCount = _midiData?.Tracks?.Count ?? 0;
            var volumeRowsNeeded = trackCount + 1;

            _contentPanel = new Panel
            {
                WidthSizingMode = SizingMode.Fill,
                HeightSizingMode = SizingMode.AutoSize,
                Parent = this
            };

            var volumeColumnWidth = Math.Min(VolumeColumnWidth, _panelWidth / 3);
            var playbackColumnWidth = _panelWidth - volumeColumnWidth - ColumnGap - RightMargin;

            _playbackColumn = new Panel
            {
                Width = playbackColumnWidth,
                HeightSizingMode = SizingMode.AutoSize,
                Location = new Point(0, TopMargin),
                Parent = _contentPanel
            };

            _volumePanelContainer = new Panel
            {
                ShowBorder = true,
                Width = volumeColumnWidth,
                HeightSizingMode = SizingMode.AutoSize,
                Location = new Point(playbackColumnWidth + ColumnGap, TopMargin),
                Parent = _contentPanel
            };

            new Label
            {
                Text = "Volume",
                Font = GameService.Content.DefaultFont16,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(10, 5),
                Parent = _volumePanelContainer
            };

            _volumePanel = new FlowPanel
            {
                WidthSizingMode = SizingMode.Fill,
                HeightSizingMode = SizingMode.AutoSize,
                FlowDirection = ControlFlowDirection.SingleTopToBottom,
                ControlPadding = new Vector2(0, 2),
                OuterControlPadding = new Vector2(5, 5),
                Location = new Point(0, VolumeHeaderHeight),
                Parent = _volumePanelContainer
            };

            BuildPlaybackControls();
            BuildVolumeControls(volumeColumnWidth);
            BuildSpeedControls(volumeColumnWidth);

            var maxHeight = Math.Max(_playbackColumn.Height, _speedPanelContainer?.Bottom ?? _volumePanelContainer.Height);
            _contentPanel.Height = maxHeight + 10;
        }

        private void BuildPlaybackControls()
        {
            BuildSectionedSeekBar();
        }

        private void BuildSectionedSeekBar()
        {
            var sections = CalculateSectionTimes();

            _playbackPanelContainer = new Panel
            {
                ShowBorder = true,
                WidthSizingMode = SizingMode.Fill,
                Height = PlaybackHeaderHeight + SeekBarHeight + 5,
                Parent = _playbackColumn
            };

            new Label
            {
                Text = "Control",
                Font = GameService.Content.DefaultFont16,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(10, 5),
                Parent = _playbackPanelContainer
            };

            var controlsPanel = new Panel
            {
                WidthSizingMode = SizingMode.Fill,
                Height = SeekBarHeight,
                Location = new Point(0, PlaybackHeaderHeight),
                Parent = _playbackPanelContainer
            };

            _playPauseButton = new GlowButton
            {
                Icon = _playTexture,
                ActiveIcon = _playTexture,
                BasicTooltipText = "Play",
                Size = new Point(PlayButtonSize, PlayButtonSize),
                Location = new Point(5, 27),
                Parent = controlsPanel
            };
            _playPauseButton.Click += OnPlayPauseClicked;

            _sectionedSeekBar = new SectionedSeekBar
            {
                Width = _playbackColumn.Width - PlayButtonSize - 30,
                Location = new Point(PlayButtonSize + 15, 0),
                Duration = _midiData?.Duration ?? 0,
                Parent = controlsPanel
            };
            _sectionedSeekBar.SetSections(sections);
            _sectionedSeekBar.SeekRequested += OnSectionedSeekBarSeekRequested;
            _sectionedSeekBar.MarkerAdded += OnMarkerChanged;
            _sectionedSeekBar.MarkerRemoved += OnMarkerChanged;
            _sectionedSeekBar.MarkerMoved += OnMarkerChanged;
            RestoreMarkers();

            BuildSectionsAndMarkersRow();
        }

        private void BuildSectionsAndMarkersRow()
        {
            var rowTop = _playbackPanelContainer.Bottom + PanelSpacing;
            var availableWidth = _playbackColumn.Width;
            _isStackedLayout = availableWidth < MinPanelWidthForSideBySide * 2 + PanelSpacing;

            int sectionsPanelWidth;
            int markersPanelWidth;
            int sectionsTop = rowTop;
            int markersTop;
            int markersLeft;

            var sections = _sectionedSeekBar?.GetSections();
            var markerCount = (_savedState?.Markers?.Count ?? 0) + 1;

            if (_isStackedLayout)
            {
                sectionsPanelWidth = availableWidth;
                markersPanelWidth = availableWidth;
                var sectionsHeight = EstimateSectionsPanelHeight(sectionsPanelWidth, sections);
                markersTop = rowTop + sectionsHeight + PanelSpacing;
                markersLeft = 0;
            }
            else
            {
                var totalWidth = availableWidth - PanelSpacing;
                sectionsPanelWidth = Math.Max(80, (int)(totalWidth * 0.65));
                markersPanelWidth = Math.Max(80, totalWidth - sectionsPanelWidth);
                markersTop = rowTop;
                markersLeft = sectionsPanelWidth + PanelSpacing;
            }

            var sectionsHeightFinal = EstimateSectionsPanelHeight(sectionsPanelWidth, sections);
            var markersHeightFinal = CalculatePanelHeight(markersPanelWidth, markerCount);

            _sectionsPanelContainer = new Panel
            {
                ShowBorder = true,
                Height = sectionsHeightFinal,
                Width = sectionsPanelWidth,
                Location = new Point(0, sectionsTop),
                Parent = _playbackColumn
            };

            new Label
            {
                Text = "Sections",
                Font = GameService.Content.DefaultFont16,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(10, 5),
                Parent = _sectionsPanelContainer
            };

            _sectionsPanel = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.LeftToRight,
                ControlPadding = new Vector2(ButtonSpacing, ButtonSpacing),
                OuterControlPadding = new Vector2(ButtonSpacing, ButtonSpacing),
                HeightSizingMode = SizingMode.Fill,
                WidthSizingMode = SizingMode.Fill,
                Location = new Point(0, SectionHeaderHeight),
                Parent = _sectionsPanelContainer
            };

            _markersPanelContainer = new Panel
            {
                ShowBorder = true,
                Height = markersHeightFinal,
                Width = markersPanelWidth,
                Location = new Point(markersLeft, markersTop),
                Parent = _playbackColumn
            };

            new Label
            {
                Text = "Markers",
                Font = GameService.Content.DefaultFont16,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(10, 5),
                Parent = _markersPanelContainer
            };

            _markersPanel = new FlowPanel
            {
                FlowDirection = ControlFlowDirection.LeftToRight,
                ControlPadding = new Vector2(ButtonSpacing, ButtonSpacing),
                OuterControlPadding = new Vector2(ButtonSpacing, ButtonSpacing),
                HeightSizingMode = SizingMode.Fill,
                WidthSizingMode = SizingMode.Fill,
                Location = new Point(0, SectionHeaderHeight),
                Parent = _markersPanelContainer
            };

            _addMarkerButton = new StandardButton
            {
                Text = "Add",
                Width = 50,
                Height = ButtonSize,
                BasicTooltipText = "Add marker at current position",
                Parent = _markersPanel
            };
            _addMarkerButton.Click += (s, e) => _sectionedSeekBar?.AddMarker(_sectionedSeekBar.CurrentPosition);

            BuildSectionButtons();
            RebuildMarkerButtons();
        }

        private void BuildSectionButtons()
        {
            foreach (var btn in _sectionButtons)
            {
                btn.Dispose();
            }
            _sectionButtons.Clear();

            if (_sectionedSeekBar == null) return;

            foreach (var section in _sectionedSeekBar.GetSections())
            {
                var capturedSection = section;
                var buttonWidth = CalculateSectionButtonWidth(section.Label);
                var btn = new StandardButton
                {
                    Text = section.Label,
                    Width = buttonWidth,
                    Height = ButtonSize,
                    BasicTooltipText = $"Jump to {section.Label}",
                    Parent = _sectionsPanel
                };
                btn.Click += (s, e) => OnSectionedSeekBarSeekRequested(this, capturedSection.StartTime);
                _sectionButtons.Add(btn);
            }
        }

        private int CalculateSectionButtonWidth(string label)
        {
            if (string.IsNullOrEmpty(label) || label.Length <= 2) return ButtonSize;

            var textWidth = label.Length * 8;
            return Math.Max(ButtonSize, textWidth + 16);
        }

        private void RebuildMarkerButtons()
        {
            foreach (var btn in _markerButtons)
            {
                btn.Dispose();
            }
            _markerButtons.Clear();

            if (_addMarkerButton != null)
            {
                _addMarkerButton.Parent = null;
            }

            if (_sectionedSeekBar == null) return;

            var markers = _sectionedSeekBar.GetMarkers();
            for (int i = 0; i < markers.Count; i++)
            {
                var marker = markers[i];
                var index = i;

                var markerBtn = new Panel
                {
                    Width = ButtonSize,
                    Height = ButtonSize,
                    BackgroundColor = marker.Color * 0.3f,
                    BasicTooltipText = $"Left-click: Jump to marker\nRight-click: Remove",
                    Parent = _markersPanel
                };

                new Panel
                {
                    Width = 12,
                    Height = 12,
                    BackgroundColor = marker.Color,
                    Location = new Point((ButtonSize - 12) / 2, (ButtonSize - 12) / 2),
                    Parent = markerBtn
                };

                markerBtn.LeftMouseButtonPressed += (s, e) => OnSectionedSeekBarSeekRequested(this, marker.Time);
                markerBtn.RightMouseButtonPressed += (s, e) => _sectionedSeekBar?.RemoveMarker(index);

                _markerButtons.Add(markerBtn);
            }

            if (_addMarkerButton != null)
            {
                _addMarkerButton.Parent = _markersPanel;
            }
        }

        private void OnMarkerChanged(object sender, MarkerInfo e)
        {
            RebuildMarkerButtons();
            UpdateSectionsAndMarkersLayout(_playbackColumn?.Width ?? _panelWidth);
            SaveState();
            MarkersChanged?.Invoke(this, EventArgs.Empty);
        }

        public IReadOnlyList<MarkerInfo> GetMarkers() => _sectionedSeekBar?.GetMarkers() ?? Array.Empty<MarkerInfo>();

        public int GetNoteIndexForTime(double time) => GetNoteIndexForTime(time, -1);

        public int GetNoteIndexForTime(double time, int trackIndex)
        {
            if (_midiData?.Duration <= 0) return 0;

            var notes = GetTrackNotes(trackIndex);
            if (notes != null && notes.Count > 0)
            {
                for (int i = 0; i < notes.Count; i++)
                {
                    if (notes[i].Time >= time)
                    {
                        return i;
                    }
                }
                return notes.Count - 1;
            }

            var totalNotes = GetTotalNoteCount(trackIndex);
            if (totalNotes <= 0) return 0;

            var ratio = time / _midiData.Duration;
            return (int)(ratio * totalNotes);
        }

        private List<MidiNote> GetTrackNotes(int trackIndex)
        {
            if (trackIndex >= 0 && _midiData?.Tracks != null && trackIndex < _midiData.Tracks.Count)
            {
                return _midiData.Tracks[trackIndex].Notes;
            }
            if (_midiData?.Tracks?.Count > 0)
            {
                return _midiData.Tracks[0].Notes;
            }
            return null;
        }

        private int GetTotalNoteCount(int trackIndex)
        {
            var notation = GetNotationForTrack(trackIndex);
            if (string.IsNullOrEmpty(notation)) return 0;

            int count = 0;
            foreach (var c in notation)
            {
                if ((c >= '1' && c <= '8') || (c >= '\u2460' && c <= '\u24FF'))
                {
                    count++;
                }
            }
            return count;
        }

        private string GetNotationForTrack(int trackIndex)
        {
            if (trackIndex < 0 || _midiData?.Tracks == null || trackIndex >= _midiData.Tracks.Count)
            {
                return null;
            }
            return _midiData.Tracks[trackIndex].Notation;
        }

        private List<SectionInfo> CalculateSectionTimes()
        {
            var sections = new List<SectionInfo>();
            if (_practiceSections?.Sections == null || _practiceSections.Sections.Count == 0) return sections;

            var duration = _midiData?.Duration ?? 0;
            if (duration <= 0) return sections;

            var barTimes = CalculateBarTimes();
            Logger.Debug($"CalculateSectionTimes: duration={duration:F2}s, barTimes.Count={barTimes.Count}");

            for (int i = 0; i < _practiceSections.Sections.Count; i++)
            {
                var section = _practiceSections.Sections[i];
                var barIndex = section.Bar;
                var startTime = GetTimeForBar(barIndex, barTimes, duration);

                double endTime;
                if (i + 1 < _practiceSections.Sections.Count)
                {
                    var nextSection = _practiceSections.Sections[i + 1];
                    var nextBarIndex = nextSection.Bar;
                    endTime = GetTimeForBar(nextBarIndex, barTimes, duration);
                }
                else
                {
                    endTime = duration;
                }

                Logger.Debug($"Section '{section.Label}': Bar={section.Bar} -> barIndex={barIndex}, startTime={startTime:F2}s, endTime={endTime:F2}s");

                sections.Add(new SectionInfo
                {
                    Label = section.Label,
                    StartTime = Math.Min(startTime, duration),
                    EndTime = Math.Min(endTime, duration)
                });
            }

            return sections;
        }

        private double GetTimeForBar(int barIndex, List<double> barTimes, double duration)
        {
            if (barTimes == null || barTimes.Count == 0)
            {
                var timePerBar = CalculateTimePerBar();
                return Math.Min(barIndex * timePerBar, duration);
            }

            if (barIndex < barTimes.Count)
            {
                return barTimes[barIndex];
            }

            if (barTimes.Count >= 2)
            {
                var lastBarTime = barTimes[barTimes.Count - 1];
                var avgBarDuration = lastBarTime / (barTimes.Count - 1);
                var extraBars = barIndex - (barTimes.Count - 1);
                return Math.Min(lastBarTime + extraBars * avgBarDuration, duration);
            }

            return Math.Min(barIndex * CalculateTimePerBar(), duration);
        }

        private List<double> CalculateBarTimes()
        {
            var barTimes = new List<double>();
            if (_midiData == null) return barTimes;

            var ppq = _midiData.Ppq > 0 ? _midiData.Ppq : 480;
            var duration = _midiData.Duration;
            var tempos = _midiData.Tempos ?? new List<MidiTempo>();
            var timeSignatures = _midiData.TimeSignatures ?? new List<MidiTimeSignature>();

            if (tempos.Count == 0)
            {
                tempos = new List<MidiTempo> { new MidiTempo { Ticks = 0, Bpm = 120 } };
            }
            if (timeSignatures.Count == 0)
            {
                timeSignatures = new List<MidiTimeSignature> { new MidiTimeSignature { Ticks = 0, Numerator = 4, Denominator = 4 } };
            }

            int tempoIndex = 0;
            int tsIndex = 0;
            double currentTime = 0;
            int currentTick = 0;

            int maxBars = (int)(duration / 0.5) + 10;

            for (int bar = 0; bar < maxBars && currentTime < duration; bar++)
            {
                barTimes.Add(currentTime);

                while (tempoIndex + 1 < tempos.Count && tempos[tempoIndex + 1].Ticks <= currentTick)
                {
                    tempoIndex++;
                }
                while (tsIndex + 1 < timeSignatures.Count && timeSignatures[tsIndex + 1].Ticks <= currentTick)
                {
                    tsIndex++;
                }

                var currentTempo = tempos[tempoIndex];
                var currentTs = timeSignatures[tsIndex];

                var beatsPerBar = currentTs.Numerator;
                var beatUnit = currentTs.Denominator;
                var ticksPerBeat = ppq * 4 / beatUnit;
                var ticksPerBar = beatsPerBar * ticksPerBeat;

                var secondsPerTick = 60.0 / (currentTempo.Bpm * ppq);
                var barDuration = ticksPerBar * secondsPerTick;

                currentTick += ticksPerBar;
                currentTime += barDuration;
            }

            return barTimes;
        }

        private double CalculateTimePerBar()
        {
            if (_midiData?.Tempos == null || _midiData.Tempos.Count == 0 || _midiData?.TimeSignatures == null || _midiData.TimeSignatures.Count == 0)
            {
                return 2.0;
            }

            var tempo = _midiData.Tempos[0];
            var timeSignature = _midiData.TimeSignatures[0];

            var beatsPerBar = timeSignature.Numerator;
            var secondsPerBeat = 60.0 / tempo.Bpm;

            return beatsPerBar * secondsPerBeat;
        }

        private void OnSectionedSeekBarSeekRequested(object sender, double seekTime)
        {
            _playbackService.Seek(seekTime);
        }

        private void BuildVolumeControls(int columnWidth)
        {
            int sliderWidth = Math.Max(MinSliderWidth, columnWidth - VolumeLabelWidth - MuteButtonSize - PercentLabelWidth - 30);

            var savedMasterVolume = _savedState?.MasterVolume ?? 1.0f;
            var savedMasterMuted = _savedState?.MasterMuted ?? false;
            var currentMasterValue = savedMasterMuted ? 0 : savedMasterVolume * 100;

            var masterRow = new Panel
            {
                WidthSizingMode = SizingMode.Fill,
                Height = VolumeRowHeight,
                Parent = _volumePanel
            };

            new Label
            {
                Text = "Master",
                Font = GameService.Content.DefaultFont12,
                Width = VolumeLabelWidth,
                AutoSizeHeight = true,
                Location = new Point(0, (VolumeRowHeight - 14) / 2),
                Parent = masterRow
            };

            var masterMuteButton = new GlowButton
            {
                Icon = savedMasterMuted ? _volumeMutedTexture : _volumeTexture,
                ActiveIcon = savedMasterMuted ? _volumeMutedTexture : _volumeTexture,
                BasicTooltipText = savedMasterMuted ? "Unmute" : "Mute",
                Size = new Point(MuteButtonSize, MuteButtonSize),
                Location = new Point(VolumeLabelWidth, (VolumeRowHeight - MuteButtonSize) / 2),
                Parent = masterRow
            };

            var masterSlider = new TrackBar
            {
                MinValue = 0,
                MaxValue = 100,
                Value = (int)currentMasterValue,
                SmallStep = true,
                Width = sliderWidth,
                Height = 16,
                Location = new Point(VolumeLabelWidth + MuteButtonSize + 5, (VolumeRowHeight - 16) / 2),
                Parent = masterRow
            };

            var masterPercentLabel = new Label
            {
                Text = $"{(int)masterSlider.Value}%",
                Font = GameService.Content.DefaultFont12,
                Width = PercentLabelWidth,
                AutoSizeHeight = true,
                Location = new Point(masterSlider.Right + 3, (VolumeRowHeight - 14) / 2),
                Parent = masterRow
            };

            _masterVolumeControl = new TrackVolumeControl
            {
                TrackIndex = -1,
                MuteButton = masterMuteButton,
                VolumeSlider = masterSlider,
                PercentLabel = masterPercentLabel,
                LastVolume = savedMasterVolume
            };

            masterMuteButton.Click += (s, e) => OnMasterMuteClicked();
            masterSlider.ValueChanged += (s, e) => OnMasterVolumeChanged(s, e);

            BuildTrackVolumeControls(sliderWidth);
        }

        private void BuildTrackVolumeControls(int sliderWidth)
        {
            if (_midiData?.Tracks == null || _midiData.Tracks.Count == 0) return;

            foreach (var track in _midiData.Tracks)
            {
                var trackControl = CreateTrackVolumeControl(track, sliderWidth);
                _trackControls.Add(trackControl);
            }
        }

        private void BuildSpeedControls(int columnWidth)
        {
            var savedSpeed = _savedState?.PlaybackSpeed ?? 1.0f;
            var playbackColumnWidth = _panelWidth - columnWidth - ColumnGap - RightMargin;

            var volumePanelHeight = CalculateVolumePanelHeight();

            _speedPanelContainer = new Panel
            {
                ShowBorder = true,
                Width = columnWidth,
                HeightSizingMode = SizingMode.AutoSize,
                Location = new Point(playbackColumnWidth + ColumnGap, TopMargin + volumePanelHeight + PanelSpacing),
                Parent = _contentPanel
            };

            new Label
            {
                Text = "Speed",
                Font = GameService.Content.DefaultFont16,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(10, 5),
                Parent = _speedPanelContainer
            };

            var speedPanel = new Panel
            {
                WidthSizingMode = SizingMode.Fill,
                Height = VolumeRowHeight + 10,
                Location = new Point(0, VolumeHeaderHeight),
                Parent = _speedPanelContainer
            };

            int sliderWidth = Math.Max(MinSliderWidth, columnWidth - PercentLabelWidth - 20);

            _speedTrackBar = new TrackBar
            {
                MinValue = 50,
                MaxValue = 100,
                Value = savedSpeed * 100,
                SmallStep = true,
                Width = sliderWidth,
                Height = 16,
                Location = new Point(5, (VolumeRowHeight - 16) / 2),
                Parent = speedPanel
            };
            _speedTrackBar.ValueChanged += OnSpeedTrackBarChanged;

            _speedPercentLabel = new Label
            {
                Text = $"{(int)(savedSpeed * 100)}%",
                Font = GameService.Content.DefaultFont12,
                Width = PercentLabelWidth,
                AutoSizeHeight = true,
                Location = new Point(_speedTrackBar.Right + 3, (VolumeRowHeight - 14) / 2),
                Parent = speedPanel
            };
        }

        private int CalculateVolumePanelHeight()
        {
            var trackCount = _midiData?.Tracks?.Count ?? 0;
            var rowCount = trackCount + 1;
            var flowPanelPadding = 10;
            var rowSpacing = 2;
            var contentHeight = rowCount * VolumeRowHeight + (rowCount - 1) * rowSpacing + flowPanelPadding;
            return VolumeHeaderHeight + contentHeight;
        }

        private void OnSpeedTrackBarChanged(object sender, ValueEventArgs<float> e)
        {
            var speed = e.Value / 100f;
            _playbackService?.SetPlaybackSpeed(speed);
            if (_speedPercentLabel != null)
            {
                _speedPercentLabel.Text = $"{(int)e.Value}%";
            }
            SaveState();
        }

        private TrackVolumeControl CreateTrackVolumeControl(MidiTrack track, int sliderWidth)
        {
            var trackIndex = track.Index;

            var trackRow = new Panel
            {
                WidthSizingMode = SizingMode.Fill,
                Height = VolumeRowHeight,
                Parent = _volumePanel
            };

            new Label
            {
                Text = track.GetDisplayName(),
                Font = GameService.Content.DefaultFont12,
                Width = VolumeLabelWidth,
                AutoSizeHeight = true,
                Location = new Point(0, (VolumeRowHeight - 14) / 2),
                Parent = trackRow
            };

            TrackVolumeState savedTrackState = null;
            _savedState?.TrackStates?.TryGetValue(trackIndex, out savedTrackState);
            var savedTrackVolume = savedTrackState?.Volume ?? 1.0f;
            var savedTrackMuted = savedTrackState?.Muted ?? false;

            var muteButton = new GlowButton
            {
                Icon = savedTrackMuted ? _volumeMutedTexture : _volumeTexture,
                ActiveIcon = savedTrackMuted ? _volumeMutedTexture : _volumeTexture,
                BasicTooltipText = savedTrackMuted ? "Unmute" : "Mute",
                Size = new Point(MuteButtonSize, MuteButtonSize),
                Location = new Point(VolumeLabelWidth, (VolumeRowHeight - MuteButtonSize) / 2),
                Parent = trackRow
            };

            var volumeSlider = new TrackBar
            {
                MinValue = 0,
                MaxValue = 100,
                Value = savedTrackVolume * 100,
                SmallStep = true,
                Width = sliderWidth,
                Height = 16,
                Location = new Point(VolumeLabelWidth + MuteButtonSize + 5, (VolumeRowHeight - 16) / 2),
                Parent = trackRow
            };

            var percentLabel = new Label
            {
                Text = $"{(int)volumeSlider.Value}%",
                Font = GameService.Content.DefaultFont12,
                Width = PercentLabelWidth,
                AutoSizeHeight = true,
                Location = new Point(volumeSlider.Right + 3, (VolumeRowHeight - 14) / 2),
                Parent = trackRow
            };

            var trackControl = new TrackVolumeControl
            {
                TrackIndex = trackIndex,
                MuteButton = muteButton,
                VolumeSlider = volumeSlider,
                PercentLabel = percentLabel,
                LastVolume = savedTrackVolume
            };

            muteButton.Click += (s, e) => OnTrackMuteClicked(trackControl);
            volumeSlider.ValueChanged += (s, e) => OnTrackVolumeChanged(trackControl, e.Value);

            return trackControl;
        }

        private void OnMasterMuteClicked()
        {
            if (_masterVolumeControl == null) return;

            bool isMuted = _masterVolumeControl.IsMuted;
            if (!isMuted)
            {
                _masterVolumeControl.LastVolume = _masterVolumeControl.CurrentVolume;
            }

            _masterVolumeControl.SetMuted(!isMuted, _volumeTexture, _volumeMutedTexture);
            _playbackService.SetMasterVolume(isMuted ? _masterVolumeControl.CurrentVolume : 0);
            SaveState();
        }

        private void OnTrackMuteClicked(TrackVolumeControl control)
        {
            bool isMuted = _playbackService.IsTrackMuted(control.TrackIndex);
            if (!isMuted)
            {
                control.LastVolume = control.CurrentVolume;
            }

            _playbackService.SetTrackMuted(control.TrackIndex, !isMuted);
            control.SetMuted(!isMuted, _volumeTexture, _volumeMutedTexture);
            SaveState();
        }

        private void OnTrackVolumeChanged(TrackVolumeControl control, float value)
        {
            var volume = value / 100f;
            _playbackService.SetTrackVolume(control.TrackIndex, volume);

            if (volume > 0 && _playbackService.IsTrackMuted(control.TrackIndex))
            {
                _playbackService.SetTrackMuted(control.TrackIndex, false);
            }

            control.UpdateFromVolumeChange(value, _volumeTexture, _volumeMutedTexture);
            SaveState();
        }

        private void OnMasterVolumeChanged(object sender, ValueEventArgs<float> e)
        {
            _playbackService.SetMasterVolume(e.Value / 100f);
            _masterVolumeControl?.UpdateFromVolumeChange(e.Value, _volumeTexture, _volumeMutedTexture);
            SaveState();
        }

        private void OnPanelResized(object sender, ResizedEventArgs e)
        {
            CheckCollapsedStateChanged();

            if (Width == _panelWidth || Width <= 0) return;

            _panelWidth = Width;
            UpdateControlLayout();
        }

        private void CheckCollapsedStateChanged()
        {
            if (Collapsed == _lastCollapsedState) return;
            _lastCollapsedState = Collapsed;
            CollapsedChanged?.Invoke(this, Collapsed);
        }

        private void UpdateControlLayout()
        {
            var volumeColumnWidth = Math.Min(VolumeColumnWidth, _panelWidth / 3);
            var playbackColumnWidth = _panelWidth - volumeColumnWidth - ColumnGap - RightMargin;

            if (_playbackColumn != null)
            {
                _playbackColumn.Width = playbackColumnWidth;
            }

            if (_volumePanelContainer != null)
            {
                _volumePanelContainer.Width = volumeColumnWidth;
                _volumePanelContainer.Location = new Point(playbackColumnWidth + ColumnGap, TopMargin);
            }

            if (_speedPanelContainer != null)
            {
                var volumePanelHeight = CalculateVolumePanelHeight();
                _speedPanelContainer.Width = volumeColumnWidth;
                _speedPanelContainer.Location = new Point(playbackColumnWidth + ColumnGap, TopMargin + volumePanelHeight + PanelSpacing);
            }

            if (_sectionedSeekBar != null)
            {
                _sectionedSeekBar.Width = playbackColumnWidth - PlayButtonSize - 30;
            }

            UpdateSectionsAndMarkersLayout(playbackColumnWidth);
            UpdateVolumeControlLayout(volumeColumnWidth);
            UpdateSpeedControlLayout(volumeColumnWidth);
        }

        private void UpdateSectionsAndMarkersLayout(int availableWidth)
        {
            if (_sectionsPanelContainer == null || _markersPanelContainer == null || _playbackPanelContainer == null) return;

            var rowTop = _playbackPanelContainer.Bottom + PanelSpacing;
            _isStackedLayout = availableWidth < MinPanelWidthForSideBySide * 2 + PanelSpacing;

            int sectionsPanelWidth;
            int markersPanelWidth;
            int markersTop;
            int markersLeft;

            var markerButtonCount = _markerButtons.Count + 1;

            if (_isStackedLayout)
            {
                sectionsPanelWidth = availableWidth;
                markersPanelWidth = availableWidth;
                markersTop = rowTop + CalculateSectionsPanelHeight(sectionsPanelWidth) + PanelSpacing;
                markersLeft = 0;
            }
            else
            {
                var totalWidth = availableWidth - PanelSpacing;
                sectionsPanelWidth = Math.Max(80, (int)(totalWidth * 0.65));
                markersPanelWidth = Math.Max(80, totalWidth - sectionsPanelWidth);
                markersTop = rowTop;
                markersLeft = sectionsPanelWidth + PanelSpacing;
            }

            var sectionsHeight = CalculateSectionsPanelHeight(sectionsPanelWidth);
            var markersHeight = CalculatePanelHeight(markersPanelWidth, markerButtonCount);

            _sectionsPanelContainer.Width = sectionsPanelWidth;
            _sectionsPanelContainer.Height = sectionsHeight;
            _sectionsPanelContainer.Location = new Point(0, rowTop);

            _markersPanelContainer.Width = markersPanelWidth;
            _markersPanelContainer.Height = markersHeight;
            _markersPanelContainer.Location = new Point(markersLeft, markersTop);
        }

        private int CalculatePanelHeight(int panelWidth, int buttonCount)
        {
            if (buttonCount <= 0) return SectionPanelHeight;

            var flowPanelPadding = ButtonSpacing * 2;
            var contentWidth = panelWidth - flowPanelPadding;
            var buttonWithSpacing = ButtonSize + ButtonSpacing;
            var buttonsPerRow = Math.Max(1, contentWidth / buttonWithSpacing);
            var rowsNeeded = (int)Math.Ceiling((double)buttonCount / buttonsPerRow);
            var contentHeight = rowsNeeded * buttonWithSpacing + flowPanelPadding;

            return Math.Max(SectionPanelHeight, SectionHeaderHeight + contentHeight + 5);
        }

        private int CalculateSectionsPanelHeight(int panelWidth)
        {
            if (_sectionButtons.Count == 0) return SectionPanelHeight;
            return CalculateFlowPanelHeight(panelWidth, _sectionButtons.Select(b => b.Width));
        }

        private int EstimateSectionsPanelHeight(int panelWidth, IReadOnlyList<SectionInfo> sections)
        {
            if (sections == null || sections.Count == 0) return SectionPanelHeight;
            return CalculateFlowPanelHeight(panelWidth, sections.Select(s => CalculateSectionButtonWidth(s.Label)));
        }

        private int CalculateFlowPanelHeight(int panelWidth, IEnumerable<int> buttonWidths)
        {
            var flowPanelPadding = ButtonSpacing * 2;
            var contentWidth = panelWidth - flowPanelPadding;
            int currentRowWidth = 0;
            int rowsNeeded = 1;

            foreach (var width in buttonWidths)
            {
                var buttonWithSpacing = width + ButtonSpacing;
                if (currentRowWidth + buttonWithSpacing > contentWidth && currentRowWidth > 0)
                {
                    rowsNeeded++;
                    currentRowWidth = buttonWithSpacing;
                }
                else
                {
                    currentRowWidth += buttonWithSpacing;
                }
            }

            var contentHeight = rowsNeeded * (ButtonSize + ButtonSpacing) + flowPanelPadding;
            return Math.Max(SectionPanelHeight, SectionHeaderHeight + contentHeight + 5);
        }

        private void UpdateVolumeControlLayout(int columnWidth)
        {
            int sliderWidth = Math.Max(MinSliderWidth, columnWidth - VolumeLabelWidth - MuteButtonSize - PercentLabelWidth - 30);

            if (_masterVolumeControl?.VolumeSlider != null)
            {
                _masterVolumeControl.VolumeSlider.Width = sliderWidth;
                if (_masterVolumeControl.PercentLabel != null)
                {
                    _masterVolumeControl.PercentLabel.Location = new Point(_masterVolumeControl.VolumeSlider.Right + 3, _masterVolumeControl.PercentLabel.Location.Y);
                }
            }

            foreach (var control in _trackControls)
            {
                if (control.VolumeSlider != null)
                {
                    control.VolumeSlider.Width = sliderWidth;
                    if (control.PercentLabel != null)
                    {
                        control.PercentLabel.Location = new Point(control.VolumeSlider.Right + 3, control.PercentLabel.Location.Y);
                    }
                }
            }
        }

        private void UpdateSpeedControlLayout(int columnWidth)
        {
            int sliderWidth = Math.Max(MinSliderWidth, columnWidth - PercentLabelWidth - 20);

            if (_speedTrackBar != null)
            {
                _speedTrackBar.Width = sliderWidth;
                if (_speedPercentLabel != null)
                {
                    _speedPercentLabel.Location = new Point(_speedTrackBar.Right + 3, _speedPercentLabel.Location.Y);
                }
            }
        }

        private void OnPlayPauseClicked(object sender, Blish_HUD.Input.MouseEventArgs e)
        {
            if (_playbackService.IsPlaying)
            {
                _playbackService.Pause();
            }
            else
            {
                _playbackService.Play();
            }
        }

        private void OnPositionChanged(object sender, double position)
        {
            _pendingPosition = position;
            _positionDirty = true;
        }

        private void OnPlaybackStarted(object sender, EventArgs e)
        {
            _pendingPlayState = true;
        }

        private void OnPlaybackStopped(object sender, EventArgs e)
        {
            _pendingStopState = true;
        }

        private void OnPlaybackFinished(object sender, EventArgs e)
        {
            _pendingFinished = true;
        }

        public override void UpdateContainer(GameTime gameTime)
        {
            base.UpdateContainer(gameTime);

            if (_pendingSoundFontLoaded)
            {
                _pendingSoundFontLoaded = false;
                _loadingLabel?.Dispose();
                _loadingLabel = null;
                BuildTwoColumnLayout();
            }

            if (_pendingPlayState)
            {
                _pendingPlayState = false;
                UpdatePlayPauseButton(true);
            }

            if (_pendingStopState)
            {
                _pendingStopState = false;
                UpdatePlayPauseButton(false);
            }

            if (_pendingFinished)
            {
                _pendingFinished = false;
                UpdatePlayPauseButton(false);
                UpdateSeekBar(0);
            }

            if (_positionDirty)
            {
                _positionDirty = false;
                var pos = _pendingPosition;
                UpdateSeekBar(pos);
                PositionUpdated?.Invoke(this, pos);
            }
        }

        private void UpdatePlayPauseButton(bool isPlaying)
        {
            if (_playPauseButton == null) return;

            if (isPlaying)
            {
                _playPauseButton.Icon = _pauseTexture;
                _playPauseButton.ActiveIcon = _pauseTexture;
                _playPauseButton.BasicTooltipText = "Pause";
            }
            else
            {
                _playPauseButton.Icon = _playTexture;
                _playPauseButton.ActiveIcon = _playTexture;
                _playPauseButton.BasicTooltipText = "Play";
            }
        }

        private void UpdateSeekBar(double position)
        {
            if (_sectionedSeekBar == null) return;
            _sectionedSeekBar.CurrentPosition = position;
        }

        protected override void DisposeControl()
        {
            if (_disposed) return;
            _disposed = true;

            _playbackService.PositionChanged -= OnPositionChanged;
            _playbackService.PlaybackStarted -= OnPlaybackStarted;
            _playbackService.PlaybackStopped -= OnPlaybackStopped;
            _playbackService.PlaybackFinished -= OnPlaybackFinished;
            _playbackService.SoundFontLoaded -= OnSoundFontLoaded;
            _playbackService.Stop();

            if (_sectionedSeekBar != null)
            {
                _sectionedSeekBar.SeekRequested -= OnSectionedSeekBarSeekRequested;
                _sectionedSeekBar.MarkerAdded -= OnMarkerChanged;
                _sectionedSeekBar.MarkerRemoved -= OnMarkerChanged;
                _sectionedSeekBar.MarkerMoved -= OnMarkerChanged;
            }

            if (_playPauseButton != null)
            {
                _playPauseButton.Click -= OnPlayPauseClicked;
            }

            Resized -= OnPanelResized;

            base.DisposeControl();
        }

        private sealed class TrackVolumeControl
        {
            public int TrackIndex { get; set; }
            public GlowButton MuteButton { get; set; }
            public TrackBar VolumeSlider { get; set; }
            public Label PercentLabel { get; set; }
            public float LastVolume { get; set; }

            public bool IsMuted => VolumeSlider?.Value == 0;
            public float CurrentVolume => (VolumeSlider?.Value ?? 0) / 100f;

            public void SetMuted(bool muted, AsyncTexture2D volumeIcon, AsyncTexture2D mutedIcon)
            {
                if (muted)
                {
                    if (VolumeSlider != null) VolumeSlider.Value = 0;
                    if (PercentLabel != null) PercentLabel.Text = "0%";
                }
                else
                {
                    var restoreVolume = LastVolume > 0 ? LastVolume : 1.0f;
                    if (VolumeSlider != null) VolumeSlider.Value = restoreVolume * 100;
                    if (PercentLabel != null) PercentLabel.Text = $"{(int)(restoreVolume * 100)}%";
                }
                UpdateMuteButtonVisuals(muted, volumeIcon, mutedIcon);
            }

            public void UpdateMuteButtonVisuals(bool muted, AsyncTexture2D volumeIcon, AsyncTexture2D mutedIcon)
            {
                if (MuteButton == null) return;
                MuteButton.Icon = muted ? mutedIcon : volumeIcon;
                MuteButton.ActiveIcon = muted ? mutedIcon : volumeIcon;
                MuteButton.BasicTooltipText = muted ? "Unmute" : "Mute";
            }

            public void UpdateFromVolumeChange(float value, AsyncTexture2D volumeIcon, AsyncTexture2D mutedIcon)
            {
                if (PercentLabel != null) PercentLabel.Text = $"{(int)value}%";
                if (value > 0)
                {
                    LastVolume = value / 100f;
                    UpdateMuteButtonVisuals(false, volumeIcon, mutedIcon);
                }
                else
                {
                    UpdateMuteButtonVisuals(true, volumeIcon, mutedIcon);
                }
            }
        }
    }
}
