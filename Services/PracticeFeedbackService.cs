using System;
using System.Collections.Generic;

using Blish_HUD;
using Blish_HUD.Input;

using Microsoft.Xna.Framework.Input;

using SongbookOfTyria.Models;
using SongbookOfTyria.Settings;

namespace SongbookOfTyria.Services
{
    public sealed class PracticeFeedbackService : IDisposable
    {
        private const double HitWindowMs = 200.0;
        private const double FeedbackDisplayDurationMs = 400.0;
        private const double MaxLookBehindMs = 500.0;
        private const int OctaveDownMarker = -100;
        private const int OctaveUpMarker = -101;

        private static readonly Logger Logger = Logger.GetLogger<PracticeFeedbackService>();

        private readonly MidiPlaybackService _playbackService;
        private readonly ModuleSettings _settings;
        private MidiTrack _activeTrack;
        private bool _isEnabled;
        private bool _disposed;
        private int _currentOctaveOffset;
        private double _lastProcessedPosition = -1;
        private readonly Dictionary<int, NoteFeedback> _noteFeedback = new Dictionary<int, NoteFeedback>();
        private readonly HashSet<int> _hitNoteIndices = new HashSet<int>();
        private readonly object _feedbackLock = new object();

        public event EventHandler<PracticeFeedbackEventArgs> FeedbackChanged;

