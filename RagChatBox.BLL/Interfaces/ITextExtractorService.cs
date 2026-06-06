using System.Threading.Tasks;

namespace RagChatBox.BLL.Interfaces
{
    public interface ITextExtractorService
    {
        Task<string> ExtractTextAsync(string physicalFilePath, string fileType);
    }
}
