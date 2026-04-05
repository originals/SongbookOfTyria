using Newtonsoft.Json;

namespace SongbookOfTyria.Models
{
    public class TabberInfo
    {
        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("display_name")]
        public string DisplayName { get; set; }

        [JsonProperty("profile_url")]
        public string ProfileUrl { get; set; }

        [JsonProperty("picture_url")]
        public string PictureUrl { get; set; }
    }
}
