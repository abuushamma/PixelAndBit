using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace PixelAndBit.Web.Areas.Identity.Pages.Account;

/// <summary>
/// Server-side session state + helpers for the multi-step forgot-password flow.
/// No DB migration required — state lives in <see cref="HttpContext.Session"/>.
/// </summary>
internal static class PasswordResetFlow
{
    // --- Session keys (all values are stored as strings) ------------------
    internal const string K_Email        = "pb.reset.email";
    internal const string K_UserId       = "pb.reset.userId";
    internal const string K_CodeHash     = "pb.reset.codeHash";
    internal const string K_ExpiresTicks = "pb.reset.expiresTicks";
    internal const string K_Attempts     = "pb.reset.attempts";
    internal const string K_MaxAttempts  = "pb.reset.maxAttempts";
    internal const string K_ResetToken   = "pb.reset.token";
    internal const string K_Verified     = "pb.reset.verified";
    internal const string K_LastSentTicks= "pb.reset.lastSentTicks";

    internal const int CodeTtlMinutes        = 15;
    internal const int ResendCooldownSeconds = 60;
    internal const int DefaultMaxAttempts    = 6;

    /// <summary>Generates a cryptographically random 5-digit numeric code.</summary>
    internal static string Generate5DigitCode()
    {
        var n = RandomNumberGenerator.GetInt32(10000, 100000);
        return n.ToString();
    }

