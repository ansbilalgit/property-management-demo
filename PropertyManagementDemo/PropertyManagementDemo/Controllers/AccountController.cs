using Domain.Constants;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PropertyManagementDemo.Extensions;
using PropertyManagementDemo.ViewModels.Account;

namespace PropertyManagementDemo.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [HttpGet, AllowAnonymous]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
                return this.RedirectToRoleHome();

            return View(new RegisterViewModel());
        }

        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var isApplicant = model.AccountType == AccountType.Applicant;

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                // The field is only shown to applicants; ignore anything posted for managers.
                CurrentAddress = isApplicant && !string.IsNullOrWhiteSpace(model.CurrentAddress)
                    ? model.CurrentAddress.Trim()
                    : null
            };

            var createResult = await _userManager.CreateAsync(user, model.Password);
            if (!createResult.Succeeded)
            {
                AddErrors(createResult);
                return View(model);
            }

            var role = isApplicant ? Roles.Applicant : Roles.PropertyManager;

            var roleResult = await _userManager.AddToRoleAsync(user, role);
            if (!roleResult.Succeeded)
            {
                // Never leave a user behind without a role.
                await _userManager.DeleteAsync(user);
                AddErrors(roleResult);
                return View(model);
            }

            await _signInManager.SignInAsync(user, isPersistent: false);
            return this.RedirectToRoleHome();
        }

        [HttpGet, AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return this.RedirectToRoleHome();

            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var result = await _signInManager.PasswordSignInAsync(
                model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                // Only follow local return URLs to avoid open-redirect attacks.
                if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                    return LocalRedirect(model.ReturnUrl);

                return this.RedirectToRoleHome();
            }

            // Deliberately generic: do not reveal whether the email exists.
            ModelState.AddModelError(string.Empty, result.IsLockedOut
                ? "This account is temporarily locked. Please try again later."
                : "Invalid email or password.");
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home", new { area = "" });
        }

        [HttpGet, AllowAnonymous]
        public IActionResult AccessDenied() => View();

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
        }
    }
}
