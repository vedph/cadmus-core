using System.Text;

namespace Cadmus.Graph;

/// <summary>
/// A node mapping used to build RDF nodes and triples from a source object.
/// This is the concrete, RDF-specific implementation of <see cref="NodeMapping"/>.
/// </summary>
/// <seealso cref="NodeMapping" />
public sealed class GraphNodeMapping : NodeMapping
{
    /// <summary>
    /// The output of this mapping.
    /// </summary>
    public NodeMappingOutput? Output { get; set; }

    /// <summary>
    /// Clones this instance, deep copying all the <see cref="NodeMapping"/>'s.
    /// </summary>
    /// <returns>New mapping object.</returns>
    public override GraphNodeMapping Clone()
    {
        GraphNodeMapping clone = new() { Output = Output };
        CopyTo(clone);
        return clone;
    }

    /// <summary>
    /// Appends the output summary to the string representation built by
    /// <see cref="NodeMapping.ToString"/>.
    /// </summary>
    /// <param name="sb">The target string builder.</param>
    protected override void AppendExtra(StringBuilder sb)
    {
        if (Output != null) sb.Append(" -> ").Append(Output);
    }
}
