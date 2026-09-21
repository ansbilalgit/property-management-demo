using Domain.Enums;
using Services.Dtos;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace PropertyManagementDemo.ViewModels.Applicant
{
    public class ApplicationViewModel
    {
        // Posted by the form.
        public int Id { get; set; }
        public ApplicationSection CurrentSection { get; set; } = ApplicationSection.ApplicantInfo;
        public ApplicationCommand Command { get; set; }
        public ApplicantInfoSectionViewModel ApplicantInfo { get; set; } = new();

        // Filled in by the server on every render; never bound from the request.
        [BindNever] 
        public string PropertyName { get; set; } = string.Empty;
        [BindNever] 
        public string UnitNumber { get; set; } = string.Empty;
        [BindNever] 
        public decimal MonthlyRent { get; set; }
        [BindNever] 
        public ApplicationStatus Status { get; set; }
        [BindNever] 
        public IReadOnlyList<ResidenceDto> Residences { get; set; } = [];
        [BindNever] 
        public bool ApplicantInfoSaved { get; set; }
        [BindNever] 
        public bool ResidenceHistorySaved { get; set; }

        public bool IsEditable => Status is ApplicationStatus.Draft or ApplicationStatus.Returned;

        public bool CanSubmit => IsEditable && ApplicantInfoSaved && ResidenceHistorySaved;
    }
}
