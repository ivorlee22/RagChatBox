using System.Collections.Generic;
using System.Threading.Tasks;
using RagChatBox.DAL.Entities;

namespace RagChatBox.BLL.Interfaces
{
    public interface ICourseService
    {
        // ── Shared ──────────────────────────────────────────────────────────────
        Task<Course?> GetCourseByIdAsync(int id);
        Task<bool> CourseExistsAsync(int id);

        // ── Admin ────────────────────────────────────────────────────────────────
        /// <summary>Admin: get all courses regardless of owner or visibility.</summary>
        Task<List<Course>> GetAllCoursesAsync();

        // ── Teacher ──────────────────────────────────────────────────────────────
        /// <summary>Get courses created by a specific user (teacher or student personal).</summary>
        Task<List<Course>> GetCoursesByOwnerAsync(int userId);

        Task<Course> CreateCourseAsync(
            string name,
            string? description,
            string courseType,
            bool isVisible,
            string? coursePassword,
            int? createdBy,
            string userRole);
        Task<bool> UpdateCourseAsync(
            int id,
            string name,
            string? description,
            bool isVisible,
            string? coursePassword,
            int? assignedTeacherId,
            int currentUserId,
            string userRole);
        Task<bool> DeleteCourseAsync(int id, int currentUserId, string userRole);

        /// <summary>Toggle IsVisible flag for a teacher course.</summary>
        Task<bool> ToggleVisibilityAsync(int courseId, int currentUserId, string userRole);

        /// <summary>Teacher invites a student to a course (status = "invited").</summary>
        Task<bool> InviteStudentAsync(int courseId, string studentUsername, int currentUserId, string userRole);

        /// <summary>Get all students enrolled (status = "active") in a teacher's course.</summary>
        Task<List<CourseEnrollment>> GetEnrolledStudentsAsync(int courseId);

        /// <summary>Remove a student from a course.</summary>
        Task<bool> UnenrollStudentAsync(int courseId, int userId, int currentUserId, string userRole);

        // ── Student ───────────────────────────────────────────────────────────────
        /// <summary>Get teacher-created courses that are visible, optionally filtered by keyword.</summary>
        Task<List<Course>> SearchTeacherCoursesAsync(string? keyword, int userId);

        /// <summary>
        /// Enroll a student in a teacher course.
        /// Returns (success, errorMessage). Fails if password is wrong or already enrolled.
        /// </summary>
        Task<(bool Success, string? Error)> EnrollStudentAsync(int courseId, int userId, string? password);

        /// <summary>Accept a pending invitation.</summary>
        Task<bool> AcceptInvitationAsync(int courseId, int userId);

        /// <summary>Get all active teacher courses a student has enrolled in.</summary>
        Task<List<Course>> GetEnrolledCoursesAsync(int userId);

        /// <summary>Get pending invitations for a student.</summary>
        Task<List<CourseEnrollment>> GetPendingInvitationsAsync(int userId);
    }
}
