using Eventely.Api.Common;
using Eventely.Api.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eventely.Api.Features.Events;

[ApiController]
[Route("api/events")]
[Produces("application/json")]
public sealed class EventsController(IEventService eventService) : ControllerBase
{
    /// <summary>List events with creator details, images, and participant counts.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<PagedResponse<EventResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResponse<EventResponse>>> GetMany(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await eventService.GetManyAsync(page, pageSize, cancellationToken));
    }

    /// <summary>Return an event and its creator, image list, and participant count.</summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType<EventResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await eventService.GetByIdAsync(id, cancellationToken));
    }

    /// <summary>Create an event with a required cover image and up to ten detail images.</summary>
    [HttpPost]
    [Authorize(Roles = UserRoles.EventCreator)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<EventResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    [RequestSizeLimit(60 * 1024 * 1024)]
    public async Task<ActionResult<EventResponse>> Create(
        [FromForm] CreateEventRequest request,
        CancellationToken cancellationToken)
    {
        var created = await eventService.CreateAsync(CurrentUser.GetId(User), request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Update an event created by the authenticated EventCreator.</summary>
    /// <remarks>Detail images are replaced only when ReplaceDetailImages is true. Set it true with no files to clear the existing list.</remarks>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = UserRoles.EventCreator)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<EventResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [RequestSizeLimit(60 * 1024 * 1024)]
    public async Task<ActionResult<EventResponse>> Update(
        Guid id,
        [FromForm] UpdateEventRequest request,
        CancellationToken cancellationToken)
    {
        var updated = await eventService.UpdateAsync(CurrentUser.GetId(User), id, request, cancellationToken);
        return Ok(updated);
    }

    /// <summary>Delete an event created by the authenticated EventCreator.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = UserRoles.EventCreator)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await eventService.DeleteAsync(CurrentUser.GetId(User), id, cancellationToken);
        return NoContent();
    }

    /// <summary>Join an event as any authenticated role.</summary>
    [HttpPost("{id:guid}/join")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Join(Guid id, CancellationToken cancellationToken)
    {
        await eventService.JoinAsync(CurrentUser.GetId(User), id, cancellationToken);
        return NoContent();
    }

    /// <summary>Leave an event as any authenticated role.</summary>
    [HttpDelete("{id:guid}/join")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Leave(Guid id, CancellationToken cancellationToken)
    {
        await eventService.LeaveAsync(CurrentUser.GetId(User), id, cancellationToken);
        return NoContent();
    }
}
