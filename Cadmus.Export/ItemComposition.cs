using Fusi.Tools;
using System;
using System.Collections.Generic;
using System.IO;

namespace Cadmus.Export;

/// <summary>
/// Result of an <see cref="IItemComposer"/>. This combines a generic data
/// dictionary with a set of text writers. You can derive from this class
/// to define more specialized results.
/// </summary>
/// <seealso cref="DataDictionary" />
/// <seealso cref="IDisposable" />
public class ItemComposition : DataDictionary, IDisposable
{
    private bool _disposed;

    /// <summary>
    /// Gets the writers used in this composition. Each text writer has
    /// a string ID.
    /// </summary>
    public IDictionary<string, TextWriter> Writers { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemComposition"/> class.
    /// </summary>
    public ItemComposition()
    {
        Writers = new Dictionary<string, TextWriter>();
    }

    /// <summary>
    /// Flushes all the writers at once.
    /// </summary>
    /// <param name="close">True to close and remove all the writers
    /// after flushing them.</param>
    public void FlushWriters(bool close = false)
    {
        foreach (TextWriter writer in Writers.Values)
        {
            writer.Flush();
            if (close) writer.Close();
        }
        if (close) Writers.Clear();
    }

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and
    /// unmanaged resources; <c>false</c> to release only unmanaged resources.
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing) FlushWriters(true);
            _disposed = true;
        }
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing,
    /// releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
