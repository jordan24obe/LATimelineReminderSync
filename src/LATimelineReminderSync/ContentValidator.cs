using LATimelineReminderSync.Models;

namespace LATimelineReminderSync;

public class ContentValidator : IContentValidator
{
    private const long MaxContentSizeBytes = 5 * 1024 * 1024; // 5 MB

    public ValidationResult Validate(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return new ValidationResult(false, "Content is empty or whitespace.");

        if (content.Length * sizeof(char) > MaxContentSizeBytes)
            return new ValidationResult(false, $"Content exceeds the maximum allowed size of 5 MB.");

        if (!content.Contains(Constants.BlockIdentifier, StringComparison.Ordinal))
            return new ValidationResult(false, $"Content does not contain the required structural marker '{Constants.BlockIdentifier}'.");

        return new ValidationResult(true, null);
    }
}
