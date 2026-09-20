namespace Infrastructure.Dtos
{
    public class ApplicantInfoInputDto
    {
        public int ApplicationId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string CurrentAddress { get; set; } = string.Empty;
    }
}
