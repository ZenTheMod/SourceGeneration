using System.Text.RegularExpressions;

namespace ZourceGen.Utils;

internal static partial class Utilities
{
    public static string Capitalize(this string name) =>
        name[0].ToString().ToUpper() + name[1..];

    public static string Decapitalize(this string name) =>
        name[0].ToString().ToLower() + name[1..];

        // TODO: Add cases for other invalid characters.
    public static string CleanName(this string name) =>
        Regex.Replace(name, "[0-9]", string.Empty);
}
