using Fusi.Antiquity.Chronology;
using System.Text;

namespace Cadmus.Refs.Bricks;

/// <summary>
/// A pair of temporal and spatial coordinates.
/// </summary>
public class Chronotope
{
    /// <summary>
    /// Gets or sets the optional tag. This is an arbitrary value used
    /// to classify or group chronotopes.
    /// </summary>
    public string? Tag { get; set; }

    /// <summary>
    /// Gets or sets the place. This can be some ID, or the place name
    /// following some conventions.
    /// </summary>
    public string? Place { get; set; }

    /// <summary>
    /// Gets or sets the date.
    /// </summary>
    public HistoricalDate? Date { get; set; }

    /// <summary>
    /// Converts to string.
    /// </summary>
    /// <returns>
    /// A <see cref="string" /> that represents this instance.
    /// </returns>
    public override string ToString()
    {
        StringBuilder sb = new();
        if (!string.IsNullOrEmpty(Tag))
            sb.Append('[').Append(Tag).Append("] ");

        if (!string.IsNullOrEmpty(Place)) sb.Append(Place);
        if (Date is not null)
        {
            if (!string.IsNullOrEmpty(Place)) sb.Append(", ");
            sb.Append(Date);
        }

        return sb.ToString();
    }
}
