using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using RagChatBox.BLL.Interfaces;
using UglyToad.PdfPig;

namespace RagChatBox.BLL.Services
{
    public class TextExtractorService : ITextExtractorService
    {
        public Task<string> ExtractTextAsync(string physicalFilePath, string fileType)
        {
            if (string.IsNullOrWhiteSpace(physicalFilePath) || !System.IO.File.Exists(physicalFilePath))
            {
                throw new System.IO.FileNotFoundException($"Không tìm thấy file tại đường dẫn: {physicalFilePath}");
            }

            string text;
            var ext = fileType.ToLower().Trim();

            switch (ext)
            {
                case ".pdf":
                    text = ExtractFromPdf(physicalFilePath);
                    break;
                case ".docx":
                    text = ExtractFromDocx(physicalFilePath);
                    break;
                default:
                    throw new NotSupportedException($"Định dạng file '{ext}' không được hỗ trợ.");
            }

            return Task.FromResult(text);
        }

        private string ExtractFromPdf(string filePath)
        {
            var sb = new StringBuilder();

            using (var document = PdfDocument.Open(filePath))
            {
                foreach (var page in document.GetPages())
                {
                    var pageText = page.Text;
                    if (!string.IsNullOrWhiteSpace(pageText))
                    {
                        sb.AppendLine($"--- Trang {page.Number} ---");
                        sb.AppendLine(pageText.Trim());
                        sb.AppendLine();
                    }
                }
            }

            var result = sb.ToString().Trim();
            if (string.IsNullOrWhiteSpace(result))
            {
                throw new InvalidOperationException("File PDF không chứa văn bản có thể trích xuất (có thể là file scan/hình ảnh).");
            }

            return result;
        }

        private string ExtractFromDocx(string filePath)
        {
            var sb = new StringBuilder();

            using (var doc = WordprocessingDocument.Open(filePath, false))
            {
                var body = doc.MainDocumentPart?.Document?.Body;
                if (body == null)
                {
                    throw new InvalidOperationException("File DOCX không có nội dung văn bản.");
                }

                foreach (var paragraph in body.Elements<Paragraph>())
                {
                    var text = paragraph.InnerText;
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        sb.AppendLine(text.Trim());
                    }
                }
            }

            var result = sb.ToString().Trim();
            if (string.IsNullOrWhiteSpace(result))
            {
                throw new InvalidOperationException("File DOCX không chứa văn bản có thể trích xuất.");
            }

            return result;
        }
    }
}
