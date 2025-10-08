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

    public void AddPostInitializationSource(IncrementalGeneratorPostInitializationContext context)
    {
        IEnumerable<GeneratedFile> files = WritePostInitialization();

        foreach (GeneratedFile file in files)
            context.AddSource(file);
    }

    protected virtual IEnumerable<GeneratedFile> WritePostInitialization() =>
        [];

    public void AddSource(SourceProductionContext context, ImmutableArray<AdditionalText> texts)
    {
        IEnumerable<GeneratedFile> files = Write(texts);

        foreach (GeneratedFile file in files)
            context.AddSource(file);
    }

    protected virtual IEnumerable<GeneratedFile> Write(ImmutableArray<AdditionalText> texts) =>
        [];

    #endregion
}
