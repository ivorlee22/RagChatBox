using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace RagChatBox.Presentation.Models
{
    public class ImportUsersViewModel
    {
        [Required(ErrorMessage = "Vui lòng chọn tệp Excel hoặc CSV.")]
        [Display(Name = "Tệp dữ liệu (.xlsx, .csv)")]
        public IFormFile File { get; set; } = null!;

        [Required(ErrorMessage = "Vui lòng chọn vai trò mặc định.")]
        [Display(Name = "Vai trò mặc định")]
        public string DefaultRole { get; set; } = "student";

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mặc định.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu mặc định phải từ 6 đến 100 ký tự.")]
        [Display(Name = "Mật khẩu mặc định")]
        public string DefaultPassword { get; set; } = "School123@";
    }
}
