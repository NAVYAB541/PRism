# PRism

**PRism** is an AI-powered code review bot built with .NET 10. It listens for
GitHub pull request webhooks, sends the PR diff to an LLM, and posts the
review back as a comment on the pull request.

## How it works

```
GitHub PR (opened / synchronize)
   -> POST /webhook/github   (HMAC-SHA256 signature verified)
   -> Fetch PR file diffs via GitHub API (Octokit.NET)
   -> Send diff to an AI provider, asking for a JSON review that matches a schema
   -> Validate the findings (file is in the diff, known severity, non-empty message)
   -> Post the validated findings as a markdown PR comment
```

## Structured reviews

The model is prompted with a system prompt plus two few-shot examples (one diff
with real issues, one clean diff with no findings) and must answer with JSON:

```json
{"summary": "...", "findings": [{"file": "src/A.cs", "line": 12, "severity": "critical", "message": "..."}]}
```

Gemini gets the schema through `generationConfig.responseSchema` and the
system prompt through `systemInstruction`. Azure OpenAI gets it through
`response_format` with a strict `json_schema`. In both cases
`ReviewResultParser` checks the output again before anything is posted: it
normalises severities (`critical`, `major`, `minor`, `nit`), drops findings for
files that are not in the diff, and caps the number of findings. If the output
can't be used at all, a short fallback message is posted and the raw output is
logged.

## AI provider

The AI call is behind an `IAiReviewService` interface with two
implementations:

- **Gemini** (default) — uses Google's free-tier `gemini-2.0-flash` model, so
  the bot can be run and demoed without any cloud subscription.
- **Azure OpenAI** — a fully implemented, test-covered alternative using
  Azure OpenAI Chat Completions with structured outputs (needs a deployment
  that supports `json_schema`, e.g. gpt-4o 2024-08-06 or later). This is the intended production swap (e.g.
  for an organization already on Azure). Switch to it by setting
  `Bot:AiProvider` to `"AzureOpenAi"` and configuring the `AzureOpenAi*`
  settings below.

## Configuration

All settings live under the `Bot` section of configuration and should be
supplied via environment variables or `dotnet user-secrets` (never commit
real secrets to `appsettings.json`).

| Setting | Description |
| --- | --- |
| `Bot:GitHubToken` | GitHub Personal Access Token with `repo` scope (used to read diffs and post comments) |
| `Bot:WebhookSecret` | Shared secret configured on the GitHub webhook |
| `Bot:AiProvider` | `"Gemini"` (default) or `"AzureOpenAi"` |
| `Bot:GeminiApiKey` | API key from [Google AI Studio](https://aistudio.google.com/) (free tier) |
| `Bot:GeminiModel` | Defaults to `gemini-2.0-flash` |
| `Bot:AzureOpenAiEndpoint` | Azure OpenAI resource endpoint, e.g. `https://my-resource.openai.azure.com` |
| `Bot:AzureOpenAiApiKey` | Azure OpenAI API key |
| `Bot:AzureOpenAiDeployment` | Azure OpenAI deployment name |
| `Bot:AzureOpenAiApiVersion` | Defaults to `2024-08-01-preview` |

### Local setup with user secrets

```bash
cd src/PRism.Api
dotnet user-secrets init
dotnet user-secrets set "Bot:GitHubToken" "<your-github-pat>"
dotnet user-secrets set "Bot:WebhookSecret" "<your-webhook-secret>"
dotnet user-secrets set "Bot:GeminiApiKey" "<your-gemini-api-key>"
```

## Running locally

```bash
dotnet run --project src/PRism.Api
```

The API listens on `http://localhost:5000` by default. Expose it to the
internet so GitHub can deliver webhooks, e.g. with [ngrok](https://ngrok.com/):

```bash
ngrok http 5000
```

## Configuring the GitHub webhook

In your repository, go to **Settings → Webhooks → Add webhook**:

- **Payload URL**: `https://<your-ngrok-domain>/webhook/github`
- **Content type**: `application/json`
- **Secret**: same value as `Bot:WebhookSecret`
- **Events**: select "Pull requests"

Once configured, opening or updating a pull request will trigger PRism to
fetch the diff, generate an AI review, and post it as a comment.

## Building and testing

```bash
dotnet build
dotnet test
```

## Project structure

```
src/PRism.Api/
  Program.cs                  # app startup, DI registration
  Endpoints/WebhookEndpoints.cs
  Services/
    WebhookSignatureValidator.cs
    GitHubService.cs          # diff fetching, comment posting (Octokit)
    GeminiReviewService.cs    # default AI provider
    AzureOpenAiReviewService.cs  # production-ready Azure OpenAI alternative
    ReviewPrompts.cs          # system prompt, few-shot examples, response schemas
    ReviewResultParser.cs     # validates and normalises the model's JSON
    ReviewFormatter.cs        # renders findings as the PR comment
  Models/
  Configuration/BotOptions.cs
tests/PRism.Tests/
```
