using System.ComponentModel.DataAnnotations;

namespace RagChatBox.Presentation.Models
{
    public class InviteStudentViewModel
    {
        public int CourseId { get; set; }
        public string CourseName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập username của học sinh")]
        [Display(Name = "Username học sinh")]
        public string StudentUsername { get; set; } = string.Empty;
    }
}