        public int CurrentOctaveOffset => _currentOctaveOffset;

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled == value) return;
                _isEnabled = value;
                if (_isEnabled)
                {
                    SubscribeToKeyboard();
                }
                else
                {
                    UnsubscribeFromKeyboard();
                    ClearFeedback();
                }
            }
        }

        public PracticeFeedbackService(MidiPlaybackService playbackService, ModuleSettings settings)
        {
            _playbackService = playbackService ?? throw new ArgumentNullException(nameof(playbackService));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void SetActiveTrack(MidiTrack track)
        {
            _activeTrack = track;
            _currentOctaveOffset = 0;
            ClearFeedback();
        }

        public void ClearFeedback()
        {
            lock (_feedbackLock)
            {
                _noteFeedback.Clear();
                _hitNoteIndices.Clear();
                _lastProcessedPosition = -1;
            }
            FeedbackChanged?.Invoke(this, new PracticeFeedbackEventArgs(new Dictionary<int, NoteFeedbackState>()));
        }

        public void Update()
        {
            if (!_isEnabled || _activeTrack?.Notes == null) return;

            var now = DateTime.UtcNow;
            var currentPosition = _playbackService.CurrentPosition;
            bool feedbackChanged = false;

            lock (_feedbackLock)
            {
                var expiredKeys = new List<int>();
                foreach (var kvp in _noteFeedback)
                {
                    if ((now - kvp.Value.Timestamp).TotalMilliseconds > FeedbackDisplayDurationMs)
                    {
                        expiredKeys.Add(kvp.Key);
                    }
                }

                foreach (var key in expiredKeys)
                {
                    _noteFeedback.Remove(key);
                }

                feedbackChanged = expiredKeys.Count > 0 || CheckMissedNotes(currentPosition);
            }

            if (feedbackChanged)
            {
                RaiseFeedbackChanged();
            }
        }

        private bool CheckMissedNotes(double currentPosition)
        {
            if (_activeTrack?.Notes == null) return false;

            var hitWindowSeconds = HitWindowMs / 1000.0;
            var maxLookBehindSeconds = MaxLookBehindMs / 1000.0;

            if (_lastProcessedPosition < 0 || currentPosition < _lastProcessedPosition)
            {
                _lastProcessedPosition = currentPosition;
                return false;
            }

            var checkWindowStart = Math.Max(_lastProcessedPosition - hitWindowSeconds, currentPosition - maxLookBehindSeconds);
            bool anyMissed = false;

            int startIndex = FindNoteIndexAtOrAfter(checkWindowStart - hitWindowSeconds);
            for (int i = startIndex; i < _activeTrack.Notes.Count; i++)
            {
                var note = _activeTrack.Notes[i];
                var noteEndTime = note.Time + hitWindowSeconds;

                if (note.Time > currentPosition) break;
                if (_hitNoteIndices.Contains(i)) continue;
                if (_noteFeedback.ContainsKey(i)) continue;
                if (noteEndTime < checkWindowStart) continue;

                if (currentPosition > noteEndTime)
                {
                    _noteFeedback[i] = new NoteFeedback(NoteFeedbackState.Missed, DateTime.UtcNow);
                    anyMissed = true;
                }
            }

            _lastProcessedPosition = currentPosition;
            return anyMissed;
        }

        private int FindNoteIndexAtOrAfter(double time)
        {
            if (_activeTrack?.Notes == null || _activeTrack.Notes.Count == 0) return 0;

            int left = 0;
            int right = _activeTrack.Notes.Count - 1;

            while (left < right)
            {
                int mid = left + (right - left) / 2;
                if (_activeTrack.Notes[mid].Time < time)
                {
                    left = mid + 1;
                }
                else
                {
                    right = mid;
                }
            }

            return left;
        }

        private void SubscribeToKeyboard()
        {
            if (GameService.Input?.Keyboard == null) return;
            GameService.Input.Keyboard.KeyPressed += OnKeyPressed;
        }

        private void UnsubscribeFromKeyboard()
        {
            if (GameService.Input?.Keyboard == null) return;
            GameService.Input.Keyboard.KeyPressed -= OnKeyPressed;
        }

        private void OnKeyPressed(object sender, KeyboardEventArgs e)
        {
            if (!_isEnabled || _activeTrack?.Notes == null) return;

            var activeModifiers = GameService.Input.Keyboard.ActiveModifiers;
            var midiNote = _settings.GetMidiNoteFromKey(e.Key, activeModifiers, _currentOctaveOffset);

            if (!midiNote.HasValue) return;

            if (midiNote.Value == OctaveDownMarker)
            {
                _currentOctaveOffset = Math.Max(-2, _currentOctaveOffset - 1);
                return;
            }

            if (midiNote.Value == OctaveUpMarker)
            {
                _currentOctaveOffset = Math.Min(2, _currentOctaveOffset + 1);
                return;
            }

            if (!_playbackService.IsPlaying) return;

            var currentPosition = _playbackService.CurrentPosition;
            var hitWindowSeconds = HitWindowMs / 1000.0;
            var now = DateTime.UtcNow;
            bool feedbackChanged = false;

            lock (_feedbackLock)
            {
                int startIndex = FindNoteIndexAtOrAfter(currentPosition - hitWindowSeconds);
                for (int i = startIndex; i < _activeTrack.Notes.Count; i++)
                {
                    var note = _activeTrack.Notes[i];
                    if (note.Time > currentPosition + hitWindowSeconds) break;
                    if (_hitNoteIndices.Contains(i)) continue;

                    var timeDiff = Math.Abs(currentPosition - note.Time);
                    if (timeDiff <= hitWindowSeconds && note.Midi == midiNote.Value)
                    {
                        _hitNoteIndices.Add(i);
                        _noteFeedback[i] = new NoteFeedback(NoteFeedbackState.Correct, now);
                        feedbackChanged = true;
                        break;
                    }
                }

                if (!feedbackChanged)
                {
                    var closestNoteIndex = FindClosestNoteIndex(currentPosition, hitWindowSeconds);
                    if (closestNoteIndex >= 0 && !_hitNoteIndices.Contains(closestNoteIndex))
                    {
                        _noteFeedback[closestNoteIndex] = new NoteFeedback(NoteFeedbackState.Wrong, now);
                        feedbackChanged = true;
                    }
                }
            }

            if (feedbackChanged)
            {
                RaiseFeedbackChanged();
            }
        }

        private int FindClosestNoteIndex(double currentPosition, double windowSeconds)
        {
            if (_activeTrack?.Notes == null) return -1;

            int closestIndex = -1;
            double closestDiff = double.MaxValue;

            int startIndex = FindNoteIndexAtOrAfter(currentPosition - windowSeconds);
            for (int i = startIndex; i < _activeTrack.Notes.Count; i++)
            {
                var note = _activeTrack.Notes[i];
                if (note.Time > currentPosition + windowSeconds) break;
                if (_hitNoteIndices.Contains(i)) continue;

                var timeDiff = Math.Abs(currentPosition - note.Time);
                if (timeDiff < closestDiff)
                {
                    closestDiff = timeDiff;
                    closestIndex = i;
                }
            }

            return closestIndex;
        }

        private void RaiseFeedbackChanged()
        {
            var handler = FeedbackChanged;
            if (handler == null) return;

            var feedbackCopy = new Dictionary<int, NoteFeedbackState>();
            lock (_feedbackLock)
            {
                foreach (var kvp in _noteFeedback)
                {
                    feedbackCopy[kvp.Key] = kvp.Value.State;
                }
            }
            handler.Invoke(this, new PracticeFeedbackEventArgs(feedbackCopy));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            UnsubscribeFromKeyboard();
        }

        private class NoteFeedback
        {
            public NoteFeedbackState State { get; }
            public DateTime Timestamp { get; }

            public NoteFeedback(NoteFeedbackState state, DateTime timestamp)
            {
                State = state;
                Timestamp = timestamp;
            }
        }
    }

    public enum NoteFeedbackState
    {
        None,
        Correct,
        Wrong,
        Missed
    }

    public class PracticeFeedbackEventArgs : EventArgs
    {
        public Dictionary<int, NoteFeedbackState> NoteFeedback { get; }

        public PracticeFeedbackEventArgs(Dictionary<int, NoteFeedbackState> noteFeedback)
        {
            NoteFeedback = noteFeedback;
        }
    }
}
