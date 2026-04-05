using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using Blish_HUD;

using NAudio.Midi;

using SongbookOfTyria.Models;
using SongbookOfTyria.Utilities;

namespace SongbookOfTyria.Services
{
    public sealed class MidiFileParser : IDisposable
    {
        private static readonly Logger Logger = Logger.GetLogger<MidiFileParser>();

        private readonly HttpClient _httpClient;
        private readonly bool _ownsHttpClient;
        private readonly string _cacheDirectory;
        private bool _disposed;

        public MidiFileParser(string cacheDirectory, HttpClient httpClient = null)
        {
            _cacheDirectory = Path.Combine(cacheDirectory, "midi_cache");
            _ownsHttpClient = httpClient == null;
            _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            Directory.CreateDirectory(_cacheDirectory);
        }

        public Task<MidiData> ParseFromUrlAsync(string midiFileUrl) =>
            ParseFromUrlAsync(midiFileUrl, System.Threading.CancellationToken.None);

        public async Task<MidiData> ParseFromUrlAsync(string midiFileUrl, System.Threading.CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(midiFileUrl)) return null;

            try
            {
                var filePath = await DownloadMidiFileAsync(midiFileUrl, cancellationToken).ConfigureAwait(false);
                if (filePath == null) return null;

                return ParseMidiFile(filePath);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to parse MIDI file from {Url}", midiFileUrl);
                return null;
            }
        }

        private async Task<string> DownloadMidiFileAsync(string url, System.Threading.CancellationToken cancellationToken)
        {
            var fileName = GenerateCacheFileName(url);
            var cachedPath = Path.Combine(_cacheDirectory, fileName);

            if (File.Exists(cachedPath))
            {
                Logger.Debug("Using cached MIDI file: {Path}", cachedPath);
                return cachedPath;
            }

            Logger.Debug("Downloading MIDI file from {Url}", url);
            using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var fileStream = new FileStream(cachedPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
                {
                    await stream.CopyToAsync(fileStream).ConfigureAwait(false);
                }
            }

            return cachedPath;
        }

        private MidiData ParseMidiFile(string filePath)
        {
            var midi = new MidiFile(filePath, false);
            var ticksPerQuarterNote = midi.DeltaTicksPerQuarterNote;

            var tempoMap = BuildTempoMap(midi);
            var tracks = new List<MidiTrack>();
            double maxDuration = 0;

            for (int trackIndex = 0; trackIndex < midi.Tracks; trackIndex++)
            {
                var events = midi.Events[trackIndex];
                var trackName = GetTrackName(events);
                var instrument = GetTrackInstrument(events);
                var notes = ExtractNotes(events, tempoMap, ticksPerQuarterNote);

                if (notes.Count == 0) continue;

                var trackDuration = notes.Max(n => n.Time + n.Duration);
                if (trackDuration > maxDuration) maxDuration = trackDuration;

                tracks.Add(new MidiTrack
                {
                    Index = tracks.Count,
                    Name = trackName ?? $"Track {tracks.Count + 1}",
                    Instrument = instrument,
                    NoteCount = notes.Count,
                    Notes = notes
                });
            }

            if (tracks.Count == 0)
            {
                Logger.Warn("MIDI file contained no note data: {Path}", filePath);
                return null;
            }

            var tempos = tempoMap.Select(t => new MidiTempo
            {
                Ticks = (int)t.Tick,
                Bpm = 60000000.0 / t.MicrosecondsPerBeat,
                MicrosecondsPerBeat = t.MicrosecondsPerBeat
            }).ToList();

            Logger.Debug("Parsed MIDI: {TrackCount} tracks, {Duration:F1}s duration", tracks.Count, maxDuration);

            return new MidiData
            {
                Tracks = tracks,
                Duration = maxDuration,
                Ppq = ticksPerQuarterNote,
                Tempos = tempos
            };
        }

