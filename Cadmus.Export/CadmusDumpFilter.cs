using Cadmus.Core.Storage;
using System.Collections.Generic;

namespace Cadmus.Export;

/// <summary>
/// Filter for <see cref="CadmusMongoDataFramer"/>.
/// </summary>
public class CadmusDumpFilter : ItemFilter
{
    /// <summary>
    /// The keys of the part types to include in the export. If not specified,
    /// all part types are included. If specified, only parts with these
    /// keys will be included in the export.
    /// Each key is in the format <c>typeId[:roleId]</c>.
    /// </summary>
    public List<string>? WhitePartTypeKeys { get; set; }

    /// <summary>
    /// The keys of the part types to exclude from the export. If not specified,
    /// no part types are excluded. If specified, parts with these keys will
    /// be excluded from the export.
    /// Each key is in the format <c>typeId[:roleId]</c>.
    /// </summary>
    public List<string>? BlackPartTypeKeys { get; set; }

    /// <summary>
    /// True if the filter is empty, meaning it does not specify any criteria.
    /// </summary>
    public bool IsEmpty =>
        (WhitePartTypeKeys?.Count ?? 0) == 0 &&
        (BlackPartTypeKeys?.Count ?? 0) == 0 &&
        string.IsNullOrEmpty(Title) &&
        string.IsNullOrEmpty(Description) &&
        string.IsNullOrEmpty(FacetId) &&
        string.IsNullOrEmpty(GroupId) &&
        Flags == null && FlagMatching == FlagMatching.BitsAllSet &&
        string.IsNullOrEmpty(UserId) &&
        MinModified == null && MaxModified == null;
}
