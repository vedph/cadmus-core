namespace Cadmus.Index.Search;

/// <summary>
/// The target of a clause in an index query.
/// </summary>
public enum IndexQueryField
{
    /// <summary>A pin with a given name (see
    /// <see cref="IndexQueryClause.PinName"/>), matched against its value.
    /// </summary>
    Pin = 0,

    // item fields

    /// <summary>Item ID (<c>itemId</c>).</summary>
    ItemId,
    /// <summary>Item title (<c>title</c>, <c>t</c>).</summary>
    Title,
    /// <summary>Item description (<c>description</c>, <c>dsc</c>).</summary>
    Description,
    /// <summary>Item facet ID (<c>facetId</c>, <c>facet</c>).</summary>
    FacetId,
    /// <summary>Item group ID (<c>groupId</c>, <c>group</c>).</summary>
    GroupId,
    /// <summary>Item sort key (<c>sortKey</c>).</summary>
    SortKey,
    /// <summary>Item flags (<c>flags</c>).</summary>
    Flags,
    /// <summary>Item creator ID (<c>creatorId</c>).</summary>
    CreatorId,
    /// <summary>Item last user ID (<c>userId</c>).</summary>
    UserId,
    /// <summary>Item creation time (<c>timeCreated</c>).</summary>
    TimeCreated,
    /// <summary>Item last modified time (<c>timeModified</c>).</summary>
    TimeModified,

    // pin fields

    /// <summary>Pin's part ID (<c>partId</c>).</summary>
    PartId,
    /// <summary>Pin's part type ID (<c>partTypeId</c>, <c>partType</c>,
    /// <c>type</c>).</summary>
    PartTypeId,
    /// <summary>Pin's part role ID (<c>roleId</c>, <c>role</c>).</summary>
    RoleId,
    /// <summary>Pin's name (<c>name</c>, <c>n</c>).</summary>
    Name,
    /// <summary>Pin's value (<c>value</c>, <c>v</c>).</summary>
    Value,
    /// <summary>Pin's indexing time (<c>timeIndexed</c>, <c>time</c>).
    /// </summary>
    TimeIndexed,
}

/// <summary>
/// Extensions for <see cref="IndexQueryField"/>.
/// </summary>
public static class IndexQueryFieldExtensions
{
    /// <summary>
    /// Determines whether the field refers to a pin rather than to an item.
    /// Note that <see cref="IndexQueryField.ItemId"/> is an item field, even
    /// if pins also have it.
    /// </summary>
    /// <param name="field">The field.</param>
    /// <returns>True if pin field.</returns>
    public static bool IsPinField(this IndexQueryField field) =>
        field is IndexQueryField.Pin or IndexQueryField.PartId
            or IndexQueryField.PartTypeId or IndexQueryField.RoleId
            or IndexQueryField.Name or IndexQueryField.Value
            or IndexQueryField.TimeIndexed;

    /// <summary>
    /// Determines whether the field holds a date and time.
    /// </summary>
    /// <param name="field">The field.</param>
    /// <returns>True if time field.</returns>
    public static bool IsTimeField(this IndexQueryField field) =>
        field is IndexQueryField.TimeCreated or IndexQueryField.TimeModified
            or IndexQueryField.TimeIndexed;

    /// <summary>
    /// Determines whether the field is optional, i.e. can be null.
    /// </summary>
    /// <param name="field">The field.</param>
    /// <returns>True if nullable.</returns>
    public static bool IsNullable(this IndexQueryField field) =>
        field is IndexQueryField.GroupId or IndexQueryField.CreatorId
            or IndexQueryField.UserId or IndexQueryField.RoleId;
}
