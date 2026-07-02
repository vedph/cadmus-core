using Cadmus.Core;
using Cadmus.Core.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Text.Json;

namespace Cadmus.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/settings")]
public sealed class SettingsController(IRepositoryProvider repositoryProvider)
    : ControllerBase
{
    private readonly IRepositoryProvider _repositoryProvider = repositoryProvider ??
        throw new ArgumentNullException(nameof(repositoryProvider));

    /// <summary>
    /// Gets the setting with the specified ID.
    /// </summary>
    /// <returns>JSON code representing the settings, equal to <c>{}</c> if
    /// not found.</returns>
    [HttpGet("{id:regex(^(?!import$).+$)}")]
    [Produces("application/json")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public IActionResult Get(string id)
    {
        ICadmusRepository repository =
            _repositoryProvider.CreateRepository();
        string? json = repository.GetSetting(id);
        if (json == null) return Ok(JsonDocument.Parse("{}"));

        try
        {
            JsonDocument document = JsonDocument.Parse(json);
            return Ok(document.RootElement);
        }
        catch (JsonException ex)
        {
            return BadRequest("Invalid JSON format for setting with ID " +
                $"{id}: {ex.Message}");
        }
    }

    /// <summary>
    /// Adds or updates the specified setting.
    /// </summary>
    /// <param name="id">The setting's identifier.</param>
    /// <param name="value">The JSON code representing the setting.</param>
    [HttpPost("{id:regex(^(?!import$).+$)}")]
    [ProducesResponseType(200)]
    public ActionResult Add(string id, [FromBody] string value)
    {
        ICadmusRepository repository =
            _repositoryProvider.CreateRepository();

        repository.AddSetting(id, value);

        return Ok();
    }

    /// <summary>
    /// Deletes the setting with the specified identifier.
    /// </summary>
    /// <param name="id">The setting's identifier.</param>
    [HttpDelete("{id:regex(^(?!import$).+$)}")]
    public void Delete(string id)
    {
        ICadmusRepository repository =
            _repositoryProvider.CreateRepository();
        repository.DeleteSetting(id);
    }
}
