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
            using var timeout = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(30));
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
                    SecureSocketOptions.StartTls, timeout.Token);

                await smtp.AuthenticateAsync(
                    _config["SmtpSettings:SenderEmail"],
                    _config["SmtpSettings:Password"], timeout.Token);

                await smtp.SendAsync(email, timeout.Token);
                // Once the server accepts the invitation, a disconnect failure
                // must not turn success into an error and delete the pending staff.
                try { await smtp.DisconnectAsync(true, timeout.Token); }
                catch (Exception) { /* Delivery succeeded; disposal closes the connection. */ }

                return (true, null);
            }
            catch (OperationCanceledException)
            {
                return (false, "The invitation email could not be confirmed within 30 seconds. Staff registration was not completed. Please try again.");
            }
            catch (Exception ex)
            {
                return (false, $"Failed to send confirmation email: {ex.Message}");
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> SendTrainerMessageAsync(
            string recipientEmail, string trainerName, string subject, string message)
        {
            try
            {
                var email = new MimeMessage();
                email.From.Add(new MailboxAddress(_config["SmtpSettings:SenderName"], _config["SmtpSettings:SenderEmail"]));
                email.To.Add(MailboxAddress.Parse(recipientEmail));
                email.Subject = subject;
                email.Body = new TextPart("html")
                {
                    Text = $"<div style='font-family:Arial,sans-serif;max-width:560px;margin:auto'><h2 style='color:#064e19'>YOKOKS Gym</h2><p>Hello {System.Net.WebUtility.HtmlEncode(trainerName)},</p><p style='white-space:pre-line'>{System.Net.WebUtility.HtmlEncode(message)}</p><p style='color:#64748b;font-size:12px'>This message was sent by the gym administrator.</p></div>"
                };
                using var smtp = new SmtpClient();
                await smtp.ConnectAsync(_config["SmtpSettings:Server"], int.Parse(_config["SmtpSettings:Port"]!), SecureSocketOptions.StartTls);
                await smtp.AuthenticateAsync(_config["SmtpSettings:SenderEmail"], _config["SmtpSettings:Password"]);
                await smtp.SendAsync(email);
                await smtp.DisconnectAsync(true);
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, $"Failed to send trainer email: {ex.Message}");
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> SendWalkInReceiptAsync(
            string recipientEmail,
            string clientName,
            decimal amountPaid,
            DateTime transactionDate,
            string paymentMethod)
        {
            using var timeout = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                var safeName = System.Net.WebUtility.HtmlEncode(clientName);
                var safeMethod = System.Net.WebUtility.HtmlEncode(paymentMethod);
                var email = new MimeMessage();
                email.From.Add(new MailboxAddress(
                    _config["SmtpSettings:SenderName"],
                    _config["SmtpSettings:SenderEmail"]));
                email.To.Add(MailboxAddress.Parse(recipientEmail));
                email.Subject = "Gymlytics walk-in transaction receipt";
                email.Body = new TextPart("html")
                {
                    Text = $@"
                    <div style='font-family:Arial,sans-serif;max-width:520px;margin:auto;border:1px solid #dbe5df;border-radius:8px;overflow:hidden'>
                      <div style='background:#083d02;color:white;padding:18px 22px'><h2 style='margin:0'>Transaction Receipt</h2></div>
                      <div style='padding:22px'>
                        <p>Thank you, <strong>{safeName}</strong>.</p>
                        <table style='width:100%;border-collapse:collapse'>
                          <tr><td style='padding:9px 0;color:#66736b'>Amount paid</td><td style='text-align:right;font-weight:bold'>₱{amountPaid:N2}</td></tr>
                          <tr><td style='padding:9px 0;color:#66736b'>Payment method</td><td style='text-align:right'>{safeMethod}</td></tr>
                          <tr><td style='padding:9px 0;color:#66736b'>Transaction date</td><td style='text-align:right'>{transactionDate:MMM d, yyyy h:mm tt}</td></tr>
                        </table>
                        <p style='font-size:12px;color:#7a857e;margin-top:24px'>This email is your official walk-in payment receipt.</p>
                      </div>
                    </div>"
                };

                using var smtp = new SmtpClient();
                await smtp.ConnectAsync(
                    _config["SmtpSettings:Server"],
                    int.Parse(_config["SmtpSettings:Port"]!),
                    SecureSocketOptions.StartTls, timeout.Token);
                await smtp.AuthenticateAsync(
                    _config["SmtpSettings:SenderEmail"],
                    _config["SmtpSettings:Password"], timeout.Token);
                await smtp.SendAsync(email, timeout.Token);
                // A disconnect failure cannot undo an accepted receipt.
                try { await smtp.DisconnectAsync(true, timeout.Token); }
                catch (Exception) { /* Disposal closes the connection. */ }
                return (true, null);
            }
            catch (OperationCanceledException)
            {
                return (false, "Registration and payment were saved, but receipt delivery could not be confirmed within 30 seconds. Do not register again.");
            }
            catch (Exception ex)
            {
                return (false, $"Registration was saved, but the digital receipt could not be sent: {ex.Message}");
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> SendMemberFollowUpAsync(
            string recipientEmail,
            string memberName,
            DateTime followUpDate,
            string followUpPurpose)
        {
            try
            {
                var safeName = System.Net.WebUtility.HtmlEncode(memberName);
                var safePurpose = System.Net.WebUtility.HtmlEncode(followUpPurpose);
                var purposeKey = followUpPurpose.Trim().ToLowerInvariant();
                var letter = purposeKey switch
                {
                    "welcome & orientation" => @"
                        <p>Welcome to YOKOKS Gym! We would like to help you become familiar with our facilities, gym guidelines, and available services.</p>
                        <p>During your orientation, our team can answer your questions and help you prepare for a comfortable first visit.</p>",
                    "fitness assessment" => @"
                        <p>We would like to arrange your fitness assessment so we can review your current fitness level and better support your gym goals.</p>
                        <p>Please come in comfortable workout clothing and let us know if you have any health concerns our team should consider.</p>",
                    "payment follow-up" => @"
                        <p>This is a friendly follow-up regarding your gym membership payment.</p>
                        <p>Please contact the front desk if you have already completed payment, need clarification about your balance, or require assistance with your membership.</p>",
                    _ => $"<p>We are contacting you regarding <strong>{safePurpose}</strong>.</p><p>Please reply if you need further information or would like assistance from our front desk team.</p>"
                };

                var senderName = _config["SmtpSettings:SenderName"];
                var senderEmail = _config["SmtpSettings:SenderEmail"];
                var email = new MimeMessage();
                email.From.Add(new MailboxAddress(senderName, senderEmail));
                email.ReplyTo.Add(new MailboxAddress(senderName, senderEmail));
                email.To.Add(MailboxAddress.Parse(recipientEmail));
                email.Subject = $"YOKOKS Gym follow-up: {followUpPurpose}";
                email.Body = new TextPart("html")
                {
                    Text = $@"
                    <div style='font-family:Arial,sans-serif;max-width:560px;margin:auto;border:1px solid #dbe5df;border-radius:8px;overflow:hidden;color:#183028'>
                      <div style='background:#083d02;color:#fff;padding:20px 24px'>
                        <h2 style='margin:0'>Member Follow-up</h2>
                      </div>
                      <div style='padding:24px'>
                        <p>Dear <strong>{safeName}</strong>,</p>
                        {letter}
                        <div style='margin:22px 0;padding:14px 16px;background:#f0f7f2;border-left:4px solid #08752a'>
                          <div style='font-size:12px;color:#617069;text-transform:uppercase'>Follow-up purpose</div>
                          <strong>{safePurpose}</strong>
                          <div style='margin-top:10px;font-size:12px;color:#617069;text-transform:uppercase'>Scheduled date</div>
                          <strong>{followUpDate:MMMM d, yyyy}</strong>
                        </div>
                        <p>You may reply directly to this email to contact the gym.</p>
                        <p>Sincerely,<br/><strong>YOKOKS Gym Front Desk</strong></p>
                      </div>
                    </div>"
                };

                using var smtp = new SmtpClient();
                await smtp.ConnectAsync(
                    _config["SmtpSettings:Server"],
                    int.Parse(_config["SmtpSettings:Port"]!),
                    SecureSocketOptions.StartTls);
                await smtp.AuthenticateAsync(senderEmail, _config["SmtpSettings:Password"]);
                await smtp.SendAsync(email);
                await smtp.DisconnectAsync(true);
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, $"Member details were saved, but the follow-up email could not be sent: {ex.Message}");
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> SendMemberPaymentReceiptAsync(
            string recipientEmail,
            string memberName,
            string transactionCode,
            decimal amountPaid,
            DateTime transactionDate,
            string membershipPlan,
            decimal planPrice,
            decimal totalPaid,
            decimal remainingBalance)
        {
            using var timeout = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                var safeName = System.Net.WebUtility.HtmlEncode(memberName);
                var safeCode = System.Net.WebUtility.HtmlEncode(transactionCode);
                var email = new MimeMessage();
                email.From.Add(new MailboxAddress(
                    _config["SmtpSettings:SenderName"],
                    _config["SmtpSettings:SenderEmail"]));
                email.To.Add(MailboxAddress.Parse(recipientEmail));
                email.Subject = "YOKOKS Gym membership payment receipt";
                email.Body = new TextPart("html")
                {
                    Text = $@"
                    <div style='font-family:Arial,sans-serif;max-width:540px;margin:auto;border:1px solid #3c443d;border-radius:8px;overflow:hidden;background:#1b201c;color:#ffffff'>
                      <div style='background:#b7ff1b;color:#182000;padding:20px 24px'><h2 style='margin:0'>Payment Receipt</h2></div>
                      <div style='padding:24px'>
                        <p>Dear <strong>{safeName}</strong>,</p>
                        <p>We received your cash payment. The transaction details are shown below.</p>
                        <table style='width:100%;border-collapse:collapse;margin:20px 0'>
                          <tr><td style='padding:10px 0;color:#c4ccc6'>Transaction code</td><td style='text-align:right;font-weight:bold'>{safeCode}</td></tr>
                          <tr><td style='padding:10px 0;color:#c4ccc6'>Payment method</td><td style='text-align:right'>Cash</td></tr>
                          <tr><td style='padding:10px 0;color:#c4ccc6'>Membership plan</td><td style='text-align:right'>{System.Net.WebUtility.HtmlEncode(membershipPlan)}</td></tr>
                          <tr><td style='padding:10px 0;color:#c4ccc6'>Plan price</td><td style='text-align:right'>₱{planPrice:N2}</td></tr>
                          <tr><td style='padding:10px 0;color:#c4ccc6'>Amount paid</td><td style='text-align:right;font-size:20px;font-weight:bold;color:#b7ff1b'>₱{amountPaid:N2}</td></tr>
                          <tr><td style='padding:10px 0;color:#c4ccc6'>Total paid</td><td style='text-align:right'>₱{totalPaid:N2}</td></tr>
                          <tr><td style='padding:10px 0;color:#c4ccc6'>Transaction date</td><td style='text-align:right'>{transactionDate:MMMM d, yyyy h:mm tt}</td></tr>
                        </table>
                        <p style='font-size:12px;color:#aeb7b0'>This email is your official membership payment receipt.</p>
                      </div>
                    </div>"
                };

                using var smtp = new SmtpClient();
                await smtp.ConnectAsync(
                    _config["SmtpSettings:Server"],
                    int.Parse(_config["SmtpSettings:Port"]!),
                    SecureSocketOptions.StartTls, timeout.Token);
                await smtp.AuthenticateAsync(
                    _config["SmtpSettings:SenderEmail"],
                    _config["SmtpSettings:Password"], timeout.Token);
                await smtp.SendAsync(email, timeout.Token);
                // A disconnect failure cannot undo an accepted receipt.
                try { await smtp.DisconnectAsync(true, timeout.Token); }
                catch (Exception) { /* Disposal closes the connection. */ }
                return (true, null);
            }
            catch (OperationCanceledException)
            {
                return (false, "Registration and payment were saved, but receipt delivery could not be confirmed within 30 seconds. Do not register again.");
            }
            catch (Exception ex)
            {
                return (false, $"Payment was recorded, but the email receipt could not be sent: {ex.Message}");
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> SendPasswordResetLinkAsync(string recipientEmail, string resetUrl)
        {
            try
            {
                var email = new MimeMessage();
                email.From.Add(new MailboxAddress(_config["SmtpSettings:SenderName"], _config["SmtpSettings:SenderEmail"]));
                email.To.Add(MailboxAddress.Parse(recipientEmail));
                email.Subject = "YOKOKS Gym password reset";
                email.Body = new TextPart("html") { Text = $@"<div style='max-width:540px;margin:auto;background:#1b201c;color:white;font-family:Arial,sans-serif;padding:24px;border:1px solid #b7ff1b'>
                    <h2 style='color:#b7ff1b'>Reset your password</h2><p>Use the button below to choose a new password. This link expires in 30 minutes and can only be used once.</p>
                    <p><a href='{System.Net.WebUtility.HtmlEncode(resetUrl)}' style='display:inline-block;background:#b7ff1b;color:#182000;padding:14px 22px;text-decoration:none'>Reset password</a></p>
                    <p>If you did not request this, you can ignore this email. Your password has not changed.</p></div>" };
                using var smtp = new SmtpClient();
                await smtp.ConnectAsync(_config["SmtpSettings:Server"], int.Parse(_config["SmtpSettings:Port"]!), SecureSocketOptions.StartTls);
                await smtp.AuthenticateAsync(_config["SmtpSettings:SenderEmail"], _config["SmtpSettings:Password"]);
                await smtp.SendAsync(email);
                await smtp.DisconnectAsync(true);
                return (true, null);
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public async Task<(bool Success, string? ErrorMessage)> SendAdminPasswordChangeNoticeAsync(
            string recipientEmail, string accountName, DateTime changedAt)
        {
            try
            {
                var email = new MimeMessage();
                email.From.Add(new MailboxAddress(_config["SmtpSettings:SenderName"], _config["SmtpSettings:SenderEmail"]));
                email.To.Add(MailboxAddress.Parse(recipientEmail));
                email.Subject = "YOKOKS Gym security notice: your password was changed";
                email.Body = new TextPart("html") { Text = $@"
                    <div style='max-width:540px;margin:auto;font-family:Arial,sans-serif;background:#1b201c;color:#fff;border:1px solid #3c443d'>
                    <h2 style='margin:0;padding:24px;background:#b7ff1b;color:#182000'>Password changed</h2>
                    <div style='padding:24px'><p>Dear {System.Net.WebUtility.HtmlEncode(accountName)},</p>
                    <p>An administrator changed the password for your YOKOKS Gym account on {changedAt:MMMM d, yyyy h:mm tt} (gym local time).</p>
                    <p>Your previous password will no longer work. Contact your administrator for access assistance.</p>
                    <p>If you did not expect this change, contact gym management immediately.</p>
                    <p style='color:#aeb7b0'>For your security, passwords are never included in this email.</p></div></div>" };
                using var smtp = new SmtpClient();
                await smtp.ConnectAsync(_config["SmtpSettings:Server"], int.Parse(_config["SmtpSettings:Port"]!), SecureSocketOptions.StartTls);
                await smtp.AuthenticateAsync(_config["SmtpSettings:SenderEmail"], _config["SmtpSettings:Password"]);
                await smtp.SendAsync(email);
                await smtp.DisconnectAsync(true);
                return (true, null);
            }
            catch (Exception ex) { return (false, $"The password was updated, but the account notification could not be emailed: {ex.Message}"); }
        }

        public async Task<(bool Success, string? ErrorMessage)> SendCancellationReceiptAsync(
            string recipientEmail, string memberName, decimal planPrice, decimal paid,
            int durationDays, int usedDays)
        {
            try
            {
                var deduction = decimal.Round(planPrice / Math.Max(1, durationDays) * usedDays, 2);
                var refund = Math.Max(0m, paid - deduction);
                var email = new MimeMessage();
                email.From.Add(new MailboxAddress(_config["SmtpSettings:SenderName"], _config["SmtpSettings:SenderEmail"]));
                email.To.Add(MailboxAddress.Parse(recipientEmail));
                email.Subject = "YOKOKS Gym membership cancellation and refund breakdown";
                email.Body = new TextPart("html") { Text = $@"
                    <div style='max-width:540px;margin:auto;font-family:Arial,sans-serif;background:#1b201c;color:#fff;border:1px solid #b7ff1b'>
                    <h2 style='margin:0;padding:24px;background:#b7ff1b;color:#182000'>Membership cancellation</h2>
                    <div style='padding:24px'><p>Dear {System.Net.WebUtility.HtmlEncode(memberName)},</p>
                    <p>Your membership has been cancelled on {DateTime.Today:MMMM d, yyyy}.</p>
                    <p>Plan price: ₱{planPrice:N2}<br>Plan duration: {durationDays} days<br>
                    Payment received: ₱{paid:N2}<br>Days used: {usedDays}<br>Deduction for days used: ₱{deduction:N2}</p>
                    <p style='color:#b7ff1b;font-size:20px'>Estimated refund: ₱{refund:N2}</p>
                    <p>This is the cancellation calculation, not confirmation of a refund payment. Please contact management to arrange your refund.</p>
                    </div></div>" };
                using var smtp = new SmtpClient();
                await smtp.ConnectAsync(_config["SmtpSettings:Server"], int.Parse(_config["SmtpSettings:Port"]!), SecureSocketOptions.StartTls);
                await smtp.AuthenticateAsync(_config["SmtpSettings:SenderEmail"], _config["SmtpSettings:Password"]);
                await smtp.SendAsync(email);
                await smtp.DisconnectAsync(true);
                return (true, null);
            }
            catch (Exception ex) { return (false, $"Cancellation saved, but the refund breakdown could not be emailed: {ex.Message}"); }
        }

        public async Task<(bool Success, string? ErrorMessage)> SendTrainerSessionReminderAsync(
            string recipientEmail,
            string memberName,
            string trainerName,
            DateTime startTime,
            DateTime endTime,
            string confirmUrl,
            string cancelUrl)
        {
            try
            {
                var safeMember = System.Net.WebUtility.HtmlEncode(memberName);
                var safeTrainer = System.Net.WebUtility.HtmlEncode(trainerName);
                var email = new MimeMessage();
                email.From.Add(new MailboxAddress(_config["SmtpSettings:SenderName"], _config["SmtpSettings:SenderEmail"]));
                email.To.Add(MailboxAddress.Parse(recipientEmail));
                email.Subject = $"YOKOKS Gym training reminder - {startTime:MMM d, yyyy}";
                email.Body = new TextPart("html")
                {
                    Text = $@"
                    <div style='font-family:Arial,sans-serif;max-width:560px;margin:auto;border:1px solid #dbe5df;border-radius:8px;overflow:hidden;color:#183028'>
                      <div style='background:#083d02;color:#fff;padding:20px 24px'><h2 style='margin:0'>Training Session Reminder</h2></div>
                      <div style='padding:24px'>
                        <p>Dear <strong>{safeMember}</strong>,</p>
                        <p>This is a reminder for your scheduled gym training session.</p>
                        <div style='margin:20px 0;padding:15px;background:#293321;color:#f5f5f5;border-left:4px solid #b7ff1b'>
                          <div><strong>Trainer:</strong> {safeTrainer}</div>
                          <div><strong>Date:</strong> {startTime:MMMM d, yyyy}</div>
                          <div><strong>Time:</strong> {startTime:h:mm tt} - {endTime:h:mm tt}</div>
                        </div>
                        <p>Please confirm whether you can attend:</p>
                        <p style='display:flex;gap:12px'>
                          <a href='{confirmUrl}' style='padding:12px 22px;border-radius:6px;background:#08752a;color:#fff;text-decoration:none;font-weight:bold'>Confirm attendance</a>
                          <a href='{cancelUrl}' style='padding:12px 22px;border-radius:6px;background:#b42318;color:#fff;text-decoration:none;font-weight:bold'>Cannot attend</a>
                        </p>
                        <p style='font-size:12px;color:#76827b;margin-top:24px'>These response links expire in 48 hours.</p>
                      </div>
                    </div>"
                };

                using var smtp = new SmtpClient();
                await smtp.ConnectAsync(_config["SmtpSettings:Server"], int.Parse(_config["SmtpSettings:Port"]!), SecureSocketOptions.StartTls);
                await smtp.AuthenticateAsync(_config["SmtpSettings:SenderEmail"], _config["SmtpSettings:Password"]);
                await smtp.SendAsync(email);
                await smtp.DisconnectAsync(true);
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, $"The session reminder could not be sent: {ex.Message}");
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> SendMembershipExpirationReminderAsync(
            string recipientEmail,
            string memberName,
            DateTime expirationDate)
        {
            try
            {
                var safeName = System.Net.WebUtility.HtmlEncode(memberName);
                var daysRemaining = Math.Max(0, (expirationDate.Date - DateTime.Today).Days);
                var email = new MimeMessage();
                email.From.Add(new MailboxAddress(
                    _config["SmtpSettings:SenderName"],
                    _config["SmtpSettings:SenderEmail"]));
                email.To.Add(MailboxAddress.Parse(recipientEmail));
                email.Subject = "YOKOKS Gym membership expiration reminder";
                email.Body = new TextPart("html")
                {
                    Text = $@"
                    <div style='font-family:Arial,sans-serif;max-width:540px;margin:auto;border:1px solid #dbe5df;border-radius:8px;overflow:hidden;color:#183028'>
                      <div style='background:#083d02;color:#fff;padding:20px 24px'><h2 style='margin:0'>Membership Expiring Soon</h2></div>
                      <div style='padding:24px'>
                        <p>Dear <strong>{safeName}</strong>,</p>
                        <p>Your YOKOKS Gym membership will expire in <strong>{daysRemaining} day(s)</strong>.</p>
                        <div style='margin:20px 0;padding:15px;background:#293321;color:#f5f5f5;border-left:4px solid #b7ff1b'>
                          <strong>Expiration date:</strong> {expirationDate:MMMM d, yyyy}
                        </div>
                        <p>Please visit or contact the Front Desk if you would like to renew your membership.</p>
                      </div>
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
                return (false, $"The membership expiration reminder could not be sent: {ex.Message}");
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> SendWeeklyAttendanceSummaryAsync(
            string recipientEmail,
            string memberName,
            DateTime weekStart,
            DateTime weekEnd,
            int attendanceDays,
            IReadOnlyList<DateTime> checkInDates)
        {
            try
            {
                var safeName = System.Net.WebUtility.HtmlEncode(memberName);
                var dates = checkInDates.Count == 0
                    ? "No attendance was recorded."
                    : string.Join("", checkInDates.Select(date =>
                        $"<li style='margin:5px 0'>{date:dddd, MMMM d, yyyy}</li>"));
                var attendanceDetails = checkInDates.Count == 0
                    ? $"<p style='color:#68756e'>{dates}</p>"
                    : $"<ul style='padding-left:20px'>{dates}</ul>";

                var email = new MimeMessage();
                email.From.Add(new MailboxAddress(
                    _config["SmtpSettings:SenderName"],
                    _config["SmtpSettings:SenderEmail"]));
                email.To.Add(MailboxAddress.Parse(recipientEmail));
                email.Subject = "YOKOKS Gym weekly attendance summary";
                email.Body = new TextPart("html")
                {
                    Text = $@"
                    <div style='font-family:Arial,sans-serif;max-width:540px;margin:auto;border:1px solid #dbe5df;border-radius:8px;overflow:hidden;color:#183028'>
                      <div style='background:#083d02;color:#fff;padding:20px 24px'><h2 style='margin:0'>Weekly Attendance Summary</h2></div>
                      <div style='padding:24px'>
                        <p>Dear <strong>{safeName}</strong>,</p>
                        <p>Here is your gym attendance summary for {weekStart:MMMM d}–{weekEnd:MMMM d, yyyy}.</p>
                        <div style='margin:20px 0;padding:16px;background:#293321;color:#f5f5f5;border-left:4px solid #b7ff1b'>
                          <div style='font-size:13px;color:#68756e'>Confirmed attendance days</div>
                          <div style='font-size:30px;font-weight:bold;color:#08752a'>{attendanceDays}</div>
                        </div>
                        {attendanceDetails}
                      </div>
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
                return (false, $"The weekly attendance email could not be sent: {ex.Message}");
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> SendDailyTrainingReminderAsync(
            string recipientEmail, string memberName, string trainerName, DateTime date, string timeSlot, string? attendUrl = null, string? absentUrl = null)
        {
            try
            {
                var safeMember = System.Net.WebUtility.HtmlEncode(memberName);
                var safeTrainer = System.Net.WebUtility.HtmlEncode(trainerName);
                var safeTime = System.Net.WebUtility.HtmlEncode(timeSlot);
                var responseButtons = attendUrl != null && absentUrl != null
                    ? $"<p>Replying is optional. Let your trainer know your plans:</p><p><a href='{System.Net.WebUtility.HtmlEncode(attendUrl)}' style='display:inline-block;padding:12px 22px;background:#b7ff1b;color:#182000;border:1px solid #b7ff1b;border-radius:6px;font-weight:700;text-decoration:none'>Attend</a> <a href='{System.Net.WebUtility.HtmlEncode(absentUrl)}' style='display:inline-block;padding:12px 22px;background:#dc2626;color:#fff;border:1px solid #dc2626;border-radius:6px;font-weight:700;text-decoration:none'>Absent</a></p><p>Your response informs your trainer; actual attendance is recorded when you check in.</p>"
                    : "";
                var email = new MimeMessage();
                email.From.Add(new MailboxAddress(_config["SmtpSettings:SenderName"], _config["SmtpSettings:SenderEmail"]));
                email.To.Add(MailboxAddress.Parse(recipientEmail));
                email.Subject = $"YOKOKS Gym daily training reminder - {date:MMM d, yyyy}";
                email.Body = new TextPart("html")
                {
                    Text = $@"<div style='font-family:Arial,sans-serif;max-width:540px;margin:auto;border:1px solid #b7ff1b;background:#1b201c;color:#f5f5f5'>
                    <div style='background:#b7ff1b;color:#182000;padding:20px 24px'><h2 style='margin:0'>Daily Training Reminder</h2></div>
                    <div style='padding:24px'><p>Dear <strong>{safeMember}</strong>,</p><p>This is your reminder for today's scheduled gym training.</p>
                    <div style='margin:20px 0;padding:16px;background:#293321;color:#f5f5f5;border-left:4px solid #b7ff1b'><div><strong>Trainer:</strong> {safeTrainer}</div><div><strong>Date:</strong> {date:MMMM d, yyyy}</div><div><strong>Time:</strong> {safeTime}</div></div>
                    {responseButtons}<p>We look forward to seeing you at YOKOKS Gym.</p></div></div>"
                };
                using var smtp = new SmtpClient();
                await smtp.ConnectAsync(_config["SmtpSettings:Server"], int.Parse(_config["SmtpSettings:Port"]!), SecureSocketOptions.StartTls);
                await smtp.AuthenticateAsync(_config["SmtpSettings:SenderEmail"], _config["SmtpSettings:Password"]);
                await smtp.SendAsync(email);
                await smtp.DisconnectAsync(true);
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, $"The daily training reminder could not be sent: {ex.Message}");
            }
        }
    }
}
