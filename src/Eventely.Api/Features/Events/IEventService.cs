namespace Eventely.Api.Features.Events;

public interface IEventService
{
    Task<PagedResponse<EventResponse>> GetManyAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<EventResponse> GetByIdAsync(Guid eventId, CancellationToken cancellationToken);
    Task<EventResponse> CreateAsync(Guid creatorId, CreateEventRequest request, CancellationToken cancellationToken);
    Task<EventResponse> UpdateAsync(Guid userId, Guid eventId, UpdateEventRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid userId, Guid eventId, CancellationToken cancellationToken);
    Task JoinAsync(Guid userId, Guid eventId, CancellationToken cancellationToken);
    Task LeaveAsync(Guid userId, Guid eventId, CancellationToken cancellationToken);
}
