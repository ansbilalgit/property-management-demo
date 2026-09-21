namespace Services.Dtos
{
    public class UnitDto
    {
        public int Id { get; set; }
        public int PropertyId { get; set; }
        public string PropertyName { get; set; } = string.Empty;
        public string UnitNumber { get; set; } = string.Empty;
        public int Bedrooms { get; set; }
        public decimal MonthlyRent { get; set; }
        public int UnitTypeId { get; set; }
        public string UnitTypeName { get; set; } = string.Empty;
        public bool UnitTypeIsActive { get; set; }
        public bool IsAvailable { get; set; }
    }
}
