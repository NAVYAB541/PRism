using System.Text.Json.Serialization;

namespace PRism.Api.Models;

/// <summary>Minimal subset of the GitHub "pull_request" webhook event payload.</summary>
public class PullRequestWebhookPayload
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("pull_request")]
    public PullRequestInfo? PullRequest { get; set; }

    [JsonPropertyName("repository")]
    public RepositoryInfo? Repository { get; set; }
}

public class PullRequestInfo
{
    [JsonPropertyName("number")]
    public int Number { get; set; }
}

public class RepositoryInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("owner")]
    public RepositoryOwner Owner { get; set; } = new();
}

public class RepositoryOwner
{
    [JsonPropertyName("login")]
    public string Login { get; set; } = string.Empty;
}
