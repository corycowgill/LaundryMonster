using UnityEditor;

/// <summary>
/// Import settings for the wall posters.
///
/// They arrive as 1254-pixel PNGs, which is a megabyte and a half each before Unity has
/// touched them. On a wall across the room a poster occupies maybe a hundred and fifty
/// pixels of screen, so 512 is already generous and the rest is download time - and this
/// is a WebGL build where the player waits for every byte before the game starts.
///
/// Done as a postprocessor rather than by hand so the next poster someone drops in the
/// folder gets the same treatment without anybody having to remember.
/// </summary>
public class PosterImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').Contains("Assets/Art/Posters/")) return;

        var t = (TextureImporter)assetImporter;
        t.textureType = TextureImporterType.Default;
        t.maxTextureSize = 512;
        t.textureCompression = TextureImporterCompression.Compressed;
        t.mipmapEnabled = true;          // they are seen at an angle and at a distance
        t.wrapMode = UnityEngine.TextureWrapMode.Clamp;
        t.alphaSource = TextureImporterAlphaSource.None;
    }
}
