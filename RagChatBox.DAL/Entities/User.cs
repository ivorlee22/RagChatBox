using System;
using System.Collections.Generic;

namespace RagChatBox.DAL.Entities
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = null!;
        public string PasswordHash { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string Role { get; set; } = null!; // student / teacher / admin
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string SubscriptionTier { get; set; } = "Free"; // Free / Plus / Pro / Max

        // Navigation properties
        public ICollection<ChatSession> ChatSessions { get; set; } = new List<ChatSession>();
        public ICollection<Experiment> Experiments { get; set; } = new List<Experiment>();
        public ICollection<Course> CreatedCourses { get; set; } = new List<Course>();
        public ICollection<CourseEnrollment> Enrollments { get; set; } = new List<CourseEnrollment>();
    }
}
