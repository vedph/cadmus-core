using Cadmus.Api.Models.Preview;
using Cadmus.Export;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Proteus.Rendering;
using System.Collections.Generic;

namespace Cadmus.Api.Controllers;

/// <summary>
/// Preview controller.
/// </summary>
/// <seealso cref="Controller" />
[Authorize]
[ApiController]
[Route("api/preview")]
public sealed class PreviewController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly CadmusPreviewer _previewer;

    private bool IsPreviewEnabled() =>
        _previewer != null &&
        _configuration.GetValue<bool>("Preview:IsEnabled");

    public PreviewController(IConfiguration configuration,
        CadmusPreviewer previewer)
    {
        _configuration = configuration;
        _previewer = previewer;
    }

    /// <summary>
    /// Gets all the Cadmus objects keys registered for preview.
    /// </summary>
    /// <param name="type">The type of component key to get: <c>F</c>
    /// =text flatteners, <c>C</c>=item composers, <c>J</c>=JSON renderers.
    /// </param>
    /// <returns>List of unique keys.</returns>
    [HttpGet("keys")]
    [ProducesResponseType(200)]
    public HashSet<string> GetKeys([FromQuery] char type)
    {
        if (!IsPreviewEnabled()) return [];
        return char.ToUpperInvariant(type) switch
        {
            'F' => _previewer.GetFlattenerKeys(),
            'C' => _previewer.GetComposerKeys(),
            _ => _previewer.GetJsonRendererKeys(),
        };
    }

    /// <summary>
    /// Renders the part with the specified ID.
    /// </summary>
    /// <param name="itemId">The item's identifier.</param>
    /// <param name="partId">The part's identifier.</param>
    /// <returns>Rendition or empty string.</returns>
    [HttpGet("items/{itemId}/parts/{partId}")]
    [ProducesResponseType(200)]
    public RenditionModel RenderPart([FromRoute] string itemId,
        [FromRoute] string partId)
    {
        if (!IsPreviewEnabled()) return new RenditionModel("");
        return new RenditionModel(_previewer.RenderPart(itemId, partId));
    }

    /// <summary>
    /// Renders the fragment at index <paramref name="frIndex"/> in the layer
    /// part with ID equal to <paramref name="partId"/>.
    /// </summary>
    /// <param name="itemId">The item's identifier.</param>
    /// <param name="partId">The layer part type identifier.</param>
    /// <param name="frIndex">The index of the fragment in the part (0-N).
    /// </param>
    /// <returns>Rendition or empty string.</returns>
    [HttpGet("items/{itemId}/parts/{partId}/{frIndex}")]
    [ProducesResponseType(200)]
    public RenditionModel RenderFragment([FromRoute] string itemId,
        [FromRoute] string partId,
        [FromRoute] int frIndex)
    {
        if (!IsPreviewEnabled()) return new RenditionModel("");

        return new RenditionModel(_previewer.RenderFragment(
            itemId, partId, frIndex));
    }

    /// <summary>
    /// Gets the text segments built by flattening the text part with the
    /// specified ID with all the layers specified.
    /// </summary>
    /// <param name="textPartId">The base text part's identifier.</param>
    /// <param name="layerPartId">The layer parts identifiers.</param>
    /// <returns>List of segments.</returns>
    [HttpGet("text-parts/{textPartId}")]
    [ProducesResponseType(200)]
    public IList<ExportedSegment> GetTextSegments([FromRoute] string textPartId,
        [FromQuery] string[] layerPartId)
    {
        if (!IsPreviewEnabled()) return [];

        return _previewer.BuildTextSegments(textPartId, layerPartId);
    }
}
