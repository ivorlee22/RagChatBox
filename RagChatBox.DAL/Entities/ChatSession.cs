using System;
using System.Collections.Generic;

namespace RagChatBox.DAL.Entities
{
    public class ChatSession
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int CourseId { get; set; }
        public string Title { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public User User { get; set; } = null!;
        public Course Course { get; set; } = null!;
        public ICollection<Message> Messages { get; set; } = new List<Message>();
    }
}
