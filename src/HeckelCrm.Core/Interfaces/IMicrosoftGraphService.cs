namespace HeckelCrm.Core.Interfaces;

public interface IMicrosoftGraphService
{
    /// <summary>
    /// Checks if Microsoft Graph is configured. Returns true if TenantId, ClientId, and ClientSecret are configured.
    /// </summary>
    bool IsConfigured();
    
    Task<string> CreateCalendarEventAsync(CalendarEventData eventData, CancellationToken cancellationToken = default);
    Task UpdateCalendarEventAsync(string eventId, CalendarEventData eventData, CancellationToken cancellationToken = default);
    Task DeleteCalendarEventAsync(string eventId, CancellationToken cancellationToken = default);
    Task<IEnumerable<CalendarEventData>> GetCalendarEventsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<CalendarEventData?> GetCalendarEventAsync(string eventId, CancellationToken cancellationToken = default);
    Task<IEnumerable<TimeSlot>> GetAvailableTimeSlotsAsync(DateTime date, CancellationToken cancellationToken = default);
}

public record CalendarEventData(
    string Subject,
    string Body,
    DateTime Start,
    DateTime End,
    string? Location,
    List<string> Attendees,
    bool IsOnlineMeeting = false
);

public record TimeSlot(
    DateTime Start,
    DateTime End,
    bool IsAvailable
);

