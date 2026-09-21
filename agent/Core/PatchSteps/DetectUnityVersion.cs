using AssetsTools.NET.Extra;
using System;
using System.IO;
using System.IO.Compression;
using UnityVersion = AssetRipper.Primitives.UnityVersion;

namespace MelonLoader.Installer.Core.PatchSteps;

/// <summary>
/// Reads the Unity version out of an APK. Split out from the patch step so it can also run on its own.
/// </summary>
public static class UnityVersionDetector
{
    // Streams from a zip entry can't seek, but the Unity file reader needs to.
    // Small entries are copied to memory, big ones to a temporary file that is deleted when closed.
    private const long MaxMemoryBytes = 64L * 1024 * 1024;
    private const string TempFolder = "/data/local/tmp/lemon/tmp";

    private static Stream OpenSeekable(ZipArchiveEntry entry, IPatchLogger logger)
    {
        using Stream source = entry.Open();

        if (entry.Length <= MaxMemoryBytes)
        {
            MemoryStream memory = new((int)entry.Length);
            source.CopyTo(memory);
            memory.Position = 0;
            return memory;
        }

        logger.Log($"Copying {entry.FullName} to read the Unity version, this can take a moment");

        Directory.CreateDirectory(TempFolder);
        FileStream file = new(
            Path.Combine(TempFolder, Guid.NewGuid().ToString("N")),
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            81920,
            FileOptions.DeleteOnClose);

        source.CopyTo(file, 81920);
        file.Position = 0;
        return file;
    }

    public static UnityVersion? Detect(string apkPath, IPatchLogger logger)
    {
        using FileStream apkStream = new(apkPath, FileMode.Open, FileAccess.Read);
        using ZipArchive archive = new(apkStream, ZipArchiveMode.Read);

        AssetsManager uAssetsManager = new();
        Exception? firstError = null;

        // Try to read directly from file
        try
        {
            ZipArchiveEntry assetEntry = archive.GetEntry("assets/bin/Data/globalgamemanagers")!;
            using Stream stream = OpenSeekable(assetEntry, logger);

            AssetsFileInstance instance = uAssetsManager.LoadAssetsFile(stream, "/bin/Data/globalgamemanagers", true);
            return UnityVersion.Parse(instance.file.Metadata.UnityVersion);
        }
        catch (Exception ex)
        {
            firstError = ex;
        }

        // If failed before, try to get the data from data.unity3d
        try
        {
            ZipArchiveEntry assetEntry = archive.GetEntry("assets/bin/Data/data.unity3d")!;
            using Stream stream = OpenSeekable(assetEntry, logger);

            BundleFileInstance bundle = uAssetsManager.LoadBundleFile(stream, "/bin/Data/data.unity3d");
            AssetsFileInstance instance = uAssetsManager.LoadAssetsFileFromBundle(bundle, "globalgamemanagers");
            return UnityVersion.Parse(instance.file.Metadata.UnityVersion);
        }
        catch (Exception ex)
        {
            logger.Log("Failed to get Unity version, cannot patch.");
            logger.Log("First attempt (globalgamemanagers): " + firstError.Message);
            logger.Log(ex.ToString());
            return null;
        }
    }
}

internal class DetectUnityVersion : IPatchStep
{
    public bool Run(Patcher patcher)
    {
        if (patcher.Args.UnityVersion != null && patcher.Args.UnityVersion != UnityVersion.MinVersion)
            return true;

        UnityVersion? version = UnityVersionDetector.Detect(patcher.Info.OutputBaseApkPath, patcher.Logger);
        if (version == null)
        {
            patcher.Args.UnityVersion = UnityVersion.MinVersion;
            return false;
        }

        patcher.Args.UnityVersion = version;
        return true;
    }
}
