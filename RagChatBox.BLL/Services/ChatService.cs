using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Pgvector;
using RagChatBox.BLL.Interfaces;
using RagChatBox.DAL;
using RagChatBox.DAL.Entities;

namespace RagChatBox.BLL.Services
{
    public class ChatService : IChatService
    {
        private readonly AppDbContext _context;
        private readonly IEmbeddingService _embeddingService;
        private readonly HttpClient _httpClient;
        private readonly ILogger<ChatService> _logger;
        
        private readonly string _apiKey;
        private readonly string _endpoint;
        private readonly string _model;
        private readonly double _temperature;

        public ChatService(
            AppDbContext context,
            IEmbeddingService embeddingService,
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<ChatService> logger)
        {
            _context = context;
            _embeddingService = embeddingService;
            _httpClient = httpClient;
            _logger = logger;

            _apiKey = FirstNonEmpty(
                configuration["LlmSettings:ApiKey"],
                configuration["EmbeddingSettings:ApiKey"],
                Environment.GetEnvironmentVariable("GEMINI_API_KEY"));
                
            _endpoint = configuration["LlmSettings:Endpoint"] ?? "https://generativelanguage.googleapis.com/v1beta";
            _model = configuration["LlmSettings:Model"] ?? "gemini-1.5-flash";
            
            _temperature = double.TryParse(configuration["LlmSettings:Temperature"], out var temp) ? temp : 0.0;
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
        }

