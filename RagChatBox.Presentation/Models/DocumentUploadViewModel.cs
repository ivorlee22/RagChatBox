using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace RagChatBox.Presentation.Models
{
    public class DocumentUploadViewModel
    {
        [Required]
        public int CourseId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn tệp tài liệu để tải lên")]
        [Display(Name = "Tệp tài liệu")]
        public IFormFile File { get; set; } = null!;
    }
}
