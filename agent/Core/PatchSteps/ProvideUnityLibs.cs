using System.IO;

namespace MelonLoader.Installer.Core.PatchSteps;

/// <summary>
/// Replaces the installer's download step. The website supplies the libunity.so for the game's Unity version
/// (or a full Unity dependencies zip), so the agent never needs to download anything itself.
/// </summary>
internal class ProvideUnityLibs : IPatchStep
{
    public bool Run(Patcher patcher)
    {
        if (File.Exists(patcher.Args.UnityDependenciesPath))
        {
            patcher.Logger.Log("Using the Unity dependencies zip");
            return new ExtractUnityLibs().Run(patcher);
        }

        string source = patcher.Args.LibUnityPath;
        if (string.IsNullOrEmpty(source) || !File.Exists(source))
        {
            patcher.Logger.Log("libunity.so for this Unity version was not provided.");
            return false;
        }

        string libDir = Path.Combine(patcher.Info.UnityNativeDirectory, "arm64-v8a");
        Directory.CreateDirectory(libDir);
        File.Copy(source, Path.Combine(libDir, "libunity.so"), true);

        patcher.Logger.Log("Using the provided libunity.so");
        return true;
    }
}
