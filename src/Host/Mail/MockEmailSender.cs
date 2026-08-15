using LibreLms.SharedKernel;
using Microsoft.Extensions.Logging;

namespace LibreLms.Host.Mail;

/// <summary>
/// Development mock of ITransactionalEmailSender (spec 027 FR-019..FR-022, ADR-0004):
/// records every email in the developer-observable outbox and logs it in full, and sends
/// NOTHING real. One sentence: the stand-in email provider that makes links retrievable
/// without a mailbox. A future SendGrid implementation replaces this class in DI only.
/// </summary>
public sealed class MockEmailSender : ITransactionalEmailSender
{
    private readonly DevEmailOutbox _outbox;
    private readonly ILogger<MockEmailSender> _logger;

    public MockEmailSender(DevEmailOutbox outbox, ILogger<MockEmailSender> logger)
    {
        _outbox = outbox;
        _logger = logger;
    }

    /// <summary>Records the email (outbox + log). Never throws and never blocks the
    /// originating flow — a failed "send" is logged and the user keeps a resend path (FR-022).</summary>
    public Task SendAsync(OutboundEmail email)
    {
        try
        {
            _outbox.Add(email);
            _logger.LogInformation(
                "[mock-email] To={To} Purpose={Purpose} Subject={Subject}\n{Body}",
                email.To, email.Purpose, email.Subject, email.Body);
        }
        catch (Exception ex)
        {
            // Delivery failure must never fail the user's action (FR-022).
            _logger.LogError(ex, "[mock-email] Failed to record email To={To} Purpose={Purpose}", email.To, email.Purpose);
        }

        return Task.CompletedTask;
    }
}
