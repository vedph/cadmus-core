using Cadmus.Graph;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Cadmus.GraphStudio.Api.Models;

public sealed class NodeMappingBindingModel
{
    /// <summary>
    /// Gets or sets a numeric identifier for this mapping. This is
    /// assigned when the mapping is archived in a database.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the parent mapping's identifier. This is assigned
    /// when the mapping is archived in a database.
    /// </summary>
    public int ParentId { get; set; }

    /// <summary>
    /// Gets or sets an optional ordinal value used to define the order
    /// of application of sibling mappings. Default is 0.
    /// </summary>
    public int Ordinal { get; set; }

    /// <summary>
    /// Gets or sets the mapping's human friendly name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// The type of the source object mapped by this mapping. This is
    /// meaningful for the root mapping only.
    /// </summary>
    [Required]
    public int SourceType { get; set; }

    /// <summary>
    /// The optional item's facet filter.
    /// </summary>
    public string? FacetFilter { get; set; }

    /// <summary>
    /// The optional item's group filter.
    /// </summary>
    public string? GroupFilter { get; set; }

    /// <summary>
    /// The optional item's flags filter.
    /// </summary>
    public int? FlagsFilter { get; set; }

    /// <summary>
    /// The optional item's title filter.
    /// </summary>
    public string? TitleFilter { get; set; }

    /// <summary>
    /// The optional part's type ID filter.
    /// </summary>
    public string? PartTypeFilter { get; set; }

    /// <summary>
    /// The optional part's role filter.
    /// </summary>
    public string? PartRoleFilter { get; set; }

    /// <summary>
    /// A short description of this mapping.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The source expression representing the data selected by this mapping.
    /// </summary>
    [Required(AllowEmptyStrings = true)]
    public string Source { get; set; }

    /// <summary>
    /// The template for building the SID for this mapping.
    /// </summary>
    public string? Sid { get; set; }

    /// <summary>
    /// Gets or sets the scalar pattern.
    /// </summary>
    public string? ScalarPattern { get; set; }

    /// <summary>
    /// The output of this mapping.
    /// </summary>
    public NodeMappingOutputBindingModel? Output { get; set; }

    /// <summary>
    /// The optional children mappings of this mapping.
    /// </summary>
    public List<NodeMappingBindingModel>? Children { get; set; }

    public NodeMappingBindingModel()
    {
        Source = "";
    }

    public NodeMapping ToNodeMapping()
    {
        return new NodeMapping
        {
            Id = Id,
            ParentId = ParentId,
            Ordinal = Ordinal,
            Name = Name,
            SourceType = SourceType,
            FacetFilter = FacetFilter,
            GroupFilter = GroupFilter,
            FlagsFilter = FlagsFilter,
            TitleFilter = TitleFilter,
            PartTypeFilter = PartTypeFilter,
            PartRoleFilter = PartRoleFilter,
            Description = Description,
            Source = Source,
            Sid = Sid,
            ScalarPattern = ScalarPattern,
            Output = Output?.ToNodeMappingOutput(),
            Children = Children?.Select(m => m.ToNodeMapping())?.ToArray()
                ?? Array.Empty<NodeMapping>()
        };
    }
}
