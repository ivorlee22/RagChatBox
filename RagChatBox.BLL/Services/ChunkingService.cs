using System;
using System.Collections.Generic;
using RagChatBox.BLL.Interfaces;

namespace RagChatBox.BLL.Services
{
    public class ChunkingService : IChunkingService
    {
        public List<(string Content, string Metadata)> ChunkText(string fullText, string fileName, int chunkSize = 500, int overlap = 50)
        {
            var chunks = new List<(string Content, string Metadata)>();

            if (string.IsNullOrWhiteSpace(fullText))
            {
                return chunks;
            }

            // Normalize whitespace
            fullText = fullText.Replace("\r\n", "\n").Replace("\r", "\n");

            int start = 0;
            int chunkIndex = 0;

            while (start < fullText.Length)
            {
                int end = Math.Min(start + chunkSize, fullText.Length);
                string raw = fullText.Substring(start, end - start);

                // Try to split at period, newline, or last space
                if (end < fullText.Length)
                {
                    int lastSentenceEnd = raw.LastIndexOf('.');
                    int lastNewline = raw.LastIndexOf('\n');
                    int breakPoint = Math.Max(lastSentenceEnd, lastNewline);

                    if (breakPoint > chunkSize / 3) // Only split if breakpoint is at least 1/3 of the chunk
                    {
                        raw = raw.Substring(0, breakPoint + 1);
                        end = start + breakPoint + 1;
                    }
                }

                string content = raw.Trim();
                if (!string.IsNullOrWhiteSpace(content) && content.Length >= 20) // Skip very short chunks
                {
                    string metadata = $"Chunk {chunkIndex + 1} - File: {fileName}";
                    chunks.Add((content, metadata));
                    chunkIndex++;
                }

                // Move start, minus overlap
                if (end >= fullText.Length)
                {
                    start = fullText.Length; // Finished processing, stop loop
                }
                else
                {
                    start = end - overlap;
                    if (start <= (end - chunkSize) || start < 0)
                    {
                        start = end; // Avoid infinite loops
                    }
                }
            }

            return chunks;
        }
    }
}
