using Microsoft.CodeAnalysis;
using ZourceGen.DataStructures;

namespace ZourceGen.Utils;

public static partial class Utilities
{
    public static void AddSource(this IncrementalGeneratorPostInitializationContext context, GeneratedFile file)
        => context.AddSource(file.Directory, file.Contents);

    public static void AddSource(this SourceProductionContext context, GeneratedFile file)
        => context.AddSource(file.Directory, file.Contents);
}
