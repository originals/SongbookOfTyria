using System;
using System.Collections.Generic;
using System.IO;

using Blish_HUD;

using Microsoft.Xna.Framework;

using Newtonsoft.Json;

using SongbookOfTyria.Models;
using SongbookOfTyria.UI.Controls.Notation;
using SongbookOfTyria.UI.Views.Helpers;

namespace SongbookOfTyria.Services
{
    public sealed class UserSettingsService
    {
        private static readonly Logger Logger = Logger.GetLogger<UserSettingsService>();

        private const string StateFileName = "user_settings.json";

        private readonly string _cacheDirectory;
        private UserSettingsData _state;

        public event EventHandler<int> FavoriteChanged;

        public UserSettingsService(string cacheDirectory)
        {
            _cacheDirectory = cacheDirectory;
            LoadState();
        }

        #region Filter State

        public FilterState GetFilterState()
        {
            return _state.FilterState ?? (_state.FilterState = new FilterState());
        }

        public void SaveFilterState(FilterState filterState)
        {
            _state.FilterState = filterState;
            SaveState();
        }

        #endregion

        #region Tab Window State

        public TabWindowState GetTabWindowState(int tabId)
        {
            if (_state.TabWindowStates.TryGetValue(tabId, out var windowState))
            {
                return windowState;
            }

            return null;
        }

        public void SaveTabWindowState(int tabId, TabWindowState windowState)
        {
            _state.TabWindowStates[tabId] = windowState;
            SaveState();
        }

        #endregion

        #region Global Panel Collapsed States

        public bool GetGlobalDetailsCollapsed()
        {
            return _state.GlobalDetailsCollapsed;
        }

        public void SaveGlobalDetailsCollapsed(bool collapsed)
        {
            _state.GlobalDetailsCollapsed = collapsed;
            SaveState();
        }

        public bool GetGlobalViewOptionsCollapsed()
        {
            return _state.GlobalViewOptionsCollapsed;
        }

        public void SaveGlobalViewOptionsCollapsed(bool collapsed)
        {
            _state.GlobalViewOptionsCollapsed = collapsed;
            SaveState();
        }

        public bool GetGlobalAudioPlayerCollapsed()
        {
            return _state.GlobalAudioPlayerCollapsed;
        }

        public void SaveGlobalAudioPlayerCollapsed(bool collapsed)
        {
            _state.GlobalAudioPlayerCollapsed = collapsed;
            SaveState();
        }

        public bool GetGlobalPianoKeybindsCollapsed()
        {
            return _state.GlobalPianoKeybindsCollapsed;
        }

        public void SaveGlobalPianoKeybindsCollapsed(bool collapsed)
        {
            _state.GlobalPianoKeybindsCollapsed = collapsed;
            SaveState();
        }

        public bool GetGlobalPlaybackCollapsed()
        {
            return _state.GlobalPlaybackCollapsed;
        }

        public void SaveGlobalPlaybackCollapsed(bool collapsed)
        {
            _state.GlobalPlaybackCollapsed = collapsed;
            SaveState();
        }

        public bool GetHitDetectionFeedbackEnabled()
        {
            return _state.HitDetectionFeedbackEnabled;
        }

        public void SaveHitDetectionFeedbackEnabled(bool enabled)
        {
            _state.HitDetectionFeedbackEnabled = enabled;
            SaveState();
        }

        public int GetSelectedMainWindowTabIndex()
        {
            return _state.SelectedMainWindowTabIndex;
        }

        public void SaveSelectedMainWindowTabIndex(int index)
        {
            _state.SelectedMainWindowTabIndex = index;
            SaveState();
        }

        #endregion

        #region Piano Keybinds

        public PianoKeybinds GetPianoKeybinds()
        {
            return _state.PianoKeybinds ?? (_state.PianoKeybinds = new PianoKeybinds());
        }

        public void SavePianoKeybinds(PianoKeybinds keybinds)
        {
            _state.PianoKeybinds = keybinds;
            SaveState();
        }

        #endregion

        #region Practice Mode State

        public PracticeModeState GetPracticeModeState(int tabId)
        {
            if (_state.PracticeModeStates.TryGetValue(tabId, out var state))
            {
                return state;
            }

            return null;
        }

        public void SavePracticeModeState(int tabId, PracticeModeState state)
        {
            _state.PracticeModeStates[tabId] = state;
            SaveState();
        }

        #endregion

        #region Favorites

        public bool IsFavorite(int tabId)
        {
            return _state.Favorites.Contains(tabId);
        }

