using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
using System;
using System.Threading.Tasks;

namespace Gym.Services
{
    public class GmailValidationService
    {
        private readonly IConfiguration _config;

        public GmailValidationService(IConfiguration config)
        {
            _config = config;
        }

        // Sends the "click to confirm" registration email with a button link
        public async Task<(bool Success, string? ErrorMessage)> SendConfirmationEmailAsync(string recipientEmail, string confirmationUrl)
        {
            try
            {
                var email = new MimeMessage();

                email.From.Add(new MailboxAddress(
                    _config["SmtpSettings:SenderName"],
                    _config["SmtpSettings:SenderEmail"]));

                email.To.Add(MailboxAddress.Parse(recipientEmail));

                email.Subject = "Confirm your Gymlytics staff account";

                email.Body = new TextPart("html")
                {
                    Text = $@"
                    <div style='font-family: Arial, sans-serif; max-width: 480px; margin: auto;'>
                        <h2>Confirm your email</h2>
                        <p>Click the button below to confirm this is your Gmail account and complete your staff registration.</p>
                        <p style='text-align: center; margin: 24px 0;'>
                            <a href='{confirmationUrl}'
                               style='background:#2a78d6;color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none;font-weight:bold;display:inline-block;'>
                               Confirm my account
                            </a>
                        </p>
                        <p style='font-size:12px;color:#888;'>This link expires in 30 minutes. If you didn't request this, you can ignore this email.</p>
                    </div>"
                };

                using var smtp = new SmtpClient();

                await smtp.ConnectAsync(
                    _config["SmtpSettings:Server"],
                    int.Parse(_config["SmtpSettings:Port"]!),
                    SecureSocketOptions.StartTls);

                await smtp.AuthenticateAsync(
                    _config["SmtpSettings:SenderEmail"],
                    _config["SmtpSettings:Password"]);

                await smtp.SendAsync(email);
                await smtp.DisconnectAsync(true);

                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, $"Failed to send confirmation email: {ex.Message}");
            }
        }
    }
}