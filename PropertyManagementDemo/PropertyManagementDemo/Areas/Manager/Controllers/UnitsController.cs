using Domain.Constants;
using Domain.Exceptions;
using Infrastructure.Dtos;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PropertyManagementDemo.ViewModels.Manager;

namespace PropertyManagementDemo.Areas.Manager.Controllers
{
    [Area("Manager")]
    [Authorize(Roles = Roles.PropertyManager)]
    public class UnitsController : Controller
    {
        private readonly IUnitService _units;
        private readonly IPropertyService _properties;

        public UnitsController(IUnitService units, IPropertyService properties)
        {
            _units = units;
            _properties = properties;
        }

        public async Task<IActionResult> Index(int propertyId, CancellationToken cancellationToken)
        {
            var property = await _properties.GetByIdAsync(propertyId, cancellationToken);
            if (property is null) return NotFound();

            return View(property);
        }

        public async Task<IActionResult> List(int propertyId, CancellationToken cancellationToken) =>
            PartialView("_UnitList", await _units.GetUnitsByPropertyAsync(propertyId, cancellationToken));

        [HttpGet]
        public async Task<IActionResult> Create(int propertyId, CancellationToken cancellationToken)
        {
            var property = await _properties.GetByIdAsync(propertyId, cancellationToken);
            if (property is null) return NotFound();

            return await FormAsync(new UnitFormViewModel { PropertyId = propertyId, Bedrooms = 1 }, cancellationToken);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
        {
            var unit = await _units.GetByIdAsync(id, cancellationToken);
            if (unit is null) return NotFound();

            return await FormAsync(new UnitFormViewModel
            {
                Id = unit.Id,
                PropertyId = unit.PropertyId,
                UnitNumber = unit.UnitNumber,
                Bedrooms = unit.Bedrooms,
                MonthlyRent = unit.MonthlyRent,
                UnitTypeId = unit.UnitTypeId
            }, cancellationToken);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(UnitFormViewModel model, CancellationToken cancellationToken)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    await _units.SaveAsync(new UnitInputDto
                    {
                        Id = model.Id,
                        PropertyId = model.PropertyId,
                        UnitNumber = model.UnitNumber,
                        Bedrooms = model.Bedrooms,
                        MonthlyRent = model.MonthlyRent,
                        UnitTypeId = model.UnitTypeId
                    }, cancellationToken);
                    return NoContent();
                }
                catch (BusinessRuleException ex)
                {
                    ModelState.AddModelError(ex.Key, ex.Message);
                }
            }

            return await FormAsync(model, cancellationToken);
        }

        [HttpGet]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        {
            var unit = await _units.GetByIdAsync(id, cancellationToken);
            if (unit is null) return NotFound();

            return PartialView("_UnitDelete", unit);
        }

        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id, CancellationToken cancellationToken)
        {
            await _units.DeleteAsync(id, cancellationToken);
            return NoContent();
        }

        private async Task<IActionResult> FormAsync(UnitFormViewModel model, CancellationToken cancellationToken)
        {
            // When editing, the unit's current type stays selectable even if it has since been made inactive.
            var currentTypeId = model.Id is { } id ? (await _units.GetByIdAsync(id, cancellationToken))?.UnitTypeId : null;
            var types = await _units.GetSelectableUnitTypesAsync(currentTypeId, cancellationToken);
            model.UnitTypes = types.Select(t => new SelectListItem(t.IsActive ? t.Name : $"{t.Name} (inactive)", t.Id.ToString()));

            return PartialView("_UnitForm", model);
        }
    }
}
