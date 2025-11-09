using Microsoft.CodeAnalysis;
using ZourceGen.DataStructures;
using ZourceGen.Utils;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace ZourceGen.Assets;

internal abstract class AssetGenerator
{
    public abstract string[] FileExtensions { get; }

    #region Methods

    public void AddSource(SourceProductionContext context, ImmutableArray<AssetFile> assets, string assemblyName)
    {
        if (assets.Length <= 0)
            return;

        IEnumerable<GeneratedFile> files = Write(assets, assemblyName);

        foreach (GeneratedFile file in files)
            context.AddSource(file);
    }

    protected virtual IEnumerable<GeneratedFile> Write(ImmutableArray<AssetFile> assets, string assemblyName) =>
        [];

    #endregion
}
