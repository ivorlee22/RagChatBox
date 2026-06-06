using System.Collections.Generic;
using System.Threading.Tasks;
using RagChatBox.DAL.Entities;

namespace RagChatBox.BLL.Interfaces
{
    public interface IUserService
    {
        Task<User?> AuthenticateAsync(string username, string password);

        // ── Admin operations ───────────────────────────────────────────────────
        Task<List<User>> GetAllUsersAsync();
        Task<List<User>> GetUsersByRoleAsync(string role);
        Task<bool> UpdateUserRoleAsync(int userId, string newRole);
        Task<User?> GetUserByIdAsync(int userId);
        Task<User?> CreateUserAsync(string username, string name, string password, string role, string? templateContent = null, string? loginUrl = null);
        Task<(int SuccessCount, List<string> Errors)> ImportUsersAsync(System.IO.Stream fileStream, string fileType, string defaultRole, string defaultPassword, string? templateContent = null, string? loginUrl = null);

        Task<bool> UpdateSubscriptionAsync(int userId, string tier);
        Task<int> GetRemainingChatsAsync(int userId);
        Task<bool> CanUserChatAsync(int userId);
        Task<bool> RecordTestChatAsync(int userId);
        Task<bool> ChangePasswordAsync(int userId, string oldPassword, string newPassword);
    }
}
