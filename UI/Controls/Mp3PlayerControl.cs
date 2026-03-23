using System;

using Blish_HUD;
using Blish_HUD.Content;
using Blish_HUD.Controls;

using Microsoft.Xna.Framework;

using SongbookOfTyria.Services;

namespace SongbookOfTyria.UI.Controls
{
    public class Mp3PlayerControl : Panel
    {
        private const int IconSize = 32;
        private const int VolumeIconSize = 24;
        private const int TimeLabelWidth = 90;
        private const int VolumeTrackBarWidth = 80;
        private const int TrackBarMaxValue = 1000;
        private const int ControlHeight = 32;
        private const int SeekBarHeight = 16;
        private const float DefaultVolume = 0.5f;

        private readonly AudioService _audioService;
        private readonly TextureService _textureService;
        private readonly string _mp3Url;

        private readonly AsyncTexture2D _playTexture;
        private readonly AsyncTexture2D _pauseTexture;
        private readonly AsyncTexture2D _volumeTexture;
        private readonly AsyncTexture2D _volumeMutedTexture;

        private GlowButton _playPauseButton;
        private TrackBar _seekBar;
        private Label _timeLabel;
        private Label _loadingLabel;
        private GlowButton _volumeButton;
        private TrackBar _volumeTrackBar;
        private bool _isLoading;
        private bool _isLoaded;
        private bool _disposed;
        private bool _isSeeking;
        private float _lastVolume = DefaultVolume;
        private float _currentVolume = DefaultVolume;

        public Mp3PlayerControl(AudioService audioService, TextureService textureService, string mp3Url)
        {
            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
            _textureService = textureService ?? throw new ArgumentNullException(nameof(textureService));
            _mp3Url = mp3Url;

            _playTexture = _textureService.GetPlayIcon();
            _pauseTexture = _textureService.GetPauseIcon();
            _volumeTexture = _textureService.GetVolumeIcon();
            _volumeMutedTexture = _textureService.GetVolumeMutedIcon();

            WidthSizingMode = SizingMode.Fill;
            Height = ControlHeight;

            BuildControls();
            SubscribeToEvents();

            if (!string.IsNullOrEmpty(_mp3Url))
            {
                LoadAudioAsync();
            }
        }

        private void BuildControls()
        {
            var xOffset = 0;

            _playPauseButton = new GlowButton
            {
                Icon = _playTexture,
                ActiveIcon = _playTexture,
                Size = new Point(IconSize, IconSize),
                Location = new Point(xOffset, 0),
                BasicTooltipText = "Play",
                Parent = this
            };
            _playPauseButton.Click += OnPlayPauseClick;
            xOffset += IconSize + 8;

            _loadingLabel = new Label
            {
                Text = "Loading audio...",
                Font = GameService.Content.DefaultFont12,
                AutoSizeWidth = true,
                AutoSizeHeight = true,
                Location = new Point(xOffset, (ControlHeight - 14) / 2),
                Visible = false,
                Parent = this
            };

            _seekBar = new TrackBar
            {
                MinValue = 0,
                MaxValue = TrackBarMaxValue,
                Value = 0,
                SmallStep = true,
                Location = new Point(xOffset, (ControlHeight - SeekBarHeight) / 2),
                Enabled = false,
                Parent = this
            };
            _seekBar.ValueChanged += OnSeekBarValueChanged;
            _seekBar.LeftMouseButtonPressed += OnSeekBarMousePressed;
            _seekBar.LeftMouseButtonReleased += OnSeekBarMouseReleased;

            _timeLabel = new Label
            {
                Text = "0:00 / 0:00",
                Font = GameService.Content.DefaultFont12,
                TextColor = Color.LightGray,
                Width = TimeLabelWidth,
                AutoSizeHeight = true,
                HorizontalAlignment = HorizontalAlignment.Left,
                Parent = this
            };

            _volumeButton = new GlowButton
            {
                Icon = _volumeTexture,
                ActiveIcon = _volumeTexture,
                Size = new Point(VolumeIconSize, VolumeIconSize),
                BasicTooltipText = "Mute",
                Parent = this
            };
            _volumeButton.Click += OnVolumeButtonClick;

            _volumeTrackBar = new TrackBar
            {
                MinValue = 0,
                MaxValue = 100,
                Value = _currentVolume * 100,
                SmallStep = true,
                Width = VolumeTrackBarWidth,
                Parent = this
            };
            _volumeTrackBar.ValueChanged += OnVolumeTrackBarChanged;

            _audioService.SetVolume(_currentVolume);

            UpdateLayout();
        }

