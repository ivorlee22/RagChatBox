using System.ComponentModel.DataAnnotations;

namespace RagChatBox.Presentation.Models
{
    public class CourseViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Tên khóa học không được để trống")]
        [StringLength(255, ErrorMessage = "Tên khóa học không vượt quá 255 ký tự")]
        [Display(Name = "Tên khóa học")]
        public string Name { get; set; } = null!;

        [Display(Name = "Mô tả")]
        public string? Description { get; set; }

        /// <summary>
        /// "teacher" (default for teacher role) or "personal" (student private course).
        /// Set by controller before POST, not shown to user directly.
        /// </summary>
        public string CourseType { get; set; } = "teacher";

        [Display(Name = "Hiển thị với học sinh")]
        public bool IsVisible { get; set; } = true;

        [Display(Name = "Mật khẩu lớp học (để trống nếu không cần mật khẩu)")]
        [StringLength(100, ErrorMessage = "Mật khẩu không vượt quá 100 ký tự")]
        public string? CoursePassword { get; set; }

        [Display(Name = "Giáo viên giảng dạy")]
        public int? CreatedBy { get; set; }
    }
}
