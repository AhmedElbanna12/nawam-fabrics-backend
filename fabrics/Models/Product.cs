namespace fabrics.Models
{
    public class Product
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int PricePerMeter { get; set; }
        public string Category { get; set; } // ID للـ SubCategory
    }
}
