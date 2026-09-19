using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace PropertyManagementDemo.ViewModels.Manager
{
    public class UnitFormViewModel
    {
        public int? Id { get; set; }

        public int PropertyId { get; set; }

        [Required, StringLength(20), Display(Name = "Unit number")]
        public string UnitNumber { get; set; } = string.Empty;

        [Range(0, 10)]
        public int Bedrooms { get; set; }

        [Range(1, 100000), DataType(DataType.Currency), Display(Name = "Monthly rent")]
        public decimal MonthlyRent { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Select a unit type."), Display(Name = "Unit type")]
        public int UnitTypeId { get; set; }

        public IEnumerable<SelectListItem> UnitTypes { get; set; } = [];
    }
}
