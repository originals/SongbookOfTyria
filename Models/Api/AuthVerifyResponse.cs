using Newtonsoft.Json;

namespace SongbookOfTyria.Models.Api
{
    public class AuthVerifyResponse
    {
        [JsonProperty("valid")]
        public bool Valid { get; set; }

        [JsonProperty("in_opus_guild")]
        public bool InOpusGuild { get; set; }

        [JsonProperty("account_name")]
        public string AccountName { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("code")]
        public string ErrorCode { get; set; }
    }
}
