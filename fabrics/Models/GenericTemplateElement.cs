using System.Text.Json.Serialization;

namespace fabrics.Models
{
    public class GenericTemplateElement
    {




        [JsonPropertyName("title")]

        public string Title { get; set; } = string.Empty;



        [JsonPropertyName("subtitle")]
        public string Subtitle { get; set; } = string.Empty;



        //[JsonPropertyName("image_url")]
        //public string ImageUrl { get; set; } = string.Empty;


        [JsonPropertyName("buttons")]
        public List<Button> Buttons { get; set; } = new List<Button>();
    }
}
