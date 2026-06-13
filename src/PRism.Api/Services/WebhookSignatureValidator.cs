using System.Security.Cryptography;
using System.Text;

namespace PRism.Api.Services;

public static class WebhookSignatureValidator
{
    /// <summary>
    /// Validates a GitHub "X-Hub-Signature-256" header (format: "sha256={hex}")
    /// against the raw request body using the shared webhook secret.
    /// </summary>
    public static bool IsValid(string? signatureHeader, byte[] body, string secret)
    {
        const string prefix = "sha256=";

        if (string.IsNullOrEmpty(signatureHeader) || !signatureHeader.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var providedHex = signatureHeader[prefix.Length..];

        byte[] providedSignature;
        try
        {
            providedSignature = Convert.FromHexString(providedHex);
        }
        catch (FormatException)
        {
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expectedSignature = hmac.ComputeHash(body);

        return CryptographicOperations.FixedTimeEquals(providedSignature, expectedSignature);
    }
}
