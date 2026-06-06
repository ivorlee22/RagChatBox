using System.Collections.Generic;

namespace RagChatBox.DAL.Entities
{
    public class TestQuestion
    {
        public int Id { get; set; }
        public string Question { get; set; } = null!;
        public string GroundTruthAnswer { get; set; } = null!;

        public ICollection<EvaluationResult> EvaluationResults { get; set; } = new List<EvaluationResult>();
    }
}
