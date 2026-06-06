using System.Collections.Generic;
using Pgvector;

namespace RagChatBox.DAL.Entities
{
    public class DocumentChunk
    {
        public int Id { get; set; }
        public int DocumentId { get; set; }
        public string Content { get; set; } = null!;
        public int ChunkIndex { get; set; }
        
        public Vector? EmbeddingE5 { get; set; }
        public Vector? EmbeddingOpenAI { get; set; }
        
        public string? MetadataJson { get; set; } // Page, section, etc.

        public Document Document { get; set; } = null!;
        public ICollection<RetrievalLog> RetrievalLogs { get; set; } = new List<RetrievalLog>();
    }
}
