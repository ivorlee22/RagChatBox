using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RagChatBox.BLL.Exceptions;
using RagChatBox.BLL.Interfaces;
using RagChatBox.Presentation.Models;
using System.Security.Claims;
namespace RagChatBox.Presentation.Controllers
{
    [Authorize]
    public class DocumentController : Controller
    {
        private readonly IDocumentService _documentService;
        private readonly ICourseService _courseService;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<DocumentController> _logger;

        private int CurrentUserId =>
            int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        public DocumentController(
            IDocumentService documentService,
            ICourseService courseService,
            IWebHostEnvironment environment,
            ILogger<DocumentController> _logger)
        {
            _documentService = documentService;
            _courseService = courseService;
            _environment = environment;
            this._logger = _logger;
        }

        // GET: Document/Index?courseId=5
        public async Task<IActionResult> Index(int courseId)
        {
            var course = await _courseService.GetCourseByIdAsync(courseId);
            if (course == null)
            {
                return NotFound();
            }

            ViewBag.Course = course;
            var documents = await _documentService.GetDocumentsByCourseAsync(courseId);
            return View(documents);
        }

        // GET: Document/Upload?courseId=5
        [Authorize(Roles = "admin,teacher")]
        public async Task<IActionResult> Upload(int courseId)
        {
            var course = await _courseService.GetCourseByIdAsync(courseId);
            if (course == null)
            {
                return NotFound();
            }

            // Check permission: Admin can upload anywhere, teacher can only upload to their own course
            if (!User.IsInRole("admin") && course.CreatedBy != CurrentUserId)
            {
                return Forbid();
            }

            ViewBag.Course = course;
            var model = new DocumentUploadViewModel { CourseId = courseId };
            return View(model);
        }

        // POST: Document/Upload
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin,teacher")]
        public async Task<IActionResult> Upload(DocumentUploadViewModel model)
        {
            var course = await _courseService.GetCourseByIdAsync(model.CourseId);
            if (course == null)
            {
                return NotFound();
            }

            // Check permission: Admin can upload anywhere, teacher can only upload to their own course
            if (!User.IsInRole("admin") && course.CreatedBy != CurrentUserId)
            {
                return Forbid();
            }

            ViewBag.Course = course;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var file = model.File;
            if (file == null || file.Length == 0)
            {
                ModelState.AddModelError("File", "Tệp tải lên không hợp lệ hoặc rỗng.");
                TempData["ErrorMessage"] = "Tệp tải lên không hợp lệ hoặc rỗng.";
                return View(model);
            }

            try
            {
                var uploaderName = User.FindFirst("FullName")?.Value ?? User.Identity?.Name ?? "Unknown";
                var uploadsFolder = Path.Combine(_environment.ContentRootPath, "App_Data", "uploads");
                
                using (var stream = file.OpenReadStream())
                {
                    await _documentService.UploadDocumentAsync(
                        stream,
                        file.FileName,
                        file.Length,
                        file.ContentType,
                        model.CourseId,
                        uploadsFolder,
                        uploaderName
                    );
                }

                return RedirectToAction(nameof(Index), new { courseId = model.CourseId });
            }
            catch (DocumentValidationException ex)
            {
                ModelState.AddModelError("File", ex.Message);
                TempData["ErrorMessage"] = ex.Message;
            }
            catch (DuplicateDocumentException ex)
            {
                ModelState.AddModelError("File", ex.Message);
                TempData["ErrorMessage"] = ex.Message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Upload failed for file: {FileName}", file.FileName);
                ModelState.AddModelError("File", $"Đã xảy ra lỗi: {ex.Message}");
                TempData["ErrorMessage"] = $"Đã xảy ra lỗi: {ex.Message}";
            }

            return View(model);
        }

