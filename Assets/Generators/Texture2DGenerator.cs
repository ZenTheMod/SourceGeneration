using Microsoft.CodeAnalysis;
using SourceGeneration.DataStructures;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SourceGeneration.Assets.Generators;

public sealed class Texture2DGenerator : AssetGenerator
{
    public override string[] FileExtensions => ["png"];

    protected override IEnumerable<GeneratedFile> Write(ImmutableArray<AdditionalText> images)
    {
        StringBuilder writer = new();

            // Group all textures by their path.
        AliasedList<string, AdditionalText> groupedPaths = new(images, i =>
            Regex.Match(i.Path, $"(?=({ExecutingAssemblyName}[{DirectorySeparators}])).*?(?=[{DirectorySeparators}]({Path.GetFileName(i.Path)})$)").Value);

        List<GeneratedFile> outputFiles = [];

        foreach ((HashSet<string> keys, List<AdditionalText> items) in groupedPaths)
        {
            string folder = keys.First();

            string outputPath = folder;

            writer.Append(Header);

            writer.Append(@$"
namespace {Regex.Replace(folder, $"[{DirectorySeparators}]", ".")};

public static class Textures
{{");
            HashSet<string> arrays = [];

            foreach (AdditionalText texture in items)
            {
                string name = CleanTextureName(texture.Path);

                if (!arrays.Add(name))
                    continue;

                string assetPath = GetAssetPath(texture.Path, ExecutingAssemblyName);

                string assetName = Capitalize(name);

                    // Handle texture arrays.
                List<string> arrayPaths = [.. items.Where(i => CleanTextureName(i.Path) == name).Select(i => GetAssetPath(i.Path, ExecutingAssemblyName))];

                if (arrayPaths.Count() > 1)
                {
                        // Sort the array based on the numbers in the file name.
                    var sortedPaths = GetSortedTexturePaths(arrayPaths);

                        // Arrays are a bit messy, unsure if this really works well.
                    writer.Append(@$"
    public static LazyAsset<Texture2D>[] {assetName} =
    [");

                    foreach (string subAssetPath in sortedPaths)
                        writer.Append(@$"
        new(""{subAssetPath}""),");

                    writer.AppendLine(@$"
    ];");

                    continue;
                }

                writer.AppendLine(@$"
    public static LazyAsset<Texture2D> {assetName} = new(""{assetPath}"");");
            }

            writer.Append(@$"}}");

            outputFiles.Add(new(outputPath + "Textures.cs", writer.ToString()));

            writer.Clear();
        }


        return outputFiles;
    }

    #region Private Methods

    private static string CleanTextureName(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);

            // Remove all numbers from the files name.
                // TODO: Add cases for other invalid characters.
        name = Regex.Replace(name, "[0-9]", string.Empty);

        return name;
    }

    private static IOrderedEnumerable<string> GetSortedTexturePaths(IEnumerable<string> paths) =>
        paths.OrderBy(
            s => int.Parse(
                string.Concat(Regex.Matches(Path.GetFileNameWithoutExtension(s), "[0-9]")
                .OfType<Match>()
                .Select(m => m.ToString())
                )));

    #endregion
}
