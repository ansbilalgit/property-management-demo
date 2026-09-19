using Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PropertyManagementDemo.Areas.Applicant.Controllers
{
    [Area("Applicant")]
    [Authorize(Roles = Roles.Applicant)]
    public class UnitsController : Controller
    {
        public IActionResult Index() => View();
    }
}
