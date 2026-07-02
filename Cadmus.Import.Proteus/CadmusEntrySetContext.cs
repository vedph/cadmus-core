using Cadmus.Core;
using Fusi.Tools.Configuration;
using Proteus.Core.Regions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Cadmus.Import.Proteus;

/// <summary>
/// Cadmus entry set context. This adds a list of imported items to the
/// base <see cref="EntrySetContext"/>, so that importers can collect items
/// and their parts.
/// <para>Tag: <c>it.vedph.entry-set-context.cadmus</c>.</para>
/// </summary>
[Tag("it.vedph.entry-set-context.cadmus")]
public class CadmusEntrySetContext : EntrySetContext
{
    /// <summary>
    /// Gets or sets the optional thesaurus entry map.
    /// </summary>
    public ThesaurusEntryMap? ThesaurusEntryMap { get; set; }

    /// <summary>
    /// Gets the items.
    /// </summary>
    public List<IItem> Items { get; } = [];

    /// <summary>
    /// Gets the current item, i.e. the last added item in <see cref="Items"/>
    /// or null if this is empty.
    /// </summary>
    public IItem? CurrentItem => Items.Count > 0 ? Items[^1] : null;

    /// <summary>
    /// Resets the collection to its initial state, optionally performing
    /// initialization logic.
    /// </summary>
    /// <param name="init">true to perform initialization logic during the reset;
    /// otherwise, false.</param>
    public override void Reset(bool init = false)
    {
        base.Reset(init);
        Items.Clear();
    }

    /// <summary>
    /// Clones this instance.
    /// </summary>
    /// <returns>Cloned context.</returns>
    public override IEntrySetContext Clone()
    {
        CadmusEntrySetContext context = (CadmusEntrySetContext)base.Clone();

        // copy items into a cloned list
        context.Items.AddRange(Items);
        return context;
    }

    /// <summary>
    /// Ensures that the a part of the specified type and role exists in the
    /// specified <paramref name="item"/>, creating and adding it to the item
    /// if not found. If a part with the same type and role already exists, it is
    /// returned unchanged.
    /// </summary>
    /// <typeparam name="T">The part's type.</typeparam>
    /// <param name="item">The item.</param>
    /// <param name="roleId">The role identifier.</param>
    /// <returns>The part.</returns>
    public static T EnsurePartForItem<T>(IItem item,
        string? roleId = null) where T : IPart, new()
    {
        ArgumentNullException.ThrowIfNull(item);

        IPart? part = item.Parts.OfType<T>().FirstOrDefault(p =>
            roleId == null || p.RoleId == roleId);

        if (part == null)
        {
            part = Activator.CreateInstance<T>();
            part.RoleId = roleId;
            part.ItemId = item.Id;
            part.CreatorId = item.CreatorId;
            part.UserId = item.UserId;
            item.Parts.Add(part);
        }

        return (T)part;
    }

    /// <summary>
    /// Ensures that the a part of the specified type and role exists in
    /// <see cref="CurrentItem"/>, creating and adding the part to it if not
    /// found. If a part with the same type and role already exists, it is
    /// returned unchanged.
    /// </summary>
    /// <typeparam name="T">The part's type.</typeparam>
    /// <param name="roleId">The role identifier.</param>
    /// <returns>The part.</returns>
    /// <exception cref="InvalidOperationException">No current item</exception>
    public T EnsurePartForCurrentItem<T>(string? roleId = null)
        where T : IPart, new()
    {
        if (CurrentItem == null)
            throw new InvalidOperationException("No current item");

        return EnsurePartForItem<T>(CurrentItem, roleId);
    }

    /// <summary>
    /// Converts to string.
    /// </summary>
    /// <returns>String.</returns>
    public override string ToString()
    {
        return $"{base.ToString()} I={Items.Count}";
    }
}
