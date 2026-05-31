using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace WebApplication4.Services
{
    /// <summary>
    /// Email service for sending verification and password reset emails using MailKit
    /// </summary>
    public class EmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        /// <summary>
        /// Send email to the specified recipient
        /// </summary>
        /// <param name="toEmail">Recipient email address</param>
        /// <param name="subject">Email subject</param>
        /// <param name="message">Email body (HTML format)</param>
        public void SendEmail(string toEmail, string subject, string message)
        {
            try
            {
                var email = new MimeMessage();

                email.From.Add(MailboxAddress.Parse(
                    _config["EmailSettings:FromEmail"]
                ));

                email.To.Add(MailboxAddress.Parse(toEmail));
                email.Subject = subject;

                email.Body = new TextPart("html")
                {
                    Text = message
                };

                using var smtp = new SmtpClient();

                smtp.Connect(
                    _config["EmailSettings:SmtpServer"],
                    int.Parse(_config["EmailSettings:Port"]),
                    SecureSocketOptions.StartTls
                );

                smtp.Authenticate(
                    _config["EmailSettings:Username"],
                    _config["EmailSettings:Password"]
                );

                smtp.Send(email);
                smtp.Disconnect(true);

                _logger.LogInformation($"Email sent successfully to {toEmail}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to send email to {toEmail}");
                throw;
            }
        }

        /// <summary>
        /// Send verification email with verification code
        /// </summary>
        public void SendVerificationEmail(string toEmail, int verificationCode)
        {
            string subject = "Email Verification Code";
            string message = $@"
                <html>
                <body style='font-family: Arial, sans-serif;'>
                    <h2>Email Verification</h2>
                    <p>Thank you for registering! Please use the following code to verify your email address:</p>
                    <h3 style='color: #4CAF50;'>{verificationCode}</h3>
                    <p>This code will expire in 15 minutes.</p>
                    <p>If you did not register, please ignore this email.</p>
                </body>
                </html>";

            SendEmail(toEmail, subject, message);
        }

        /// <summary>
        /// Send password reset email with reset code
        /// </summary>
        public void SendPasswordResetEmail(string toEmail, string resetCode)
        {
            string subject = "Password Reset Code";
            string message = $@"
                <html>
                <body style='font-family: Arial, sans-serif;'>
                    <h2>Password Reset Request</h2>
                    <p>You have requested to reset your password. Please use the following code:</p>
                    <h3 style='color: #FF5722;'>{resetCode}</h3>
                    <p>This code will expire in 30 minutes.</p>
                    <p>If you did not request a password reset, please ignore this email.</p>
                </body>
                </html>";

            SendEmail(toEmail, subject, message);
        }

        /// <summary>
        /// Send booking confirmation email
        /// </summary>
        public void SendBookingConfirmationEmail(string toEmail, string userName, string eventTitle, int tickets, decimal totalAmount, string transactionId)
        {
            string subject = "Booking Confirmed - " + eventTitle;
            string message = $@"
                <html>
                <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; rounded-corners: 10px;'>
                        <h2 style='color: #0d6efd; text-align: center;'>Booking Confirmation</h2>
                        <p>Hi <strong>{userName}</strong>,</p>
                        <p>Thank you for your booking! Your tickets for <strong>{eventTitle}</strong> have been confirmed.</p>
                        
                        <div style='background-color: #f8f9fa; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                            <h4 style='margin-top: 0;'>Booking Details:</h4>
                            <table style='width: 100%;'>
                                <tr>
                                    <td><strong>Event:</strong></td>
                                    <td>{eventTitle}</td>
                                </tr>
                                <tr>
                                    <td><strong>Number of Tickets:</strong></td>
                                    <td>{tickets}</td>
                                </tr>
                                <tr>
                                    <td><strong>Total Amount:</strong></td>
                                    <td>{totalAmount:C}</td>
                                </tr>
                                <tr>
                                    <td><strong>Transaction ID:</strong></td>
                                    <td>{transactionId}</td>
                                </tr>
                            </table>
                        </div>
                        
                        <p>We look forward to seeing you at the event!</p>
                        <p style='font-size: 0.9em; color: #777;'>If you have any questions, please contact our support team.</p>
                        <hr style='border: 0; border-top: 1px solid #eee;'>
                        <p style='text-align: center; color: #999; font-size: 0.8em;'>&copy; {DateTime.Now.Year} WebApplication4. All rights reserved.</p>
                    </div>
                </body>
                </html>";

            SendEmail(toEmail, subject, message);
        }
    }
}
