using Microsoft.CodeAnalysis;
using SourceGeneration.Assets.Generators;
using SourceGeneration.DataStructures;
using SourceGeneration.Utils;
using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;

namespace SourceGeneration.Assets;

[Generator(LanguageNames.CSharp)]
public sealed class AssetGeneration : IIncrementalGenerator
{
    #region Private Fields

    private const string BuildManifestFileName = "build.txt";

    private static readonly AssetGenerator[] Generators =
        [
            new Texture2DGenerator()
        ];

    #endregion

    #region Public Fields

    public const string AssetNamespace = "GeneratedAssets";

    #endregion

    #region Initialization

    void IIncrementalGenerator.Initialize(IncrementalGeneratorInitializationContext context)
    {
        var assemblyName = context.CompilationProvider
            .Select((compilation, _) => compilation.AssemblyName!);

        context.RegisterSourceOutput(assemblyName, static (context, assemblyName) =>
            context.AddSource(GenerateLazyAsset(assemblyName)));

            // Search for the build manifest (build.txt) file to grab a root directory.
        var projectRoot = context.AdditionalTextsProvider
            .Where(file => file.Path.EndsWith(BuildManifestFileName))
            .Collect()
            .Select(static (files, _) =>
                Path.GetDirectoryName(files[0].Path)
                    .Replace('\\', '/'));

        foreach (AssetGenerator generator in Generators)
        {
            var texts = context.AdditionalTextsProvider
                .Where(p =>
                    generator.FileExtensions.Any(ext => p.Path.EndsWith($".{ext}", StringComparison.OrdinalIgnoreCase)));

            var assets = texts
                .Combine(projectRoot)
                .Select((tuple, _) =>
                    new AssetFile(tuple.Left, tuple.Right))
                .Collect();

            IncrementalValueProvider<(
                ImmutableArray<AssetFile> Assets,
                string AssemblyName)> 
                tuple = assets.Combine(assemblyName);

            context.RegisterSourceOutput(tuple, (context, tuple) =>
                generator.AddSource(context, tuple.Assets, tuple.AssemblyName));
        }
    }

    #endregion

    #region Common

    private static GeneratedFile GenerateLazyAsset(string assemblyName)
    {
        StringBuilder writer = new();

        writer.Append(Header);
        writer.Append(@$"
using ReLogic.Content;
using System;
using Terraria.ModLoader;

namespace {assemblyName}.{AssetNamespace}.DataStructures;

/// <inheritdoc cref=""Asset{{T}}""/>
public readonly record struct LazyAsset<T> where T : class
{{
    #region Private Fields

    private readonly Lazy<Asset<T>> _asset;

    private readonly string _key;

    #endregion

    #region Public Properties

    public readonly Asset<T> Asset => 
        _asset.Value;

    /// <inheritdoc cref=""Asset{{T}}.Value""/>
    public readonly T Value => 
        Asset.Value;

    public readonly bool IsReady => 
        Asset is not null && 
        Value is not null && 
        Asset.IsLoaded &&
        !Asset.IsDisposed;

    public readonly string Key =>
        _key;

    #endregion

    #region Public Constructors

    /// <inheritdoc cref=""ModContent.Request{{T}}""/>
    public LazyAsset(string name)
    {{
        _key = name;
        _asset = new(() => ModContent.Request<T>(name));
    }}

    #endregion

    #region Public Operators

    public static implicit operator Asset<T>(LazyAsset<T> asset) =>
        asset.Asset;

    public static implicit operator T(LazyAsset<T> asset) =>
        asset.Value;

    #endregion
}}");

        return new($"DataStructures/LazyAsset.g.cs", writer.ToString());
    }

    #endregion
}
