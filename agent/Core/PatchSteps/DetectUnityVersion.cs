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
    public static UnityVersion? Detect(string apkPath, IPatchLogger logger)
    {
        using FileStream apkStream = new(apkPath, FileMode.Open, FileAccess.Read);
        using ZipArchive archive = new(apkStream, ZipArchiveMode.Read);

        AssetsManager uAssetsManager = new();

        // Try to read directly from file
        try
        {
            ZipArchiveEntry assetEntry = archive.GetEntry("assets/bin/Data/globalgamemanagers")!;
            using Stream stream = assetEntry.Open();

            AssetsFileInstance instance = uAssetsManager.LoadAssetsFile(stream, "/bin/Data/globalgamemanagers", true);
            return UnityVersion.Parse(instance.file.Metadata.UnityVersion);
        }
        catch { }

        // If failed before, try to get the data from data.unity3d
        try
        {
            ZipArchiveEntry assetEntry = archive.GetEntry("assets/bin/Data/data.unity3d")!;
            using Stream stream = assetEntry.Open();

            BundleFileInstance bundle = uAssetsManager.LoadBundleFile(stream, "/bin/Data/data.unity3d");
            AssetsFileInstance instance = uAssetsManager.LoadAssetsFileFromBundle(bundle, "globalgamemanagers");
            return UnityVersion.Parse(instance.file.Metadata.UnityVersion);
        }
        catch (Exception ex)
        {
            logger.Log("Failed to get Unity version, cannot patch.\n" + ex);
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
