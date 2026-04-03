namespace LATimelineReminderSync.Models;

public record WriteResult(bool Success, string? ErrorMessage, int AttemptsUsed);
