using System.Text.Json.Serialization;

namespace fabrics.Models
{
    public class Button
    {



        [JsonPropertyName("type")]
        public string Type { get; set; } = "postback";

        [JsonPropertyName("title")]
        public string Title { get; set; }



        [JsonPropertyName("payload")]
        public string Payload { get; set; }
    }
}
