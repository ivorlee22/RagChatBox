using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RagChatBox.BLL.Interfaces;
using RagChatBox.DAL.Entities;
using RagChatBox.Presentation.Models;
using System.Security.Claims;

namespace RagChatBox.Presentation.Controllers
{
    [Authorize]
    public class CourseController : Controller
    {
        private readonly ICourseService _courseService;
        private readonly IUserService _userService;

        public CourseController(ICourseService courseService, IUserService userService)
        {
            _courseService = courseService;
            _userService = userService;
        }

        private int CurrentUserId =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private string CurrentUserRole =>
            User.FindFirstValue(ClaimTypes.Role) ?? "";

        // ── GET: Course ──────────────────────────────────────────────────────────

        public async Task<IActionResult> Index()
        {
            if (User.IsInRole("admin"))
            {
                var all = await _courseService.GetAllCoursesAsync();
                ViewBag.ViewMode = "admin";
                return View(all);
            }

            if (User.IsInRole("teacher"))
            {
                var mine = await _courseService.GetCoursesByOwnerAsync(CurrentUserId);
                ViewBag.ViewMode = "teacher";
                return View(mine);
            }

            // Student: enrolled teacher courses
            var enrolled = await _courseService.GetEnrolledCoursesAsync(CurrentUserId);
            var pendingInvitations = await _courseService.GetPendingInvitationsAsync(CurrentUserId);
            ViewBag.EnrolledCourses = enrolled;
            ViewBag.PersonalCourses = new List<Course>(); // Disable personal courses
            ViewBag.PendingInvitations = pendingInvitations;
            ViewBag.ViewMode = "student";
            return View(enrolled);
        }

        // ── GET: Course/Create ───────────────────────────────────────────────────

        [Authorize(Roles = "admin,teacher")]
        public async Task<IActionResult> Create()
        {
            var model = new CourseViewModel
            {
                CourseType = "teacher",
                IsVisible = true
            };
            if (User.IsInRole("admin"))
            {
                var teachers = await _userService.GetUsersByRoleAsync("teacher");
                ViewBag.Teachers = teachers;
            }
            return View(model);
        }

