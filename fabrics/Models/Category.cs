using Newtonsoft.Json;

namespace fabrics.Models
{
    public class Category
    {

        [JsonProperty("Name")]
        public string Name { get; set; }

        [JsonProperty("ParentCategory")]
        public string[] ParentCategory { get; set; } // Airtable returns links as an array of reco       

        public bool IsMainCategory => ParentCategory == null || ParentCategory.Length == 0;

    }
}