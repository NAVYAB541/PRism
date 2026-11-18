namespace PRism.Api.Services;

public interface IAiReviewService
{
    /// <summary>Generates a structured review for the diff, validates it, and returns the markdown comment to post.</summary>
    Task<string> ReviewAsync(string diff);
}
