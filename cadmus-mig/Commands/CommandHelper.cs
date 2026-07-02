using System.IO;
using System.Text;

namespace Cadmus.Migration.Cli.Commands;

internal static class CommandHelper
{
    public static string LoadFileContent(string path)
    {
        using StreamReader reader = new(path, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
