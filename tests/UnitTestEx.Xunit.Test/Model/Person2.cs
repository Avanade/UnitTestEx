using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace UnitTestEx.Xunit.Test.Model
{
    class Person2
    {
        [JsonProperty("first")]
        [JsonPropertyName("first")]
        public string First { get; set; }

        [JsonProperty("last")]
        [JsonPropertyName("last")]
        public string Last { get; set; }
    }
}