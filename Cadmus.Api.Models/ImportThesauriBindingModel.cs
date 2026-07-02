using System.ComponentModel.DataAnnotations;

namespace Cadmus.Api.Models;

/// <summary>
/// Import thesauri binding model.
/// </summary>
public class ImportThesauriBindingModel : ImportBindingModel
{
    /// <summary>
    /// The sheet number (1-N), used when importing from Excel.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? ExcelSheet { get; set; }

    /// <summary>
    /// The start row number, used when importing from Excel.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? ExcelRow { get; set; }

    /// <summary>
    /// The start column number, used when importing from Excel.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? ExcelColumn { get; set; }
}
