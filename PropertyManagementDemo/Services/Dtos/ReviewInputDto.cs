using Domain.Enums;

namespace Services.Dtos
{
    public class ReviewInputDto
    {
        public int ApplicationId { get; set; }
        public ReviewOutcome Outcome { get; set; }
        public string? Comment { get; set; }
    }
}
