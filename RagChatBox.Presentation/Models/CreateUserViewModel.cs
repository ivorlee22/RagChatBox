using System.ComponentModel.DataAnnotations;

namespace RagChatBox.Presentation.Models
{
    public class CreateUserViewModel
    {
        [Required(ErrorMessage = "Tên đăng nhập không được để trống.")]
        [EmailAddress(ErrorMessage = "Tên đăng nhập phải là một địa chỉ Email hợp lệ.")]
        [Display(Name = "Tên đăng nhập / Email")]
        public string Username { get; set; } = null!;

        [Required(ErrorMessage = "Họ tên không được để trống.")]
        [StringLength(100, ErrorMessage = "Họ tên không vượt quá 100 ký tự.")]
        [Display(Name = "Họ tên đầy đủ")]
        public string Name { get; set; } = null!;

        [Required(ErrorMessage = "Mật khẩu không được để trống.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu từ 6 đến 100 ký tự.")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu")]
        public string Password { get; set; } = null!;

        [Required(ErrorMessage = "Vai trò không được để trống.")]
        [Display(Name = "Vai trò")]
        public string Role { get; set; } = "student";
    }
}
