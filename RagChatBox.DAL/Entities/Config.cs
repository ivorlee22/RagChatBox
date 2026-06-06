using System.Collections.Generic;

namespace RagChatBox.DAL.Entities
{
    public class Config
    {
        public int Id { get; set; }
        public int ExperimentId { get; set; }
        public int ChunkSize { get; set; }
        public int ChunkOverlap { get; set; }
        public string EmbeddingModel { get; set; } = null!; // e5 / openai
        public string LlmModel { get; set; } = null!;

        public Experiment Experiment { get; set; } = null!;
        public ICollection<EvaluationResult> EvaluationResults { get; set; } = new List<EvaluationResult>();
    }
}
