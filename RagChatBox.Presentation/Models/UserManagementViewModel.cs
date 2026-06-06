using System.Collections.Generic;
using RagChatBox.DAL.Entities;

namespace RagChatBox.Presentation.Models
{
    public class UserManagementViewModel
    {
        public List<User> Users { get; set; } = new();
        public string? FilterRole { get; set; }
    }

    public class UpdateRoleViewModel
    {
        public int UserId { get; set; }
        public string NewRole { get; set; } = string.Empty;
    }
}
