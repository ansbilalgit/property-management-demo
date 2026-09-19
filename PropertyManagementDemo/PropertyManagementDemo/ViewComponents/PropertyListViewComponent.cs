using Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace PropertyManagementDemo.ViewComponents
{
    public class PropertyListViewComponent : ViewComponent
    {
        private readonly IPropertyService _properties;

        public PropertyListViewComponent(IPropertyService properties) => _properties = properties;

        public async Task<IViewComponentResult> InvokeAsync() =>
            View("~/Areas/Manager/Views/Properties/_PropertyList.cshtml",
                await _properties.ListAsync(HttpContext.RequestAborted));
    }
}
