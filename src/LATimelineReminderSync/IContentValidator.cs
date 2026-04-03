using LATimelineReminderSync.Models;

namespace LATimelineReminderSync;

public interface IContentValidator
{
    ValidationResult Validate(string content);
}
