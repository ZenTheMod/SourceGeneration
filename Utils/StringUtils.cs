namespace SourceGeneration.Utils;

public static partial class Utilities
{
    public static string Capitalize(this string name) =>
        name[0].ToString().ToUpper() + name[1..];

    public static string Decapitalize(this string name) =>
        name[0].ToString().ToLower() + name[1..];
}
