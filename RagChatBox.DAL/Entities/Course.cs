using System;
using System.Collections.Generic;

namespace RagChatBox.DAL.Entities
{
    public class Course
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }

        /// <summary>
        /// FK to User (teacher or student who created this course). Nullable for legacy data.
        /// </summary>
        public int? CreatedBy { get; set; }

        /// <summary>
        /// "teacher" = class created by teacher (can be joined by students)
        /// "personal" = private class created by student (only that student sees it)
        /// </summary>
        public string CourseType { get; set; } = "teacher";

        /// <summary>
        /// Whether this teacher class is visible (searchable) to students.
        /// Only applies when CourseType = "teacher".
        /// </summary>
        public bool IsVisible { get; set; } = true;

        /// <summary>
        /// Optional password for enrollment. Null means no password required.
        /// Only applies when CourseType = "teacher".
        /// </summary>
        public string? CoursePassword { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public User? CreatedByUser { get; set; }
        public ICollection<Document> Documents { get; set; } = new List<Document>();
        public ICollection<ChatSession> ChatSessions { get; set; } = new List<ChatSession>();
        public ICollection<CourseEnrollment> Enrollments { get; set; } = new List<CourseEnrollment>();
    }
}
