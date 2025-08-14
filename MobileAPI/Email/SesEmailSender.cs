using Amazon;
using Amazon.Runtime;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;

namespace MobileAPI.Email;

public class SesEmailSender : IEmailSender
{
    private readonly string _from;
    private readonly IAmazonSimpleEmailService _ses;

    public SesEmailSender(IConfiguration cfg)
    {
        var region = cfg["AwsSes:Region"] ?? "eu-central-1";
        _from = cfg["AwsSes:FromEmail"] ?? throw new InvalidOperationException("AwsSes:FromEmail not set");

        var access = cfg["AwsSes:AccessKeyId"];
        var secret = cfg["AwsSes:SecretAccessKey"];

        if (!string.IsNullOrWhiteSpace(access) && !string.IsNullOrWhiteSpace(secret))
        {
            var creds = new BasicAWSCredentials(access, secret);
            _ses = new AmazonSimpleEmailServiceClient(creds, RegionEndpoint.GetBySystemName(region));
        }
        else
        {
            // Falls back to the default chain: env vars -> shared creds -> EC2/ECS
            _ses = new AmazonSimpleEmailServiceClient(RegionEndpoint.GetBySystemName(region));
        }
    }

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        try
        {
            var req = new SendEmailRequest
            {
                Source = _from,
                Destination = new Destination { ToAddresses = new List<string> { toEmail } },
                Message = new Message
                {
                    Subject = new Content(subject),
                    Body = new Body { Html = new Content(htmlBody) }
                }
            };
            await _ses.SendEmailAsync(req, ct);
        }
        catch (AmazonServiceException ex)
        {
            throw new InvalidOperationException(
                "SES send failed. Provide AwsSes:AccessKeyId/SecretAccessKey or set Email:Provider=Dev for local dev.",
                ex);
        }
    }
}
