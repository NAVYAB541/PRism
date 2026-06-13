using Octokit;
using PRism.Api.Configuration;
using PRism.Api.Endpoints;
using PRism.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<BotOptions>()
    .Bind(builder.Configuration.GetSection(BotOptions.SectionName))
    .ValidateDataAnnotations();

builder.Services.AddSingleton<IGitHubClient>(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<BotOptions>>().Value;
    var client = new GitHubClient(new ProductHeaderValue("PRism"))
    {
        Credentials = new Credentials(options.GitHubToken)
    };
    return client;
});

builder.Services.AddSingleton<IGitHubService, GitHubService>();

builder.Services.AddHttpClient<GeminiReviewService>();
builder.Services.AddHttpClient<AzureOpenAiReviewService>();

builder.Services.AddScoped<IAiReviewService>(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<BotOptions>>().Value;
    return options.AiProvider.Equals("AzureOpenAi", StringComparison.OrdinalIgnoreCase)
        ? sp.GetRequiredService<AzureOpenAiReviewService>()
        : sp.GetRequiredService<GeminiReviewService>();
});

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapWebhookEndpoints();

app.Run();

public partial class Program;
