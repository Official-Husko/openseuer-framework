using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace ObenFramework.Assets;

/// <summary>
/// Memory an asset takes. The profiler only reports sizes in development builds and the game is a
/// release build, so this works it out from the asset itself when the profiler has nothing.
/// </summary>
internal static class AssetSizes
{
    public static long Of(Object asset)
    {
        if (asset == null)
            return 0;
        long reported = Profiler.GetRuntimeMemorySizeLong(asset);
        if (reported > 0)
            return reported;

        switch (asset)
        {
            case Texture2D texture:
                return Texture(texture);
            case AudioClip clip:
                // Decompressed clips sit in memory as 16-bit PCM.
                return (long)clip.samples * clip.channels * 2;
            case Mesh mesh:
                return Mesh(mesh);
            default:
                return 0;
        }
    }

    private static long Texture(Texture2D texture)
    {
        long bytes = 0;
        var format = texture.graphicsFormat;
        for (int mip = 0; mip < texture.mipmapCount; mip++)
        {
            int width = Mathf.Max(1, texture.width >> mip);
            int height = Mathf.Max(1, texture.height >> mip);
            bytes += GraphicsFormatUtility.ComputeMipmapSize(width, height, format);
        }
        // A readable texture keeps a copy in main memory as well.
        return texture.isReadable ? bytes * 2 : bytes;
    }

    private static long Mesh(Mesh mesh)
    {
        long bytes = 0;
        for (int stream = 0; stream < mesh.vertexBufferCount; stream++)
            bytes += (long)mesh.vertexCount * mesh.GetVertexBufferStride(stream);
        int indexSize = mesh.indexFormat == IndexFormat.UInt16 ? 2 : 4;
        for (int sub = 0; sub < mesh.subMeshCount; sub++)
            bytes += (long)mesh.GetIndexCount(sub) * indexSize;
        return bytes;
    }
}
