using Newtonsoft.Json;

namespace fabrics.Models
{
    public class Product
    {

        [JsonProperty("Name")]
        public string Name { get; set; }

        [JsonProperty("Description")]
        public string Description { get; set; }

        [JsonProperty("PricePerMeter")]
        public decimal PricePerMeter { get; set; }

        [JsonProperty("Image")]
        public string Image { get; set; }

        // ✅ في Airtable، Category يكون array of record IDs
        [JsonProperty("Category")]
        public string[] Category { get; set; }

        [JsonProperty("MainCategory")]
        public string[] MainCategory { get; set; }

        [JsonProperty("SubCategory")]
        public string[] SubCategory { get; set; }

    }
}
