using System.Net.Http;
using System.Text;
using System.Text.Json;
using HeckelCrm.Core.Interfaces;
using TimeSlot = HeckelCrm.Core.Interfaces.TimeSlot;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Authentication;
using Microsoft.Graph.Models;
using Microsoft.Identity.Client;

namespace HeckelCrm.Infrastructure.Services;

public class MicrosoftGraphService : IMicrosoftGraphService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<MicrosoftGraphService> _logger;
    private readonly GraphServiceClient? _graphClient;

    public MicrosoftGraphService(IConfiguration configuration, ILogger<MicrosoftGraphService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _graphClient = InitializeGraphClient();
    }

    public bool IsConfigured()
    {
        var tenantId = _configuration["MicrosoftGraph:TenantId"];
        var clientId = _configuration["MicrosoftGraph:ClientId"];
        var clientSecret = _configuration["MicrosoftGraph:ClientSecret"];
        return !string.IsNullOrEmpty(tenantId) && !string.IsNullOrEmpty(clientId) && !string.IsNullOrEmpty(clientSecret);
    }

    private GraphServiceClient? InitializeGraphClient()
    {
        var tenantId = _configuration["MicrosoftGraph:TenantId"];
        var clientId = _configuration["MicrosoftGraph:ClientId"];
        var clientSecret = _configuration["MicrosoftGraph:ClientSecret"];

        if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            _logger.LogWarning("Microsoft Graph configuration is missing. Calendar sync will be disabled.");
            return null;
        }

        try
        {
            var confidentialClientApplication = ConfidentialClientApplicationBuilder
                .Create(clientId)
                .WithTenantId(tenantId)
                .WithClientSecret(clientSecret)
                .Build();

            // Create GraphServiceClient with token credential
            var tokenCredential = new TokenCredentialProvider(confidentialClientApplication);
            return new GraphServiceClient(tokenCredential);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Microsoft Graph client");
            return null;
        }
    }

    public async Task<string> CreateCalendarEventAsync(CalendarEventData eventData, CancellationToken cancellationToken = default)
    {
        if (_graphClient == null)
        {
            _logger.LogWarning("Microsoft Graph is not configured. Skipping calendar event creation.");
            return string.Empty;
        }

        try
        {
            var calendarEvent = new Event
            {
                Subject = eventData.Subject,
                Body = new ItemBody
                {
                    ContentType = BodyType.Html,
                    Content = eventData.Body
                },
                Start = new DateTimeTimeZone
                {
                    DateTime = eventData.Start.ToString("yyyy-MM-ddTHH:mm:ss"),
                    TimeZone = "Europe/Berlin"
                },
                End = new DateTimeTimeZone
                {
                    DateTime = eventData.End.ToString("yyyy-MM-ddTHH:mm:ss"),
                    TimeZone = "Europe/Berlin"
                },
                Location = eventData.Location != null ? new Location
                {
                    DisplayName = eventData.Location
                } : null,
                Attendees = eventData.Attendees.Select(email => new Attendee
                {
                    EmailAddress = new EmailAddress
                    {
                        Address = email,
                        Name = email
                    },
                    Type = AttendeeType.Required
                }).ToList(),
                IsOnlineMeeting = eventData.IsOnlineMeeting
            };

            var createdEvent = await _graphClient.Me.Events.PostAsync(calendarEvent, cancellationToken: cancellationToken);
            
            if (createdEvent?.Id == null)
            {
                throw new InvalidOperationException("Event ID not returned from Microsoft Graph");
            }
            
            _logger.LogInformation("Created M365 calendar event: {EventId}", createdEvent.Id);
            return createdEvent.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create calendar event in M365");
            throw;
        }
    }

    public async Task UpdateCalendarEventAsync(string eventId, CalendarEventData eventData, CancellationToken cancellationToken = default)
    {
        if (_graphClient == null)
        {
            _logger.LogWarning("Microsoft Graph is not configured. Skipping calendar event update.");
            return;
        }

        try
        {
            var calendarEvent = new Event
            {
                Subject = eventData.Subject,
                Body = new ItemBody
                {
                    ContentType = BodyType.Html,
                    Content = eventData.Body
                },
                Start = new DateTimeTimeZone
                {
                    DateTime = eventData.Start.ToString("yyyy-MM-ddTHH:mm:ss"),
                    TimeZone = "Europe/Berlin"
                },
                End = new DateTimeTimeZone
                {
                    DateTime = eventData.End.ToString("yyyy-MM-ddTHH:mm:ss"),
                    TimeZone = "Europe/Berlin"
                },
                Location = eventData.Location != null ? new Location
                {
                    DisplayName = eventData.Location
                } : null
            };

            await _graphClient.Me.Events[eventId].PatchAsync(calendarEvent, cancellationToken: cancellationToken);
            _logger.LogInformation("Updated M365 calendar event: {EventId}", eventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update calendar event {EventId} in M365", eventId);
            throw;
        }
    }

    public async Task DeleteCalendarEventAsync(string eventId, CancellationToken cancellationToken = default)
    {
        if (_graphClient == null)
        {
            _logger.LogWarning("Microsoft Graph is not configured. Skipping calendar event deletion.");
            return;
        }

        try
        {
            await _graphClient.Me.Events[eventId].DeleteAsync(cancellationToken: cancellationToken);
            _logger.LogInformation("Deleted M365 calendar event: {EventId}", eventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete calendar event {EventId} from M365", eventId);
            throw;
        }
    }

    public async Task<IEnumerable<CalendarEventData>> GetCalendarEventsAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        if (_graphClient == null)
        {
            return Enumerable.Empty<CalendarEventData>();
        }

        try
        {
            var requestConfiguration = new Microsoft.Graph.Me.Events.EventsRequestBuilder.EventsRequestBuilderGetRequestConfiguration
            {
                QueryParameters = new Microsoft.Graph.Me.Events.EventsRequestBuilder.EventsRequestBuilderGetQueryParameters
                {
                    Filter = $"start/dateTime ge '{startDate:yyyy-MM-ddTHH:mm:ss}' and end/dateTime le '{endDate:yyyy-MM-ddTHH:mm:ss}'",
                    Orderby = new[] { "start/dateTime" }
                }
            };

            var events = await _graphClient.Me.Events.GetAsync((requestConfiguration) => 
            {
                requestConfiguration.QueryParameters = new Microsoft.Graph.Me.Events.EventsRequestBuilder.EventsRequestBuilderGetQueryParameters
                {
                    Filter = $"start/dateTime ge '{startDate:yyyy-MM-ddTHH:mm:ss}' and end/dateTime le '{endDate:yyyy-MM-ddTHH:mm:ss}'",
                    Orderby = new[] { "start/dateTime" }
                };
            }, cancellationToken);
            
            return events?.Value?.Select(e => new CalendarEventData(
                e.Subject ?? string.Empty,
                e.Body?.Content ?? string.Empty,
                ParseDateTime(e.Start),
                ParseDateTime(e.End),
                e.Location?.DisplayName,
                e.Attendees?.Select(a => a.EmailAddress?.Address ?? string.Empty).Where(a => !string.IsNullOrEmpty(a)).ToList() ?? new List<string>(),
                e.IsOnlineMeeting ?? false
            )) ?? Enumerable.Empty<CalendarEventData>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get calendar events from M365");
            return Enumerable.Empty<CalendarEventData>();
        }
    }

    public async Task<CalendarEventData?> GetCalendarEventAsync(string eventId, CancellationToken cancellationToken = default)
    {
        if (_graphClient == null)
        {
            return null;
        }

        try
        {
            var calendarEvent = await _graphClient.Me.Events[eventId].GetAsync(cancellationToken: cancellationToken);
            if (calendarEvent == null) return null;

            return new CalendarEventData(
                calendarEvent.Subject ?? string.Empty,
                calendarEvent.Body?.Content ?? string.Empty,
                ParseDateTime(calendarEvent.Start),
                ParseDateTime(calendarEvent.End),
                calendarEvent.Location?.DisplayName,
                calendarEvent.Attendees?.Select(a => a.EmailAddress?.Address ?? string.Empty).Where(a => !string.IsNullOrEmpty(a)).ToList() ?? new List<string>(),
                calendarEvent.IsOnlineMeeting ?? false
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get calendar event {EventId} from M365", eventId);
            return null;
        }
    }

    public async Task<IEnumerable<TimeSlot>> GetAvailableTimeSlotsAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        if (_graphClient == null)
        {
            _logger.LogWarning("Microsoft Graph is not configured. Returning default time slots.");
            // Return default business hours (9:00 - 17:00) in 1-hour slots
            return GenerateDefaultTimeSlots(date);
        }

        var startOfDay = date.Date;
        var endOfDay = startOfDay.AddDays(1);

        var existingEvents = await GetCalendarEventsAsync(startOfDay, endOfDay, cancellationToken);
        var bookedSlots = existingEvents.Select(e => new { e.Start, e.End }).ToList();

        // Generate time slots (9:00 - 17:00, 30-minute intervals)
        var availableSlots = new List<TimeSlot>();
        var currentTime = startOfDay.AddHours(9); // Start at 9:00 AM
        var endTime = startOfDay.AddHours(17); // End at 5:00 PM

        while (currentTime < endTime)
        {
            var slotEnd = currentTime.AddMinutes(30);
            var isBooked = bookedSlots.Any(bs => 
                (currentTime >= bs.Start && currentTime < bs.End) ||
                (slotEnd > bs.Start && slotEnd <= bs.End) ||
                (currentTime <= bs.Start && slotEnd >= bs.End)
            );

            availableSlots.Add(new TimeSlot(currentTime, slotEnd, !isBooked));
            currentTime = slotEnd;
        }

        return availableSlots;
    }

    private IEnumerable<TimeSlot> GenerateDefaultTimeSlots(DateTime date)
    {
        var slots = new List<TimeSlot>();
        var startHour = 9;
        var endHour = 17;

        for (int hour = startHour; hour < endHour; hour++)
        {
            var start = date.Date.AddHours(hour);
            var end = start.AddHours(1);
            
            // Skip if time is in the past
            if (start > DateTime.UtcNow)
            {
                slots.Add(new TimeSlot(start, end, true));
            }
        }

        return slots;
    }

    private static DateTime ParseDateTime(DateTimeTimeZone? dateTimeTimeZone)
    {
        if (dateTimeTimeZone?.DateTime == null)
        {
            return DateTime.UtcNow;
        }

        if (DateTime.TryParse(dateTimeTimeZone.DateTime, out var parsedDate))
        {
            return parsedDate;
        }

        return DateTime.UtcNow;
    }
}

// Helper class for authentication using Azure.Identity pattern
public class TokenCredentialProvider : Azure.Core.TokenCredential
{
    private readonly IConfidentialClientApplication _app;

    public TokenCredentialProvider(IConfidentialClientApplication app)
    {
        _app = app;
    }

    public override async ValueTask<Azure.Core.AccessToken> GetTokenAsync(Azure.Core.TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        var scopes = new[] { "https://graph.microsoft.com/.default" };
        var result = await _app.AcquireTokenForClient(scopes).ExecuteAsync(cancellationToken);
        return new Azure.Core.AccessToken(result.AccessToken, result.ExpiresOn);
    }

    public override Azure.Core.AccessToken GetToken(Azure.Core.TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        return GetTokenAsync(requestContext, cancellationToken).GetAwaiter().GetResult();
    }
}

