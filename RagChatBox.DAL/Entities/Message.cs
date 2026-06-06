using System;
using System.Collections.Generic;

namespace RagChatBox.DAL.Entities
{
    public class Message
    {
        public int Id { get; set; }
        public int SessionId { get; set; }
        public string Role { get; set; } = null!; // user / assistant
        public string Content { get; set; } = null!;
        public int TokensUsed { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ChatSession Session { get; set; } = null!;
        public ICollection<RetrievalLog> RetrievalLogs { get; set; } = new List<RetrievalLog>();
    }
}