        public void SetFavorite(int tabId, bool isFavorite)
        {
            bool changed = false;

            if (isFavorite)
            {
                changed = _state.Favorites.Add(tabId);
            }
            else
            {
                changed = _state.Favorites.Remove(tabId);
            }

            if (changed)
            {
                SaveState();
                FavoriteChanged?.Invoke(this, tabId);
            }
        }

        public void ToggleFavorite(int tabId)
        {
            SetFavorite(tabId, !IsFavorite(tabId));
        }

        #endregion

        #region Persistence

        private void LoadState()
        {
            _state = new UserSettingsData();

            try
            {
                var stateFilePath = Path.Combine(_cacheDirectory, StateFileName);
                if (File.Exists(stateFilePath))
                {
                    var json = File.ReadAllText(stateFilePath);
                    _state = JsonConvert.DeserializeObject<UserSettingsData>(json) ?? new UserSettingsData();
                    Logger.Debug("Loaded user settings from disk");
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to load user settings from disk");
                _state = new UserSettingsData();
            }
        }

        private void SaveState()
        {
            try
            {
                var stateFilePath = Path.Combine(_cacheDirectory, StateFileName);
                var json = JsonConvert.SerializeObject(_state, Formatting.Indented);
                File.WriteAllText(stateFilePath, json);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to save user settings to disk");
            }
        }

        #endregion
    }

    public class UserSettingsData
    {
        public FilterState FilterState { get; set; } = new FilterState();
        public Dictionary<int, TabWindowState> TabWindowStates { get; set; } = new Dictionary<int, TabWindowState>();

        public bool GlobalDetailsCollapsed { get; set; }

        public bool GlobalViewOptionsCollapsed { get; set; }

        public bool GlobalAudioPlayerCollapsed { get; set; }

        public bool GlobalPianoKeybindsCollapsed { get; set; }

        public bool GlobalPlaybackCollapsed { get; set; }

        public bool HitDetectionFeedbackEnabled { get; set; } = false;

        public int SelectedMainWindowTabIndex { get; set; }

        public PianoKeybinds PianoKeybinds { get; set; } = new PianoKeybinds();

        public HashSet<int> Favorites { get; set; } = new HashSet<int>();

        public Dictionary<int, PracticeModeState> PracticeModeStates { get; set; } = new Dictionary<int, PracticeModeState>();
    }

    public class FilterState
    {
        public string SearchText { get; set; } = string.Empty;

        public bool SoloOnly { get; set; }
        public bool DuetOnly { get; set; }
        public bool BandOnly { get; set; }

        public bool BeginnerOnly { get; set; }

        public bool PracticeModeOnly { get; set; }
        public bool PianoOnly { get; set; }
        public bool FavoritesOnly { get; set; }

        public List<string> SelectedGenres { get; set; } = new List<string>();

        public List<string> SelectedTabbers { get; set; } = new List<string>();

        public bool PublicOnly { get; set; }
        public bool PrivateOnly { get; set; }

        public SortMode SortMode { get; set; } = SortMode.ReleaseDate;
        public bool SortAscending { get; set; }

        public Dictionary<string, bool> CollapsedPanels { get; set; } = new Dictionary<string, bool>();
    }

    public class TabWindowState
    {
        public int LocationX { get; set; }
        public int LocationY { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public NotationFontSize FontSize { get; set; } = NotationFontSize.Size20;
        public bool AutoScrollEnabled { get; set; }
        public float ScrollSpeed { get; set; } = 30f;
        public bool IsPracticeMode { get; set; }

        public Point GetLocation()
        {
            return new Point(LocationX, LocationY);
        }

        public Point GetSize()
        {
            return new Point(Width, Height);
        }

        public void SetLocation(Point location)
        {
            LocationX = location.X;
            LocationY = location.Y;
        }

        public void SetSize(Point size)
        {
            Width = size.X;
            Height = size.Y;
        }
    }

    public class PracticeModeState
    {
        public float MasterVolume { get; set; } = 1.0f;
        public bool MasterMuted { get; set; }
        public int SelectedTrackIndex { get; set; }
        public float PlaybackSpeed { get; set; } = 1.0f;
        public Dictionary<int, TrackVolumeState> TrackStates { get; set; } = new Dictionary<int, TrackVolumeState>();
        public List<SavedMarker> Markers { get; set; } = new List<SavedMarker>();
    }

    public class SavedMarker
    {
        public double Time { get; set; }
        public int ColorIndex { get; set; }
    }

    public class TrackVolumeState
    {
        public float Volume { get; set; } = 1.0f;
        public bool Muted { get; set; }
    }
}
