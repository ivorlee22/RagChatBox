# RAG Chatbot System & Research Platform (Vietnamese Context)

Hệ thống cho phép học sinh/sinh viên thực hiện hỏi đáp (Q&A) dựa trên tài liệu học tập của từng khóa học cụ thể (RAG Chatbot), đồng thời cung cấp môi trường nghiên cứu (Research Platform) phục vụ so sánh hiệu quả giữa RAG và Fine-tuning trong ngữ cảnh ngôn ngữ tiếng Việt.

---

## 🛠️ Kiến trúc Hệ thống (3-Layer Architecture)

Dự án được xây dựng trên nền tảng **.NET 10.0** với kiến trúc 3 lớp rõ ràng:

1. **Presentation Layer (`RagChatBox.Presentation`)**:
   - Dự án Web ASP.NET Core MVC.
   - Điều hướng Controller, hiển thị Razor Views, và cấu hình các static assets (Bootstrap, jQuery).
   - Cơ chế xác thực Cookie Authentication bảo mật chống XSS/CSRF với cờ `HttpOnly`, `SecurePolicy` và `SameSite=Strict`.

2. **Business Logic Layer (`RagChatBox.BLL`)**:
   - Xử lý các tác vụ nghiệp vụ chính.
   - **Text Extraction**: Trích xuất nội dung chữ từ các tệp tin tài liệu định dạng PDF, Word (DOCX).
   - **Chunking**: Tách nhỏ văn bản thành các đoạn nhỏ (chunks) theo kích thước và độ chồng lặp cấu hình sẵn.
   - **Embedding & RAG**: Tạo vector nhúng bằng Gemini API, truy vấn tìm kiếm tương đồng ngữ nghĩa, và tổng hợp câu trả lời thông qua LLM.
   - **Email Service**: Dịch vụ gửi email tự động (HTML/Text) tích hợp Gmail SMTP qua MailKit & MimeKit, sử dụng **Options Pattern** kết hợp tự động kiểm tra tính hợp lệ khi khởi chạy ứng dụng (`ValidateOnStart`).

3. **Data Access Layer (`RagChatBox.DAL`)**:
   - Quản lý tương tác cơ sở dữ liệu thông qua Entity Framework Core với hệ quản trị cơ sở dữ liệu PostgreSQL.
   - Chứa các Entity Models, DbContext cấu hình các ràng buộc, chỉ mục (Index), và các bản Migration nâng cấp database.

---

## ✨ Các Tính Năng Đang Có

### 1. Quản lý Khóa học & Đăng ký Học viên
- Admin có quyền tạo lớp học mới, phân công giáo viên phụ trách, và thêm học viên vào lớp.
- Học sinh có thể tìm kiếm lớp học và gửi yêu cầu đăng ký tham gia lớp học.

### 2. Quản lý Tài liệu Học tập
- Hỗ trợ tải lên tài liệu định dạng `.pdf` và `.docx` với giới hạn dung lượng 50MB.
- **Bảo mật file**: Validate kiểu MIME, loại bỏ các phần mở rộng nguy hiểm và kiểm tra chữ ký nhị phân đầu file (Binary Magic Number Signature) chống giả mạo định dạng.
- **Phân quyền tải lên chặt chẽ**: Chỉ Admin hệ thống hoặc Giáo viên phụ trách trực tiếp khóa học đó mới được quyền tải lên tài liệu học tập.
- **Theo dõi lịch sử**: Hiển thị chi tiết thời gian tải lên và tên người dùng (Giáo viên/Admin) thực hiện thao tác tải tài liệu đó lên hệ thống.

### 3. Tự Động Hóa Email Giao Dịch (SMTP Service)
- Tích hợp dịch vụ gửi email tự động bằng MailKit/MimeKit qua cổng SMTP Gmail.
- Giao diện email HTML chào mừng được thiết kế responsive, hiển thị tối ưu trên thiết bị di động và máy tính.
- Gửi tài khoản đăng nhập tạm thời khi Admin tạo tài khoản thủ công cho học sinh/giáo viên.

