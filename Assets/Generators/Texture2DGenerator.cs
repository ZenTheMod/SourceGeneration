using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ZourceGen.DataStructures;
using ZourceGen.Utils;

namespace ZourceGen.Assets.Generators;

public sealed class Texture2DGenerator : AssetGenerator
{
    public override string[] FileExtensions => ["png"];

    protected override IEnumerable<GeneratedFile> Write(ImmutableArray<AssetFile> textures, string assemblyName)
    {
        StringBuilder writer = new();

            // Group all textures by their abstract path.
        AliasedList<string, AssetFile> groupedPaths =
            new(textures, i => i.Directory);

        List<GeneratedFile> outputFiles = [];

        foreach ((HashSet<string> keys, List<AssetFile> items) in groupedPaths)
        {
            string folder = keys.First();

                // TODO: Less stupid way of checking this. :sob:
            if (items.First().InRoot)
                continue;

            string outputPath = folder;

            writer.Append(Header);

            writer.Append(@$"
using Microsoft.Xna.Framework.Graphics;

using {assemblyName}.{AssetNamespace}.DataStructures;

namespace {assemblyName}.{AssetNamespace}.{folder.Replace('/', '.')};

public static class Textures
{{");

            HashSet<string> arrays = [];

            foreach (AssetFile texture in items)
            {
                string name = texture.Name.CleanName();

                    // Don't add new properties for numbered items.
                if (!arrays.Add(name))
                    continue;

                string assetPath = texture.AssetPath;

                string assetName = name.Capitalize();

                    // Handle texture arrays.
                List<AssetFile> arrayItems = [.. items.Where(i => i.Name.CleanName() == name)];

                if (arrayItems.Count() > 1)
                {
                        // Sort the array based on the numbers in the file name.
                    string[] sortedPaths = GetSortedPaths(arrayItems);

                        // Arrays are a bit messy, unsure if this really works well.
                    writer.Append(@$"
    public static LazyAsset<Texture2D>[] {assetName} =
    [");

                    foreach (string path in sortedPaths)
                        writer.Append(@$"
        new(""{path}""),");

                    writer.AppendLine(@$"
    ];");

                    continue;
                }

                writer.AppendLine(@$"
    public static LazyAsset<Texture2D> {assetName} = new(""{assetPath}"");");
            }

            writer.Append(@$"}}");

            outputFiles.Add(new(Path.Combine(outputPath, "Textures.g.cs"), writer.ToString()));

            writer.Clear();
        }

        return outputFiles;
    }

    #region Private Methods

    private static string[] GetSortedPaths(IEnumerable<AssetFile> textures) =>
        [..
            textures.OrderBy(t =>
            {
                if (!int.TryParse(
                    string.Concat(Regex.Matches(t.Name, "[0-9]")
                    .OfType<Match>()
                    .Select(m => m.ToString())
                    ), out int result))
                    return result;

                return 0;
            })
            .Select(t => t.AssetPath)
        ];

    #endregion
}
