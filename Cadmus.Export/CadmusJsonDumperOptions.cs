using System;
using System.IO;

namespace Cadmus.Export;

/// <summary>
/// Options for <see cref="CadmusMongoJsonDumper"/>.
/// </summary>
public class CadmusJsonDumperOptions : CadmusMongoDataFramerOptions
{
    /// <summary>
    /// The output directory where the exported items will be saved.
    /// </summary>
    public string OutputDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
        DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd"));

    /// <summary>
    /// The maximum number of items to export. If not specified (0), all items
    /// will be exported in a single file.
    /// </summary>
    public int MaxItemsPerFile { get; set; }

    /// <summary>
    /// True to indent the output JSON files.
    /// </summary>
    public bool Indented { get; set; }
 }
