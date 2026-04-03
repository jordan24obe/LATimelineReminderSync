using LATimelineReminderSync.Models;

namespace LATimelineReminderSync;

public interface IProcessWatcher : IDisposable
{
    event EventHandler<ProcessStartedEventArgs>? ProcessStarted;
}
