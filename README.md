# ZourceGen 

An attempt at a rather verbose tModLoader asset generator; similar to that of [Scalar's AssGen](https://github.com/ScalarVector1/AssGen/)

# Asset Generation

Generated files will be placed into the `{YourModName}.GeneratedAssets` namespace.

An impl of `LazyAsset<T>` will be used for all asset handling within generated files, this type will implicity convert to either an `Asset<T> `or `T` if needed.

## Textures (.png)

Textures will be placed into a static `Textures` class in each namespace with actual textures.

## Effects (.fxc/.xnb/Shaders)

Effects will be placed in their own static class with wrapper properties for each parameter in the shader; effects will still be generated regardless of the method used to compile them, (thanks tomat.)

## Models (.obj/3D Models)

Models will have types similar to that of effects, however models will not be loaded by default.
To have models load correctly you can must manually load the `OBJModelReader` class from your mod's class;

Example:
```cs
public override IContentSource CreateDefaultContentSource()
{
        // Assets should not be loaded on the server.
    if (!Main.dedServ)
        AddContent(new OBJModelReader());

    return base.CreateDefaultContentSource();
}
```

# Implementation

TODO: Detail how to use this generator within your own mod.
