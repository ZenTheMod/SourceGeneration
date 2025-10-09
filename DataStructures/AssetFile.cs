using Microsoft.CodeAnalysis;
using System;
using System.IO;

namespace SourceGeneration.DataStructures;

public readonly record struct AssetFile
{
    #region Public Properties

    /// <summary>
    /// Abstract shorthand directory..
    /// </summary>
    public string Directory { get; init; }

    /// <summary>
    /// Weither the file is in the mods root folder.
    /// </summary>
    public bool InRoot { get; init; }

    /// <summary>
    /// Full name used to load the asset.
    /// </summary>
    public string AssetPath { get; init; }

    public string Name { get; init; }

    public string Extension { get; init; }

    public AdditionalText Contents { get; init; }

    #endregion

    #region Public Constructors

    public AssetFile(AdditionalText contents, string rootDirectory)
    {
        string fullPath = contents.Path.Replace('\\', '/');

        if (!fullPath.StartsWith(rootDirectory))
            throw new ArgumentException($"Path '{fullPath}' did not contain root directory '{rootDirectory}'!");

            // Get the shorthand directory used for namespaces and such.
        Directory =
            Path.GetDirectoryName(fullPath[(rootDirectory.Length + 1)..])
            .Replace('\\', '/');

        if (Directory == string.Empty)
            InRoot = true;

            // Remove reduntant 'Assets' path.
        if (Directory.StartsWith("Assets/"))
            Directory = Directory["Assets/".Length..];

            // Get the asset path -- without file extensions -- including the name of the root foler.
                // '.../ModSources/MyMod' => '.../ModSources'
                // Directory => 'Textures/blahblah'
                // AssetPath => 'ModName/Assets/Textures/blahblah/coolthing'
        AssetPath = Path.ChangeExtension(fullPath[(Path.GetDirectoryName(rootDirectory).Length + 1)..], null);

        Name = Path.GetFileNameWithoutExtension(fullPath);

        Extension = Path.GetExtension(fullPath);

        Contents = contents;
    }

    #endregion
}
