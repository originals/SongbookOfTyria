using Newtonsoft.Json;

namespace SongbookOfTyria.Models.Api
{
    public class AuthVerifyRequest
    {
        [JsonProperty("api_key")]
        public string ApiKey { get; set; }

        public AuthVerifyRequest(string apiKey)
        {
            ApiKey = apiKey;
        }
    }
}
