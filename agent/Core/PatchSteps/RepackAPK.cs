using System.IO;
using System.IO.Compression;
using System.Linq;

namespace MelonLoader.Installer.Core.PatchSteps;

internal class RepackAPK : IPatchStep
{
    public bool Run(Patcher patcher)
    {
        patcher.Logger.Log("Repacking APK");

        using FileStream zipStream = new(patcher.Info.OutputBaseApkPath, FileMode.Open);
        ZipArchive archive = new(zipStream, ZipArchiveMode.Read | ZipArchiveMode.Update);

        patcher.Logger.Log("Adding the Frida gadget to the APK");

        // *.* here (not *.so) so the gadget's .config.so.json file gets copied in too.
        if (!patcher.Args.IsSplit)
        {
            CopyTo(archive, Path.Combine(patcher.Info.UnityNativeDirectory, "arm64-v8a"), "lib/arm64-v8a", "*.*");
        }
        else
        {
            using FileStream libStream = File.Open(patcher.Info.OutputLibApkPath!, FileMode.Open);
            using ZipArchive libArchive = new(libStream, ZipArchiveMode.Read | ZipArchiveMode.Update);

            CopyTo(libArchive, Path.Combine(patcher.Info.UnityNativeDirectory, "arm64-v8a"), "lib/arm64-v8a", "*.*");
        }

        patcher.Logger.Log("Writing, this can take a few");

        // for whatever reason ZipArchive uses disposal as the only time to save
        archive.Dispose();

        patcher.Logger.Log("Done");

        return true;
    }

    private static void CopyTo(ZipArchive archive, string source, string dest, string matcher = "*.*")
    {
        if (!Directory.Exists(source)) return;

        foreach (string file in Directory.GetFiles(source, matcher, SearchOption.AllDirectories))
        {
            string entryPath = Path.Combine(dest, Path.GetRelativePath(source, file)).Replace('\\', '/');

            ZipArchiveEntry? entry = archive.GetEntry(entryPath);
            entry?.Delete();

            entry = archive.CreateEntry(entryPath, CompressionLevel.Optimal);
            using FileStream sourceStream = File.Open(file, FileMode.Open);
            using Stream entryStream = entry.Open();
            sourceStream.CopyTo(entryStream);
        }
    }
}
