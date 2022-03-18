using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace UnitTestEx.Api.Models
{
    public class Person
    {
        [JsonProperty("id")]
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonProperty("firstName")]
        [JsonPropertyName("firstName")] 
        public string FirstName { get; set; }

        [JsonProperty("lastName")]
        [JsonPropertyName("lastName")]
        public string LastName { get; set; }
    }
}