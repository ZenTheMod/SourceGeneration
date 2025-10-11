using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using ZourceGen.DataStructures;

namespace ZourceGen.Assets.Generators;

    // TODO: Allow for a compiled binary format of objs.
public sealed class OBJModelGenerator : AssetGenerator
{
    public override string[] FileExtensions => ["obj"];

    protected override IEnumerable<GeneratedFile> Write(ImmutableArray<AssetFile> models, string assemblyName)
    {
        StringBuilder writer = new();

        List<GeneratedFile> outputFiles =
            [WriteMesh(assemblyName),
            WriteOBJModel(assemblyName),
            WriteOBJModelReader(assemblyName)];

        foreach (AssetFile model in models)
        {
            string name = model.Name.CleanName().Capitalize();

            string outputPath = model.Directory;

            string assetPath = model.AssetPath;

            string source = model.Contents.GetText()!.ToString();

            writer.AppendLine(Header);

            writer.AppendLine(@$"
using ReLogic.Content;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Terraria.ModLoader;

using System;

using {assemblyName}.{AssetNamespace}.DataStructures;

namespace {assemblyName}.{AssetNamespace}.{model.Directory.Replace('/', '.')};

public static class {name}
{{
    public static LazyAsset<OBJModel> Model => new(""{assetPath}"");

    public static OBJModel Value => Model.Value;

    public static bool IsReady => Model.IsReady;");

                // Only grab lines corresponding to mesh names.
            string[] lines = [.. source.Split(["\r\n", "\r", "\n"], StringSplitOptions.None)
                    .Where(s => s.Length >= 3 && s[0] == 'o')]; // Also remember blank lines exsist.

                // Create individual draw methods for each mesh using its name.
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                if (line.Length <= 3)
                    continue;

                    // Remove all spaces and hyphenation and convert the name to PascalCase.
                string meshName = string.Empty;
                string[] meshNameParts = line[2..].Split(' ', '_', '-');

                foreach (string part in meshNameParts)
                {
                    if (part.Length <= 2)
                        continue;

                    meshName += part.Capitalize();
                }

                writer.AppendLine(@$"
    public static void Draw{meshName}(GraphicsDevice device) =>
        Value.Draw(device, {i});");
            }

            writer.Append(@$"}}");

            outputFiles.Add(new(Path.Combine(outputPath, $"{name}.g.cs"), writer.ToString()));

            writer.Clear();
        }

        return outputFiles;
    }

    #region Mesh

    private static GeneratedFile WriteMesh(string assemblyName)
    {
        StringBuilder writer = new();

        writer.Append(Header);
        writer.Append($@"
using Microsoft.Xna.Framework.Graphics;

using System;

namespace {assemblyName}.{AssetNamespace}.DataStructures;

public record struct Mesh : IDisposable
{{
    #region Public Properties

    public string Name {{ get; init; }}

    public int StartIndex {{ get; init; }}

    public int EndIndex {{ get; init; }}

    public VertexBuffer? Buffer {{  get; set; }}

    #endregion

    #region Public Constructors

    public Mesh(string name, int startIndex, int endIndex)
    {{
        Name = name;
        StartIndex = startIndex;
        EndIndex = endIndex;
    }}

    #endregion

    #region Public Methods

    /// <summary>
    /// Resets <see cref=""Buffer""/> if necessary.
    /// </summary>
    public VertexBuffer? ResetBuffer<T>(GraphicsDevice device, T[] vertices) where T : struct, IVertexType
    {{
        if (Buffer is not null && !Buffer.IsDisposed)
            return Buffer;

        if (vertices.Length < 3)
            throw new InvalidOperationException($""{{nameof(Mesh)}}: Not enough vertices to generate {{nameof(VertexBuffer)}}!"");

        Buffer = new(device, typeof(T), EndIndex - StartIndex, BufferUsage.None);
        Buffer.SetData(vertices, StartIndex, EndIndex - StartIndex);

        return Buffer;
    }}

    public readonly void Dispose() => 
        Buffer?.Dispose();

    #endregion
}}");

        return new("DataStructures/Mesh.g.cs", writer.ToString());
    }

    #endregion

    #region OBJModel

    private static GeneratedFile WriteOBJModel(string assemblyName)
    {
        StringBuilder writer = new();

        writer.Append(Header);
        writer.Append($@"
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using System;
using System.Collections.Generic;
using System.IO;

using Terraria;
using Terraria.ModLoader;

namespace {assemblyName}.{AssetNamespace}.DataStructures;

public sealed class OBJModel : IDisposable
{{
    #region Private Fields

    private VertexPositionNormalTexture[]? Vertices;
    private Mesh[]? Meshes;

    #endregion

    #region Private Methods

    private VertexBuffer? ResetBuffer(GraphicsDevice device, int i)
    {{
        if (Vertices is null || Meshes is null || !Meshes.IndexInRange(i))
            return null;

        return Meshes[i].ResetBuffer(device, Vertices);
    }}

    private void ResetBuffers(GraphicsDevice device)
    {{
        if (Vertices is null || Meshes is null)
            return;

        Array.ForEach(Meshes, m => m.ResetBuffer(device, Vertices));
    }}

    #endregion

    #region Public Methods

    public void Dispose()
    {{
        if (Meshes is not null)
            Array.ForEach(Meshes, m => m.Dispose());
    }}

    #region Reading

    public static OBJModel Create(Stream stream)
    {{
        OBJModel model = new();

        List<VertexPositionNormalTexture> vertices = [];

        List<Mesh> meshes = [];

        List<Vector3> positions = [];
        List<Vector2> textureCoordinates = [];
        List<Vector3> vertexNormals = [];

        string meshName = string.Empty;
        int startIndex = 0;

        bool containsNonTriangularFaces = false;

        using StreamReader reader = new(stream);

        string? text;
        while ((text = reader.ReadLine()) is not null)
        {{
            string[] segments = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length == 0)
                continue;

            switch (segments[0])
            {{
                case ""o"":
                    if (segments.Length < 2)
                        break;

                    if (vertices.Count > 3 && meshName != string.Empty)
                        meshes.Add(new Mesh(meshName, startIndex, vertices.Count));

                    meshName = segments[1];
                    startIndex = vertices.Count;
                    break;

                case ""v"":
                    if (segments.Length < 4)
                        break;

                    positions.Add(new(
                        float.Parse(segments[1]), 
                        float.Parse(segments[2]), 
                        float.Parse(segments[3])));
                    break;

                case ""vt"":
                    if (segments.Length < 3)
                        break;

                    textureCoordinates.Add(new(
                        float.Parse(segments[1]),
                        float.Parse(segments[2])));
                    break;

                case ""vn"":
                    if (segments.Length < 4)
                        break;

                    vertexNormals.Add(new(
                        float.Parse(segments[1]),
                        float.Parse(segments[2]),
                        float.Parse(segments[3])));
                    break;

                case ""f"":
                    if (segments.Length != 4)
                    {{
                        containsNonTriangularFaces = true;
                        break;
                    }}

                    for (int i = 1; i < segments.Length; i++) 
                    {{
                        VertexPositionNormalTexture vertex = new();

                        string[] components = segments[i].Split('/', StringSplitOptions.RemoveEmptyEntries);

                        if (components.Length != 3)
                            continue;

                        vertex.Position = positions[int.Parse(components[0]) - 1];

                            // Account for the inversed Y coordinate.
                        Vector2 coord = textureCoordinates[int.Parse(components[1]) - 1];
                        coord.Y = 1 - coord.Y;

                        vertex.TextureCoordinate = coord;

                        Vector3 normal = vertexNormals[int.Parse(components[2]) - 1];
                        vertex.Normal = normal;

                        vertices.Add(vertex);
                    }}
                    break;
            }}
        }}

        if (vertices.Count > 3 && meshName != string.Empty)
            meshes.Add(new Mesh(meshName, startIndex, vertices.Count));

        if (meshes.Count > 0) 
            model.Meshes = [.. meshes];
        else
            throw new InvalidDataException($""{{nameof(OBJModel)}}: Model did not contain at least one object!"");

        model.Vertices = [.. vertices];

            // assemblyName is not guaranteed to be the mod file name(?)
            // if (containsNonTriangularFaces)
                // ModContent.GetInstance<{assemblyName}>().Logger.Warn($""{{nameof(OBJModel)}}: Model contained non triangular faces! These will not be drawn."");

        if (model.Vertices.Length < 3)
            throw new InvalidDataException($""{{nameof(OBJModel)}}: Not enough vertices to create vertex buffer!"");

        model.ResetBuffers(Main.instance.GraphicsDevice);

        return model;
    }}

    #endregion

    #region Drawing

    /// <summary>
    /// Draws the first <see cref=""Mesh""/> where <see cref=""Mesh.Name""/> is equal to <paramref name=""name""/>.
    /// </summary>
    /// <param name=""device""></param>
    /// <param name=""name""></param>
    public void Draw(GraphicsDevice device, string name)
    {{
        if (Meshes is null)
            return;

        int i = Array.FindIndex(Meshes, m => m.Name == name);

        if (i != -1)
            Draw(device, i);
    }}

    /// <summary>
    /// Draws the <see cref=""Mesh""/> at index <paramref name=""i""/> if within range.
    /// </summary>
    /// <param name=""device""></param>
    /// <param name=""i""></param>
    public void Draw(GraphicsDevice device, int i = 0)
    {{
        VertexBuffer? buffer = ResetBuffer(device, i);

        if (buffer is null)
            return;

        device.SetVertexBuffer(buffer);

        device.DrawPrimitives(PrimitiveType.TriangleList, 0, buffer.VertexCount / 3);
    }}

    #endregion

    #endregion
}}");

        return new("DataStructures/OBJModel.g.cs", writer.ToString());
    }

    #endregion

    #region OBJModelReader

    private static GeneratedFile WriteOBJModelReader(string assemblyName)
    {
        StringBuilder writer = new();

        writer.Append(Header);
        writer.Append($@"
using ReLogic.Content;
using ReLogic.Content.Readers;
using ReLogic.Utilities;

using System.IO;
using System.Threading.Tasks;

using Terraria;
using Terraria.ModLoader;

using {assemblyName}.{AssetNamespace}.DataStructures;

namespace {assemblyName}.{AssetNamespace}.AssetReaders;

    // Based loosely and respectfully on Overhaul's OvgReader implementation: https://github.com/Mirsario/TerrariaOverhaul/blob/dev/Core/VideoPlayback/OgvReader.cs
/// <summary>
/// This class must be manually referenced and loaded with <see cref=""Mod.AddContent""/> in <see cref=""Mod.CreateDefaultContentSource""/>.<br/><br/>
/// e.g.
/// <code>
/// public override IContentSource CreateDefaultContentSource()
/// {{
///     if (!Main.dedServ)
///         AddContent(new OBJModelReader());
///
///     return base.CreateDefaultContentSource();
/// }}
/// </code>
/// </summary>
[Autoload(false)]
public sealed class OBJModelReader : IAssetReader, ILoadable
{{
    public static readonly string Extension = "".obj"";

    #region Loading

    public void Load(Mod mod)
    {{
        AssetReaderCollection? assetReaderCollection = Main.instance.Services.Get<AssetReaderCollection>();

        if (!assetReaderCollection.TryGetReader(Extension, out IAssetReader reader) || reader != this)
            assetReaderCollection.RegisterReader(this, Extension);
    }}

    public void Unload() {{ }}

    #endregion

    public async ValueTask<T> FromStream<T>(Stream stream, MainThreadCreationContext mainThreadCtx) where T : class
    {{
        if (typeof(T) != typeof(OBJModel))
            throw AssetLoadException.FromInvalidReader<OBJModelReader, T>();

        await mainThreadCtx;

        OBJModel? result = OBJModel.Create(stream);

        return (result as T)!;
    }}
}}");

        return new("AssetReaders/OBJModelReader.g.cs", writer.ToString());
    }

    #endregion
}
