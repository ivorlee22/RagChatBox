using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RagChatBox.BLL.Interfaces;
using RagChatBox.Presentation.Models;
using System.Security.Claims;

namespace RagChatBox.Presentation.Controllers
{
    public class AccountController : Controller
    {
        private readonly IUserService _userService;

        public AccountController(IUserService userService)
        {
            _userService = userService;
        }

        // GET: Account/Login
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            // If user is already authenticated, redirect to Courses
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Course");
            }

            var model = new LoginViewModel { ReturnUrl = returnUrl };
            return View(model);
        }

        // POST: Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userService.AuthenticateAsync(model.Username, model.Password);
                if (user != null)
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, user.Username),
                        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                        new Claim(ClaimTypes.Role, user.Role),
                        new Claim("FullName", user.Name)
                    };

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var authProperties = new AuthenticationProperties
                    {
                        IsPersistent = true // Preserves session cookie across browser sessions
                    };

                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(claimsIdentity),
                        authProperties);

                    if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                    {
                        return Redirect(model.ReturnUrl);
                    }
                    return RedirectToAction("Index", "Course");
                }

                ModelState.AddModelError(string.Empty, "Tên đăng nhập hoặc mật khẩu không chính xác.");
                TempData["ErrorMessage"] = "Tên đăng nhập hoặc mật khẩu không chính xác.";
            }

            return View(model);
        }

        // Register actions have been removed as self-registration is disabled.

        // POST: Account/Logout - STRICTLY POST ONLY to prevent Logout CSRF
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Account");
        }

        // GET: Account/AccessDenied
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        // GET: Account/Profile
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            {
                return RedirectToAction("Login");
            }

            var user = await _userService.GetUserByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            var model = new ProfileViewModel
            {
                Username = user.Username,
                FullName = user.Name,
                Role = user.Role,
                CreatedAt = user.CreatedAt
            };

            return View(model);
        }

        // POST: Account/Profile
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(ProfileViewModel model)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            {
                return RedirectToAction("Login");
            }

            var user = await _userService.GetUserByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            // Populate display info again in case of validation failure/success return
            model.Username = user.Username;
            model.FullName = user.Name;
            model.Role = user.Role;
            model.CreatedAt = user.CreatedAt;

            // Remove validation for display fields since they are read-only
            ModelState.Remove(nameof(model.Username));
            ModelState.Remove(nameof(model.FullName));
            ModelState.Remove(nameof(model.Role));

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            bool ok = await _userService.ChangePasswordAsync(userId, model.OldPassword, model.NewPassword);
            if (ok)
            {
                TempData["SuccessMessage"] = "Đổi mật khẩu thành công!";
                return RedirectToAction(nameof(Profile));
            }

            ModelState.AddModelError(nameof(model.OldPassword), "Mật khẩu hiện tại không chính xác.");
            TempData["ErrorMessage"] = "Đổi mật khẩu thất bại. Vui lòng kiểm tra lại.";
            return View(model);
        }
    }
}
