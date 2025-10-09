using Microsoft.CodeAnalysis;
using SourceGeneration.DataStructures;
using SourceGeneration.Utils;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace SourceGeneration.Assets;

public abstract class AssetGenerator
{
    public abstract string[] FileExtensions { get; }

    #region Methods

    public void AddSource(SourceProductionContext context, ImmutableArray<AssetFile> assets, string assemblyName)
    {
        IEnumerable<GeneratedFile> files = Write(assets, assemblyName);

        foreach (GeneratedFile file in files)
            context.AddSource(file);
    }

    protected virtual IEnumerable<GeneratedFile> Write(ImmutableArray<AssetFile> assets, string assemblyName) =>
        [];

    #endregion
}
