using Cadmus.Core;
using System;
using System.Collections.Generic;

namespace Cadmus.Export;

/// <summary>
/// Flattener for parts containing a text representing the base text of
/// a set of layer parts.
/// </summary>
public interface ITextPartFlattener
{
    /// <summary>
    /// Starting from a text part and a list of layer parts, gets a tuple
    /// with the whole text and the corresponding ranges for each fragment in
    /// each text layer part.
    /// </summary>
    /// <param name="textPart">The text part used as the base text. This is
    /// the part identified by role ID <see cref="PartBase.BASE_TEXT_ROLE_ID"/>
    /// in an item.</param>
    /// <param name="layerParts">The layer parts you want to export.</param>
    /// <returns>Tuple with 1=text and 2=ranges.</returns>
    Tuple<string, IList<AnnotatedTextRange>> Flatten(IPart textPart,
        IList<IPart> layerParts);
}
