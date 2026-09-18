using Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PropertyManagementDemo.Areas.Manager.Controllers
{
    [Area("Manager")]
    [Authorize(Roles = Roles.PropertyManager)]
    public class DashboardController : Controller
    {
        public IActionResult Index() => View();
    }
}
