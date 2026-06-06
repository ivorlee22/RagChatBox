using System.ComponentModel.DataAnnotations;

namespace RagChatBox.Presentation.Models
{
    public class EnrollViewModel
    {
        public int CourseId { get; set; }
        public string CourseName { get; set; } = string.Empty;
        public string? TeacherName { get; set; }
        public bool RequiresPassword { get; set; }

        [Display(Name = "Mật khẩu lớp học")]
        public string? Password { get; set; }
    }
}
