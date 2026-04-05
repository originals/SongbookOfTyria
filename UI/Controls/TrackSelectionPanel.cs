using System;
using System.Collections.Generic;

using Blish_HUD;
using Blish_HUD.Controls;

using Microsoft.Xna.Framework;

using SongbookOfTyria.Models;

namespace SongbookOfTyria.UI.Controls
{
    public sealed class TrackSelectionPanel : FlowPanel
    {
        private const int TrackTabHeight = 24;

        private readonly MidiData _midiData;
        private readonly List<StandardButton> _trackButtons = new List<StandardButton>();
        private int _selectedTrackIndex;

        public event EventHandler<int> TrackChanged;

        public int SelectedTrackIndex => _selectedTrackIndex;

        public TrackSelectionPanel(MidiData midiData, int width)
        {
            _midiData = midiData;

            FlowDirection = ControlFlowDirection.SingleLeftToRight;
            Width = width;
            HeightSizingMode = SizingMode.AutoSize;
            ControlPadding = new Vector2(3, 0);
            OuterControlPadding = new Vector2(5, 0);

            BuildTrackTabs();
        }

        private void BuildTrackTabs()
        {
            if (_midiData?.Tracks == null || _midiData.Tracks.Count <= 1) return;

            for (int i = 0; i < _midiData.Tracks.Count; i++)
            {
                var track = _midiData.Tracks[i];
                var displayName = track.GetDisplayName();
                var trackIndex = i;

                var button = new StandardButton
                {
                    Text = displayName,
                    Width = Math.Max(70, displayName.Length * 7 + 16),
                    Height = TrackTabHeight,
                    Parent = this
                };

                button.Click += (s, e) => SelectTrack(trackIndex);
                _trackButtons.Add(button);
            }

            UpdateTrackButtonStates();
        }

        public void SelectTrack(int trackIndex)
        {
            if (trackIndex < 0 || _midiData?.Tracks == null || trackIndex >= _midiData.Tracks.Count) return;
            if (trackIndex == _selectedTrackIndex) return;

            _selectedTrackIndex = trackIndex;
            UpdateTrackButtonStates();
            TrackChanged?.Invoke(this, _selectedTrackIndex);
        }

        private void UpdateTrackButtonStates()
        {
            for (int i = 0; i < _trackButtons.Count; i++)
            {
                _trackButtons[i].Enabled = i != _selectedTrackIndex;
            }
        }

        public string GetSelectedTrackNotation()
        {
            if (_midiData?.Tracks == null || _selectedTrackIndex >= _midiData.Tracks.Count) return null;

            var track = _midiData.Tracks[_selectedTrackIndex];
            if (string.IsNullOrEmpty(track.Notation)) return null;

            return track.Notation;
        }
    }
}
