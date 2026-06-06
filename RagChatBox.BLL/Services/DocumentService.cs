using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RagChatBox.BLL.Exceptions;
using RagChatBox.BLL.Interfaces;
using RagChatBox.DAL;
using RagChatBox.DAL.Entities;

namespace RagChatBox.BLL.Services
{
    public class DocumentService : IDocumentService
    {
        private readonly AppDbContext _context;
        private readonly ITextExtractorService _textExtractor;
        private readonly IChunkingService _chunkingService;
        private readonly IEmbeddingService _embeddingService;
        private readonly ILogger<DocumentService> _logger;

        public DocumentService(
            AppDbContext context,
            ITextExtractorService textExtractor,
            IChunkingService chunkingService,
            IEmbeddingService embeddingService,
            ILogger<DocumentService> logger)
        {
            _context = context;
            _textExtractor = textExtractor;
            _chunkingService = chunkingService;
            _embeddingService = embeddingService;
            _logger = logger;
        }

        public async Task<List<Document>> GetDocumentsByCourseAsync(int courseId)
        {
            return await _context.Documents
                .Where(d => d.CourseId == courseId)
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();
        }

        public async Task<Document?> GetDocumentByIdAsync(int id)
        {
            return await _context.Documents
                .Include(d => d.Course)
                .FirstOrDefaultAsync(d => d.Id == id);
        }

        public async Task CreateDocumentAsync(Document document)
        {
            // CRITICAL FIX: Perform early duplicate check BEFORE calling text extraction and generating embeddings
            var exists = await _context.Documents.AnyAsync(d => d.CourseId == document.CourseId && d.FileName == document.FileName);
            if (exists)
            {
                throw new DuplicateDocumentException($"Tài liệu '{document.FileName}' đã tồn tại trong khóa học này.");
            }

            // Step 1: Parse text from physical file
            string extractedText;
            try
            {
                extractedText = await _textExtractor.ExtractTextAsync(document.FilePath, document.FileType);
            }
            catch (Exception ex)
            {
                // If parsing fails, store as Failed, save the error, but preserve the record on database
                document.Status = "Failed";
                document.ErrorMessage = $"Lỗi trích xuất văn bản: {ex.Message}";
                document.ProcessedAt = DateTime.UtcNow;
                _context.Documents.Add(document);
                await _context.SaveChangesAsync();
                return;
            }

            // Step 2: Chunking the extracted text
            var chunkData = _chunkingService.ChunkText(extractedText, document.FileName);
            _logger.LogInformation(
                "Document indexing started. File={FileName}, ExtractedChars={ExtractedChars}, ChunkCount={ChunkCount}",
                document.FileName,
                extractedText.Length,
                chunkData.Count);

            if (!chunkData.Any())
            {
                document.Status = "Failed";
                document.ErrorMessage = "Không thể tách văn bản thành các đoạn (chunks). File có thể trống hoặc quá ngắn.";
                document.ProcessedAt = DateTime.UtcNow;
                _context.Documents.Add(document);
                await _context.SaveChangesAsync();
                return;
            }

            // Step 3: Create Document + DocumentChunks in a transaction
            document.Status = "Indexed";
            document.ProcessedAt = DateTime.UtcNow;
            _context.Documents.Add(document);

            var chunks = new List<DocumentChunk>();
            var embeddingCount = 0;
            for (var index = 0; index < chunkData.Count; index++)
            {
                var cd = chunkData[index];
                var embedding = await _embeddingService.GenerateDocumentEmbeddingAsync(cd.Content, document.FileName);
                if (embedding != null)
                {
                    embeddingCount++;
                }

                chunks.Add(new DocumentChunk
                {
                    Document = document,
                    ChunkIndex = index,
                    Content = cd.Content,
                    MetadataJson = cd.Metadata,
                    EmbeddingE5 = embedding == null ? null : new Pgvector.Vector(embedding),
                    EmbeddingOpenAI = null
                });
            }

            _context.DocumentChunks.AddRange(chunks);
            _logger.LogInformation(
                "Document embedding finished. File={FileName}, ChunkCount={ChunkCount}, EmbeddedChunks={EmbeddedChunks}",
                document.FileName,
                chunks.Count,
                embeddingCount);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                // Fallback catch for unique index violation
                throw new DuplicateDocumentException($"Tài liệu '{document.FileName}' đã tồn tại trong khóa học này.");
            }
        }

