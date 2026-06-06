using System;
using System.Collections.Generic;

namespace RagChatBox.DAL.Entities
{
    public class Experiment
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Type { get; set; } = null!; // rag / fine-tune
        public string Status { get; set; } = "Pending"; // pending / running / completed / failed
        public int CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }

        public User User { get; set; } = null!;
        public ICollection<Config> Configs { get; set; } = new List<Config>();
    }
}
