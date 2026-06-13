namespace PRism.Api.Services;

public interface IAiReviewService
{
    /// <summary>Generates a markdown code review for the given unified diff.</summary>
    Task<string> ReviewAsync(string diff);
}
