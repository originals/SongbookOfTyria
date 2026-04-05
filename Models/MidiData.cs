using System.Collections.Generic;

using Newtonsoft.Json;

namespace SongbookOfTyria.Models
{
    public class MidiData
    {
        [JsonProperty("tracks")]
        public List<MidiTrack> Tracks { get; set; }

        [JsonProperty("notation")]
        public string Notation { get; set; }

        [JsonProperty("duration")]
        public double Duration { get; set; }

        [JsonProperty("ppq")]
        public int Ppq { get; set; }

        [JsonProperty("tempos")]
        public List<MidiTempo> Tempos { get; set; }

        [JsonProperty("time_signatures")]
        public List<MidiTimeSignature> TimeSignatures { get; set; }

        [JsonProperty("metadata")]
        public MidiMetadata Metadata { get; set; }
    }

    public class MidiTrack
    {
        [JsonProperty("index")]
        public int Index { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("instrument")]
        public MidiInstrument Instrument { get; set; }

        [JsonProperty("is_bass")]
        public bool IsBass { get; set; }

        [JsonProperty("notation")]
        public string Notation { get; set; }

        [JsonProperty("note_count")]
        public int NoteCount { get; set; }

        [JsonProperty("notes")]
        public List<MidiNote> Notes { get; set; }

        public string GetDisplayName()
        {
            if (Instrument?.Gw2Name != null) return Instrument.Gw2Name;
            if (!string.IsNullOrEmpty(Name)) return Name;
            return $"Track {Index + 1}";
        }
    }

    public class MidiNote
    {
        [JsonProperty("midi")]
        public int Midi { get; set; }

        [JsonProperty("time")]
        public double Time { get; set; }

        [JsonProperty("duration")]
        public double Duration { get; set; }

        [JsonProperty("velocity")]
        public int Velocity { get; set; }
    }

    public class MidiInstrument
    {
        [JsonProperty("number")]
        public int Number { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("family")]
        public string Family { get; set; }

        [JsonProperty("gw2_preset")]
        public int Gw2Preset { get; set; }

        [JsonProperty("gw2_name")]
        public string Gw2Name { get; set; }
    }

    public class MidiTempo
    {
        [JsonProperty("ticks")]
        public int Ticks { get; set; }

        [JsonProperty("bpm")]
        public double Bpm { get; set; }

        [JsonProperty("microseconds_per_beat")]
        public int MicrosecondsPerBeat { get; set; }
    }

    public class MidiTimeSignature
    {
        [JsonProperty("ticks")]
        public int Ticks { get; set; }

        [JsonProperty("numerator")]
        public int Numerator { get; set; }

        [JsonProperty("denominator")]
        public int Denominator { get; set; }
    }

    public class MidiMetadata
    {
        [JsonProperty("converter_version")]
        public string ConverterVersion { get; set; }

        [JsonProperty("track_count")]
        public int TrackCount { get; set; }

        [JsonProperty("piano_mode")]
        public bool PianoMode { get; set; }

        [JsonProperty("bars_per_row")]
        public int BarsPerRow { get; set; }
    }
}
