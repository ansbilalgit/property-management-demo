namespace Domain.Entities
{
    public class Residence
    {
        public int Id { get; set; }

        public int RentalApplicationId { get; set; }
        public RentalApplication RentalApplication { get; set; } = null!;

        public string Address { get; set; } = string.Empty;
        public string LandlordName { get; set; } = string.Empty;
        public string LandlordPhone { get; set; } = string.Empty;
        public DateOnly MoveInDate { get; set; }
        public DateOnly MoveOutDate { get; set; }
    }
}
