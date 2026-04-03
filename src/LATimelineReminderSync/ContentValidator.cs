using LATimelineReminderSync.Models;

namespace LATimelineReminderSync;

public class ContentValidator : IContentValidator
{
    private const long MaxContentSizeBytes = 5 * 1024 * 1024; // 5 MB

    /// <summary>
    /// Validates the full SavedVariables file content.
    /// Checks for the root variable (LiquidRemindersSaved) as a structural marker.
    /// </summary>
    public ValidationResult Validate(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return new ValidationResult(false, "Content is empty or whitespace.");

        if (content.Length * sizeof(char) > MaxContentSizeBytes)
            return new ValidationResult(false, "Content exceeds the maximum allowed size of 5 MB.");

        if (!content.Contains(Constants.BlockIdentifier, StringComparison.Ordinal))
            return new ValidationResult(false,
                $"Content does not contain the required structural marker '{Constants.BlockIdentifier}'.");

        return new ValidationResult(true, null);
    }

    /// <summary>
    /// Validates an individual encounter snippet fetched from GitHub.
    /// Checks for reminder-like content markers: ["reminders"] or ["trigger"] or ["options"].
    /// </summary>
    public ValidationResult ValidateEncounterSnippet(string snippet)
    {
        if (string.IsNullOrWhiteSpace(snippet))
            return new ValidationResult(false, "Encounter snippet is empty or whitespace.");

        if (!snippet.Contains("[\"reminders\"]", StringComparison.Ordinal) &&
            !snippet.Contains("[\"options\"]", StringComparison.Ordinal) &&
            !snippet.Contains("[\"trigger\"]", StringComparison.Ordinal))
            return new ValidationResult(false,
                "Encounter snippet does not contain expected keys ([\"reminders\"], [\"options\"], or [\"trigger\"]).");

        return new ValidationResult(true, null);
    }
}
