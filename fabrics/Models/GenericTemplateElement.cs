using System.Text.Json.Serialization;

namespace fabrics.Models
{
    public class GenericTemplateElement
    {




        [JsonPropertyName("title")]

        public string Title { get; set; }



        [JsonPropertyName("subtitle")]
        public string Subtitle { get; set; }



        [JsonPropertyName("image_url")]
        public string image_url { get; set; }


        [JsonPropertyName("buttons")]
        public List<Button> Buttons { get; set; } = new List<Button>();
    }
}
