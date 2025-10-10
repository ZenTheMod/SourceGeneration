using ShaderDecompiler;
using ShaderDecompiler.Structures;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using ZourceGen.DataStructures;

namespace ZourceGen.Assets.Generators;

public sealed class EffectGenerator : AssetGenerator
{
    #region Private Fields

    private static readonly Dictionary<string, string> CSharpParemterTypes = new()
    {
            // EffectParameter does not support strings, bytes or doubles.
        { "float", "float" },
        { "float2", "Vector2" },
        { "float3", "Vector3" },
        { "float4", "Vector4" },
        { "int", "int" },
        { "bool", "bool" },
            // Strangly EffectParameter.SetValue(Matrix) only accepts the following sizes: 2x2, 3x4, 4x3, 4x4.
        { "float2x2", "Matrix" },
        { "float3x4", "Matrix" },
        { "float4x3", "Matrix" },
        { "float4x4", "Matrix" },
        { "matrix", "Matrix" },
            // EffectParameter.SetValue allows for use of the base Texture class.
        { "texture", "Texture" },
        { "texture2D", "Texture2D" },
        { "sampler", "Texture" }, // Unsure.
        { "sampler2D", "Texture2D" },
    };

    #endregion

    public override string[] FileExtensions => ["fxc", "xnb"];

    protected override IEnumerable<GeneratedFile> Write(ImmutableArray<AssetFile> shaders, string assemblyName)
    {
        StringBuilder writer = new();

        List<GeneratedFile> outputFiles = [];

        foreach (AssetFile shader in shaders)
        {
            string name = shader.Name.CleanName().Capitalize();

            string outputPath = shader.Directory;

            writer.Append(Header);

            writer.Append(@$"
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using {assemblyName}.{AssetNamespace}.DataStructures;

namespace {assemblyName}.{AssetNamespace}.{shader.Directory.Replace('/', '.')};

public static class {name}
{{
    public static LazyAsset<Effect> Shader => new(""{shader.AssetPath}"");

    public static Effect Value => Shader.Value;

    public static bool IsReady => Shader.IsReady;");

            Effect effect = Effect.ReadXnbOrFxc(shader.Contents.Path, out _);

            foreach (Parameter parameter in effect.Parameters)
            {
                string typeName = CSharpParemterTypes[parameter.Value.Type.ToString()];
                string propertyName = CleanParameterName(parameter.Value.Name!);
                string parameterName = parameter.Value.Name!;

                writer.AppendLine(@$"
    public static {typeName} {propertyName}
        {{ set => Value.Parameters[""{parameterName}""].SetValue(value); }}");
            }

            writer.AppendLine(@$"
    public static void Apply() =>
        Value.CurrentTechnique.Passes[0].Apply();

    public static void Apply(int pass) =>
        Value.CurrentTechnique.Passes[pass].Apply();");

            foreach (Technique technique in effect.Techniques)
                foreach (Pass pass in technique.Passes)
                    writer.AppendLine(@$"
    public static void Apply{CleanParameterName(pass.Name!)}() =>
        Value.Techniques[{technique.Name!}].Passes[pass.Name].Apply();");

            writer.Append(@$"}}");

            outputFiles.Add(new(Path.Combine(outputPath, $"{name}.g.cs"), writer.ToString()));

            writer.Clear();
        }

        return outputFiles;
    }

        // Notably removes the 'u' uniform prefix.
    private static string CleanParameterName(string name) =>
        Regex.Replace(name, "^u?(?=[A-Z])", string.Empty).Capitalize();
}
