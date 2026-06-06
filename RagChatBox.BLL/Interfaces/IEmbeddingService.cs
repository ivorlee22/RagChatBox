using System.Threading.Tasks;

namespace RagChatBox.BLL.Interfaces
{
    public interface IEmbeddingService
    {
        Task<float[]?> GenerateDocumentEmbeddingAsync(string text, string? title = null);
        Task<float[]?> GenerateQueryEmbeddingAsync(string query);
    }
}
