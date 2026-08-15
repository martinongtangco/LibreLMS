namespace LibreLms.SharedKernel;

/// <summary>Seam for sending transactional email without knowing the provider.
/// One sentence: the place any code sends transactional email without knowing who delivers it.</summary>
public interface ITransactionalEmailSender
{
    /// <summary>Send a transactional email. Implementations MUST NOT throw for delivery
    /// failures — the originating flow (sign-up, verification, reset) must never fail
    /// because an email could not be delivered (spec 027 FR-022).</summary>
    Task SendAsync(OutboundEmail email);
}

/// <summary>Why a transactional email is being sent.</summary>
public enum EmailPurpose { Verification, Welcome, PasswordReset }

/// <summary>The single payload crossing the ITransactionalEmailSender seam.</summary>
public record OutboundEmail(string To, EmailPurpose Purpose, string Subject, string Body);
