using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Cadmus.Export.ML.Test;

internal sealed class RamTeiOffItemComposer : TeiOffItemComposer,
    IItemComposer
{
    protected override void EnsureWriter(string key)
    {
        if (Output?.Writers.ContainsKey(key) != false) return;
        Output.Writers[key] =
            new StreamWriter(new MemoryStream(), Encoding.UTF8);
    }

    public IDictionary<string, string> GetFlows()
    {
        Dictionary<string, string> flows = [];
        if (Output != null)
        {
            foreach (var p in Output.Writers)
            {
                p.Value.Flush();
                MemoryStream ms = (MemoryStream)((StreamWriter)p.Value).BaseStream;
                flows[p.Key] = Encoding.UTF8.GetString(ms.GetBuffer());
            }
        }
        return flows;
    }
}