        // GET: Document/Download/5
        [HttpGet]
        public async Task<IActionResult> Download(int id)
        {
            var document = await _documentService.GetDocumentByIdAsync(id);
            if (document == null)
            {
                return NotFound();
            }

            var course = await _courseService.GetCourseByIdAsync(document.CourseId);
            if (course == null)
            {
                return NotFound();
            }

            // BOLA Authorization Check
            if (!User.IsInRole("admin"))
            {
                if (User.IsInRole("teacher"))
                {
                    if (course.CreatedBy != CurrentUserId)
                    {
                        return Forbid();
                    }
                }
                else if (User.IsInRole("student"))
                {
                    bool isOwner = course.CreatedBy == CurrentUserId;
                    var enrolledCourses = await _courseService.GetEnrolledCoursesAsync(CurrentUserId);
                    bool isEnrolled = enrolledCourses.Any(c => c.Id == course.Id);

                    if (!isOwner && !isEnrolled)
                    {
                        return Forbid();
                    }
                }
                else
                {
                    return Forbid();
                }
            }

            var physicalPath = document.FilePath;
            if (physicalPath.StartsWith("/"))
            {
                physicalPath = Path.Combine(_environment.WebRootPath, physicalPath.TrimStart('/'));
            }

            if (!System.IO.File.Exists(physicalPath))
            {
                return NotFound("File vật lý không tồn tại trên server.");
            }

            var contentType = document.FileType.ToLower() switch
            {
                ".pdf" => "application/pdf",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".doc" => "application/msword",
                ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
                ".ppt" => "application/vnd.ms-powerpoint",
                _ => "application/octet-stream"
            };

            return PhysicalFile(physicalPath, contentType, document.FileName);
        }

        // POST: Document/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "admin,teacher")]
        public async Task<IActionResult> Delete(int id, int courseId)
        {
            var document = await _documentService.GetDocumentByIdAsync(id);
            if (document == null)
            {
                return NotFound();
            }

            var course = await _courseService.GetCourseByIdAsync(courseId);
            if (course == null)
            {
                return NotFound();
            }

            // Check permission: Admin can delete from any course, teacher can only delete from their own course
            if (!User.IsInRole("admin") && course.CreatedBy != CurrentUserId)
            {
                return Forbid();
            }

            try
            {
                // CRITICAL FIX: Delete from DB first. Only delete the physical file if DB deletion succeeds.
                await _documentService.DeleteDocumentAsync(id);

                if (!string.IsNullOrEmpty(document.FilePath))
                {
                    var deletePath = document.FilePath;
                    if (deletePath.StartsWith("/"))
                    {
                        deletePath = Path.Combine(_environment.WebRootPath, deletePath.TrimStart('/'));
                    }
                    if (System.IO.File.Exists(deletePath))
                    {
                        System.IO.File.Delete(deletePath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa tài liệu {FileName}", document.FileName);
                TempData["ErrorMessage"] = "Lỗi khi xóa tài liệu khỏi hệ thống. Vui lòng thử lại.";
            }

            return RedirectToAction(nameof(Index), new { courseId = courseId });
        }

        // GET: Document/Chunks?documentId=5
        [HttpGet]
        [Authorize(Roles = "admin,teacher")]
        public async Task<IActionResult> Chunks(int documentId)
        {
            var document = await _documentService.GetDocumentByIdAsync(documentId);
            if (document == null)
            {
                return NotFound();
            }

            var course = await _courseService.GetCourseByIdAsync(document.CourseId);
            if (course == null)
            {
                return NotFound();
            }

            // BOLA Authorization Check
            if (!User.IsInRole("admin"))
            {
                if (User.IsInRole("teacher"))
                {
                    if (course.CreatedBy != CurrentUserId)
                    {
                        return Forbid();
                    }
                }
                else
                {
                    return Forbid();
                }
            }

            var chunks = await _documentService.GetDocumentChunksAsync(documentId);
            ViewBag.Document = document;
            ViewBag.Course = course;

            return View(chunks);
        }
    }
}
