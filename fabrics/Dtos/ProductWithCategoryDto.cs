namespace fabrics.Dtos
{
    public class ProductWithCategoryDto
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal PricePerMeter { get; set; }
        public string Image { get; set; }
        public string MainCategory { get; set; }
        public string SubCategory { get; set; }
    }
}
