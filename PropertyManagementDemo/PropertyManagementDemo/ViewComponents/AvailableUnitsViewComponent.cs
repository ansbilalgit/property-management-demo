using Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace PropertyManagementDemo.ViewComponents
{
    public class AvailableUnitsViewComponent : ViewComponent
    {
        private readonly IUnitService _units;

        public AvailableUnitsViewComponent(IUnitService units) => _units = units;

        public async Task<IViewComponentResult> InvokeAsync() =>
            View("~/Areas/Applicant/Views/Units/_AvailableUnits.cshtml",
                await _units.GetAvailableUnitsAsync(HttpContext.RequestAborted));
    }
}