        public async Task<ChatSession> CreateSessionAsync(int userId, int courseId, string title)
        {
            var session = new ChatSession
            {
                UserId = userId,
                CourseId = courseId,
                Title = string.IsNullOrWhiteSpace(title) ? "Cuộc trò chuyện mới" : title.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.ChatSessions.Add(session);
            await _context.SaveChangesAsync();
            return session;
        }

        public async Task<List<ChatSession>> GetSessionsByUserAndCourseAsync(int userId, int courseId)
        {
            return await _context.ChatSessions
                .Where(s => s.UserId == userId && s.CourseId == courseId)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();
        }

        public async Task<ChatSession?> GetSessionByIdAsync(int sessionId)
        {
            return await _context.ChatSessions
                .Include(s => s.Course)
                .FirstOrDefaultAsync(s => s.Id == sessionId);
        }

        public async Task<List<Message>> GetMessagesBySessionAsync(int sessionId)
        {
            return await _context.Messages
                .Where(m => m.SessionId == sessionId)
                .Include(m => m.RetrievalLogs)
                    .ThenInclude(l => l.Chunk)
                        .ThenInclude(c => c.Document)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> DeleteSessionAsync(int sessionId, int userId)
        {
            var session = await _context.ChatSessions.FindAsync(sessionId);
            if (session == null || session.UserId != userId)
            {
                return false;
            }

            _context.ChatSessions.Remove(session);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<Message> SendMessageAsync(int sessionId, string userContent, int topK = 5)
        {
            var session = await _context.ChatSessions
                .Include(s => s.Course)
                .FirstOrDefaultAsync(s => s.Id == sessionId);

            if (session == null)
            {
                throw new ArgumentException("Không tìm thấy phiên trò chuyện.");
            }

            // 1. Save user message to database
            var userMessage = new Message
            {
                SessionId = sessionId,
                Role = "user",
                Content = userContent,
                CreatedAt = DateTime.UtcNow
            };
            _context.Messages.Add(userMessage);
            await _context.SaveChangesAsync();

            // Check if API key is not configured
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                var fallbackMsg = new Message
                {
                    SessionId = sessionId,
                    Role = "assistant",
                    Content = "Chưa cấu hình API Key cho Gemini AI. Vui lòng thêm ApiKey vào mục LlmSettings trong file appsettings.json hoặc biến môi trường GEMINI_API_KEY.",
                    CreatedAt = DateTime.UtcNow,
                    TokensUsed = 0
                };
                _context.Messages.Add(fallbackMsg);
                await _context.SaveChangesAsync();
                return fallbackMsg;
            }

            // 2. RAG Retrieval Step: Generate query embedding & perform similarity search
            List<DocumentChunk> retrievedChunks = new List<DocumentChunk>();
            List<double> similarityScores = new List<double>();

            var queryEmbedding = await _embeddingService.GenerateQueryEmbeddingAsync(userContent);
            if (queryEmbedding != null)
            {
                // Scopes vector search to the selected course
                var documentIds = await _context.Documents
                    .Where(d => d.CourseId == session.CourseId && d.Status == "Indexed")
                    .Select(d => d.Id)
                    .ToListAsync();

                if (documentIds.Any())
                {
                    if (AppDbContext.UsePgVector)
                    {
                        var queryVector = new Pgvector.Vector(queryEmbedding);
                        var idsArray = documentIds.ToArray();
                        
                        // We use Raw SQL with pgvector operator <=> for fast nearest-neighbor search
                        var chunks = await _context.DocumentChunks
                            .FromSql($"SELECT * FROM \"DocumentChunk\" WHERE \"DocumentId\" = ANY({idsArray}) AND \"EmbeddingE5\" IS NOT NULL ORDER BY \"EmbeddingE5\" <=> {queryVector} LIMIT {topK}")
                            .Include(c => c.Document)
                            .ToListAsync();

                        foreach (var chunk in chunks)
                        {
                            retrievedChunks.Add(chunk);
                            // Compute similarity score in-memory for saving to logs
                            var score = ComputeCosineSimilarity(queryEmbedding, chunk.EmbeddingE5!.ToArray());
                            similarityScores.Add(score);
                        }
                    }
                    else
                    {
                        // Fallback: In-memory cosine similarity search scoped to the course's chunks
                        var allChunks = await _context.DocumentChunks
                            .Where(c => documentIds.Contains(c.DocumentId) && c.EmbeddingE5 != null)
                            .Include(c => c.Document)
                            .ToListAsync();

                        var scoredChunks = allChunks
                            .Select(c => new
                            {
                                Chunk = c,
                                Score = ComputeCosineSimilarity(queryEmbedding, c.EmbeddingE5!.ToArray())
                            })
                            .Where(x => x.Score > 0.4) // Minimum threshold
                            .OrderByDescending(x => x.Score)
                            .Take(topK)
                            .ToList();

                        foreach (var item in scoredChunks)
                        {
                            retrievedChunks.Add(item.Chunk);
                            similarityScores.Add(item.Score);
                        }
                    }
                }
            }

            // 3. Construct Context-Aware Prompt
            var systemInstruction = "Bạn là trợ lý học tập thông thái của khóa học này. Hãy trả lời câu hỏi của người dùng dựa trên bối cảnh tài liệu được cung cấp. " +
                                "Lưu ý quan trọng: Nếu bối cảnh tài liệu không chứa đủ thông tin để trả lời câu hỏi, bạn bắt buộc phải trả lời chính xác cụm từ: " +
                                "\"Không tìm thấy thông tin trong tài liệu\". Không được tự bịa đặt, suy luận hoặc sử dụng kiến thức ngoài tài liệu để trả lời.";

            var contextBuilder = new StringBuilder();
            if (retrievedChunks.Any())
            {
                contextBuilder.AppendLine("Bối cảnh tài liệu tham khảo từ khóa học:");
                contextBuilder.AppendLine("---");
                for (int i = 0; i < retrievedChunks.Count; i++)
                {
                    var chunk = retrievedChunks[i];
                    var docName = chunk.Document?.FileName ?? "Tài liệu không tên";
                    contextBuilder.AppendLine($"[Tài liệu tham khảo {i + 1} - File: {docName}]");
                    contextBuilder.AppendLine(chunk.Content);
                    contextBuilder.AppendLine("---");
                }
            }
            else
            {
                contextBuilder.AppendLine("Không tìm thấy tài liệu tham khảo nào liên quan trong khóa học.");
            }

            // Prepare prompt content with context
            var currentTurnPrompt = $"{contextBuilder}\nCâu hỏi hiện tại của người dùng:\n{userContent}";

            // 4. Retrieve chat history to include in the multi-turn format
            var priorMessages = await _context.Messages
                .Where(m => m.SessionId == sessionId && m.Id != userMessage.Id)
                .OrderBy(m => m.CreatedAt)
                .Take(10) // Limit to last 10 messages to keep context window reasonable
                .ToListAsync();

            // Construct Gemini contents array
            var contentsList = new List<object>();
            foreach (var msg in priorMessages)
            {
                contentsList.Add(new
                {
                    role = msg.Role == "user" ? "user" : "model",
                    parts = new[] { new { text = msg.Content } }
                });
            }
            // Add current turn with context
            contentsList.Add(new
            {
                role = "user",
                parts = new[] { new { text = currentTurnPrompt } }
            });

            // 5. Call Gemini API
            string assistantAnswer = "Không tìm thấy thông tin trong tài liệu";
            int tokensUsed = 0;

            try
            {
                var url = $"{_endpoint.TrimEnd('/')}/models/{_model}:generateContent";
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Add("x-goog-api-key", _apiKey);

                var payload = new
                {
                    contents = contentsList,
                    systemInstruction = new
                    {
                        parts = new[] { new { text = systemInstruction } }
                    },
                    generationConfig = new
                    {
                        temperature = _temperature,
                        maxOutputTokens = 2048
                    }
                };

                request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(responseString);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("candidates", out var candidates) 
                        && candidates.GetArrayLength() > 0
                        && candidates[0].TryGetProperty("content", out var content)
                        && content.TryGetProperty("parts", out var parts)
                        && parts.GetArrayLength() > 0
                        && parts[0].TryGetProperty("text", out var text))
                    {
                        assistantAnswer = text.GetString() ?? "Không thể tạo câu trả lời.";
                    }

                    // Extract token usage (metadata) if available
                    if (root.TryGetProperty("usageMetadata", out var usageMetadata))
                    {
                        if (usageMetadata.TryGetProperty("totalTokenCount", out var totalTokens))
                        {
                            tokensUsed = totalTokens.GetInt32();
                        }
                    }
                }
                else
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Gemini API call failed. Status={Status}, Error={Error}", response.StatusCode, errorBody);
                    assistantAnswer = "Lỗi kết nối tới AI Service để sinh câu trả lời.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Gemini chat generation API.");
                assistantAnswer = "Lỗi trong quá trình xử lý phản hồi từ AI.";
            }

