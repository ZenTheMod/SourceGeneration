using System;
using System.Collections.Generic;
using System.Text;

namespace SourceGeneration.DataStructures;

public readonly struct GeneratedFile
{
    #region Public Fields

    public readonly string Directory;

    public readonly string Contents;

    #endregion

    #region Public Constructors

    public GeneratedFile(string directory, string contents)
    {
        Directory = directory;
        Contents = contents;
    }

    #endregion
}
