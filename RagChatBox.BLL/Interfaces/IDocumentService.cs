using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using RagChatBox.DAL.Entities;

namespace RagChatBox.BLL.Interfaces
{
    public interface IDocumentService
    {
        Task<List<Document>> GetDocumentsByCourseAsync(int courseId);
        Task<Document?> GetDocumentByIdAsync(int id);
        Task CreateDocumentAsync(Document document);
        Task<Document> UploadDocumentAsync(
            Stream fileStream,
            string fileName,
            long fileSize,
            string contentType,
            int courseId,
            string uploadsFolder,
            string? uploadedBy = null);
        Task DeleteDocumentAsync(int id);
        Task<List<DocumentChunk>> GetDocumentChunksAsync(int documentId);
    }
}
