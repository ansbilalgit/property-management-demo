using Domain.Constants;
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

        [HttpGet]
        public async Task<IActionResult> Index(ApplicationListViewModel model, CancellationToken cancellationToken)
        {
            var filter = new ApplicationListFilterDto { Status = model.Status, PropertyId = model.PropertyId };

            model.Applications = await _applications.GetAllApplicationsAsync(filter, cancellationToken);
            model.Properties = await _properties.ListAsync(cancellationToken);
            model.ShowApplicant = true;

            return View(model);
        }
    }
}
