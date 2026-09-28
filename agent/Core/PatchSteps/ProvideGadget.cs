using System.IO;

namespace MelonLoader.Installer.Core.PatchSteps;

/// <summary>
/// Copies libfrida-gadget.so into the native-libs staging folder and writes its config,
/// pointing the gadget at the loader script that lives in the game's mods folder.
/// Reuses UnityNativeDirectory (previously used for the LemonLoader-specific libunity.so)
/// as the generic "extra native files to merge into the APK" staging area.
/// </summary>
internal class ProvideGadget : IPatchStep
{
    public bool Run(Patcher patcher)
    {
        string source = patcher.Args.GadgetPath;
        if (string.IsNullOrEmpty(source) || !File.Exists(source))
        {
            patcher.Logger.Log("libfrida-gadget.so was not provided.");
            return false;
        }

        string libDir = Path.Combine(patcher.Info.UnityNativeDirectory, "arm64-v8a");
        Directory.CreateDirectory(libDir);
        File.Copy(source, Path.Combine(libDir, "libfrida-gadget.so"), true);

        string loaderPath = $"/sdcard/Android/data/{patcher.Args.PackageName}/files/mods/_loader.js";
        string config = $$"""
        {
          "interaction": {
            "type": "script",
            "path": "{{loaderPath}}",
            "on_change": "reload"
          }
        }
        """;
        File.WriteAllText(Path.Combine(libDir, "libfrida-gadget.config.so.json"), config);

        patcher.Logger.Log("Added the Frida gadget and its config.");
        return true;
    }
}