            // 6. Save assistant message and retrieval logs
            var assistantMessage = new Message
            {
                SessionId = sessionId,
                Role = "assistant",
                Content = assistantAnswer.Trim(),
                TokensUsed = tokensUsed,
                CreatedAt = DateTime.UtcNow
            };
            _context.Messages.Add(assistantMessage);
            await _context.SaveChangesAsync();

            // Link message to retrieved chunks in RetrievalLog
            if (retrievedChunks.Any())
            {
                var logs = new List<RetrievalLog>();
                for (int i = 0; i < retrievedChunks.Count; i++)
                {
                    logs.Add(new RetrievalLog
                    {
                        MessageId = assistantMessage.Id,
                        ChunkId = retrievedChunks[i].Id,
                        SimilarityScore = similarityScores[i],
                        Rank = i + 1
                    });
                }
                _context.RetrievalLogs.AddRange(logs);
                await _context.SaveChangesAsync();
            }

            return assistantMessage;
        }

        private static double ComputeCosineSimilarity(float[] a, float[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return 0;
            double dotProduct = 0;
            double mA = 0;
            double mB = 0;
            for (int i = 0; i < a.Length; i++)
            {
                dotProduct += a[i] * b[i];
                mA += a[i] * a[i];
                mB += b[i] * b[i];
            }
            if (mA == 0 || mB == 0) return 0;
            return dotProduct / (Math.Sqrt(mA) * Math.Sqrt(mB));
        }
    }
}
