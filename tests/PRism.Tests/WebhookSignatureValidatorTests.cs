using System.Security.Cryptography;
using System.Text;
using PRism.Api.Services;
using Xunit;

namespace PRism.Tests;

public class WebhookSignatureValidatorTests
{
    private const string Secret = "test-secret";

    [Fact]
    public void IsValid_ReturnsTrue_ForCorrectSignature()
    {
        var body = Encoding.UTF8.GetBytes("{\"action\":\"opened\"}");
        var signature = ComputeSignature(body, Secret);

        var result = WebhookSignatureValidator.IsValid(signature, body, Secret);

        Assert.True(result);
    }

    [Fact]
    public void IsValid_ReturnsFalse_ForTamperedBody()
    {
        var originalBody = Encoding.UTF8.GetBytes("{\"action\":\"opened\"}");
        var signature = ComputeSignature(originalBody, Secret);

        var tamperedBody = Encoding.UTF8.GetBytes("{\"action\":\"closed\"}");

        var result = WebhookSignatureValidator.IsValid(signature, tamperedBody, Secret);

        Assert.False(result);
    }

    [Fact]
    public void IsValid_ReturnsFalse_ForMissingHeader()
    {
        var body = Encoding.UTF8.GetBytes("{\"action\":\"opened\"}");

        var result = WebhookSignatureValidator.IsValid(null, body, Secret);

        Assert.False(result);
    }

    private static string ComputeSignature(byte[] body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(body);
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
