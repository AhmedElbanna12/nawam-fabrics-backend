namespace fabrics.Models
{
    public class product
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string? Description { get; set; }
        public double? PricePerMeter { get; set; }

        // ID للـ Sub Category اللي مرتبط بيه
        public string? SubCategory { get; set; }

        // ID للـ Main Category (اختياري)
        public string? MainCategory { get; set; }
    }
}
