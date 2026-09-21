using System.Security.Claims;
using Domain.Constants;
using Domain.Enums;
using Domain.Exceptions;
using Infrastructure.Dtos;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertyManagementDemo.ViewModels.Applications;

namespace PropertyManagementDemo.Areas.Manager.Controllers
{
    [Area("Manager")]
    [Authorize(Roles = Roles.PropertyManager)]
    public class ApplicationsController : Controller
    {
        private readonly IApplicationService _applications;
        private readonly IPropertyService _properties;

        public ApplicationsController(IApplicationService applications, IPropertyService properties)
        {
            _applications = applications;
            _properties = properties;
        }

        private string ManagerId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        [HttpGet]
        public async Task<IActionResult> Index(ApplicationListViewModel model, CancellationToken cancellationToken)
        {
            var filter = new ApplicationListFilterDto { Status = model.Status, PropertyId = model.PropertyId };

            model.Applications = await _applications.GetAllApplicationsAsync(filter, cancellationToken);
            model.Properties = await _properties.ListAsync(cancellationToken);
            model.ShowApplicant = true;

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
        {
            var application = await _applications.GetForReviewAsync(id, cancellationToken);
            if (application is null) return NotFound();

            return View(application);
        }

        // Region refreshed by the modal script after a review, so the status and history update in place.
        [HttpGet]
        public async Task<IActionResult> Content(int id, CancellationToken cancellationToken)
        {
            var application = await _applications.GetForReviewAsync(id, cancellationToken);
            if (application is null) return NotFound();

            return PartialView("_ApplicationContent", application);
        }

        [HttpGet]
        public async Task<IActionResult> Review(int id, CancellationToken cancellationToken)
        {
            var application = await _applications.GetForReviewAsync(id, cancellationToken);
            if (application is null) return NotFound();
            if (application.Status != ApplicationStatus.Submitted) return BadRequest();

            return PartialView("_ReviewForm", new ReviewFormViewModel { ApplicationId = id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Review(ReviewFormViewModel model, CancellationToken cancellationToken)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    await _applications.ReviewAsync(ManagerId, new ReviewInputDto
                    {
                        ApplicationId = model.ApplicationId,
                        Outcome = model.Outcome!.Value,
                        Comment = model.Comment
                    }, cancellationToken);
                    return NoContent();
                }
                catch (BusinessRuleException ex)
                {
                    ModelState.AddModelError(ex.Key, ex.Message);
                }
            }

            return PartialView("_ReviewForm", model);
        }
    }
}
