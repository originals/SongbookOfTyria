using System.Collections.Generic;

using Newtonsoft.Json;

namespace SongbookOfTyria.Models.Api
{
    public class TabsResponse
    {
        [JsonProperty("albums")]
        public List<MusicTab> Tabs { get; set; }

        [JsonProperty("last_updated")]
        public long LastUpdated { get; set; }

        [JsonProperty("total_count")]
        public int TotalCount { get; set; }
    }
}
