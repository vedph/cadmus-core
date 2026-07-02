using Cadmus.Api.Models;
using Cadmus.Core;
using Cadmus.Core.Config;
using Cadmus.Core.Storage;
using Cadmus.Import;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;

namespace Cadmus.Api.Controllers.Import;

/// <summary>
/// Facets import controller.
/// </summary>
/// <seealso cref="ControllerBase" />
[Authorize]
[ApiController]
[Tags("Facet")]
public sealed class FacetImportController : ControllerBase
{
    private readonly IRepositoryProvider _repositoryProvider;
    private readonly ILogger<FacetImportController> _logger;

    public FacetImportController(IRepositoryProvider repositoryProvider,
        ILogger<FacetImportController> logger)
    {
        _repositoryProvider = repositoryProvider;
        _logger = logger;
    }

    private static ImportUpdateMode GetMode(char c)
    {
        return char.ToUpperInvariant(c) switch
        {
            // for facets, patch is equivalent to synch
            'P' => ImportUpdateMode.Synch,
            'S' => ImportUpdateMode.Synch,
            _ => ImportUpdateMode.Replace,
        };
    }

    /// <summary>
    /// Uploads one or more facets importing them into the Cadmus database.
    /// </summary>
    /// <param name="file">The JSON file.</param>
    /// <param name="model">The import model.</param>
    /// <returns>Result.</returns>
    /// <exception cref="InvalidOperationException">No ID for facet</exception>
    [Authorize(Roles = "admin")]
    [HttpPost("api/facets/import")]
    public ImportResult UploadFacets(
        // https://github.com/domaindrivendev/Swashbuckle.AspNetCore#handle-forms-and-file-uploads
        IFormFile file,
        [FromQuery] ImportBindingModel model)
    {
        _logger?.LogInformation("User {UserName} importing facets from " +
            "{FileName} from {IP} (dry={IsDry})",
            User.Identity!.Name,
            file.FileName,
            HttpContext.Connection.RemoteIpAddress,
            model.DryRun == true);

        ICadmusRepository repository = _repositoryProvider.CreateRepository();

        try
        {
            using Stream stream = file.OpenReadStream();
            using JsonFacetReader reader = new(stream);

            ImportUpdateMode mode = GetMode(model.Mode?[0] ?? 'R');
            if (model.Mode?[0] is 'P' or 'p')
            {
                _logger?.LogInformation(
                    "Patch mode requested; using Synch (equivalent for facets)");
            }

            // read all facets first to ensure the file is valid before
            // making any changes to the repository
            List<FacetDefinition> newFacets = [];
            while ((reader.Next()))
            {
                FacetDefinition facet = reader.Current!;
                if (string.IsNullOrEmpty(facet.Id))
                    throw new InvalidOperationException("No ID for facet");
                newFacets.Add(facet);
            }

            _logger?.LogInformation("Read {Count} facet(s) from file",
                newFacets.Count);

            List<string> ids = [];

            // in synch mode, the facets imported will replace all the existing
            // facets, which means that we first delete all the old ones, and
            // then add the new ones; in replace mode, instead, we just update
            // or add the facets in the source, but we do not delete those that
            // are not in it
            if (mode == ImportUpdateMode.Synch && model.DryRun != true)
            {
                _logger?.LogInformation("Deleting old facets...");
                IList<FacetDefinition> oldFacets =
                    repository.GetFacetDefinitions();
                foreach (FacetDefinition facet in oldFacets)
                {
                    _logger?.LogInformation("  - deleting facet ID: {Id}",
                        facet.Id);
                    repository.DeleteFacetDefinition(facet.Id);
                }
                _logger?.LogInformation("Old facets deleted ({Count})",
                    oldFacets.Count);
            }

            foreach (FacetDefinition facet in newFacets)
            {
                _logger?.LogInformation("Importing facet ID: {Id}", facet.Id);
                ids.Add(facet.Id);
                if (model.DryRun != true) repository.AddFacetDefinition(facet);
            }

            _logger?.LogInformation("Import completed: {Count} facet(s)",
                ids.Count);

            return new ImportResult
            {
                ImportedIds = ids
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error importing facets: {Message}",
                ex.Message);
            return new ImportResult
            {
                Error = ex.Message
            };
        }
    }
}
