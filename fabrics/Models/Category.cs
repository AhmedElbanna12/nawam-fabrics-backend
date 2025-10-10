using Newtonsoft.Json;

namespace fabrics.Models
{
    public class Category
    {
        public string Id { get; set; }

        [JsonProperty("Name")]
        public string Name { get; set; }

        [JsonProperty("ParentCategory")]
        public string[] ParentCategory { get; set; } = new string[0];// Airtable returns links as an array of reco       

        public int ProductsCount { get; set; }

        [JsonIgnore]
        public bool IsMainCategory => ParentCategory == null || ParentCategory.Length == 0;

        public string Type => IsMainCategory ? "Main Category" : "Sub Category";

    }
}