    /// <summary>
    /// SHA-256 hash of the code, salted by userId + normalized email + a purpose
    /// marker. The purpose marker isolates this hash space from the email-
    /// verification codes so the two flows can never collide.
    /// </summary>
    internal static string HashCode(string userId, string email, string code)
    {
        var input = $"reset|{userId}|{email.ToUpperInvariant()}|{code}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }

    internal static bool FixedTimeEquals(string a, string b)
    {
        var ab = Encoding.UTF8.GetBytes(a ?? string.Empty);
        var bb = Encoding.UTF8.GetBytes(b ?? string.Empty);
        return ab.Length == bb.Length && CryptographicOperations.FixedTimeEquals(ab, bb);
    }

    // --- Session helpers --------------------------------------------------

    internal static void ClearAll(ISession session)
    {
        session.Remove(K_Email);
        session.Remove(K_UserId);
        session.Remove(K_CodeHash);
        session.Remove(K_ExpiresTicks);
        session.Remove(K_Attempts);
        session.Remove(K_MaxAttempts);
        session.Remove(K_ResetToken);
        session.Remove(K_Verified);
        session.Remove(K_LastSentTicks);
    }

    /// <summary>
    /// True once Step 1 has stored an email in session. Gates access to Step 2.
    /// Stored regardless of whether a real account exists, so the response
    /// after Step 1 is indistinguishable from the caller's perspective.
    /// </summary>
    internal static bool HasPendingEmail(ISession session)
    {
        return !string.IsNullOrWhiteSpace(session.GetString(K_Email));
    }

    /// <summary>
    /// True when Step 1 actually produced a verifiable code (real, confirmed user).
    /// Used internally by Step 2's POST handler to decide whether comparison
    /// can succeed; never used to gate page access.
    /// </summary>
    internal static bool HasPendingCode(ISession session)
    {
        return !string.IsNullOrWhiteSpace(session.GetString(K_CodeHash))
            && !string.IsNullOrWhiteSpace(session.GetString(K_UserId))
            && !string.IsNullOrWhiteSpace(session.GetString(K_ExpiresTicks));
    }

    internal static bool IsCodeVerified(ISession session)
    {
        return session.GetString(K_Verified) == "1"
            && !string.IsNullOrWhiteSpace(session.GetString(K_ResetToken))
            && !string.IsNullOrWhiteSpace(session.GetString(K_UserId));
    }

    /// <summary>Seconds until user may request another code. 0 = available now.</summary>
    internal static int ResendCooldownSeconds_(ISession session)
    {
        var raw = session.GetString(K_LastSentTicks);
        if (!long.TryParse(raw, out var lastTicks) || lastTicks <= 0) return 0;
        var last = new DateTime(lastTicks, DateTimeKind.Utc);
        var elapsed = (DateTime.UtcNow - last).TotalSeconds;
        if (elapsed >= ResendCooldownSeconds) return 0;
        return (int)Math.Ceiling(ResendCooldownSeconds - elapsed);
    }

    // --- Email HTML (clean, light, transactional style) -------------------

    internal static string BuildResetEmailHtml(string code)
    {
        var preheader = $"Your Pixel & Bit password reset code is {code}. This code expires in {CodeTtlMinutes} minutes.";

        return $@"
<!DOCTYPE html PUBLIC ""-//W3C//DTD XHTML 1.0 Transitional//EN"" ""http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd"">
<html xmlns=""http://www.w3.org/1999/xhtml"" lang=""en"">
<head>
  <meta charset=""utf-8""/>
  <meta name=""viewport"" content=""width=device-width,initial-scale=1""/>
  <meta name=""x-apple-disable-message-reformatting""/>
  <meta name=""color-scheme"" content=""light only""/>
  <meta name=""supported-color-schemes"" content=""light""/>
  <title>Reset your password</title>
  <style>
    @media only screen and (max-width: 600px) {{
      .pb-container {{ width: 100% !important; max-width: 100% !important; }}
      .pb-pad-x    {{ padding-left: 20px !important; padding-right: 20px !important; }}
      .pb-pad-y    {{ padding-top: 26px !important; padding-bottom: 26px !important; }}
      .pb-code     {{ font-size: 30px !important; letter-spacing: 0.28em !important; padding: 16px 20px !important; }}
      .pb-title    {{ font-size: 20px !important; }}
      .pb-body     {{ font-size: 15px !important; }}
    }}
  </style>
</head>
<body style=""margin:0;padding:0;background-color:#f4f5f7;-webkit-text-size-adjust:100%;-ms-text-size-adjust:100%;"">
  <div style=""display:none;max-height:0;overflow:hidden;mso-hide:all;font-size:1px;line-height:1px;color:#f4f5f7;opacity:0;"">{preheader}</div>

  <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""background-color:#f4f5f7;"">
    <tr>
      <td align=""center"" style=""padding:28px 16px 40px;"">

        <table role=""presentation"" width=""560"" class=""pb-container"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""width:560px;max-width:560px;margin:0 auto 14px;"">
          <tr>
            <td align=""center"" style=""font-family:'Segoe UI',Arial,sans-serif;font-size:14px;font-weight:700;letter-spacing:0.06em;color:#111418;"">
              Pixel &amp; Bit
            </td>
          </tr>
        </table>

        <table role=""presentation"" width=""560"" class=""pb-container"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""width:560px;max-width:560px;background-color:#ffffff;border:1px solid #e5e7eb;border-radius:8px;"">
          <tr>
            <td class=""pb-pad-x"" style=""padding:28px 40px 0;text-align:left;"">
              <h1 class=""pb-title"" style=""margin:0 0 6px;font-family:'Segoe UI',Arial,sans-serif;font-size:22px;font-weight:600;color:#111418;line-height:1.3;"">Reset your password</h1>
              <p style=""margin:0;font-family:'Segoe UI',Arial,sans-serif;font-size:13px;color:#6b7280;line-height:1.5;"">We received a request to reset the password for your Pixel &amp; Bit account.</p>
            </td>
          </tr>

          <tr>
            <td style=""padding:20px 40px 0;"">
              <div style=""height:1px;line-height:1px;font-size:0;background-color:#e5e7eb;"">&nbsp;</div>
            </td>
          </tr>

          <tr>
            <td class=""pb-pad-x pb-pad-y"" style=""padding:24px 40px 8px;"">
              <p class=""pb-body"" style=""margin:0 0 20px;font-family:'Segoe UI',Arial,sans-serif;font-size:15px;line-height:1.6;color:#1f2328;"">
                Use the verification code below to continue resetting your password.
              </p>

              <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" align=""center"" style=""margin:4px auto 8px;"">
                <tr>
                  <td class=""pb-code"" align=""center"" style=""padding:18px 28px;background-color:#f9fafb;border:1px solid #d1d5db;border-radius:6px;font-family:Consolas,'Courier New',monospace;font-size:34px;font-weight:700;letter-spacing:0.32em;color:#111418;line-height:1;"">
                    {code}
                  </td>
                </tr>
              </table>

              <p style=""margin:18px 0 0;font-family:'Segoe UI',Arial,sans-serif;font-size:14px;line-height:1.6;color:#4b5563;"">
                This code expires in <strong style=""color:#111418;"">{CodeTtlMinutes} minutes</strong>.
              </p>
              <p style=""margin:6px 0 0;font-family:'Segoe UI',Arial,sans-serif;font-size:13px;line-height:1.6;color:#6b7280;"">
                If you did not request a password reset, you can safely ignore this email. Your password will not change.
              </p>
            </td>
          </tr>

          <tr>
            <td class=""pb-pad-x"" style=""padding:0 40px 28px;"">
              <p style=""margin:14px 0 0;font-family:'Segoe UI',Arial,sans-serif;font-size:14px;color:#1f2328;line-height:1.6;"">
                Thank you,<br/>
                <span style=""color:#111418;font-weight:600;"">The Pixel &amp; Bit Team</span>
              </p>
            </td>
          </tr>
        </table>

        <table role=""presentation"" width=""560"" class=""pb-container"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""width:560px;max-width:560px;margin:18px auto 0;"">
          <tr>
            <td align=""center"" style=""font-family:'Segoe UI',Arial,sans-serif;font-size:12px;color:#6b7280;line-height:1.6;"">
              This is an automated message. Please do not reply.<br/>
              Pixel &amp; Bit &middot; Amman, Jordan
            </td>
          </tr>
        </table>

      </td>
    </tr>
  </table>
</body>
</html>";
    }
}
