using Cadmus.Graph;
using Cadmus.Graph.Adapters;
using Cadmus.Graph.Extras;
using Cadmus.GraphStudio.Api.Models;
using Cadmus.GraphStudio.Api.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Cadmus.Codicology.Mapping;
using Cadmus.Export.Mapping;

namespace Cadmus.GraphStudio.Api.Controllers;

[ApiController]
[Route("api/mappings")]
public sealed class MappingController : ControllerBase
{
    private readonly JsonGraphNodeMapper _mapper;

    public MappingController()
    {
        // a new mapper (with its RAM-based UID builder) for each request
        _mapper = new();
        _mapper.AddMacro("cod-loc", new CodLocationMacro());
    }

    private void SetMetadata(RunMappingBindingModel model)
    {
        _mapper.Data.Clear();

        // mock metadata from item, as set by ItemGraphSourceAdapter
        _mapper.Data[ItemGraphSourceAdapter.M_ITEM_ID] = model.ItemId
            ?? Guid.NewGuid().ToString();
        _mapper.Data[ItemGraphSourceAdapter.M_ITEM_FACET] =
            model.FacetId ?? "";

        if (model.ItemTitle != null)
            _mapper.Data[ItemGraphSourceAdapter.M_ITEM_TITLE] = model.ItemTitle;

        if (!string.IsNullOrEmpty(model.GroupId))
        {
            _mapper.Data[ItemGraphSourceAdapter.M_ITEM_GROUP] = model.GroupId;
            // the components of a composite group ID (item-group@1, ...)
            if (model.GroupId.Contains('/'))
            {
                int n = 0;
                foreach (string g in model.GroupId.Split('/'))
                {
                    _mapper.Data[
                        $"{ItemGraphSourceAdapter.M_ITEM_GROUP}@{++n}"] = g;
                }
            }
        }

        // from ItemEidMetadataSource (Cadmus.Graph.Extras)
        if (model.ItemEid != null)
            _mapper.Data[ItemEidMetadataSource.ITEM_EID_KEY] = model.ItemEid;
        if (model.MetadataPid != null)
        {
            _mapper.Data[ItemEidMetadataSource.METADATA_PART_ID_KEY] =
                model.MetadataPid;
        }

        // these are not set by Cadmus, but are available for tests
        if (model.Flags != null)
        {
            _mapper.Data["flags"] =
                model.Flags.Value.ToString(CultureInfo.InvariantCulture);
        }
        _mapper.Data["item-uri"] = model.ItemUri ?? "";
        _mapper.Data["item-label"] = model.ItemLabel ?? "";

        // mock metadata from part, as set by PartGraphSourceAdapter
        _mapper.Data[PartGraphSourceAdapter.M_PART_ID] = model.PartId
            ?? Guid.NewGuid().ToString();
        if (model.PartTypeId != null)
            _mapper.Data[PartGraphSourceAdapter.M_PART_TYPE_ID] = model.PartTypeId;
        if (model.RoleId != null)
            _mapper.Data[PartGraphSourceAdapter.M_PART_ROLE_ID] = model.RoleId;
    }

    [HttpPost("run")]
    public RunMappingResult Run([FromBody] RunMappingBindingModel model)
    {
        RunMappingResult result = new();
        try
        {
            GraphSet set = new();
            SetMetadata(model);

            // apply mappings in the order of their ordinal, and then of
            // their definition (as in the Cadmus graph database)
            foreach (NodeMappingBindingModel mapping in model.Mappings
                .Select((m, i) => (m, i))
                .OrderBy(t => t.m.Ordinal)
                .ThenBy(t => t.i)
                .Select(t => t.m))
            {
                NodeMapping m = mapping.ToNodeMapping();
                foreach (string warning in MappingFilterChecker.Check(m, model))
                {
                    result.Warnings.Add(
                        $"{m.Name}: in Cadmus this mapping would not be " +
                        $"applied to this source, because its {warning}.");
                }
                _mapper.Map(model.Source, m, set);
            }

            result.Value = set;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.ToString());
            result.Error = ex.Message;
        }
        return result;
    }
}
