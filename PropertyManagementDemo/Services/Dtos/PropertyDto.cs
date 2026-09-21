namespace Services.Dtos
{
    public class PropertyDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string AddressLine { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public int UnitCount { get; set; }
    }
}
