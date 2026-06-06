using System.Collections.Generic;

namespace RagChatBox.BLL.Interfaces
{
    public interface IChunkingService
    {
        List<(string Content, string Metadata)> ChunkText(string fullText, string fileName, int chunkSize = 500, int overlap = 50);
    }
}
