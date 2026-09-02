using Dogity.Application.Dogs;
using Microsoft.AspNetCore.Mvc;

namespace Dogity.Api.Controllers;

[Route("api/dogs")]
public class DogsController(IDogService dogService) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DogDto>>> GetMyDogs(CancellationToken ct)
    {
        var result = await dogService.GetMyDogsAsync(CurrentUserId, ct);
        return FromResult(result);
    }

    [HttpGet("supervised")]
    public async Task<ActionResult<IReadOnlyList<SupervisedDogDto>>> GetSupervised(CancellationToken ct)
    {
        var result = await dogService.GetSupervisedDogsAsync(CurrentUserId, ct);
        return FromResult(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DogDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await dogService.GetByIdAsync(CurrentUserId, id, ct);
        return FromResult(result);
    }

    [HttpPost]
    public async Task<ActionResult<DogDto>> Create(CreateDogRequest request, CancellationToken ct)
    {
        var result = await dogService.CreateAsync(CurrentUserId, request, ct);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors });

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DogDto>> Update(Guid id, UpdateDogRequest request, CancellationToken ct)
    {
        var result = await dogService.UpdateAsync(CurrentUserId, id, request, ct);
        return FromResult(result);
    }

    [HttpPut("{id:guid}/archive")]
    public async Task<IActionResult> SetArchived(Guid id, ArchiveDogRequest request, CancellationToken ct)
    {
        var result = await dogService.SetArchivedAsync(CurrentUserId, id, request.Archived, ct);
        return FromResult(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await dogService.DeleteAsync(CurrentUserId, id, ct);
        return FromResult(result);
    }

    /// <summary>
    /// Profilbild als Data-URI - direkt in ein img-Element hängbar. 204, wenn
    /// keines hinterlegt ist; das Frontend zeigt dann das Platzhalter-Symbol.
    ///
    /// Beantwortet einen bedingten Abruf (If-None-Match) mit 304 und ohne
    /// Rumpf. Das Bild wiegt als Data-URI rund 64 KB und hing bisher an jedem
    /// Aufbau jeder Liste, in der der Hund vorkommt: Die Antwort trug kein
    /// ETag, der Browser konnte sie nicht behalten, und eine Trainerübersicht
    /// mit einem Dutzend Hunden lud bei jedem Besuch rund ein Megabyte neu.
    /// </summary>
    [HttpGet("{id:guid}/image")]
    public async Task<ActionResult<DogImageDto>> GetImage(Guid id, CancellationToken ct)
    {
        // Zuerst nur das Kennzeichen holen - das liest die Bilddaten nicht mit.
        var etag = await dogService.GetImageETagAsync(CurrentUserId, id, ct);
        // Wie bisher: kein Zugriff und kein hinterlegtes Bild sind für den
        // Aufrufer dasselbe - 204, das Frontend zeigt das Platzhalter-Symbol.
        if (!etag.Succeeded) return NoContent();

        // Private, nicht öffentlich zwischenspeicherbar: Das Bild hängt an der
        // Zugriffsprüfung des angemeldeten Nutzers, ein geteilter Cache
        // (Proxy) dürfte es nicht weiterreichen. must-revalidate, damit der
        // Browser den Wechsel eines Bildes sofort bemerkt statt es
        // auszusitzen - die Ersparnis kommt aus dem 304, nicht aus einer
        // Verfallszeit.
        Response.Headers.CacheControl = "private, no-cache, must-revalidate";
        Response.Headers.ETag = etag.Value;

        if (Request.Headers.IfNoneMatch.Contains(etag.Value))
            return StatusCode(StatusCodes.Status304NotModified);

        var result = await dogService.GetImageAsync(CurrentUserId, id, ct);
        return result.Succeeded ? Ok(result.Value) : NoContent();
    }

    [HttpPut("{id:guid}/image")]
    public async Task<IActionResult> SetImage(Guid id, DogImageDto request, CancellationToken ct)
    {
        var result = await dogService.SetImageAsync(CurrentUserId, id, request.DataUrl, ct);
        return FromResult(result);
    }

    [HttpDelete("{id:guid}/image")]
    public async Task<IActionResult> DeleteImage(Guid id, CancellationToken ct)
    {
        var result = await dogService.DeleteImageAsync(CurrentUserId, id, ct);
        return FromResult(result);
    }

    [HttpGet("{id:guid}/owners")]
    public async Task<ActionResult<IReadOnlyList<DogOwnerDto>>> GetOwners(Guid id, CancellationToken ct)
    {
        var result = await dogService.GetOwnersAsync(CurrentUserId, id, ct);
        return FromResult(result);
    }

    [HttpPost("{id:guid}/owners")]
    public async Task<IActionResult> AddOwner(Guid id, AddDogOwnerRequest request, CancellationToken ct)
    {
        var result = await dogService.AddOwnerAsync(CurrentUserId, id, request, ct);
        return FromResult(result);
    }

    [HttpDelete("{id:guid}/owners/{userId:guid}")]
    public async Task<IActionResult> RemoveOwner(Guid id, Guid userId, CancellationToken ct)
    {
        var result = await dogService.RemoveOwnerAsync(CurrentUserId, id, userId, ct);
        return FromResult(result);
    }
}