### 4. Quản lý Người dùng (Admin Dashboard)
- Giao diện Admin quản trị danh sách người dùng chuyên sâu, lọc theo vai trò.
- Hỗ trợ **nhập danh sách tài khoản hàng loạt (Bulk Import)** từ tệp tin bảng tính Excel (`.xlsx`) hoặc CSV (`.csv`).

### 5. Chatbot Q&A (RAG Pipeline)
- Trích xuất nội dung văn bản và nhúng (embedding) tự động ngay khi tải lên tài liệu học tập.
- Tìm kiếm các phân đoạn có độ tương đồng cao nhất (top-k) dựa trên câu hỏi của người dùng.
- Trả lời thông qua mô hình ngôn ngữ lớn (LLM - Gemini), hiển thị nguồn trích dẫn chi tiết (tên tài liệu, phân đoạn) và điểm số tương đồng (similarity score) trực quan.

---

## 🚀 Hướng Dẫn Cài Đặt và Chạy Dự Án Cục Bộ

### 1. Yêu cầu phần mềm hệ thống
- Cài đặt [.NET 10.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) trở lên.
- Hệ quản trị cơ sở dữ liệu PostgreSQL phiên bản 16 trở lên.

### 2. Cấu hình cơ bản (Cấu hình appsettings.json)
Mở tệp tin [appsettings.json](file:///f:/Semester7/PRN222/Assignment1/RagChatBox/RagChatBox.Presentation/appsettings.json) trong thư mục dự án `RagChatBox.Presentation` và thay đổi các cấu hình sau:

| Cấu hình | Mô tả | Giá trị mặc định / Gợi ý |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | Chuỗi kết nối đến CSDL PostgreSQL cục bộ của bạn. | `"Host=localhost;Port=5432;Database=rag_chatbot;Username=postgres;Password=YOUR_DB_PASSWORD"` |
| `EmailSettings:SmtpUser` | Địa chỉ Gmail dùng để gửi mail chào mừng tự động. | `"YOUR_EMAIL@gmail.com"` |
| `EmailSettings:SmtpPass` | Mật khẩu ứng dụng (App Password) của tài khoản Gmail trên. | *Hướng dẫn: Bật Xác minh 2 bước trên Google -> Tạo Mật khẩu ứng dụng (App Password)* |
| `LlmSettings:ApiKey` | API Key kết nối dịch vụ Gemini LLM để trả lời câu hỏi. | *Lấy từ Google AI Studio* |
| `EmbeddingSettings:ApiKey` | API Key kết nối dịch vụ Gemini Embedding để nhúng tài liệu. | *Có thể dùng chung API Key với Gemini LLM* |

> [!IMPORTANT]
> **Khuyên dùng (User Secrets):** Để tránh vô tình commit các thông tin bảo mật (như API Keys hoặc Mật khẩu Gmail) lên Github, hãy sử dụng **Secret Manager** của .NET:
> ```bash
> dotnet user-secrets set "EmailSettings:SmtpPass" "your_smtp_pass" --project RagChatBox.Presentation
> dotnet user-secrets set "LlmSettings:ApiKey" "your_gemini_key" --project RagChatBox.Presentation
> dotnet user-secrets set "EmbeddingSettings:ApiKey" "your_gemini_key" --project RagChatBox.Presentation
> ```

### 3. Khởi tạo Cơ sở Dữ liệu (Database Migration)
Mở cửa sổ dòng lệnh (Terminal/Command Prompt) tại thư mục gốc của project và chạy lệnh cập nhật database:
```bash
dotnet ef database update --project RagChatBox.DAL --startup-project RagChatBox.Presentation
```

### 4. Chạy Ứng dụng
Tiến hành khởi chạy ứng dụng web bằng lệnh:
```bash
dotnet run --project RagChatBox.Presentation
```
Sau khi chạy thành công, truy cập trình duyệt theo địa chỉ local cổng mặc định được cung cấp (ví dụ: `http://localhost:5202` hoặc `https://localhost:7127`).

### 5. Danh sách tài khoản thử nghiệm mặc định (Seeded Users):
- **Admin**: Tài khoản `admin` / Mật khẩu `admin`
- **Giáo viên**: Tài khoản `teacher` / Mật khẩu `teacher`
- **Học sinh**: Tài khoản `student` / Mật khẩu `student`
