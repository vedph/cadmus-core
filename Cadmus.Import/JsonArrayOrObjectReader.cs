using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Cadmus.Import;

/// <summary>
/// A generic JSON reader for documents containing either an array of entries,
/// or a single entry. The type of entry is defined by the generic parameter T,
/// which must be a class having a parameterless constructor. The reader
/// </summary>
/// <typeparam name="T">The entry type.</typeparam>
public class JsonArrayOrObjectReader<T> : IDisposable
{
    private readonly JsonDocument _doc;
    private readonly JsonSerializerOptions _options;
    private IList<JsonElement>? _elements;
    private int _index;
    private bool _disposedValue;

    /// <summary>
    /// The current entry read from source, or null if no more entries in source.
    /// </summary>
    public T? Current { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonArrayOrObjectReader{T}"/> class.
    /// </summary>
    /// <param name="source">The source stream.</param>
    /// <exception cref="ArgumentNullException">source</exception>
    public JsonArrayOrObjectReader(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);

        using StreamReader reader = new(source, Encoding.UTF8);
        string json = reader.ReadToEnd();
        _doc = JsonDocument.Parse(json);
        _index = -1;
        _options = new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true
        };
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonArrayOrObjectReader{T}"/> class.
    /// </summary>
    /// <param name="json">The JSON code to read from.</param>
    /// <exception cref="ArgumentNullException">json</exception>
    public JsonArrayOrObjectReader(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        _doc = JsonDocument.Parse(json);
        _index = -1;
        _options = new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true
        };
    }

    /// <summary>
    /// Read into <see cref="Current"/> the next entry from source.
    /// </summary>
    /// <returns>
    /// True if read, false if no more entries in source.
    /// </returns>
    public bool Next()
    {
        if (_index == -1)
        {
            switch (_doc.RootElement.ValueKind)
            {
                case JsonValueKind.Array:
                    _elements = [.. _doc.RootElement.EnumerateArray()];
                    if (_elements.Count == 0) return default;
                    _index = 0;
                    Current = _elements[0].Deserialize<T>(_options);
                    return true;

                case JsonValueKind.Object:
                    _index = 0;
                    _elements = [];
                    Current = _doc.RootElement.Deserialize<T>(_options);
                    return true;

                default:
                    Current = default;
                    return false;
            }
        }
        else
        {
            _index++;
            if (_index >= _elements!.Count) return false;

            Current = _elements[_index].Deserialize<T>(_options);
            return true;
        }
    }

    private void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _doc.Dispose();
            }
            _disposedValue = true;
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
