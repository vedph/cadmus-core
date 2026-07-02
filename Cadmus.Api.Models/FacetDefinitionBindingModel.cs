using Cadmus.Core.Config;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Cadmus.Api.Models;

/// <summary>
/// Facet definition binding model, used to create or update a facet definition.
/// </summary>
public class FacetDefinitionBindingModel
{
    /// <summary>
    /// The facet ID.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Id { get; set; } = "";

    /// <summary>
    /// The facet label.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Label { get; set; } = "";

    /// <summary>
    /// The facet description.
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// The facet color key (usually RRGGBB).
    /// </summary>
    [MaxLength(100)]
    public string? ColorKey { get; set; }

    /// <summary>
    /// The definitions of the parts assigned to this facet.
    /// </summary>
    public List<PartDefinition> PartDefinitions { get; set; } = [];

    /// <summary>
    /// Converts this instance to a <see cref="FacetDefinition"/> instance.
    /// </summary>
    /// <returns>Definition.</returns>
    public FacetDefinition ToFacetDefinition()
    {
        return new FacetDefinition
        {
            Id = Id,
            Label = Label,
            Description = Description,
            ColorKey = ColorKey,
            PartDefinitions = PartDefinitions
        };
    }

    /// <summary>
    /// Converts this instance to a string representation.
    /// </summary>
    /// <returns>String.</returns>
    public override string ToString()
    {
        return Id + ": " + Label + ((PartDefinitions.Count > 0)
            ? $" ({PartDefinitions.Count})" : "");
    }
}
