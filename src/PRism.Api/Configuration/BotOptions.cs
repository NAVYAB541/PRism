namespace PRism.Api.Configuration;

public class BotOptions
{
    public const string SectionName = "Bot";

    public required string GitHubToken { get; set; }

    public required string WebhookSecret { get; set; }

    /// <summary>"Gemini" (default, free tier) or "AzureOpenAi".</summary>
    public string AiProvider { get; set; } = "Gemini";

    public string GeminiApiKey { get; set; } = string.Empty;

    public string GeminiModel { get; set; } = "gemini-2.0-flash";

    public string AzureOpenAiEndpoint { get; set; } = string.Empty;

    public string AzureOpenAiApiKey { get; set; } = string.Empty;

    public string AzureOpenAiDeployment { get; set; } = string.Empty;

    public string AzureOpenAiApiVersion { get; set; } = "2024-08-01-preview";
}
