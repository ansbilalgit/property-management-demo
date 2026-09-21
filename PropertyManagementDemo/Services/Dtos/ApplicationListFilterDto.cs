using Domain.Enums;

namespace Services.Dtos
{
    public class ApplicationListFilterDto
    {
        public ApplicationStatus? Status { get; set; }
        public int? PropertyId { get; set; }
    }
}
