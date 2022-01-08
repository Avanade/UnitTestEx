using Newtonsoft.Json;

namespace UnitTestEx.MSTest.Test.Model
{
    public class Person
    {
        [JsonProperty("firstName")]
        public string FirstName { get; set; }

        [JsonProperty("lastName")]
        public string LastName { get; set; }
    }
}