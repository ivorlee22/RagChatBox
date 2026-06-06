using System;

namespace RagChatBox.DAL.Entities
{
    /// <summary>
    /// Represents a student's enrollment in a teacher's course.
    /// Also used for pending invitations (Status = "invited").
    /// </summary>
    public class CourseEnrollment
    {
        public int Id { get; set; }
        public int CourseId { get; set; }
        public int UserId { get; set; }

        /// <summary>
        /// "active"  - student has enrolled / accepted invitation
        /// "invited" - teacher has invited, student has not yet accepted
        /// </summary>
        public string Status { get; set; } = "active";

        public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public Course Course { get; set; } = null!;
        public User User { get; set; } = null!;
    }
}
