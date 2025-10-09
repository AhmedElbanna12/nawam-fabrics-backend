using Newtonsoft.Json;

namespace fabrics.Models
{
    public class Product
    {
        public string Id { get; set; }

        [JsonProperty("Name")]
        public string Name { get; set; }

        [JsonProperty("Description")]
        public string Description { get; set; }

        [JsonProperty("PricePerMeter")]
        public decimal PricePerMeter { get; set; }

        [JsonProperty("Image")]
        public string Image { get; set; }

        public string[] Category { get; set; }

    }
}
