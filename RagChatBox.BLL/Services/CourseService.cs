using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RagChatBox.BLL.Interfaces;
using RagChatBox.DAL;
using RagChatBox.DAL.Entities;

namespace RagChatBox.BLL.Services
{
    public class CourseService : ICourseService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public CourseService(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        // ── Shared ────────────────────────────────────────────────────────────────

        public async Task<Course?> GetCourseByIdAsync(int id)
        {
            return await _context.Courses
                .Include(c => c.Documents)
                .Include(c => c.CreatedByUser)
                .FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<bool> CourseExistsAsync(int id)
        {
            return await _context.Courses.AnyAsync(e => e.Id == id);
        }

        // ── Admin ─────────────────────────────────────────────────────────────────

        public async Task<List<Course>> GetAllCoursesAsync()
        {
            return await _context.Courses
                .Include(c => c.Documents)
                .Include(c => c.CreatedByUser)
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        // ── Teacher ───────────────────────────────────────────────────────────────

        public async Task<List<Course>> GetCoursesByOwnerAsync(int userId)
        {
            return await _context.Courses
                .Include(c => c.Documents)
                .Include(c => c.Enrollments)
                .Where(c => c.CreatedBy == userId)
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<Course> CreateCourseAsync(
            string name,
            string? description,
            string courseType,
            bool isVisible,
            string? coursePassword,
            int? createdBy,
            string userRole)
        {
            var course = new Course
            {
                Name = name,
                Description = description,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow
            };

            if (userRole == "student")
            {
                course.CourseType = "personal";
                course.IsVisible = false;
                course.CoursePassword = null;
            }
            else if (userRole == "teacher")
            {
                course.CourseType = "teacher";
                course.IsVisible = isVisible;
                course.CoursePassword = string.IsNullOrWhiteSpace(coursePassword) ? null : coursePassword;
            }
            else // admin
            {
                course.CourseType = courseType;
                course.IsVisible = isVisible;
                course.CoursePassword = string.IsNullOrWhiteSpace(coursePassword) ? null : coursePassword;
            }

            _context.Courses.Add(course);
            await _context.SaveChangesAsync();
            return course;
        }

        public async Task<bool> UpdateCourseAsync(
            int id,
            string name,
            string? description,
            bool isVisible,
            string? coursePassword,
            int? assignedTeacherId,
            int currentUserId,
            string userRole)
        {
            var course = await _context.Courses.FindAsync(id);
            if (course == null) return false;

            // Authorization Check
            if (userRole == "teacher" && course.CreatedBy != currentUserId)
            {
                throw new UnauthorizedAccessException("Bạn không có quyền chỉnh sửa khóa học này.");
            }

            course.Name = name;
            course.Description = description;
            course.IsVisible = isVisible;
            course.CoursePassword = string.IsNullOrWhiteSpace(coursePassword) ? null : coursePassword;

            if (userRole == "admin")
            {
                course.CreatedBy = assignedTeacherId;
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteCourseAsync(int id, int currentUserId, string userRole)
        {
            var course = await _context.Courses.FindAsync(id);
            if (course == null) return false;

            // Authorization Check
            if (userRole == "teacher" && course.CreatedBy != currentUserId)
            {
                throw new UnauthorizedAccessException("Bạn không có quyền xóa khóa học này.");
            }

            _context.Courses.Remove(course);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ToggleVisibilityAsync(int courseId, int currentUserId, string userRole)
        {
            var course = await _context.Courses.FindAsync(courseId);
            if (course == null) return false;

            // Authorization Check
            if (userRole == "teacher" && course.CreatedBy != currentUserId)
            {
                throw new UnauthorizedAccessException("Bạn không có quyền thay đổi trạng thái hiển thị của khóa học này.");
            }

            course.IsVisible = !course.IsVisible;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> InviteStudentAsync(int courseId, string studentUsername, int currentUserId, string userRole)
        {
            var course = await _context.Courses.FindAsync(courseId);
            if (course == null) return false;

            // Authorization Check
            if (userRole == "teacher" && course.CreatedBy != currentUserId)
            {
                throw new UnauthorizedAccessException("Bạn không có quyền mời học sinh tham gia khóa học này.");
            }

            var student = await _context.Users
                .FirstOrDefaultAsync(u => u.Username.ToLower() == studentUsername.ToLower() && u.Role == "student");
            if (student == null) return false;

            // Check if already enrolled or invited
            bool exists = await _context.CourseEnrollments
                .AnyAsync(e => e.CourseId == courseId && e.UserId == student.Id);
            if (exists) return false;

            _context.CourseEnrollments.Add(new CourseEnrollment
            {
                CourseId = courseId,
                UserId = student.Id,
                Status = "invited",
                EnrolledAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<CourseEnrollment>> GetEnrolledStudentsAsync(int courseId)
        {
            return await _context.CourseEnrollments
                .Include(e => e.User)
                .Where(e => e.CourseId == courseId && e.Status == "active")
                .OrderBy(e => e.User.Name)
                .ToListAsync();
        }

        public async Task<bool> UnenrollStudentAsync(int courseId, int userId, int currentUserId, string userRole)
        {
            var course = await _context.Courses.FindAsync(courseId);
            if (course == null) return false;

            // Authorization Check
            if (userRole == "teacher" && course.CreatedBy != currentUserId)
            {
                throw new UnauthorizedAccessException("Bạn không có quyền xóa học sinh khỏi khóa học này.");
            }

            var enrollment = await _context.CourseEnrollments
                .FirstOrDefaultAsync(e => e.CourseId == courseId && e.UserId == userId);
            if (enrollment != null)
            {
                _context.CourseEnrollments.Remove(enrollment);
                await _context.SaveChangesAsync();
                return true;
            }
            return false;
        }

        // ── Student ───────────────────────────────────────────────────────────────

        public async Task<List<Course>> SearchTeacherCoursesAsync(string? keyword, int userId)
        {
            var enrolledCourseIds = await _context.CourseEnrollments
                .Where(e => e.UserId == userId && e.Status == "active")
                .Select(e => e.CourseId)
                .ToListAsync();

            var query = _context.Courses
                .Include(c => c.Documents)
                .Include(c => c.CreatedByUser)
                .Where(c => c.CourseType == "teacher" && c.IsVisible && !enrolledCourseIds.Contains(c.Id));

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                keyword = keyword.Trim().ToLower();
                query = query.Where(c =>
                    c.Name.ToLower().Contains(keyword) ||
                    (c.Description != null && c.Description.ToLower().Contains(keyword)) ||
                    (c.CreatedByUser != null && c.CreatedByUser.Name.ToLower().Contains(keyword)));
            }

            return await query.OrderBy(c => c.Name).ToListAsync();
        }

        public async Task<(bool Success, string? Error)> EnrollStudentAsync(int courseId, int userId, string? password)
        {
            var course = await _context.Courses.FindAsync(courseId);
            if (course == null) return (false, "Không tìm thấy khóa học.");
            if (course.CourseType != "teacher") return (false, "Chỉ có thể tham gia lớp học của giáo viên.");
            if (!course.IsVisible) return (false, "Lớp học này hiện không mở đăng ký.");

            // Password check
            if (!string.IsNullOrEmpty(course.CoursePassword))
            {
                if (string.IsNullOrWhiteSpace(password) || password != course.CoursePassword)
                    return (false, "Mật khẩu lớp học không đúng.");
            }

            // Already enrolled?
            var existing = await _context.CourseEnrollments
                .FirstOrDefaultAsync(e => e.CourseId == courseId && e.UserId == userId);

            if (existing != null)
            {
                if (existing.Status == "active") return (false, "Bạn đã tham gia lớp học này rồi.");
                // Was invited — auto-activate
                existing.Status = "active";
                existing.EnrolledAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return (true, null);
            }

            _context.CourseEnrollments.Add(new CourseEnrollment
            {
                CourseId = courseId,
                UserId = userId,
                Status = "active",
                EnrolledAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
            return (true, null);
        }

        public async Task<bool> AcceptInvitationAsync(int courseId, int userId)
        {
            var enrollment = await _context.CourseEnrollments
                .FirstOrDefaultAsync(e => e.CourseId == courseId && e.UserId == userId && e.Status == "invited");
            if (enrollment == null) return false;

            enrollment.Status = "active";
            enrollment.EnrolledAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<Course>> GetEnrolledCoursesAsync(int userId)
        {
            return await _context.CourseEnrollments
                .Include(e => e.Course)
                    .ThenInclude(c => c.Documents)
                .Include(e => e.Course)
                    .ThenInclude(c => c.CreatedByUser)
                .Where(e => e.UserId == userId && e.Status == "active")
                .Select(e => e.Course)
                .OrderBy(c => c.Name)
                .ToListAsync();
        }

        public async Task<List<CourseEnrollment>> GetPendingInvitationsAsync(int userId)
        {
            return await _context.CourseEnrollments
                .Include(e => e.Course)
                    .ThenInclude(c => c.CreatedByUser)
                .Where(e => e.UserId == userId && e.Status == "invited")
                .OrderByDescending(e => e.EnrolledAt)
                .ToListAsync();
        }
    }
}
