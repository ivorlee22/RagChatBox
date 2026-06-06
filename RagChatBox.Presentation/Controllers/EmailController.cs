using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using RagChatBox.BLL.Interfaces;
using System;
using System.IO;
using System.Threading.Tasks;

namespace RagChatBox.Presentation.Controllers
{
    public class EmailController : Controller
    {
        private readonly IEmailService _emailService;
        private readonly IWebHostEnvironment _env;

        public EmailController(IEmailService emailService, IWebHostEnvironment env)
        {
            _emailService = emailService;
            _env = env;
        }

        [HttpPost]
        [Route("api/email/test")]
        public async Task<IActionResult> SendTestEmail([FromBody] TestEmailRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ToEmail))
            {
                return BadRequest(new { success = false, message = "Email người nhận không được để trống." });
            }

            try
            {
                var templatePath = Path.Combine(_env.ContentRootPath, "Templates", "WelcomeEmail.html");
                if (!System.IO.File.Exists(templatePath))
                {
                    return NotFound(new { success = false, message = $"Không tìm thấy file template tại đường dẫn: {templatePath}" });
                }

                var htmlTemplate = await System.IO.File.ReadAllTextAsync(templatePath);

                var name = request.Name ?? "Học Viên Mẫu";
                var username = request.Username ?? request.ToEmail;
                var tempPassword = request.TemporaryPassword ?? "TempPass@123";
                var loginUrl = $"{Request.Scheme}://{Request.Host}/Account/Login";

                var htmlBody = htmlTemplate
                    .Replace("{{Name}}", name)
                    .Replace("{{Username}}", username)
                    .Replace("{{TemporaryPassword}}", tempPassword)
                    .Replace("{{LoginUrl}}", loginUrl);

                await _emailService.SendHtmlEmailAsync(request.ToEmail, "Chào mừng bạn đến với RAG ChatBox System", htmlBody);

                return Ok(new { success = true, message = $"Email test đã được gửi thành công đến {request.ToEmail}." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Gửi email thất bại: {ex.Message}" });
            }
        }
    }

    public class TestEmailRequest
    {
        public string ToEmail { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? Username { get; set; }
        public string? TemporaryPassword { get; set; }
    }
}
