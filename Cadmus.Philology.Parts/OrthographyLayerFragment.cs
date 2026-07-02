using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Cadmus.Core;
using Cadmus.Core.Layers;
using Cadmus.Philology.Tools;
using Fusi.Tools.Configuration;

namespace Cadmus.Philology.Parts;

/// <summary>
/// Orthography layer fragment, used to mark deviations from the
/// orthographical norm.
/// <para>Tag: <c>fr.it.vedph.orthography</c>.</para>
/// </summary>
/// <seealso cref="ITextLayerFragment" />
[Tag("fr.it.vedph.orthography")]
public sealed class OrthographyLayerFragment : ITextLayerFragment
{
    /// <summary>
    /// Gets or sets the location of this fragment.
    /// </summary>
    /// <remarks>
    /// The location can be expressed in different ways according to the
    /// text coordinates system being adopted. For instance, it might be a
    /// simple token-based coordinates system (e.g. 1.2=second token of
    /// first block), or a more complex system like an XPath expression.
    /// </remarks>
    public string Location { get; set; } = "";

    /// <summary>
    /// Gets or sets the reference form for the word linked to this fragment.
    /// If you are using standard orthography in the base text, this is the
    /// attested form; otherwise, it is the standard orthography form.
    /// </summary>
    public string? Reference { get; set; }

    /// <summary>
    /// The language of the form, if applicable. This can be a BCP 47 code
    /// or similar, usually from thesaurus <c>orthography-languages</c>, and
    /// is used when the language of the form is different from the main or
    /// default language.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Generic tags to classify or group fragments.
    /// </summary>
    public List<string>? Tags { get; set; }

    /// <summary>
    /// A generic free text note.
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// Gets or sets the operations describing the relationship between the
    /// <see cref="Reference"/> form and the orthographically deviated form.
    /// Each operation is a text representing a <see cref="MspOperation"/>,
    /// to be parsed by <see cref="MspOperation.Parse(string)"/>.
    /// </summary>
    public List<string> Operations { get; set; } = [];

    /// <summary>
    /// True if the base text is the target of operations; false if the
    /// reference form is the target. This determines the source and target
    /// forms used by operations.
    /// </summary>
    public bool IsTextTarget { get; set; }

    private List<EditOperation> ParseOperations()
    {
        List<EditOperation> operations = [];
        try
        {
            foreach (string text in Operations)
                operations.Add(EditOperation.ParseOperation(text));
        }
        catch (Exception)
        {
        }
        return operations;
    }

    /// <summary>
    /// Get all the pins exposed by the implementor.
    /// </summary>
    /// <param name="item">The optional item. The item with its parts
    /// can optionally be passed to this method for those parts requiring
    /// to access further data.</param>
    /// <remarks>If operations have tags, the operations with tags are
    /// grouped by them, and a pin is returned for each group, with its name
    /// equal to <c>fr.msp-TAG-count</c>, and its value equal to the count of
    /// the operations with that tag. These pins are sorted by their name.
    /// <para>Also, if <paramref name="item"/> is received and it has
    /// a base text part and an orthography layer part, two additional pins
    /// are returned: <c>fr.orth-txt</c> with the original orthography
    /// got from the base text, and <c>fr.orth.ref</c> with the
    /// <see cref="Reference"/> orthography from this fragment.</para>
    /// </remarks>
    /// <returns>The pins.</returns>
    public IEnumerable<DataPin> GetDataPins(IItem? item = null)
    {
        List<DataPin> pins = [];

        // parse operations to extract pins data
        List<EditOperation> operations = ParseOperations();

        if (operations?.Count > 0)
        {
            HashSet<string> tags = [];
            Dictionary<string, int> counts = [];
            foreach (var operation in operations)
            {
                foreach (string tag in operation.Tags)
                {
                    tags.Add(tag);
                    if (!counts.TryGetValue(tag, out int value)) counts[tag] = 1;
                    else counts[tag] = ++value;
                }
            }

            pins.AddRange(
                from tag in tags
                orderby tag
                select new DataPin
                {
                    Name = PartBase.FR_PREFIX + $"msp-{tag}-count",
                    Value = $"{counts[tag]}"
                });
        }

        if (item != null)
        {
            // get the base text part
            IPart? textPart = item.Parts
                .Find(p => p.RoleId == PartBase.BASE_TEXT_ROLE_ID);
            if (textPart == null) return pins;

            // get the orthography layer
            TagAttribute attr = GetType().GetTypeInfo()
                .GetCustomAttribute<TagAttribute>()!;
            Regex roleIdRegex = new("^" + attr.Tag + "(?::.+)?$");

            if (item.Parts.Find(p => p.RoleId != null
                    && roleIdRegex.IsMatch(p.RoleId))
                is not IHasFragments<OrthographyLayerFragment> layerPart)
            {
                return pins;
            }

            string? baseText = layerPart.GetTextAt(textPart, Location);
            if (baseText != null)
            {
                pins.Add(new DataPin
                {
                    Name = PartBase.FR_PREFIX + "orth-txt",
                    Value = baseText
                });
                pins.Add(new DataPin
                {
                    Name = PartBase.FR_PREFIX + "orth-ref",
                    Value = Reference
                });
            }
        }

        // language
        if (!string.IsNullOrEmpty(Language))
        {
            pins.Add(new DataPin
            {
                Name = PartBase.FR_PREFIX + "orth-lng",
                Value = Language
            });
        }

        // tags
        if (Tags?.Count > 0)
        {
            foreach (string tag in Tags)
            {
                pins.Add(new DataPin
                {
                    Name = PartBase.FR_PREFIX + "orth-tag",
                    Value = tag
                });
            }
        }

        return pins;
    }

    /// <summary>
    /// Gets the definitions of data pins used by the implementor.
    /// </summary>
    /// <returns>Data pins definitions.</returns>
    public IList<DataPinDefinition> GetDataPinDefinitions()
    {
        return new List<DataPinDefinition>(
        [
            new DataPinDefinition(DataPinValueType.Integer,
                PartBase.FR_PREFIX + "msp-{TAG}-count",
                "The count of each type of misspelling operations."),
            new DataPinDefinition(DataPinValueType.String,
                PartBase.FR_PREFIX + "orth-txt",
                "The original orthography from the base text."),
            new DataPinDefinition(DataPinValueType.String,
                PartBase.FR_PREFIX + "orth-ref",
                "The reference orthography from the fragment."),
            new DataPinDefinition(DataPinValueType.String,
                PartBase.FR_PREFIX + "orth-lng",
                "The language of the misspelled form."),
            new DataPinDefinition(DataPinValueType.String,
                PartBase.FR_PREFIX + "orth-tag",
                "The general tags for the orthography entry.",
                "M"),
        ]);
    }

    /// <summary>
    /// Returns a <see cref="string" /> that represents this instance.
    /// </summary>
    /// <returns>
    /// A <see cref="string" /> that represents this instance.
    /// </returns>
    public override string ToString()
    {
        return $"[Orthography] {Location} {Reference} " +
               (Operations != null ?
               Operations.Count.ToString(CultureInfo.InvariantCulture) : "");
    }
}
