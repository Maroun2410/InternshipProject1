using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace MobileAPI.Email;

public sealed class SmtpOptions
{
    public string Host { get; set; } = null!;
    public int Port { get; set; } = 587;
    public string User { get; set; } = null!;
    public string Pass { get; set; } = null!;
    public string FromEmail { get; set; } = null!;
    public string FromName { get; set; } = "MobileAPI";
    public bool EnableStartTls { get; set; } = true;
    public int TimeoutMs { get; set; } = 15000;
}

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _opt;
    public SmtpEmailSender(IOptions<SmtpOptions> opt) => _opt = opt.Value;

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        using var msg = new MailMessage
        {
            From = new MailAddress(_opt.FromEmail, _opt.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        msg.To.Add(toEmail);

        using var client = new SmtpClient(_opt.Host, _opt.Port)
        {
            EnableSsl = _opt.EnableStartTls, // STARTTLS on 587
            Credentials = new NetworkCredential(_opt.User, _opt.Pass),
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = _opt.TimeoutMs,
            UseDefaultCredentials = false
        };

        await client.SendMailAsync(msg);
    }
}
