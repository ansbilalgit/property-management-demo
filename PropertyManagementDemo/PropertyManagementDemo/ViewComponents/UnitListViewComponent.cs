using Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace PropertyManagementDemo.ViewComponents
{
    public class UnitListViewComponent : ViewComponent
    {
        private readonly IUnitService _units;

        public UnitListViewComponent(IUnitService units) => _units = units;

        public async Task<IViewComponentResult> InvokeAsync(int propertyId) =>
            View("~/Areas/Manager/Views/Units/_UnitList.cshtml",
                await _units.GetUnitsByPropertyAsync(propertyId, HttpContext.RequestAborted));
    }
}
