using System;
using System.Collections.Generic;

namespace RagChatBox.DAL.Entities
{
    public class Document
    {
        public int Id { get; set; }
        public int CourseId { get; set; }
        public string FileName { get; set; } = null!;
        public string FilePath { get; set; } = null!;
        public long FileSize { get; set; }
        public string FileType { get; set; } = null!;
        public string Status { get; set; } = "Pending"; // pending / processing / indexed / failed
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; set; }
        public string? ErrorMessage { get; set; }
        public string? UploadedBy { get; set; }

        public Course Course { get; set; } = null!;
        public ICollection<DocumentChunk> Chunks { get; set; } = new List<DocumentChunk>();
    }
}
