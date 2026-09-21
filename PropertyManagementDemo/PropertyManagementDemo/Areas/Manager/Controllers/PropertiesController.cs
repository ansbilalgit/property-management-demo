using Domain.Constants;
using Domain.Exceptions;
using Infrastructure.Dtos;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertyManagementDemo.ViewModels.Manager;

namespace PropertyManagementDemo.Areas.Manager.Controllers
{
    [Area("Manager")]
    [Authorize(Roles = Roles.PropertyManager)]
    public class PropertiesController : Controller
    {
        private readonly IPropertyService _properties;

        public PropertiesController(IPropertyService properties) => _properties = properties;

        public IActionResult Index() => View();

        public async Task<IActionResult> List(CancellationToken cancellationToken) =>
            PartialView("_PropertyList", await _properties.ListAsync(cancellationToken));

        [HttpGet]
        public IActionResult Create() => PartialView("_PropertyForm", new PropertyFormViewModel());

        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
        {
            var property = await _properties.GetByIdAsync(id, cancellationToken);
            if (property is null) return NotFound();

            return PartialView("_PropertyForm", new PropertyFormViewModel
            {
                Id = property.Id,
                Name = property.Name,
                AddressLine = property.AddressLine,
                City = property.City,
                State = property.State,
                PostalCode = property.PostalCode
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(PropertyFormViewModel model, CancellationToken cancellationToken)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    await _properties.SaveAsync(new PropertyInputDto
                    {
                        Id = model.Id,
                        Name = model.Name,
                        AddressLine = model.AddressLine,
                        City = model.City,
                        State = model.State,
                        PostalCode = model.PostalCode
                    }, cancellationToken);
                    return NoContent();
                }
                catch (BusinessRuleException ex)
                {
                    ModelState.AddModelError(ex.Key, ex.Message);
                }
            }

            return PartialView("_PropertyForm", model);
        }

        [HttpGet]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        {
            var property = await _properties.GetByIdAsync(id, cancellationToken);
            if (property is null) return NotFound();

            return PartialView("_PropertyDelete", property);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken cancellationToken)
        {
            try
            {
                await _properties.DeleteAsync(id, cancellationToken);
                return NoContent();
            }
            catch (BusinessRuleException ex)
            {
                ModelState.AddModelError(ex.Key, ex.Message);
                var property = await _properties.GetByIdAsync(id, cancellationToken);
                return property is null ? NotFound() : PartialView("_PropertyDelete", property);
            }
        }
    }
}
