using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RagChatBox.BLL.Interfaces;
using RagChatBox.DAL;
using RagChatBox.DAL.Entities;
using MiniExcelLibs;
using System.IO;

namespace RagChatBox.BLL.Services
{
    public class UserService : IUserService
    {
        private readonly AppDbContext _dbContext;
        private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;
        private readonly IEmailService _emailService;

        public UserService(
            AppDbContext dbContext, 
            Microsoft.Extensions.Configuration.IConfiguration configuration,
            IEmailService emailService)
        {
            _dbContext = dbContext;
            _configuration = configuration;
            _emailService = emailService;
        }

        public async Task<User?> AuthenticateAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return null;

            var user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());

            if (user == null) return null;

            bool isValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
            return isValid ? user : null;
        }

        // RegisterAsync has been removed as self-registration is disabled.

        // ── Admin operations ────────────────────────────────────────────────────

        public async Task<List<User>> GetAllUsersAsync()
        {
            return await _dbContext.Users
                .OrderBy(u => u.Role)
                .ThenBy(u => u.Name)
                .ToListAsync();
        }

        public async Task<List<User>> GetUsersByRoleAsync(string role)
        {
            return await _dbContext.Users
                .Where(u => u.Role == role)
                .OrderBy(u => u.Name)
                .ToListAsync();
        }

        public async Task<bool> UpdateUserRoleAsync(int userId, string newRole)
        {
            var allowed = new[] { "student", "teacher", "admin" };
            if (!Array.Exists(allowed, r => r == newRole)) return false;

            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return false;

            user.Role = newRole;
            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<User?> GetUserByIdAsync(int userId)
        {
            return await _dbContext.Users.FindAsync(userId);
        }

        public async Task<User?> CreateUserAsync(
            string username, 
            string name, 
            string password, 
            string role, 
            string? templateContent = null, 
            string? loginUrl = null)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(name))
                return null;

            bool exists = await _dbContext.Users.AnyAsync(u => u.Username.ToLower() == username.ToLower());
            if (exists) return null;

            string passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
            var user = new User
            {
                Username = username.Trim(),
                Name = name.Trim(),
                PasswordHash = passwordHash,
                Role = role.ToLower().Trim(),
                CreatedAt = DateTime.UtcNow,
                SubscriptionTier = "Free"
            };

            _dbContext.Users.Add(user);
            await _dbContext.SaveChangesAsync();

            if (!string.IsNullOrEmpty(templateContent))
            {
                try
                {
                    var htmlBody = templateContent
                        .Replace("{{Name}}", name.Trim())
                        .Replace("{{Username}}", username.Trim())
                        .Replace("{{TemporaryPassword}}", password)
                        .Replace("{{LoginUrl}}", loginUrl ?? "");

                    await _emailService.SendHtmlEmailAsync(username.Trim(), "Chào mừng bạn đến với RAG ChatBox System", htmlBody);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lỗi gửi email chào mừng: {ex.Message}");
                }
            }

            return user;
        }

        public async Task<(int SuccessCount, List<string> Errors)> ImportUsersAsync(
            System.IO.Stream fileStream, 
            string fileType, 
            string defaultRole, 
            string defaultPassword,
            string? templateContent = null,
            string? loginUrl = null)
        {
            var errors = new List<string>();
            int successCount = 0;
            var listToProcess = new List<(string Username, string Name, string Password, string Role)>();

            if (fileType.Equals(".csv", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var reader = new System.IO.StreamReader(fileStream, System.Text.Encoding.UTF8);
                    var headerLine = await reader.ReadLineAsync();
                    if (headerLine == null)
                    {
                        errors.Add("Tệp CSV rỗng.");
                        return (0, errors);
                    }

                    var headers = ParseCsvLine(headerLine);
                    int usernameIdx = FindHeaderIndex(headers, new[] { "username", "email", "tài khoản", "email/tài khoản" });
                    int nameIdx = FindHeaderIndex(headers, new[] { "name", "full name", "họ tên", "tên", "họ và tên" });
                    int passwordIdx = FindHeaderIndex(headers, new[] { "password", "mật khẩu" });
                    int roleIdx = FindHeaderIndex(headers, new[] { "role", "vai trò", "phân quyền" });

                    int rowNum = 1;
                    while (!reader.EndOfStream)
                    {
                        rowNum++;
                        var line = await reader.ReadLineAsync();
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        var fields = ParseCsvLine(line);
                        if (fields.Count == 0) continue;

                        string? username = usernameIdx >= 0 && usernameIdx < fields.Count ? fields[usernameIdx]?.Trim() : null;
                        string? name = nameIdx >= 0 && nameIdx < fields.Count ? fields[nameIdx]?.Trim() : null;
                        string? password = passwordIdx >= 0 && passwordIdx < fields.Count ? fields[passwordIdx]?.Trim() : null;
                        string? role = roleIdx >= 0 && roleIdx < fields.Count ? fields[roleIdx]?.Trim() : null;

                        if (string.IsNullOrEmpty(username))
                        {
                            errors.Add($"Dòng {rowNum}: Tài khoản/Email không được trống.");
                            continue;
                        }

                        if (string.IsNullOrEmpty(name))
                        {
                            errors.Add($"Dòng {rowNum} ({username}): Họ tên không được trống.");
                            continue;
                        }

                        string finalPassword = string.IsNullOrEmpty(password) ? defaultPassword : password;
                        if (string.IsNullOrEmpty(finalPassword))
                        {
                            errors.Add($"Dòng {rowNum} ({username}): Mật khẩu không được trống.");
                            continue;
                        }

                        string finalRole = string.IsNullOrEmpty(role) ? defaultRole : role.ToLower();
                        if (finalRole != "student" && finalRole != "teacher" && finalRole != "admin")
                        {
                            finalRole = defaultRole;
                        }

                        listToProcess.Add((username, name, finalPassword, finalRole));
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Lỗi đọc file CSV: {ex.Message}");
                    return (0, errors);
                }
            }
            else if (fileType.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var rows = MiniExcelLibs.MiniExcel.Query(fileStream, useHeaderRow: true);
                    int rowNum = 1;
                    foreach (IDictionary<string, object> row in rows)
                    {
                        rowNum++;
                        string? username = null;
                        string? name = null;
                        string? password = null;
                        string? role = null;

                        foreach (var kvp in row)
                        {
                            var key = kvp.Key.Trim().ToLower();
                            var val = kvp.Value?.ToString()?.Trim();
                            if (string.IsNullOrEmpty(val)) continue;

                            if (key.Contains("username") || key.Contains("email") || key.Contains("tài khoản"))
                                username = val;
                            else if (key.Contains("name") || key.Contains("họ tên") || key.Contains("tên") || key.Contains("họ và tên"))
                                name = val;
                            else if (key.Contains("password") || key.Contains("mật khẩu"))
                                password = val;
                            else if (key.Contains("role") || key.Contains("vai trò") || key.Contains("phân quyền"))
                                role = val;
                        }

                        if (string.IsNullOrEmpty(username))
                        {
                            errors.Add($"Dòng {rowNum}: Tài khoản/Email không được trống.");
                            continue;
                        }

                        if (string.IsNullOrEmpty(name))
                        {
                            errors.Add($"Dòng {rowNum} ({username}): Họ tên không được trống.");
                            continue;
                        }

                        string finalPassword = string.IsNullOrEmpty(password) ? defaultPassword : password;
                        if (string.IsNullOrEmpty(finalPassword))
                        {
                            errors.Add($"Dòng {rowNum} ({username}): Mật khẩu không được trống.");
                            continue;
                        }

                        string finalRole = string.IsNullOrEmpty(role) ? defaultRole : role.ToLower();
                        if (finalRole != "student" && finalRole != "teacher" && finalRole != "admin")
                        {
                            finalRole = defaultRole;
                        }

                        listToProcess.Add((username, name, finalPassword, finalRole));
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Lỗi đọc file Excel: {ex.Message}");
                    return (0, errors);
                }
            }
            else
            {
                errors.Add("Định dạng tệp không được hỗ trợ. Vui lòng chọn tệp .xlsx hoặc .csv");
                return (0, errors);
            }

            // Database Save
            foreach (var item in listToProcess)
            {
                bool exists = await _dbContext.Users.AnyAsync(u => u.Username.ToLower() == item.Username.ToLower());
                if (exists)
                {
                    errors.Add($"Tài khoản \"{item.Username}\" đã tồn tại, bỏ qua dòng này.");
                    continue;
                }

                try
                {
                    string passwordHash = BCrypt.Net.BCrypt.HashPassword(item.Password);
                    var user = new User
                    {
                        Username = item.Username,
                        Name = item.Name,
                        PasswordHash = passwordHash,
                        Role = item.Role,
                        CreatedAt = DateTime.UtcNow,
                        SubscriptionTier = "Free"
                    };

                    _dbContext.Users.Add(user);
                    await _dbContext.SaveChangesAsync();
                    successCount++;

                    if (!string.IsNullOrEmpty(templateContent))
                    {
                        try
                        {
                            var htmlBody = templateContent
                                .Replace("{{Name}}", item.Name.Trim())
                                .Replace("{{Username}}", item.Username.Trim())
                                .Replace("{{TemporaryPassword}}", item.Password)
                                .Replace("{{LoginUrl}}", loginUrl ?? "");

                            await _emailService.SendHtmlEmailAsync(item.Username.Trim(), "Chào mừng bạn đến với RAG ChatBox System", htmlBody);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Lỗi gửi email chào mừng cho {item.Username}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Tài khoản \"{item.Username}\": Lỗi khi lưu vào CSDL - {ex.Message}");
                }
            }

            return (successCount, errors);
        }

        private static List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            var inQuotes = false;
            var currentField = new System.Text.StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(currentField.ToString());
                    currentField.Clear();
                }
                else
                {
                    currentField.Append(c);
                }
            }
            result.Add(currentField.ToString());
            return result;
        }

        private static int FindHeaderIndex(List<string> headers, string[] matchWords)
        {
            for (int i = 0; i < headers.Count; i++)
            {
                var h = headers[i].Trim().ToLower();
                if (matchWords.Any(w => h.Contains(w)))
                {
                    return i;
                }
            }
            return -1;
        }

        public async Task<bool> UpdateSubscriptionAsync(int userId, string tier)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return false;
            user.SubscriptionTier = tier;
            await _dbContext.SaveChangesAsync();
            return true;
        }

        public Task<int> GetRemainingChatsAsync(int userId)
        {
            return Task.FromResult(int.MaxValue);
        }

        public Task<bool> CanUserChatAsync(int userId)
        {
            return Task.FromResult(true);
        }

        public async Task<bool> RecordTestChatAsync(int userId)
        {
            return true;
        }

        public async Task<bool> ChangePasswordAsync(int userId, string oldPassword, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(oldPassword) || string.IsNullOrWhiteSpace(newPassword))
                return false;

            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return false;

            bool isValid = BCrypt.Net.BCrypt.Verify(oldPassword, user.PasswordHash);
            if (!isValid) return false;

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            await _dbContext.SaveChangesAsync();
            return true;
        }
    }
}
