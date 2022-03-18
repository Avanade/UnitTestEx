using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace UnitTestEx.MSTest.Test.Model
{
    public class Person
    {
        [JsonProperty("firstName")]
        [JsonPropertyName("firstName")]
        public string FirstName { get; set; }

        [JsonProperty("lastName")]
        [JsonPropertyName("lastName")]
        public string LastName { get; set; }
    }
}