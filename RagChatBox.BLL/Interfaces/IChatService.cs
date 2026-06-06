using System.Collections.Generic;
using System.Threading.Tasks;
using RagChatBox.DAL.Entities;

namespace RagChatBox.BLL.Interfaces
{
    public interface IChatService
    {
        Task<ChatSession> CreateSessionAsync(int userId, int courseId, string title);
        Task<List<ChatSession>> GetSessionsByUserAndCourseAsync(int userId, int courseId);
        Task<ChatSession?> GetSessionByIdAsync(int sessionId);
        Task<List<Message>> GetMessagesBySessionAsync(int sessionId);
        Task<Message> SendMessageAsync(int sessionId, string userContent, int topK = 5);
        Task<bool> DeleteSessionAsync(int sessionId, int userId);
    }
}
