using Microsoft.CodeAnalysis;
using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using ZourceGen.Assets.Generators;
using ZourceGen.DataStructures;
using ZourceGen.Utils;

namespace ZourceGen.Assets;

[Generator(LanguageNames.CSharp)]
public sealed class AssetGeneration : IIncrementalGenerator
{
    #region Private Fields

    private const string BuildManifestFileName = "build.txt";

    private static readonly AssetGenerator[] Generators =
        [
            new EffectGenerator(),
            new OBJModelGenerator(),
            new Texture2DGenerator(),
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
        {
            context.AddSource(GenerateLazyAsset(assemblyName));

            context.AddSource(GenerateAssetReloader(assemblyName));
            context.AddSource(GenerateLocalAssetSource(assemblyName));
        });

            // Search for the build manifest (build.txt) file to grab a root directory.
        var projectRoot = context.AdditionalTextsProvider
            .Where(file => file.Path.EndsWith(BuildManifestFileName))
            .Collect()
            .Select(static (files, _) =>
                Path.GetDirectoryName(files[0].Path)!
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

    #region LazyAsset

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

        return new("DataStructures/LazyAsset.g.cs", writer.ToString());
    }

    #endregion

    #region AssetReloader

    private static GeneratedFile GenerateAssetReloader(string assemblyName)
    {
        StringBuilder writer = new();
        
        writer.Append(Header);
        writer.Append(@$"
using ReLogic.Content;
using ReLogic.Utilities;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using Terraria;

using Terraria.ModLoader;

using static System.IO.WatcherChangeTypes;
using static System.Reflection.BindingFlags;

namespace {assemblyName}.{AssetNamespace}.Debug;

#nullable enable

#if DEBUG

[Autoload(Side = ModSide.Client)]
public sealed class AssetReloader : ModSystem
{{
    #region Private Fields

    private static FieldInfo? AssetsInfo;

    private static FieldInfo? RequestLockInfo;

    private static MethodInfo? ForceReloadAssetInfo;

    private const NotifyFilters AllFilters =
        NotifyFilters.FileName |
        NotifyFilters.DirectoryName |
        NotifyFilters.Attributes |
        NotifyFilters.Size |
        NotifyFilters.LastWrite |
        NotifyFilters.LastAccess |
        NotifyFilters.CreationTime |
        NotifyFilters.Security;

    private static FileSystemWatcher? AssetWatcher;

    private static string ModSource = """";

    private static LocalAssetSource? AssetSource;

    #endregion

    #region Loading

    public override void PostSetupContent()
    {{
        try
        {{
            AssetsInfo = typeof(AssetRepository).GetField(""_assets"", NonPublic | Instance);

            RequestLockInfo = typeof(AssetRepository).GetField(""_requestLock"", NonPublic | Instance);

            MethodInfo[] repositoryMethods = typeof(AssetRepository).GetMethods(NonPublic | Instance);
            ForceReloadAssetInfo = repositoryMethods.Single(m => m.Name == ""ForceReloadAsset"" && !m.IsGenericMethod);

            ModSource = Mod.SourceFolder.Replace('\\', '/');

            if (!Directory.Exists(ModSource))
                throw new DirectoryNotFoundException(""Mod source directory does not exsist; this warning should not be present for mod consumers!"");

            AssetSource = new(ModSource);

            ChangeContentSource(ModSource);

            AssetReaderCollection assetReaderCollection = Main.instance.Services.Get<AssetReaderCollection>();

            string[] extensions = assetReaderCollection.GetSupportedExtensions();

            AssetWatcher = new(ModSource);

            foreach (string e in extensions)
                AssetWatcher.Filters.Add($""*{{e}}"");

            AssetWatcher.Changed += AssetChanged;

            AssetWatcher.NotifyFilter = AllFilters;

            AssetWatcher.IncludeSubdirectories = true;
            AssetWatcher.EnableRaisingEvents = true;
        }}
        catch (Exception e)
        {{
            Mod.Logger.Warn($""Unable to load Asset Reloader! - {{e}}"");
        }}
    }}

    public override void Unload()
    {{
            // Null conditional assignment should not be assumed.
        if (AssetWatcher is not null)
        {{
            AssetWatcher.EnableRaisingEvents = false;
            AssetWatcher.Dispose();
        }}
    }}

    #endregion

    #region Assets

    private void AssetChanged(object sender, FileSystemEventArgs e)
    {{
        if (e.ChangeType.HasFlag(Created))
            return;

        string assetPath = Path.GetRelativePath(ModSource, e.FullPath).Replace('/', '\\');
        
        AssetSource?.AddAssetPath(assetPath);

        assetPath = Path.ChangeExtension(assetPath, null);

        if (e.ChangeType.HasFlag(Deleted) ||
            e.ChangeType.HasFlag(Renamed))
        {{
            Mod.Logger.Warn($""Asset at {{assetPath}} was removed or renamed!"");
            return;
        }}

        Dictionary<string, IAsset>? repositoryAssets = (Dictionary<string, IAsset>?)AssetsInfo?.GetValue(Mod.Assets);

        if (repositoryAssets is null)
            throw new NullReferenceException(""'Mod.Assets._assets' was null!"");

        if (!repositoryAssets.TryGetValue(assetPath, out IAsset? asset) ||
            asset is null)
            return;

        Main.QueueMainThreadAction(() =>
            ReloadAsset(asset));
    }}

    private void ReloadAsset(IAsset asset)
    {{
        if (RequestLockInfo is null ||
            ForceReloadAssetInfo is null)
            throw new NullReferenceException(""Could not force reload asset!"");

        lock (RequestLockInfo.GetValue(Mod.Assets))
            ForceReloadAssetInfo.Invoke(Mod.Assets, [asset, AssetRequestMode.ImmediateLoad]);

            // Unsure if this is required to correctly reload the asset;
                // ForceReloadAsset doesn't run Asset.Wait for ImmediateLoad unlike the ordinary AssetRepository.Request.
        InvokeAssetWait(asset);
    }}

    private static void InvokeAssetWait(IAsset asset)
    {{
        Type type = asset.GetType();

        if (!type.IsGenericType ||
            type.GetGenericTypeDefinition() != typeof(Asset<>))
            throw new ArgumentException($""IAsset was not of type {{nameof(Asset<>)}}!"");

        MethodInfo? getAssetWait = type.GetProperty(nameof(Asset<>.Wait), Public | Instance)?.GetGetMethod();

        Action wait = (Action?)getAssetWait?.Invoke(asset, []) ??
            throw new NullReferenceException($""Asset wait function was null!"");

        wait();
    }}

    #endregion

    #region AssetSource

    private void ChangeContentSource(string modSource) =>
        Main.QueueMainThreadAction(() => Mod.Assets.SetSources([AssetSource, Mod.RootContentSource]));

    #endregion
}}

#endif");

        return new("Debug/AssetReloader.g.cs", writer.ToString());
    }

    #endregion

    #region LocalAssetSource

    private static GeneratedFile GenerateLocalAssetSource(string assemblyName)
    {
        StringBuilder writer = new();

        writer.Append(Header);
        writer.Append(@$"
using ReLogic.Content;
using ReLogic.Content.Sources;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using Terraria.Initializers;

using static System.Reflection.BindingFlags;

namespace {assemblyName}.{AssetNamespace}.Debug;

#nullable enable

#if DEBUG

public class LocalAssetSource : ContentSource
{{
    #region Private Properties

    private string ModSource {{ get; init; }}

    public string[] AssetPaths
	{{
		get => assetPaths;
		set => SetAssetNames(value);
	}}

    #endregion

    #region Public Constructors

    public LocalAssetSource(string modSource) : base()
    {{
        ModSource = modSource;

        assetPaths = Array.Empty<string>();
    }}

    #endregion

    #region Public Methods

    public override Stream OpenStream(string fullAssetName) =>
        File.OpenRead(Path.Combine(ModSource, fullAssetName));

    public void AddAssetPath(string path) =>
        AssetPaths = [.. AssetPaths.Append(path)];

    #endregion
}}

#endif");

        return new("Debug/LocalAssetSource.g.cs", writer.ToString());
    }

    #endregion

    #endregion
}
