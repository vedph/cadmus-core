using Fusi.Tools;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace Cadmus.Export.Mapping;

/// <summary>
/// Base class for node mappers.
/// </summary>
public abstract class NodeMapper : DataDictionary
{
    /// <summary>
    /// The name of the mapper's applied mappings list in metadata.
    /// </summary>
    public const string APPLIED_MAPPING_LIST = "applied-mapping-list";

    private readonly Dictionary<string, INodeMappingMacro> _macros;

    /// <summary>
    /// Gets or sets the optional logger to use.
    /// </summary>
    public ILogger? Logger { get; set; }

    /// <summary>
    /// Gets or sets the optional macro functions eventually used to resolve
    /// placeholders during the mapping process. Each macro gets an object
    /// representing the mapping context, and returns a computed value.
    /// </summary>
    protected IDictionary<string, INodeMappingMacro> Macros => _macros;

    /// <summary>
    /// The object representing the mapping source context, usually
    /// corresponding to the context of mapping's source, like an
    /// item and/or a part.
    /// The source is directly passed to <see cref="INodeMapper{TTarget}.Map"/>;
    /// this rather refers to the source's context. For instance, when
    /// mapping a part you would still need to know about its parent item.
    /// </summary>
    public MapperSource? Context { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether applied mappings tracing is
    /// enabled. This property is used by implementors to trace the list of
    /// applied mappings in the metadata, under key
    /// <see cref="APPLIED_MAPPING_LIST"/>.
    /// </summary>
    public bool IsMappingTracingEnabled { get; set; }

    /// <summary>
    /// Creates a new instance of the <see cref="NodeMapper"/> object.
    /// </summary>
    protected NodeMapper()
    {
        _macros = [];

        // setup builtin macros
        ResetMacros();
    }

    /// <summary>
    /// Adds the specified macro.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="macro">The macro.</param>
    /// <exception cref="ArgumentNullException">key or macro</exception>
    public void AddMacro(string key, INodeMappingMacro macro)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(macro);

        _macros[key] = macro;
    }

    /// <summary>
    /// Deletes the macro with the specified key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <exception cref="ArgumentNullException">key</exception>
    public void DeleteMacro(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        _macros.Remove(key);
    }

    /// <summary>
    /// Sets the macros to the specified ones, removing any existing macro.
    /// </summary>
    /// <param name="macros">The macros to set.</param>
    /// <exception cref="ArgumentNullException">macros</exception>
    public void SetMacros(IDictionary<string, INodeMappingMacro> macros)
    {
        ArgumentNullException.ThrowIfNull(macros);

        _macros.Clear();
        foreach (var p in macros) _macros[p.Key] = p.Value;
    }

    /// <summary>
    /// Resets the macros to the builtin ones.
    /// </summary>
    public void ResetMacros()
    {
        _macros.Clear();
        _macros["_hdate"] = new HistoricalDateMacro();
        _macros["_substring"] = new SubstringMacro();
    }

    /// <summary>
    /// Resolves the specified macro.
    /// </summary>
    /// <param name="macro">The macro to resolve.</param>
    /// <returns>The resolved macro value.</returns>
    protected string ResolveMacro(string macro)
    {
        // syntax of placeholder's value for macro is:
        // id or id(" & "-delimited args)
        int i = macro.IndexOf('(');

        string id;
        string[]? args = null;
        if (i == -1)
        {
            id = macro;
        }
        else
        {
            id = macro[..i];
            if (macro[^1] == ')') macro = macro[..^1];
            args = macro[(i + 1)..].Split(" & ");
        }

        return _macros.TryGetValue(id, out INodeMappingMacro? value)
            ? value.Run(Context, args) ?? ""
            : "";
    }

    /// <summary>
    /// Resolves the specified data expression.
    /// </summary>
    /// <param name="expression">The data expression to resolve.</param>
    /// <returns>The resolved data expression value.</returns>
    protected abstract string ResolveDataExpression(string expression);
}
