using Domain.Enums;

namespace Infrastructure.Dtos
{
    public class ApplicationListFilterDto
    {
        public ApplicationStatus? Status { get; set; }
        public int? PropertyId { get; set; }
    }
}
