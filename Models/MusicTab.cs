using System.Collections.Generic;

using Newtonsoft.Json;

using SongbookOfTyria.Serialization;

namespace SongbookOfTyria.Models
{
    public class MusicTab
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("thumbnail")]
        public string Thumbnail { get; set; }

        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("release_date")]
        public string ReleaseDate { get; set; }

        [JsonProperty("genre")]
        public string Genre { get; set; }

        [JsonProperty("tab_type")]
        [JsonConverter(typeof(StringOrArrayConverter))]
        public List<string> TabType { get; set; }

        [JsonProperty("is_beginner")]
        public bool IsBeginner { get; set; }

        [JsonProperty("is_collection")]
        public bool IsCollection { get; set; }

        [JsonProperty("piano")]
        public bool Piano { get; set; }

        [JsonProperty("practise_mode")]
        public bool PracticeMode { get; set; }

        [JsonProperty("tabbed_by")]
        public string TabbedBy { get; set; }

        [JsonProperty("tabbed_by_member")]
        [JsonConverter(typeof(StringOrArrayConverter))]
        public List<string> TabbedByMember { get; set; }

        [JsonProperty("tabber_info")]
        public List<ArrangerInfo> ArrangerInfo { get; set; }

        [JsonProperty("last_updated")]
        public long LastUpdated { get; set; }

        [JsonProperty("api_url")]
        public string ApiUrl { get; set; }

        [JsonProperty("midi_file")]
        public string MidiFile { get; set; }

        [JsonProperty("notation")]
        public string Notation { get; set; }

        [JsonProperty("notation_blishhud")]
        public string NotationBlishhud { get; set; }

        [JsonProperty("song_id")]
        public int SongId { get; set; }

        [JsonProperty("song_title")]
        public string SongTitle { get; set; }

        [JsonProperty("song_mp3")]
        public string SongMp3 { get; set; }

        [JsonProperty("is_private")]
        public bool IsPrivate { get; set; }
    }
}
