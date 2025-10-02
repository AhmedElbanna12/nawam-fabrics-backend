using System.Text.Json.Serialization;

namespace fabrics.Dtos
{

    public class CreateReservationDto
    {
        public string ProductRecordId { get; set; }
        public decimal QuantityMeters { get; set; }
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public string CustomerAddress { get; set; }

        
    
    }

}