        private void SubscribeToEvents()
        {
            _audioService.StateChanged += OnAudioStateChanged;
            _audioService.PositionChanged += OnAudioPositionChanged;
        }

        private void UnsubscribeFromEvents()
        {
            _audioService.StateChanged -= OnAudioStateChanged;
            _audioService.PositionChanged -= OnAudioPositionChanged;
        }

        private async void LoadAudioAsync()
        {
            if (string.IsNullOrEmpty(_mp3Url))
            {
                return;
            }

            _isLoading = true;
            _loadingLabel.Visible = true;
            _seekBar.Visible = false;

            try
            {
                await _audioService.LoadAsync(_mp3Url).ConfigureAwait(false);
                _isLoaded = _audioService.IsLoaded;

                GameService.Overlay.QueueMainThreadUpdate(_ =>
                {
                    if (_disposed)
                    {
                        return;
                    }

                    _isLoading = false;
                    _loadingLabel.Visible = false;
                    _seekBar.Visible = true;
                    UpdateIconStates();
                    UpdateTimeLabels();
                });
            }
            catch (Exception ex)
            {
                Logger.GetLogger<Mp3PlayerControl>().Warn(ex, "Failed to load audio");

                GameService.Overlay.QueueMainThreadUpdate(_ =>
                {
                    if (_disposed)
                    {
                        return;
                    }

                    _isLoading = false;
                    _loadingLabel.Text = "Failed to load";
                    _loadingLabel.TextColor = Color.Red;
                });
            }
        }

        private void OnPlayPauseClick(object sender, Blish_HUD.Input.MouseEventArgs e)
        {
            if (!_isLoaded)
            {
                return;
            }

            if (_audioService.PlaybackState == AudioPlaybackState.Playing)
            {
                _audioService.Pause();
            }
            else
            {
                _audioService.Play();
            }
        }

        private void OnAudioStateChanged(object sender, AudioStateChangedEventArgs e)
        {
            GameService.Overlay.QueueMainThreadUpdate(_ =>
            {
                if (_disposed)
                {
                    return;
                }

                UpdateIconStates();
            });
        }

        private void OnAudioPositionChanged(object sender, AudioPositionChangedEventArgs e)
        {
            GameService.Overlay.QueueMainThreadUpdate(_ =>
            {
                if (_disposed)
                {
                    return;
                }

                UpdateTimeLabels();
                UpdateSeekBar();
            });
        }

        private void OnSeekBarValueChanged(object sender, ValueEventArgs<float> e)
        {
            if (!_isLoaded || !_isSeeking)
            {
                return;
            }

            var totalSeconds = _audioService.TotalDuration.TotalSeconds;
            if (totalSeconds <= 0)
            {
                return;
            }

            var seekPosition = TimeSpan.FromSeconds((e.Value / TrackBarMaxValue) * totalSeconds);
            _audioService.Seek(seekPosition);
            UpdateTimeLabels();
        }

        private void OnSeekBarMousePressed(object sender, Blish_HUD.Input.MouseEventArgs e)
        {
            _isSeeking = true;
        }

        private void OnSeekBarMouseReleased(object sender, Blish_HUD.Input.MouseEventArgs e)
        {
            _isSeeking = false;
        }

        private void OnVolumeButtonClick(object sender, Blish_HUD.Input.MouseEventArgs e)
        {
            if (_currentVolume > 0)
            {
                _lastVolume = _currentVolume;
                _currentVolume = 0;
            }
            else
            {
                _currentVolume = _lastVolume > 0 ? _lastVolume : DefaultVolume;
            }

            _volumeTrackBar.Value = _currentVolume * 100;
            _audioService.SetVolume(_currentVolume);
            UpdateVolumeIcon();
        }

        private void OnVolumeTrackBarChanged(object sender, ValueEventArgs<float> e)
        {
            _currentVolume = e.Value / 100f;
            _audioService.SetVolume(_currentVolume);
            UpdateVolumeIcon();
        }

