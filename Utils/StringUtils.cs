using System.Text.RegularExpressions;

namespace SourceGeneration.Utils;

public static partial class Utilities
{
    public static string GetAssetPath(string path, string modName)
    {
        string ret = Regex.Match(path, @$"(?=({modName}[{DirectorySeparators}])).*?(?=\.([a-z]+)$)").Value;
        ret = Regex.Replace(ret, @"[{DirectorySeparators}]", "/");

        return ret;
    }

    public static string Capitalize(string name) =>
        name[0].ToString().ToUpper() + name[1..];

    public static string Decapitalize(string name) =>
        name[0].ToString().ToLower() + name[1..];
}
