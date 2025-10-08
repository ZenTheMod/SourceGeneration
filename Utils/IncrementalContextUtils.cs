using Microsoft.CodeAnalysis;
using SourceGeneration.DataStructures;

namespace SourceGeneration.Utils;

public static partial class Utilities
{
    public static void AddSource(this IncrementalGeneratorPostInitializationContext context, GeneratedFile file)
        => context.AddSource(file.Directory, file.Contents);

    public static void AddSource(this SourceProductionContext context, GeneratedFile file)
        => context.AddSource(file.Directory, file.Contents);
}
