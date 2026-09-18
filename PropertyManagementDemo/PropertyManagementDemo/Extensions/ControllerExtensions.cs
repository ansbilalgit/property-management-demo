using Domain.Constants;
using Microsoft.AspNetCore.Mvc;

namespace PropertyManagementDemo.Extensions
{
    public static class ControllerExtensions
    {
        /// <summary>
        /// Sends a signed-in user to the landing page for their role.
        /// Anonymous users (or users without a role) go to the public home page.
        /// </summary>
        public static IActionResult RedirectToRoleHome(this ControllerBase controller)
        {
            var user = controller.User;

            if (user.IsInRole(Roles.PropertyManager))
                return controller.RedirectToAction("Index", "Dashboard", new { area = "Manager" });

            if (user.IsInRole(Roles.Applicant))
                return controller.RedirectToAction("Index", "Dashboard", new { area = "Applicant" });

            return controller.RedirectToAction("Index", "Home", new { area = "" });
        }
    }
}