        private List<TempoEntry> BuildTempoMap(MidiFile midi)
        {
            var tempoMap = new List<TempoEntry>();

            for (int trackIndex = 0; trackIndex < midi.Tracks; trackIndex++)
            {
                foreach (var midiEvent in midi.Events[trackIndex])
                {
                    if (midiEvent is TempoEvent tempoEvent)
                    {
                        tempoMap.Add(new TempoEntry
                        {
                            Tick = midiEvent.AbsoluteTime,
                            MicrosecondsPerBeat = (int)tempoEvent.MicrosecondsPerQuarterNote
                        });
                    }
                }
            }

            var ordered = tempoMap.OrderBy(t => t.Tick).ToList();

            if (ordered.Count == 0 || ordered[0].Tick > 0)
            {
                ordered.Insert(0, new TempoEntry { Tick = 0, MicrosecondsPerBeat = 500000 });
            }

            return ordered;
        }

        private double TicksToSeconds(long ticks, List<TempoEntry> tempoMap, int ticksPerQuarterNote)
        {
            double seconds = 0;
            long lastTick = 0;
            int currentTempo = tempoMap[0].MicrosecondsPerBeat;

            foreach (var entry in tempoMap.Skip(1))
            {
                if (entry.Tick >= ticks) break;

                var deltaTicks = entry.Tick - lastTick;
                seconds += deltaTicks * currentTempo / (ticksPerQuarterNote * 1000000.0);
                lastTick = entry.Tick;
                currentTempo = entry.MicrosecondsPerBeat;
            }

            var remainingTicks = ticks - lastTick;
            seconds += remainingTicks * currentTempo / (ticksPerQuarterNote * 1000000.0);

            return seconds;
        }

        private List<Models.MidiNote> ExtractNotes(IList<MidiEvent> events, List<TempoEntry> tempoMap, int ticksPerQuarterNote)
        {
            var notes = new List<Models.MidiNote>();
            var activeNotes = new Dictionary<int, (long tick, int velocity)>();

            foreach (var midiEvent in events)
            {
                if (midiEvent is NoteOnEvent noteOn)
                {
                    if (noteOn.Velocity > 0)
                    {
                        activeNotes[noteOn.NoteNumber] = (noteOn.AbsoluteTime, noteOn.Velocity);
                    }
                    else
                    {
                        CompleteNote(noteOn.NoteNumber, noteOn.AbsoluteTime);
                    }
                }
                else if (midiEvent is NoteEvent noteOff && midiEvent.CommandCode == MidiCommandCode.NoteOff)
                {
                    CompleteNote(noteOff.NoteNumber, noteOff.AbsoluteTime);
                }
            }

            return notes;

            void CompleteNote(int noteNumber, long endTick)
            {
                if (!activeNotes.TryGetValue(noteNumber, out var start)) return;
                activeNotes.Remove(noteNumber);

                var startTime = TicksToSeconds(start.tick, tempoMap, ticksPerQuarterNote);
                var endTime = TicksToSeconds(endTick, tempoMap, ticksPerQuarterNote);
                var duration = endTime - startTime;

                if (duration > 0)
                {
                    notes.Add(new Models.MidiNote
                    {
                        Midi = noteNumber,
                        Time = startTime,
                        Duration = duration,
                        Velocity = start.velocity
                    });
                }
            }
        }

        private static string GetTrackName(IList<MidiEvent> events) =>
            events.OfType<TextEvent>()
                  .FirstOrDefault(e => e.MetaEventType == MetaEventType.SequenceTrackName)
                  ?.Text;

        private static MidiInstrument GetTrackInstrument(IList<MidiEvent> events)
        {
            var patchChange = events.OfType<PatchChangeEvent>().FirstOrDefault();
            if (patchChange == null) return null;

            return new MidiInstrument
            {
                Number = patchChange.Patch,
                Name = PatchChangeEvent.GetPatchName(patchChange.Patch)
            };
        }

        private static string GenerateCacheFileName(string url)
        {
            var hash = HashUtility.GetStableHashCode(url).ToString("X8");
            var uri = new Uri(url);
            var originalName = Path.GetFileNameWithoutExtension(uri.LocalPath);
            if (string.IsNullOrEmpty(originalName)) originalName = "midi";
            return $"{originalName}_{hash}.mid";
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_ownsHttpClient)
            {
                _httpClient?.Dispose();
            }
        }

        private class TempoEntry
        {
            public long Tick;
            public int MicrosecondsPerBeat;
        }
    }
}
