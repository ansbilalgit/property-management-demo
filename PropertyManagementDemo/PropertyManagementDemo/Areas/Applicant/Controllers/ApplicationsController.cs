using System.Security.Claims;
using Domain.Constants;
using Domain.Enums;
using Domain.Exceptions;
using Services.Dtos;
using Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertyManagementDemo.ViewModels.Applicant;
using PropertyManagementDemo.ViewModels.Applications;

namespace PropertyManagementDemo.Areas.Applicant.Controllers
{
    [Area("Applicant")]
    [Authorize(Roles = Roles.Applicant)]
    public class ApplicationsController : Controller
    {
        private readonly IApplicationService _applications;
        private readonly IPropertyService _properties;

        public ApplicationsController(IApplicationService applications, IPropertyService properties)
        {
            _applications = applications;
            _properties = properties;
        }

        private string ApplicantId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        [HttpGet]
        public async Task<IActionResult> Index(ApplicationListViewModel model, CancellationToken cancellationToken)
        {
            var filter = new ApplicationListFilterDto { Status = model.Status, PropertyId = model.PropertyId };

            model.Applications = await _applications.GetApplicantApplicationsAsync(ApplicantId, filter, cancellationToken);
            model.Properties = await _properties.ListAsync(cancellationToken);
            model.ShowApplicant = false;

            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Start(int unitId, CancellationToken cancellationToken)
        {
            try
            {
                var id = await _applications.StartAsync(ApplicantId, unitId, cancellationToken);
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (BusinessRuleException)
            {
                return NotFound();
            }
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id, ApplicationSection? section, CancellationToken cancellationToken)
        {
            var application = await _applications.GetByIdAsync(id, ApplicantId, cancellationToken);
            if (application is null) return NotFound();

            return View(ToViewModel(application, section ?? FirstOpenSection(application)));
        }

        // One form, one action: the clicked button (Command) decides what happens.
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Details(ApplicationViewModel model, CancellationToken cancellationToken)
        {
            var application = await _applications.GetByIdAsync(model.Id, ApplicantId, cancellationToken);
            if (application is null) return NotFound();

            var section = model.CurrentSection;
            var isEditable = application.Status is ApplicationStatus.Draft or ApplicationStatus.Returned;

            if (!Enum.IsDefined(model.Command) || !Enum.IsDefined(section))
                return BadRequest();

            if (model.Command == ApplicationCommand.Back)
                return RedirectToSection(model.Id, Previous(section));

            // Submit exists only on the Summary, and the Summary has no Continue.
            if ((model.Command == ApplicationCommand.Submit) != (section == ApplicationSection.Summary))
                return BadRequest();

            // Read-only applications only navigate; Submit falls through so the service rejects it with a message.
            if (model.Command == ApplicationCommand.Continue && !isEditable)
                return RedirectToSection(model.Id, Next(section));

            // Only the current section's fields are posted, so errors on other sections are meaningless.
            if (section == ApplicationSection.ApplicantInfo && !ModelState.IsValid)
            {
                var hasSectionErrors = ModelState.Any(e =>
                    e.Key.StartsWith(nameof(ApplicationViewModel.ApplicantInfo) + ".") && e.Value?.Errors.Count > 0);
                if (hasSectionErrors)
                    return View(nameof(Details), Rerender(application, model));
            }

            try
            {
                switch (section)
                {
                    case ApplicationSection.ApplicantInfo:
                        await _applications.SaveApplicantInfoAsync(ApplicantId, new ApplicantInfoInputDto
                        {
                            ApplicationId = model.Id,
                            FullName = model.ApplicantInfo.FullName,
                            Phone = model.ApplicantInfo.Phone,
                            Email = model.ApplicantInfo.Email,
                            CurrentAddress = model.ApplicantInfo.CurrentAddress
                        }, cancellationToken);
                        break;

                    case ApplicationSection.ResidenceHistory:
                        await _applications.CompleteResidenceHistoryAsync(ApplicantId, model.Id, cancellationToken);
                        break;

                    case ApplicationSection.Summary:
                        await _applications.SubmitAsync(ApplicantId, model.Id, cancellationToken);
                        return RedirectToSection(model.Id, ApplicationSection.Summary);
                }
            }
            catch (BusinessRuleException ex)
            {
                ModelState.Clear();
                ModelState.AddModelError(ex.Key, ex.Message);
                return View(nameof(Details), Rerender(application, model));
            }

            return RedirectToSection(model.Id, Next(section));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Withdraw(int id, CancellationToken cancellationToken)
        {
            try
            {
                await _applications.WithdrawAsync(ApplicantId, id, cancellationToken);
            }
            catch (BusinessRuleException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // Region refreshed by the modal script after a residence is added, edited or removed.
        [HttpGet]
        public async Task<IActionResult> ResidenceList(int id, CancellationToken cancellationToken)
        {
            var application = await _applications.GetByIdAsync(id, ApplicantId, cancellationToken);
            if (application is null) return NotFound();

            return PartialView("_ResidenceList", ToViewModel(application, ApplicationSection.ResidenceHistory));
        }

        [HttpGet]
        public async Task<IActionResult> AddResidence(int id, CancellationToken cancellationToken)
        {
            var application = await _applications.GetByIdAsync(id, ApplicantId, cancellationToken);
            if (application is null) return NotFound();
            if (!IsEditable(application)) return BadRequest();

            return PartialView("_ResidenceForm", new ResidenceFormViewModel { ApplicationId = id });
        }

        [HttpGet]
        public async Task<IActionResult> EditResidence(int applicationId, int residenceId, CancellationToken cancellationToken)
        {
            var residence = await FindResidenceAsync(applicationId, residenceId, cancellationToken);
            if (residence is null) return NotFound();

            return PartialView("_ResidenceForm", residence);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveResidence(ResidenceFormViewModel model, CancellationToken cancellationToken)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    await _applications.SaveResidenceAsync(ApplicantId, new ResidenceInputDto
                    {
                        Id = model.Id,
                        ApplicationId = model.ApplicationId,
                        Address = model.Address,
                        LandlordName = model.LandlordName,
                        LandlordPhone = model.LandlordPhone,
                        MoveInDate = model.MoveInDate!.Value,
                        MoveOutDate = model.MoveOutDate!.Value
                    }, cancellationToken);
                    return NoContent();
                }
                catch (BusinessRuleException ex)
                {
                    ModelState.AddModelError(ex.Key, ex.Message);
                }
            }

            return PartialView("_ResidenceForm", model);
        }

        [HttpGet]
        public async Task<IActionResult> DeleteResidence(int applicationId, int residenceId, CancellationToken cancellationToken)
        {
            var residence = await FindResidenceAsync(applicationId, residenceId, cancellationToken);
            if (residence is null) return NotFound();

            return PartialView("_ResidenceDelete", residence);
        }

        [HttpPost, ActionName("DeleteResidence"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteResidenceConfirmed(int applicationId, int residenceId, CancellationToken cancellationToken)
        {
            try
            {
                await _applications.DeleteResidenceAsync(ApplicantId, applicationId, residenceId, cancellationToken);
                return NoContent();
            }
            catch (BusinessRuleException ex)
            {
                ModelState.AddModelError(ex.Key, ex.Message);
                var residence = await FindResidenceAsync(applicationId, residenceId, cancellationToken);
                return residence is null ? NotFound() : PartialView("_ResidenceDelete", residence);
            }
        }

        private static bool IsEditable(ApplicationDto application) =>
            application.Status is ApplicationStatus.Draft or ApplicationStatus.Returned;

        private async Task<ResidenceFormViewModel?> FindResidenceAsync(int applicationId, int residenceId, CancellationToken cancellationToken)
        {
            var application = await _applications.GetByIdAsync(applicationId, ApplicantId, cancellationToken);
            var residence = application?.Residences.FirstOrDefault(r => r.Id == residenceId);
            if (application is null || residence is null || !IsEditable(application)) return null;

            return new ResidenceFormViewModel
            {
                Id = residence.Id,
                ApplicationId = applicationId,
                Address = residence.Address,
                LandlordName = residence.LandlordName,
                LandlordPhone = residence.LandlordPhone,
                MoveInDate = residence.MoveInDate,
                MoveOutDate = residence.MoveOutDate
            };
        }

        private RedirectToActionResult RedirectToSection(int id, ApplicationSection section) =>
            RedirectToAction(nameof(Details), new { id, section });

        private static ApplicationSection Next(ApplicationSection section) => section switch
        {
            ApplicationSection.ApplicantInfo => ApplicationSection.ResidenceHistory,
            _ => ApplicationSection.Summary
        };

        private static ApplicationSection Previous(ApplicationSection section) => section switch
        {
            ApplicationSection.Summary => ApplicationSection.ResidenceHistory,
            _ => ApplicationSection.ApplicantInfo
        };

        // Rebuilds the server-side state and keeps what the applicant typed into the section they were on.
        private static ApplicationViewModel Rerender(ApplicationDto application, ApplicationViewModel posted)
        {
            var viewModel = ToViewModel(application, posted.CurrentSection);
            if (posted.CurrentSection == ApplicationSection.ApplicantInfo)
                viewModel.ApplicantInfo = posted.ApplicantInfo;

            return viewModel;
        }

        // Editable applications resume at the first unsaved section; anything else opens on the read-only summary.
        private static ApplicationSection FirstOpenSection(ApplicationDto application)
        {
            if (application.Status is not (ApplicationStatus.Draft or ApplicationStatus.Returned))
                return ApplicationSection.Summary;

            if (!application.ApplicantInfoSaved) return ApplicationSection.ApplicantInfo;
            if (!application.ResidenceHistorySaved) return ApplicationSection.ResidenceHistory;
            return ApplicationSection.Summary;
        }

        private static ApplicationViewModel ToViewModel(ApplicationDto application, ApplicationSection section) => new()
        {
            Id = application.Id,
            CurrentSection = section,
            PropertyName = application.PropertyName,
            UnitNumber = application.UnitNumber,
            MonthlyRent = application.MonthlyRent,
            Status = application.Status,
            Residences = application.Residences,
            ApplicantInfoSaved = application.ApplicantInfoSaved,
            ResidenceHistorySaved = application.ResidenceHistorySaved,
            ApplicantInfo = new ApplicantInfoSectionViewModel
            {
                FullName = application.FullName,
                Phone = application.Phone,
                Email = application.Email,
                CurrentAddress = application.CurrentAddress
            }
        };
    }
}
