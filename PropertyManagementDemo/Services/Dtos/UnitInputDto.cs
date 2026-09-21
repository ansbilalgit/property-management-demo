namespace Services.Dtos
{
    public class UnitInputDto
    {
        public int? Id { get; set; }
        public int PropertyId { get; set; }
        public string UnitNumber { get; set; } = string.Empty;
        public int Bedrooms { get; set; }
        public decimal MonthlyRent { get; set; }
        public int UnitTypeId { get; set; }
    }
}
