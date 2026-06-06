using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RagChatBox.BLL.Interfaces
{
    public interface IEmailService
    {
        Task SendHtmlEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);
        Task SendHtmlEmailAsync(IEnumerable<string> toEmails, string subject, string htmlBody, CancellationToken cancellationToken = default);
        Task SendTextEmailAsync(string toEmail, string subject, string textBody, CancellationToken cancellationToken = default);
        Task SendTextEmailAsync(IEnumerable<string> toEmails, string subject, string textBody, CancellationToken cancellationToken = default);
    }
}
