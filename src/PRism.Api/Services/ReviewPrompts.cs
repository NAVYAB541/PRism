using System.Text.Json.Nodes;
using PRism.Api.Models;

namespace PRism.Api.Services;

public static class ReviewPrompts
{
    public const string SystemPrompt =
        """
        You are a senior code reviewer reviewing a GitHub pull request diff.

        The diff lists each changed file under a "--- path/to/file ---" header followed by its unified patch.
        Lines starting with "+" were added, "-" removed, and " " are unchanged context.

        Report real problems in the ADDED or CHANGED lines only:
        - critical: security vulnerabilities, data loss, crashes, or broken core behaviour.
        - major: bugs, incorrect logic, missing error handling, race conditions, serious performance issues.
        - minor: maintainability problems, missing validation on non-critical paths, confusing code.
        - nit: naming, formatting, or style.
        Do not comment on unchanged lines, do not praise the code, and do not invent issues: an empty findings list is a valid answer.

        Respond with JSON only, matching this shape:
        {"summary": "<1-3 sentences on what the PR does and its overall quality>",
         "findings": [{"file": "<path exactly as in the diff header>", "line": <line number in the new file, or null>, "severity": "critical"|"major"|"minor"|"nit", "message": "<what is wrong and how to fix it>"}]}
        Work out "line" from the "@@ -a,b +c,d @@" hunk header: the first line after the header is new-file line c, and each " " or "+" line adds one. Use null if you are unsure.
        """;

    public const string NothingToReviewMessage =
        "PRism: No reviewable file changes were found in this pull request.";

    public static string BuildUserPrompt(string diff) =>
        $"Review this diff:\n\n{diff}";

    /// <summary>A diff and the ideal answer for it, sent to the model as an earlier conversation turn.</summary>
    public sealed record FewShotExample(string Diff, string ExpectedJson);

    public static readonly IReadOnlyList<FewShotExample> FewShotExamples =
    [
        new(
            """
            --- src/Auth/LoginController.cs ---
            @@ -10,4 +10,9 @@ public class LoginController
                 public IActionResult Login(string user, string pass)
                 {
            +        var sql = $"SELECT * FROM Users WHERE Name = '{user}' AND Password = '{pass}'";
            +        var row = _db.QuerySingle(sql);
            +        if (row == null) return Unauthorized();
            +        var Token = _tokens.Create(row.Id);
            +        return Ok(Token);
                 }

            """,
            """
            {"summary":"Adds a login endpoint that looks users up by name and password. The query is vulnerable to SQL injection and compares plaintext passwords.","findings":[{"file":"src/Auth/LoginController.cs","line":12,"severity":"critical","message":"User input is interpolated into SQL, allowing SQL injection. Use a parameterised query."},{"file":"src/Auth/LoginController.cs","line":12,"severity":"critical","message":"Passwords are compared in plaintext, so they must be stored unhashed. Store a salted hash (e.g. PasswordHasher) and verify against it."},{"file":"src/Auth/LoginController.cs","line":15,"severity":"nit","message":"Local variable 'Token' should be camelCase: 'token'."}]}
            """),
        new(
            """
            --- README.md ---
            @@ -1,3 +1,4 @@
             # PRism
            +AI code review bot for GitHub pull requests.

             ## Setup

            """,
            """
            {"summary":"Documentation-only change adding a one-line project description to the README.","findings":[]}
            """)
    ];

    /// <summary>JSON Schema for Azure OpenAI structured outputs (strict mode: every property required, nulls explicit).</summary>
    public static JsonNode AzureResponseSchema() => JsonNode.Parse(
        $$"""
        {
          "type": "object",
          "properties": {
            "summary": { "type": "string" },
            "findings": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "file": { "type": "string" },
                  "line": { "type": ["integer", "null"] },
                  "severity": { "type": "string", "enum": {{SeverityEnumJson}} },
                  "message": { "type": "string" }
                },
                "required": ["file", "line", "severity", "message"],
                "additionalProperties": false
              }
            }
          },
          "required": ["summary", "findings"],
          "additionalProperties": false
        }
        """)!;

    /// <summary>Response schema in the OpenAPI subset that Gemini's generationConfig.responseSchema accepts.</summary>
    public static JsonNode GeminiResponseSchema() => JsonNode.Parse(
        $$"""
        {
          "type": "OBJECT",
          "properties": {
            "summary": { "type": "STRING" },
            "findings": {
              "type": "ARRAY",
              "items": {
                "type": "OBJECT",
                "properties": {
                  "file": { "type": "STRING" },
                  "line": { "type": "INTEGER", "nullable": true },
                  "severity": { "type": "STRING", "enum": {{SeverityEnumJson}} },
                  "message": { "type": "STRING" }
                },
                "required": ["file", "severity", "message"]
              }
            }
          },
          "required": ["summary", "findings"]
        }
        """)!;

    private static string SeverityEnumJson =>
        "[" + string.Join(", ", ReviewSeverity.All.Select(s => $"\"{s}\"")) + "]";
}
