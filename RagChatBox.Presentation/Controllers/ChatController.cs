using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RagChatBox.BLL.Interfaces;
using RagChatBox.DAL.Entities;

namespace RagChatBox.Presentation.Controllers
{
    [Authorize]
    public class ChatController : Controller
    {
        private readonly IChatService _chatService;
        private readonly ICourseService _courseService;

        public ChatController(IChatService chatService, ICourseService courseService)
        {
            _chatService = chatService;
            _courseService = courseService;
        }

        private int CurrentUserId =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private string CurrentUserRole =>
            User.FindFirstValue(ClaimTypes.Role) ?? "";

        // Verifies if the current user has access to this course
        private async Task<bool> UserHasCourseAccessAsync(int courseId)
        {
            if (User.IsInRole("admin")) return true;

            var course = await _courseService.GetCourseByIdAsync(courseId);
            if (course == null) return false;

            // Creator always has access
            if (course.CreatedBy == CurrentUserId) return true;

            // Student must be enrolled in teacher-created courses
            if (course.CourseType == "teacher")
            {
                var enrolled = await _courseService.GetEnrolledCoursesAsync(CurrentUserId);
                return enrolled.Any(c => c.Id == courseId);
            }

            return false;
        }

        // ── GET: Chat ────────────────────────────────────────────────────────────
        public async Task<IActionResult> Index(int courseId, int? sessionId)
        {
            if (!await UserHasCourseAccessAsync(courseId))
            {
                return Forbid();
            }

            var course = await _courseService.GetCourseByIdAsync(courseId);
            if (course == null) return NotFound();

            var sessions = await _chatService.GetSessionsByUserAndCourseAsync(CurrentUserId, courseId);
            
            ChatSession? activeSession = null;
            List<Message> messages = new List<Message>();

            if (sessionId.HasValue)
            {
                activeSession = sessions.FirstOrDefault(s => s.Id == sessionId.Value);
            }
            
            // Default to the most recent session if none selected
            if (activeSession == null && sessions.Any())
            {
                activeSession = sessions.First();
            }

            if (activeSession != null)
            {
                messages = await _chatService.GetMessagesBySessionAsync(activeSession.Id);
            }

            ViewBag.Course = course;
            ViewBag.Sessions = sessions;
            ViewBag.ActiveSession = activeSession;
            ViewBag.Messages = messages;

            return View();
        }

        // ── POST: Chat/CreateSession ─────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSession(int courseId, string? title)
        {
            if (!await UserHasCourseAccessAsync(courseId))
            {
                return Forbid();
            }

            var defaultTitle = string.IsNullOrWhiteSpace(title) 
                ? $"Cuộc hội thoại {DateTime.Now:dd/MM HH:mm}" 
                : title.Trim();

            var session = await _chatService.CreateSessionAsync(CurrentUserId, courseId, defaultTitle);
            return RedirectToAction(nameof(Index), new { courseId, sessionId = session.Id });
        }

        // ── POST: Chat/DeleteSession ─────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSession(int id, int courseId)
        {
            bool ok = await _chatService.DeleteSessionAsync(id, CurrentUserId);
            if (ok)
            {
                TempData["SuccessMessage"] = "Đã xóa cuộc hội thoại.";
            }
            else
            {
                TempData["ErrorMessage"] = "Không thể xóa cuộc hội thoại.";
            }
            return RedirectToAction(nameof(Index), new { courseId });
        }

        // ── POST: Chat/SendMessage (AJAX) ────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage(int sessionId, string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return Json(new { success = false, message = "Nội dung câu hỏi không được trống." });
            }

            var session = await _chatService.GetSessionByIdAsync(sessionId);
            if (session == null || session.UserId != CurrentUserId)
            {
                return Forbid();
            }

            try
            {
                var responseMsg = await _chatService.SendMessageAsync(sessionId, content.Trim());

                // Fetch logs with complete entity references
                var fullResponseMsg = (await _chatService.GetMessagesBySessionAsync(sessionId))
                    .FirstOrDefault(m => m.Id == responseMsg.Id);

                var citations = fullResponseMsg?.RetrievalLogs.Select(l => new
                {
                    docName = l.Chunk?.Document?.FileName ?? "Tài liệu",
                    similarity = l.SimilarityScore,
                    rank = l.Rank,
                    snippet = l.Chunk?.Content
                }).ToList() ?? new();

                return Json(new
                {
                    success = true,
                    role = "assistant",
                    content = responseMsg.Content,
                    citations = citations,
                    tokens = responseMsg.TokensUsed,
                    createdAt = responseMsg.CreatedAt.ToLocalTime().ToString("HH:mm")
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi xử lý câu hỏi: {ex.Message}" });
            }
        }
    }
}
