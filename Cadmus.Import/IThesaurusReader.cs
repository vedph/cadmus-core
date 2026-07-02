using Cadmus.Core.Config;
using System;

namespace Cadmus.Import;

/// <summary>
/// A generic thesaurus reader.
/// </summary>
/// <seealso cref="IDisposable" />
public interface IThesaurusReader : IDisposable
{
    /// <summary>
    /// The current thesaurus read from source, or null.
    /// </summary>
    public Thesaurus? Current { get; }

    /// <summary>
    /// Read the next thesaurus entry from source.
    /// </summary>
    /// <returns>True if read, false if no more thesauri in source.</returns>
    bool Next();
}