        public async Task<Document> UploadDocumentAsync(
            Stream fileStream,
            string fileName,
            long fileSize,
            string contentType,
            int courseId,
            string uploadsFolder,
            string? uploadedBy = null)
        {
            // 1. Validate File Empty
            if (fileSize == 0)
            {
                throw new DocumentValidationException("Tệp tải lên không hợp lệ hoặc rỗng.");
            }

            // 2. Validate Max File Size (50MB)
            const long maxFileSize = 50 * 1024 * 1024;
            if (fileSize > maxFileSize)
            {
                throw new DocumentValidationException("Tệp tải lên vượt quá giới hạn cho phép (tối đa 50MB).");
            }

            // 3. Validate File Extension
            var allowedExtensions = new[] { ".pdf", ".docx" };
            var extension = Path.GetExtension(fileName).ToLower();
            if (!allowedExtensions.Contains(extension))
            {
                throw new DocumentValidationException("Định dạng tệp không được hỗ trợ. Chỉ cho phép các định dạng: PDF, DOCX.");
            }

            // 4. Validate MIME Type
            var allowedMimeTypes = new[] {
                "application/pdf",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
            };
            if (!allowedMimeTypes.Contains(contentType.ToLower()))
            {
                throw new DocumentValidationException("Loại tệp không an toàn hoặc không được hỗ trợ.");
            }

            // 5. Binary Magic Number Signature Verification
            if (!ValidateFileSignature(fileStream, extension))
            {
                throw new DocumentValidationException("Nội dung tệp không khớp với phần mở rộng của nó. Phát hiện tệp không an toàn.");
            }

            // 6. DB Duplicate Check
            var isDuplicate = await _context.Documents.AnyAsync(d => d.CourseId == courseId && d.FileName.ToLower() == fileName.ToLower());
            if (isDuplicate)
            {
                throw new DuplicateDocumentException($"Tài liệu '{fileName}' đã tồn tại trong khóa học này.");
            }

            // 7. Save Physical File
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }
            var cleanFileName = Path.GetFileName(fileName);
            var uniqueFileName = $"{Guid.NewGuid()}_{cleanFileName}";
            var physicalPath = Path.Combine(uploadsFolder, uniqueFileName);

            try
            {
                using (var fileStreamDest = new FileStream(physicalPath, FileMode.Create))
                {
                    await fileStream.CopyToAsync(fileStreamDest);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi lưu file vật lý trên server: {Path}", physicalPath);
                throw new Exception("Lỗi lưu file vật lý trên server.");
            }

            // 8. Create Document entity and process indexing
            var document = new Document
            {
                CourseId = courseId,
                FileName = cleanFileName,
                FilePath = physicalPath,
                FileSize = fileSize,
                FileType = extension,
                Status = "Pending",
                UploadedAt = DateTime.UtcNow,
                UploadedBy = uploadedBy
            };

            try
            {
                await CreateDocumentAsync(document);
            }
            catch (Exception)
            {
                DeletePhysicalFile(physicalPath);
                throw;
            }

            return document;
        }

        public async Task DeleteDocumentAsync(int id)
        {
            var document = await _context.Documents.FindAsync(id);
            if (document != null)
            {
                _context.Documents.Remove(document);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<List<DocumentChunk>> GetDocumentChunksAsync(int documentId)
        {
            return await _context.DocumentChunks
                .Where(c => c.DocumentId == documentId)
                .OrderBy(c => c.ChunkIndex)
                .ToListAsync();
        }

        private static bool ValidateFileSignature(Stream stream, string extension)
        {
            try
            {
                var originalPosition = stream.Position;
                stream.Position = 0;

                using (var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true))
                {
                    if (stream.Length < 8) return false;
                    var bytes = reader.ReadBytes(8);

                    stream.Position = originalPosition;

                    if (extension == ".pdf")
                    {
                        return bytes.Length >= 4 && bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46;
                    }
                    if (extension == ".docx")
                    {
                        return bytes.Length >= 2 && bytes[0] == 0x50 && bytes[1] == 0x4B;
                    }
                }
            }
            catch
            {
                return false;
            }
            return true;
        }

        private static void DeletePhysicalFile(string? path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // ignored
            }
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        {
            return ex.InnerException?.Message?.Contains("23505") == true
                || ex.InnerException?.Message?.Contains("unique") == true
                || ex.InnerException?.Message?.Contains("UQ_Document_CourseId_FileName") == true;
        }
    }
}
