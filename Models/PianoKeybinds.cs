using System.Collections.Generic;

using Newtonsoft.Json;

namespace SongbookOfTyria.Models
{
    public class PianoKeybinds
    {
        [JsonProperty("csdb")]
        public string CsDb { get; set; } = "①";

        [JsonProperty("dseb")]
        public string DsEb { get; set; } = "②";

        [JsonProperty("fsgb")]
        public string FsGb { get; set; } = "③";

        [JsonProperty("gsab")]
        public string GsAb { get; set; } = "④";

        [JsonProperty("asbb")]
        public string AsBb { get; set; } = "⑤";

        public static PianoKeybinds CreateDefault()
        {
            return new PianoKeybinds();
        }

        public static PianoKeybinds CreateApostrophe()
        {
            return new PianoKeybinds
            {
                CsDb = "'1",
                DsEb = "'2",
                FsGb = "'3",
                GsAb = "'4",
                AsBb = "'5"
            };
        }

        public static PianoKeybinds CreateSuperscript()
        {
            return new PianoKeybinds
            {
                CsDb = "F¹",
                DsEb = "F²",
                FsGb = "F³",
                GsAb = "F⁴",
                AsBb = "F⁵"
            };
        }

        public static PianoKeybinds CreateHashtag()
        {
            return new PianoKeybinds
            {
                CsDb = "#1",
                DsEb = "#2",
                FsGb = "#3",
                GsAb = "#5",
                AsBb = "#6"
            };
        }

        public Dictionary<char, string> GetReplacementMap()
        {
            return new Dictionary<char, string>
            {
                { '①', CsDb },
                { '②', DsEb },
                { '③', FsGb },
                { '④', GsAb },
                { '⑤', AsBb }
            };
        }

        public string ApplyToNotation(string notation)
        {
            if (string.IsNullOrEmpty(notation))
            {
                return notation;
            }

            var result = notation;
            var replacements = GetReplacementMap();

            foreach (var replacement in replacements)
            {
                if (replacement.Value != replacement.Key.ToString())
                {
                    result = result.Replace(replacement.Key.ToString(), replacement.Value);
                }
            }

            return result;
        }
    }
}
