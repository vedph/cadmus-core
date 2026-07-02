using Cadmus.Codicology.Graph;
using Cadmus.Graph;
using Cadmus.Graph.Adapters;
using Cadmus.Graph.Extras;
using Cadmus.GraphStudio.Api.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Diagnostics;

namespace Cadmus.GraphStudio.Api.Controllers;

[ApiController]
[Route("api/mappings")]
public sealed class MappingController : ControllerBase
{
    private readonly JsonNodeMapper _mapper;

    public MappingController()
    {
        _mapper = new();
        _mapper.AddMacro("cod-loc", new CodLocationMacro());
    }

    [HttpPost("run")]
    public ErrorWrapper<GraphSet> Run([FromBody] RunMappingBindingModel model)
    {
        try
        {
            // setup context
            GraphSet set = new();
            _mapper.Data.Clear();

            // mock metadata from item
            _mapper.Data[ItemGraphSourceAdapter.M_ITEM_ID] = model.ItemId
                ?? Guid.NewGuid().ToString();
            _mapper.Data[ItemGraphSourceAdapter.M_ITEM_FACET] =
                model.FacetId ?? "";

            if (model.ItemTitle != null)
                _mapper.Data[ItemGraphSourceAdapter.M_ITEM_TITLE] = model.ItemTitle;

            if (model.GroupId != null)
                _mapper.Data[ItemGraphSourceAdapter.M_ITEM_GROUP] = model.GroupId;

            // this is an extension
            if (model.ItemEid != null)
                _mapper.Data[ItemEidMetadataSource.ITEM_EID_KEY] = model.ItemEid;
            if (model.MetadataPid != null)
            {
                _mapper.Data[ItemEidMetadataSource.METADATA_PART_ID_KEY] =
                    model.MetadataPid;
            }

            if (model.Flags != null) _mapper.Data["flags"] = model.Flags;
            _mapper.Data["item-uri"] = model.ItemUri ?? "";
            _mapper.Data["item-label"] = model.ItemLabel ?? "";

            // mock metadata from part
            _mapper.Data[PartGraphSourceAdapter.M_PART_ID] = model.PartId
                ?? Guid.NewGuid().ToString();
            if (model.PartTypeId != null)
                _mapper.Data[PartGraphSourceAdapter.M_PART_TYPE_ID] = model.PartTypeId;
            if (model.RoleId != null)
                _mapper.Data[PartGraphSourceAdapter.M_PART_ROLE_ID] = model.RoleId;

            // apply mappings
            foreach (NodeMappingBindingModel mapping in model.Mappings)
            {
                NodeMapping m = mapping.ToNodeMapping();
                _mapper.Map(model.Source, m, set);
            }

            return new ErrorWrapper<GraphSet> { Value = set };
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.ToString());
            return new ErrorWrapper<GraphSet> { Error = ex.Message };
        }
    }
}
