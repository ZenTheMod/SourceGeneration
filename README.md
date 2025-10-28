# ZourceGen 

An attempt at a rather verbose tModLoader asset generator; similar to that of [Scalar's AssGen](https://github.com/ScalarVector1/AssGen/)

## Usage

This project is now available as a nuget package, implementation is as simple as referencing `ZourceGen` as an an analyzer. (Ensure version `1.0.3` or higher is used.)

Example:
```xml
<ItemGroup>
    <!-- Add every asset as an additional file so that our source generator can find it -->
    <AdditionalFiles Include="Assets\**" />
    <PackageReference Include="ZourceGen" Version="1.1.6" ReferenceOutputAssembly="false" OutputItemType="Analyzer" />
</ItemGroup>
```

## Asset Hot-Reloading

Assets for your mod will automatically reload when debugging.

TODO: Recompile shaders when modified.

## Asset Generation

Generated files will be placed into the `{YourModName}.GeneratedAssets` namespace.

An impl of `LazyAsset<T>` will be used for all asset handling within generated files, this type will implicity convert to either an `Asset<T> `or `T` if needed.

### Textures (.png/Texture2D)

Textures will be placed into a static `Textures` class in each namespace with actual textures.\
Textures can also be placed in arrays by including numbers in the file names.

Example:\
`MyCoolImage0`, `MyCoolImage1`, `MyCoolImage2`, `MyCoolImage3`

### Effects (.fxc/.xnb/Effect)

Effects will be placed in their own static class with wrapper properties for each parameter in the shader; effects will still be generated regardless of the method used to compile them, (thanks tomat.)

> [!IMPORTANT]
> Make sure you check for the `IsReady` property before using an effect; otherwise it may be null!

### Models (.obj/OBJModel)

Models will be placed into a static class much-like effects, these classes only have basic drawing methods at the moment.

> [!IMPORTANT]
> To have models load correctly you can must manually load the `OBJModelReader` class from your mod's class!
> 
> Example:
> ```cs
> public override IContentSource CreateDefaultContentSource()
> {
>         // Assets should not be loaded on the server.
>     if (!Main.dedServ)
>         AddContent(new OBJModelReader());
> 
>     return base.CreateDefaultContentSource();
> }
> ```