        // ── POST: Course/Create ──────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin,teacher")]
        public async Task<IActionResult> Create(CourseViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    int? createdBy = User.IsInRole("admin") ? model.CreatedBy : CurrentUserId;
                    await _courseService.CreateCourseAsync(
                        model.Name,
                        model.Description,
                        "teacher",
                        model.IsVisible,
                        model.CoursePassword,
                        createdBy,
                        CurrentUserRole
                    );
                    TempData["SuccessMessage"] = "Tạo lớp học thành công!";
                    return RedirectToAction(nameof(Index));
                }
                catch (System.InvalidOperationException ex)
                {
                    TempData["ErrorMessage"] = ex.Message;
                }
            }
            if (User.IsInRole("admin"))
            {
                var teachers = await _userService.GetUsersByRoleAsync("teacher");
                ViewBag.Teachers = teachers;
            }
            return View(model);
        }

        // ── GET: Course/Edit/5 ───────────────────────────────────────────────────

        [Authorize(Roles = "admin,teacher")]
        public async Task<IActionResult> Edit(int id)
        {
            var course = await _courseService.GetCourseByIdAsync(id);
            if (course == null) return NotFound();

            // Teacher can only edit their own courses
            if (User.IsInRole("teacher") && course.CreatedBy != CurrentUserId)
                return Forbid();

            var model = new CourseViewModel
            {
                Id = course.Id,
                Name = course.Name,
                Description = course.Description,
                CourseType = course.CourseType,
                IsVisible = course.IsVisible,
                CoursePassword = course.CoursePassword,
                CreatedBy = course.CreatedBy
            };

            if (User.IsInRole("admin"))
            {
                var teachers = await _userService.GetUsersByRoleAsync("teacher");
                ViewBag.Teachers = teachers;
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin,teacher")]
        public async Task<IActionResult> Edit(int id, CourseViewModel model)
        {
            if (id != model.Id) return BadRequest();

            if (ModelState.IsValid)
            {
                try
                {
                    bool success = await _courseService.UpdateCourseAsync(
                        id,
                        model.Name,
                        model.Description,
                        model.IsVisible,
                        model.CoursePassword,
                        User.IsInRole("admin") ? model.CreatedBy : (int?)null,
                        CurrentUserId,
                        CurrentUserRole
                    );

                    if (!success) return NotFound();

                    return RedirectToAction(nameof(Index));
                }
                catch (UnauthorizedAccessException)
                {
                    return Forbid();
                }
            }

            if (User.IsInRole("admin"))
            {
                var teachers = await _userService.GetUsersByRoleAsync("teacher");
                ViewBag.Teachers = teachers;
            }
            return View(model);
        }

        // ── GET: Course/Delete/5 ─────────────────────────────────────────────────

        [Authorize(Roles = "admin,teacher")]
        public async Task<IActionResult> Delete(int id)
        {
            var course = await _courseService.GetCourseByIdAsync(id);
            if (course == null) return NotFound();

            if (User.IsInRole("teacher") && course.CreatedBy != CurrentUserId)
                return Forbid();

            return View(course);
        }

        // ── POST: Course/Delete/5 ────────────────────────────────────────────────

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin,teacher")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                bool success = await _courseService.DeleteCourseAsync(id, CurrentUserId, CurrentUserRole);
                if (!success) return NotFound();
                return RedirectToAction(nameof(Index));
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        // ── POST: Course/ToggleVisibility/5 ─────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin,teacher")]
        public async Task<IActionResult> ToggleVisibility(int id)
        {
            try
            {
                bool success = await _courseService.ToggleVisibilityAsync(id, CurrentUserId, CurrentUserRole);
                if (!success) return NotFound();
                return RedirectToAction(nameof(Index));
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        // ── GET: Course/MyStudents/5 ─────────────────────────────────────────────

        [Authorize(Roles = "admin,teacher")]
        public async Task<IActionResult> MyStudents(int id)
        {
            var course = await _courseService.GetCourseByIdAsync(id);
            if (course == null) return NotFound();

            if (User.IsInRole("teacher") && course.CreatedBy != CurrentUserId)
                return Forbid();

            var students = await _courseService.GetEnrolledStudentsAsync(id);
            ViewBag.Course = course;
            return View(students);
        }

        // ── GET: Course/Invite/5 ─────────────────────────────────────────────────

        [Authorize(Roles = "admin,teacher")]
        public async Task<IActionResult> Invite(int id)
        {
            var course = await _courseService.GetCourseByIdAsync(id);
            if (course == null) return NotFound();

            if (User.IsInRole("teacher") && course.CreatedBy != CurrentUserId)
                return Forbid();

            var model = new InviteStudentViewModel { CourseId = id, CourseName = course.Name };
            return View(model);
        }

        // ── POST: Course/Invite ──────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin,teacher")]
        public async Task<IActionResult> Invite(InviteStudentViewModel model)
        {
            var course = await _courseService.GetCourseByIdAsync(model.CourseId);
            if (course == null) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    bool ok = await _courseService.InviteStudentAsync(model.CourseId, model.StudentUsername, CurrentUserId, CurrentUserRole);
                    if (ok)
                    {
                        TempData["SuccessMessage"] = $"Đã gửi lời mời tới học sinh \"{model.StudentUsername}\" thành công.";
                        return RedirectToAction(nameof(MyStudents), new { id = model.CourseId });
                    }
                    ModelState.AddModelError("StudentUsername", "Không tìm thấy học sinh hoặc học sinh đã tham gia lớp này.");
                    TempData["ErrorMessage"] = "Không tìm thấy học sinh hoặc học sinh đã tham gia lớp này.";
                }
                catch (UnauthorizedAccessException)
                {
                    return Forbid();
                }
            }
            model.CourseName = course.Name;
            return View(model);
        }

        // ── POST: Course/RemoveStudent ───────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin,teacher")]
        public async Task<IActionResult> RemoveStudent(int courseId, int userId)
        {
            try
            {
                bool success = await _courseService.UnenrollStudentAsync(courseId, userId, CurrentUserId, CurrentUserRole);
                if (!success) return NotFound();

                TempData["SuccessMessage"] = "Đã xóa học sinh khỏi lớp.";
                return RedirectToAction(nameof(MyStudents), new { id = courseId });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        // ── GET: Course/Search ───────────────────────────────────────────────────

        [Authorize(Roles = "student,admin")]
        public async Task<IActionResult> Search(string? keyword)
        {
            var courses = await _courseService.SearchTeacherCoursesAsync(keyword, CurrentUserId);
            ViewBag.Keyword = keyword;
            return View(courses);
        }

        // ── GET: Course/Enroll/5 ─────────────────────────────────────────────────

        [Authorize(Roles = "student")]
        public async Task<IActionResult> Enroll(int id)
        {
            var course = await _courseService.GetCourseByIdAsync(id);
            if (course == null) return NotFound();

            var model = new EnrollViewModel
            {
                CourseId = id,
                CourseName = course.Name,
                TeacherName = course.CreatedByUser?.Name,
                RequiresPassword = !string.IsNullOrEmpty(course.CoursePassword)
            };
            return View(model);
        }

        // ── POST: Course/Enroll ──────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "student")]
        public async Task<IActionResult> Enroll(EnrollViewModel model)
        {
            var course = await _courseService.GetCourseByIdAsync(model.CourseId);
            if (course == null) return NotFound();

            var (success, error) = await _courseService.EnrollStudentAsync(model.CourseId, CurrentUserId, model.Password);
            if (success)
            {
                TempData["SuccessMessage"] = $"Tham gia lớp \"{course.Name}\" thành công!";
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError("Password", error ?? "Không thể tham gia lớp học.");
            TempData["ErrorMessage"] = error ?? "Không thể tham gia lớp học.";
            model.CourseName = course.Name;
            model.TeacherName = course.CreatedByUser?.Name;
            model.RequiresPassword = !string.IsNullOrEmpty(course.CoursePassword);
            return View(model);
        }

        // ── GET: Course/Invitations ──────────────────────────────────────────────

        [Authorize(Roles = "student")]
        public IActionResult Invitations()
        {
            return RedirectToAction(nameof(Index));
        }

        // ── POST: Course/AcceptInvitation ────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "student")]
        public async Task<IActionResult> AcceptInvitation(int courseId)
        {
            bool ok = await _courseService.AcceptInvitationAsync(courseId, CurrentUserId);
            if (ok)
                TempData["SuccessMessage"] = "Đã chấp nhận lời mời thành công!";
            else
                TempData["ErrorMessage"] = "Không tìm thấy lời mời hoặc lời mời đã hết hạn.";

            return RedirectToAction(nameof(Index));
        }
    }
}
