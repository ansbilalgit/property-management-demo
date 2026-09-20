using Domain.Enums;
using Infrastructure.Dtos;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace PropertyManagementDemo.ViewModels.Applications
{
    public class ApplicationListViewModel
    {
        // Bound from the query string, so filtered lists can be bookmarked.
        public ApplicationStatus? Status { get; set; }
        public int? PropertyId { get; set; }

        // Filled in by the server; never bound from the request.
        [BindNever]
        public IReadOnlyList<ApplicationDto> Applications { get; set; } = [];

        [BindNever]
        public IReadOnlyList<PropertyDto> Properties { get; set; } = [];

        [BindNever]
        public bool ShowApplicant { get; set; }
    }
}
