using Gym.Models;
using Gym.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Gym.Controllers
{
    public class AccountController : Controller
    {
        private readonly SetupService _setupService;
        private readonly HashingService _hashingService;
        private readonly GmailValidationService _gmailService;
        private readonly PendingRegistrationService _pendingRegistrationService;
        private readonly IConfiguration _configuration;

        public AccountController(
            SetupService setupService,
            HashingService hashingService,
            GmailValidationService gmailService,
            PendingRegistrationService pendingRegistrationService,
            IConfiguration configuration)
        {
            _setupService = setupService;
            _hashingService = hashingService;
            _gmailService = gmailService;
            _pendingRegistrationService = pendingRegistrationService;
            _configuration = configuration;
        }

        // GET: /Account/RegisterAdmin
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> RegisterAdmin()
        {
            // If an admin account already exists or setup is marked complete, redirect to Login
            if (await _setupService.IsSetupCompletedAsync())
            {
                return RedirectToAction("Login");
            }

            return View(); // Resolves to Views/Account/RegisterAdmin.cshtml
        }

        // POST: /Account/RegisterAdmin
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterAdmin(AdminViewModel model)
        {
            if (await _setupService.IsSetupCompletedAsync())
            {
                return RedirectToAction("Login");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string passwordHash = _hashingService.HashPassword(model.Password);
            var (success, error) = await _setupService.RegisterAdminAsync(model, passwordHash);

            if (!success)
            {
                ModelState.AddModelError(string.Empty, error ?? "Failed to register main admin.");
                return View(model);
            }

            // Redirect to Login page upon successful setup
            return RedirectToAction("Login");
        }

        // GET: /Account/Login
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToLocal(returnUrl);
            }

            if (!string.IsNullOrEmpty(returnUrl))
            {
                TempData["ErrorMessage"] = "Access Denied: You do not have permission to perform that action or view that page.";
                ViewData["ReturnUrl"] = returnUrl;
            }

            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var (isValid, userRole, userId, username, email, phone, errorMessage) =
                await _setupService.ValidateUserAsync(model.Username, model.Password);

            if (!isValid)
            {
                ModelState.AddModelError(string.Empty, errorMessage ?? "Invalid login credentials.");
                return View(model);
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, username ?? model.Username),
                new Claim(ClaimTypes.Role, userRole ?? "Staff"),
                new Claim(ClaimTypes.Email, email ?? string.Empty),
                new Claim(ClaimTypes.MobilePhone, phone ?? string.Empty)
            };

            var claimsIdentity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(2)
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            await _setupService.RecordLoginAsync(userId);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToRoleDefault(userRole);
        }

        // GET: /Account/RegisterStaff
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public IActionResult RegisterStaff()
        {
            return View();
        }

        // POST: /Account/RegisterStaff
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterStaff(StaffViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var (created, createError, newStaffId) = await _setupService.CreatePendingStaffAsync(model);

            if (!created)
            {
                ModelState.AddModelError(string.Empty, createError ?? "Failed to create the account.");
                return View(model);
            }

            // Create 30-minute valid token
            string token = _pendingRegistrationService.CreateToken(newStaffId.ToString());

            string confirmUrl = $"{Request.Scheme}://{Request.Host}{Url.Action("SetCredentials", "Account", new { token })}";

            var (emailSent, emailError) = await _gmailService.SendConfirmationEmailAsync(model.Email, confirmUrl);

            if (!emailSent)
            {
                await _setupService.DeletePendingStaffAsync(newStaffId);
                ModelState.AddModelError(string.Empty, emailError ?? "Failed to send verification email.");
                return View(model);
            }

            return RedirectToAction("CheckEmail", new { email = model.Email });
        }

        // GET: /Account/CheckEmail
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public IActionResult CheckEmail(string email)
        {
            ViewBag.Email = email;
            return View("CheckEmail");
        }

        // GET: /Account/SetCredentials?token=...
        [HttpGet]
        [AllowAnonymous]
        public IActionResult SetCredentials(string token)
        {
            // Verify if token is missing or expired BEFORE showing the form
            if (string.IsNullOrEmpty(token) || !_pendingRegistrationService.ValidateToken(token))
            {
                ViewBag.Error = "This confirmation link is invalid or has expired (30-minute time limit). Please request a new registration link.";
                return View("ConfirmationFailed");
            }

            var model = new SetCredentialsViewModel { Token = token };
            return View(model); // Resolves to Views/Account/SetCredentials.cshtml
        }

        // POST: /Account/SetCredentials
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetCredentials(SetCredentialsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model); // Returns Views/Account/SetCredentials.cshtml on invalid input
            }

            // Attempt to consume token (Fails if invalid or expired)
            var (success, staffIdText) = _pendingRegistrationService.Consume(model.Token);

            if (!success || staffIdText == null || !int.TryParse(staffIdText, out int staffId))
            {
                ViewBag.Error = "This confirmation link has expired (30-minute limit exceeded) or has already been used. Please contact an admin for a new link.";
                return View("ConfirmationFailed");
            }

            string passwordHash = _hashingService.HashPassword(model.Password);
            var (activated, activateError) = await _setupService.ActivateStaffWithCredentialsAsync(staffId, model.Username, passwordHash);

            if (!activated)
            {
                ViewBag.Error = activateError ?? "Failed to activate account.";
                return View("ConfirmationFailed");
            }

            return RedirectToAction("ConfirmationSuccess");
        }

        // GET: /Account/ConfirmationSuccess
        [HttpGet]
        [AllowAnonymous]
        public IActionResult ConfirmationSuccess()
        {
            return View();
        }

        // GET: /Account/ConfirmationFailed
        [HttpGet]
        [AllowAnonymous]
        public IActionResult ConfirmationFailed()
        {
            return View();
        }

        // POST: /Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId))
            {
                await _setupService.RecordLogoutAsync(userId);
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            return RedirectToAction("Index", "MainHomepage");
        }

        #region Helper Methods

        private IActionResult RedirectToRoleDefault(string? role)
        {
            return role switch
            {
                "Admin" or "MainAdmin" => RedirectToAction("ADashboard", "AdminDashboard"),
                "Trainer" => RedirectToAction("Index", "MainHomepage"),
                "Staff" => RedirectToAction("Login", "Account"),
                _ => RedirectToAction("Index", "MainHomepage")
            };
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            return RedirectToRoleDefault(userRole);
        }

        #endregion
    }
}