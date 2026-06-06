using System.ComponentModel.DataAnnotations;

namespace RagChatBox.BLL.Settings
{
    public class EmailSettings
    {
        [Required(ErrorMessage = "SMTP Host is required.")]
        public string SmtpHost { get; set; } = string.Empty;

        [Range(1, 65535, ErrorMessage = "SMTP Port must be between 1 and 65535.")]
        public int SmtpPort { get; set; } = 587;

        [Required(ErrorMessage = "SMTP User/Username is required.")]
        [EmailAddress(ErrorMessage = "SMTP User must be a valid email address.")]
        public string SmtpUser { get; set; } = string.Empty;

        [Required(ErrorMessage = "SMTP Password is required.")]
        public string SmtpPass { get; set; } = string.Empty;

        [Required(ErrorMessage = "Sender Name is required.")]
        public string SenderName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Sender Email is required.")]
        [EmailAddress(ErrorMessage = "Sender Email must be a valid email address.")]
        public string SenderEmail { get; set; } = string.Empty;
    }
}
