using Microsoft.CodeAnalysis;
using SourceGeneration.Assets.Generators;
using SourceGeneration.DataStructures;
using SourceGeneration.Utils;
using System;
using System.Linq;
using System.Text;

namespace SourceGeneration.Assets;

[Generator(LanguageNames.CSharp)]
public sealed class AssetGeneration : IIncrementalGenerator
{
    #region Private Fields

    private static readonly AssetGenerator[] Generators =
        [
            new Texture2DGenerator()
        ];

    #endregion

    #region Public Fields

    public const string Name = "GeneratedAssets";

    #endregion

    #region Initialization

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput((context) =>
        {
            context.AddSource(GenerateGlobals());
            context.AddSource(GenerateLazyAsset());

            foreach (AssetGenerator generator in Generators)
                generator.AddPostInitializationSource(context);
        });

        foreach (AssetGenerator generator in Generators)
        {
            var files = context.AdditionalTextsProvider
            .Where(p =>
                generator.FileExtensions.Any(ext => p.Path.EndsWith($".{ext}", StringComparison.OrdinalIgnoreCase)))
            .Collect();

            context.RegisterSourceOutput(files, generator.AddSource);
        }
    }

    #endregion

    #region Common

    private static GeneratedFile GenerateGlobals()
    {
        StringBuilder writer = new();

        writer.Append(Header);
        writer.Append(@$"
global using {Name};");

        return new("GlobalUsings.cs", writer.ToString());
    }

    private static GeneratedFile GenerateLazyAsset()
    {
        StringBuilder writer = new();

        writer.Append(Header);
        writer.Append(@$"
using ReLogic.Content;
using System;
using Terraria.ModLoader;

namespace {Name}.Core.DataStructures;

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

        return new("Core/DataStructures/LazyAsset.cs", writer.ToString());
    }

    #endregion
}
