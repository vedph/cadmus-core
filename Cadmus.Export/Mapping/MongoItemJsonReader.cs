using Cadmus.Mongo;
using Fusi.Tools.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Cadmus.Export.Mapping;

/// <summary>
/// MongoDB implementation of <see cref="IItemJsonReader"/>.
/// <para>Tag: <c>item-json-reader.mongo</c>.</para>
/// </summary>
[Tag("item-json-reader.mongo")]
public sealed class MongoItemJsonReader : MongoConsumerBase, IItemJsonReader
{
    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public Task<JsonDocument?> ReadAsync(string itemId, ItemPartFilter? filter)
    {
        ArgumentNullException.ThrowIfNull(itemId);

        throw new NotImplementedException();
    }
}