        private void UpdateVolumeIcon()
        {
            if (_currentVolume <= 0)
            {
                _volumeButton.Icon = _volumeMutedTexture;
                _volumeButton.ActiveIcon = _volumeMutedTexture;
                _volumeButton.BasicTooltipText = "Unmute";
            }
            else
            {
                _volumeButton.Icon = _volumeTexture;
                _volumeButton.ActiveIcon = _volumeTexture;
                _volumeButton.BasicTooltipText = "Mute";
            }
        }

        private void UpdateIconStates()
        {
            var enabled = _isLoaded && !_isLoading;
            _playPauseButton.Enabled = enabled;
            _seekBar.Enabled = enabled;

            if (_audioService.PlaybackState == AudioPlaybackState.Playing)
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

        private void UpdateTimeLabels()
        {
            var currentTime = FormatTime(_audioService.CurrentPosition);
            var totalTime = FormatTime(_audioService.TotalDuration);
            _timeLabel.Text = $"{currentTime} / {totalTime}";
        }

        private void UpdateLayout()
        {
            if (_seekBar == null || _timeLabel == null || _volumeButton == null || _volumeTrackBar == null)
            {
                return;
            }

            var panelWidth = Width;
            if (panelWidth <= 0)
            {
                return;
            }

            // Layout: [Play Button] [Seek Bar] [Time Label] --- gap --- [Volume Icon] [Volume Slider]
            var volumeControlsWidth = VolumeIconSize + 4 + VolumeTrackBarWidth;
            var volumeGap = 15;
            var seekBarStart = IconSize + 8;
            var seekBarWidth = panelWidth - seekBarStart - TimeLabelWidth - volumeControlsWidth - volumeGap - 15;

            if (seekBarWidth < 50)
            {
                seekBarWidth = 50;
            }

            _seekBar.Location = new Point(seekBarStart, (ControlHeight - SeekBarHeight) / 2);
            _seekBar.Width = seekBarWidth;

            // Time label close to seek bar
            var timeLabelX = seekBarStart + seekBarWidth + 10;
            _timeLabel.Location = new Point(timeLabelX, (ControlHeight - 14) / 2);

            // Volume controls with gap from time label
            var volumeButtonX = timeLabelX + TimeLabelWidth + volumeGap - 15;
            _volumeButton.Location = new Point(volumeButtonX, (ControlHeight - VolumeIconSize) / 2);

            var volumeTrackBarX = volumeButtonX + VolumeIconSize + 4;
            _volumeTrackBar.Location = new Point(volumeTrackBarX, (ControlHeight - SeekBarHeight) / 2);
        }

        private void UpdateSeekBar()
        {
            if (_isSeeking || _audioService.TotalDuration.TotalSeconds <= 0)
            {
                return;
            }

            var progress = _audioService.CurrentPosition.TotalSeconds / _audioService.TotalDuration.TotalSeconds;
            _seekBar.Value = (float)(progress * TrackBarMaxValue);
        }

        private static string FormatTime(TimeSpan time)
        {
            if (time.TotalHours >= 1)
            {
                return $"{(int)time.TotalHours}:{time.Minutes:D2}:{time.Seconds:D2}";
            }
            return $"{(int)time.TotalMinutes}:{time.Seconds:D2}";
        }

        public override void UpdateContainer(GameTime gameTime)
        {
            base.UpdateContainer(gameTime);

            if (_isLoaded && _audioService.PlaybackState == AudioPlaybackState.Playing)
            {
                _audioService.UpdatePosition();
            }
        }

        protected override void OnResized(ResizedEventArgs e)
        {
            base.OnResized(e);
            UpdateLayout();
        }

        protected override void DisposeControl()
        {
            _disposed = true;
            UnsubscribeFromEvents();

            if (_playPauseButton != null)
            {
                _playPauseButton.Click -= OnPlayPauseClick;
            }

            if (_seekBar != null)
            {
                _seekBar.ValueChanged -= OnSeekBarValueChanged;
                _seekBar.LeftMouseButtonPressed -= OnSeekBarMousePressed;
                _seekBar.LeftMouseButtonReleased -= OnSeekBarMouseReleased;
            }

            if (_volumeButton != null)
            {
                _volumeButton.Click -= OnVolumeButtonClick;
            }

            if (_volumeTrackBar != null)
            {
                _volumeTrackBar.ValueChanged -= OnVolumeTrackBarChanged;
            }

            base.DisposeControl();
        }
    }
}
