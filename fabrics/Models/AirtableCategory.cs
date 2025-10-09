namespace fabrics.Models
{
    public class AirtableCategory
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public List<string>? ParentCategoryIds { get; set; }
       
    }
}