using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RagChatBox.BLL.Interfaces;
using RagChatBox.Presentation.Models;
using System.IO;
using System.Threading.Tasks;

namespace RagChatBox.Presentation.Controllers
{
    [Authorize(Roles = "admin")]
    public class AdminController : Controller
    {
        private readonly IUserService _userService;
        private readonly ICourseService _courseService;
        private readonly IEmailService _emailService;
        private readonly IWebHostEnvironment _env;

        public AdminController(
            IUserService userService, 
            ICourseService courseService, 
            IEmailService emailService, 
            IWebHostEnvironment env)
        {
            _userService = userService;
            _courseService = courseService;
            _emailService = emailService;
            _env = env;
        }

        // GET: Admin/Users
        public async Task<IActionResult> Users(string? role)
        {
            var users = string.IsNullOrEmpty(role)
                ? await _userService.GetAllUsersAsync()
                : await _userService.GetUsersByRoleAsync(role);

            var model = new UserManagementViewModel
            {
                Users = users,
                FilterRole = role
            };
            return View(model);
        }

        // POST: Admin/UpdateRole
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateRole(UpdateRoleViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Dữ liệu không hợp lệ.";
                return RedirectToAction(nameof(Users));
            }

            bool ok = await _userService.UpdateUserRoleAsync(model.UserId, model.NewRole);
            TempData[ok ? "SuccessMessage" : "ErrorMessage"] = ok
                ? "Cập nhật vai trò thành công."
                : "Không thể cập nhật vai trò. Role không hợp lệ hoặc user không tồn tại.";

            return RedirectToAction(nameof(Users));
        }

        // GET: Admin/CreateUser
        [HttpGet]
        public IActionResult CreateUser()
        {
            return View(new CreateUserViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(CreateUserViewModel model)
        {
            if (ModelState.IsValid)
            {
                string? templateContent = null;
                string? loginUrl = null;

                try
                {
                    var templatePath = Path.Combine(_env.ContentRootPath, "Templates", "WelcomeEmail.html");
                    if (System.IO.File.Exists(templatePath))
                    {
                        templateContent = await System.IO.File.ReadAllTextAsync(templatePath);
                        loginUrl = $"{Request.Scheme}://{Request.Host}/Account/Login";
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Không thể tải email template: {ex.Message}");
                }

                var user = await _userService.CreateUserAsync(model.Username, model.Name, model.Password, model.Role, templateContent, loginUrl);
                if (user != null)
                {
                    TempData["SuccessMessage"] = $"Thêm người dùng {model.Name} thành công.";
                    return RedirectToAction(nameof(Users));
                }
                ModelState.AddModelError("Username", "Tên đăng nhập đã tồn tại hoặc dữ liệu không hợp lệ.");
            }
            return View(model);
        }

        // GET: Admin/ImportUsers
        [HttpGet]
        public IActionResult ImportUsers()
        {
            return View(new ImportUsersViewModel());
        }

        // POST: Admin/ImportUsers
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportUsers(ImportUsersViewModel model)
        {
            if (ModelState.IsValid)
            {
                var file = model.File;
                if (file == null || file.Length == 0)
                {
                    ModelState.AddModelError("File", "Tệp tin tải lên bị rỗng hoặc không tồn tại.");
                    return View(model);
                }

                var extension = Path.GetExtension(file.FileName);
                if (extension != ".csv" && extension != ".xlsx")
                {
                    ModelState.AddModelError("File", "Định dạng tệp tin phải là .xlsx hoặc .csv");
                    return View(model);
                }

                string? templateContent = null;
                string? loginUrl = null;

                try
                {
                    var templatePath = Path.Combine(_env.ContentRootPath, "Templates", "WelcomeEmail.html");
                    if (System.IO.File.Exists(templatePath))
                    {
                        templateContent = await System.IO.File.ReadAllTextAsync(templatePath);
                        loginUrl = $"{Request.Scheme}://{Request.Host}/Account/Login";
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Không thể tải email template: {ex.Message}");
                }

                using var stream = file.OpenReadStream();
                var (successCount, errors) = await _userService.ImportUsersAsync(
                    stream,
                    extension,
                    model.DefaultRole,
                    model.DefaultPassword,
                    templateContent,
                    loginUrl
                );

                ViewBag.Errors = errors;
                ViewBag.SuccessCount = successCount;
                ViewBag.Imported = true;

                if (errors.Count == 0)
                {
                    TempData["SuccessMessage"] = $"Nhập dữ liệu thành công! Đã thêm {successCount} tài khoản.";
                    return RedirectToAction(nameof(Users));
                }
                else
                {
                    TempData["ErrorMessage"] = $"Hoàn thành nhập với một số lỗi. Đã thêm {successCount} tài khoản thành công.";
                }
            }

            return View(model);
        }
    }
}
