using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using RagChatBox.BLL.Interfaces;
using RagChatBox.BLL.Settings;

namespace RagChatBox.BLL.Services
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _settings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IOptions<EmailSettings> settings, ILogger<EmailService> logger)
        {
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task SendHtmlEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
        {
            return SendEmailAsync(new[] { toEmail }, subject, htmlBody, isHtml: true, cancellationToken);
        }

        public Task SendHtmlEmailAsync(IEnumerable<string> toEmails, string subject, string htmlBody, CancellationToken cancellationToken = default)
        {
            return SendEmailAsync(toEmails, subject, htmlBody, isHtml: true, cancellationToken);
        }

        public Task SendTextEmailAsync(string toEmail, string subject, string textBody, CancellationToken cancellationToken = default)
        {
            return SendEmailAsync(new[] { toEmail }, subject, textBody, isHtml: false, cancellationToken);
        }

        public Task SendTextEmailAsync(IEnumerable<string> toEmails, string subject, string textBody, CancellationToken cancellationToken = default)
        {
            return SendEmailAsync(toEmails, subject, textBody, isHtml: false, cancellationToken);
        }

        private async Task SendEmailAsync(IEnumerable<string> toEmails, string subject, string bodyContent, bool isHtml, CancellationToken cancellationToken)
        {
            var recipientList = toEmails?.Where(email => !string.IsNullOrWhiteSpace(email)).ToList();
            if (recipientList == null || recipientList.Count == 0)
            {
                _logger.LogWarning("Email send requested, but no valid recipients were provided.");
                return;
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
            
            foreach (var email in recipientList)
            {
                message.To.Add(MailboxAddress.Parse(email.Trim()));
            }

            message.Subject = subject;

            var bodyBuilder = new BodyBuilder();
            if (isHtml)
            {
                bodyBuilder.HtmlBody = bodyContent;
            }
            else
            {
                bodyBuilder.TextBody = bodyContent;
            }

            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            try
            {
                _logger.LogInformation("Connecting to SMTP server at {Host}:{Port}...", _settings.SmtpHost, _settings.SmtpPort);
                
                SecureSocketOptions socketOptions = SecureSocketOptions.Auto;
                if (_settings.SmtpPort == 587)
                {
                    socketOptions = SecureSocketOptions.StartTls;
                }
                else if (_settings.SmtpPort == 465)
                {
                    socketOptions = SecureSocketOptions.SslOnConnect;
                }
                else if (_settings.SmtpPort == 25 || _settings.SmtpPort == 2525)
                {
                    socketOptions = SecureSocketOptions.None;
                }

                await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, socketOptions, cancellationToken);
                _logger.LogInformation("Connected successfully. Authenticating user {User}...", _settings.SmtpUser);

                if (!string.IsNullOrEmpty(_settings.SmtpUser) && !string.IsNullOrEmpty(_settings.SmtpPass))
                {
                    await client.AuthenticateAsync(_settings.SmtpUser, _settings.SmtpPass, cancellationToken);
                    _logger.LogInformation("Authentication successful.");
                }

                _logger.LogInformation("Sending email with subject: '{Subject}' to {Count} recipient(s)...", subject, recipientList.Count);
                await client.SendAsync(message, cancellationToken);
                _logger.LogInformation("Email sent successfully to recipients: {Recipients}", string.Join(", ", recipientList));
            }
            catch (MailKit.Net.Smtp.SmtpCommandException ex)
            {
                _logger.LogError(ex, "SMTP Command error occurred during email sending. StatusCode: {StatusCode}", ex.StatusCode);
                throw;
            }
            catch (MailKit.Net.Smtp.SmtpProtocolException ex)
            {
                _logger.LogError(ex, "SMTP Protocol error occurred during email sending.");
                throw;
            }
            catch (MailKit.Security.AuthenticationException ex)
            {
                _logger.LogError(ex, "Authentication failed for SMTP user: {User}.", _settings.SmtpUser);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while sending email via SMTP.");
                throw;
            }
            finally
            {
                if (client.IsConnected)
                {
                    await client.DisconnectAsync(true, cancellationToken);
                }
            }
        }
    }
}
