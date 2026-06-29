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

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await dogService.DeleteAsync(CurrentUserId, id, ct);
        return FromResult(result);
    }
}
