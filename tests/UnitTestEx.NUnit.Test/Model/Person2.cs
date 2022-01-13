using Newtonsoft.Json;

namespace UnitTestEx.NUnit.Test.Model
{
    class Person2
    {
        [JsonProperty("first")]
        public string First { get; set; }

        [JsonProperty("last")]
        public string Last { get; set; }
    }
}