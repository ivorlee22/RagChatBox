namespace RagChatBox.DAL.Entities
{
    public class RetrievalLog
    {
        public int Id { get; set; }
        public int MessageId { get; set; }
        public int ChunkId { get; set; }
        public double SimilarityScore { get; set; }
        public int Rank { get; set; }

        public Message Message { get; set; } = null!;
        public DocumentChunk Chunk { get; set; } = null!;
    }
}
