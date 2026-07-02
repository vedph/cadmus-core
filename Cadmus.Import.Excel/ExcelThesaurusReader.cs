using Cadmus.Core.Config;
using NPOI.SS.UserModel;
using System;
using System.IO;

namespace Cadmus.Import.Excel;

/// <summary>
/// Excel (XLS or XLSX) thesaurus reader.
/// </summary>
/// <seealso cref="IThesaurusReader" />
public sealed class ExcelThesaurusReader : IThesaurusReader
{
    private readonly IWorkbook _workbook;
    private readonly ExcelThesaurusReaderOptions _options;
    private bool _disposed;
    private int _rowIndex;

    /// <summary>
    /// Gets the current thesaurus if any.
    /// </summary>
    public Thesaurus? Current { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExcelThesaurusReader"/>
    /// class.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <exception cref="ArgumentNullException">stream</exception>
    public ExcelThesaurusReader(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        _workbook = WorkbookFactory.Create(stream);
        _options = new();
        _rowIndex = -1;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExcelThesaurusReader"/>
    /// class.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">stream or options</exception>
    public ExcelThesaurusReader(Stream stream,
        ExcelThesaurusReaderOptions options)
    {
        ArgumentNullException.ThrowIfNull(stream);

        _workbook = WorkbookFactory.Create(stream);
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _rowIndex = -1;
    }

    /// <summary>
    /// Read the next thesaurus entry from source.
    /// </summary>
    /// <returns>Thesaurus, or null if no more thesauri in source.</returns>
    /// <exception cref="InvalidOperationException">
    /// Expected thesaurus ID, Expected thesaurus entry ID.
    /// </exception>
    public bool Next()
    {
        ISheet sheet = _workbook.GetSheetAt(_options.SheetIndex);

        // skip top N rows if requested
        if (_rowIndex == -1 && _options.RowOffset > 0)
            _rowIndex = _options.RowOffset;

        Thesaurus? thesaurus = null;

        while (_rowIndex <= sheet.LastRowNum)
        {
            // read next row
            IRow? row = sheet.GetRow(_rowIndex);
            if (row == null) break;

            // read thesaurus ID
            string? thesaurusId = row.GetCell(_options.ColumnOffset)?.StringCellValue;

            // create thesaurus if this is the first row read
            if (thesaurus == null)
            {
                if (thesaurusId == null)
                {
                    throw new InvalidOperationException("Expected thesaurus ID " +
                        $"at {_rowIndex + 1},{_options.ColumnOffset}");
                }
                thesaurus = new Thesaurus(thesaurusId);
            }
            // else if any thesaurus ID is found (if not, we assume the previous one)
            // stop if this ID is different from the current thesaurus ID
            else if (!string.IsNullOrEmpty(thesaurusId) &&
                     thesaurus.Id != thesaurusId)
            {
                break;
            }

            // 1=id
            string? id = row.GetCell(_options.ColumnOffset + 1)?.StringCellValue;

            // 2=value
            string? val = row.GetCell(_options.ColumnOffset + 2)?.StringCellValue;

            // 3=target id
            string? targetId = row.GetCell(_options.ColumnOffset + 3)?.StringCellValue;
            if (targetId != null)
            {
                thesaurus.TargetId = targetId;
            }
            else
            {
                if (id == null)
                {
                    throw new InvalidOperationException(
                        $"Expected thesaurus {thesaurus.Id} entry ID " +
                        $"at {_rowIndex + 1},{_options.ColumnOffset + 1}");
                }
                thesaurus.AddEntry(new ThesaurusEntry
                {
                    Id = id, Value = val ?? ""
                });
            }
            _rowIndex++;
        }

        if (thesaurus == null)
        {
            Current = null;
            return false;
        }
        else
        {
            Current = thesaurus;
            return true;
        }
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _workbook.Dispose();
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing,
    /// or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Options for <see cref="ExcelThesaurusReader"/>.
/// </summary>
public class ExcelThesaurusReaderOptions
{
    /// <summary>
    /// Gets or sets the index of the sheet.
    /// </summary>
    public int SheetIndex { get; set; }

    /// <summary>
    /// Gets or sets the row offset.
    /// </summary>
    public int RowOffset { get; set; }

    /// <summary>
    /// Gets or sets the column offset.
    /// </summary>
    public int ColumnOffset { get; set; }
}
