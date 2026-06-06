namespace RagChatBox.DAL.Entities
{
    public class EvaluationResult
    {
        public int Id { get; set; }
        public int ConfigId { get; set; }
        public int QuestionId { get; set; }
        public string GeneratedAnswer { get; set; } = null!;
        public double RagasScore { get; set; }
        public double AccuracyScore { get; set; }
        public double Latency { get; set; } // Milliseconds

        public Config Config { get; set; } = null!;
        public TestQuestion Question { get; set; } = null!;
    }
}
