using System.Collections.Generic;
using System.Text;

namespace Cadmus.Export.Json;

/// <summary>
/// A filter for item parts. This is a utility class used to determine which
/// item parts should be loaded as the source of an export.
/// </summary>
public class ItemPartFilter
{
    /// <summary>
    /// True if the filter is inverted, i.e. it will match all parts except those
    /// in the clauses; false if it will match only those in the clauses.
    /// </summary>
    public bool IsInverted { get; set; }

    /// <summary>
    /// Gets or sets the clauses. Each clause is a filter for a part.
    /// </summary>
    public List<ItemPartFilterClause> Clauses = [];
}

/// <summary>
/// A filter clause for <see cref="ItemPartFilter"/>.
/// </summary>
public class ItemPartFilterClause
{
    /// <summary>
    /// The part type ID. If null, any part type is matched.
    /// </summary>
    public string? TypeId { get; set; }

    /// <summary>
    /// The part role ID. If null, any part role is matched. If empty, only
    /// parts with no role are matched.
    /// </summary>
    public string? RoleId { get; set; }

    /// <summary>
    /// Returns a string representation of the filter clause.
    /// </summary>
    /// <returns>String.</returns>
    public override string ToString()
    {
        StringBuilder sb = new();
        sb.Append(TypeId ?? "*");
        if (RoleId != null)
        {
            sb.Append(':');
            sb.Append(RoleId);
        }
        
        return sb.ToString();
    }
}
