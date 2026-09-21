using MelonLoader.Installer.Core.PatchSteps;
using System;
using System.IO;

namespace MelonLoader.Installer.Core;

/// <summary>
/// Main class that handles starting patches.
/// Based on the LemonLoader installer's Patcher; steps now report progress as "@step" lines for the website.
/// </summary>
public class Patcher(PatchArguments arguments, IPatchLogger logger)
{
    public PatchArguments Args { get; internal set; } = arguments;
    public IPatchLogger Logger { get; internal set; } = logger;
    public PatchInfo Info { get; internal set; } = new(arguments);

    private static void Marker(string id, string state, string detail = "") =>
        Console.WriteLine(detail.Length > 0 ? $"@step {id} {state} {detail}" : $"@step {id} {state}");

    public bool Run()
    {
        string current = "copy-apk";

        try
        {
            Info.CreateDirectories();

            Marker("copy-apk", "start");

            if (!Path.Exists(Info.OutputBaseApkPath))
            {
                Logger.Log($"Copying [ {Args.TargetApkPath} ] to [ {Info.OutputBaseApkPath} ]");
                File.Copy(Args.TargetApkPath, Info.OutputBaseApkPath);
            }

            if (Args.IsSplit && !Path.Exists(Info.OutputLibApkPath))
            {
                Logger.Log($"Copying [ {Args.LibraryApkPath} ] to [ {Info.OutputLibApkPath} ]");
                File.Copy(Args.LibraryApkPath, Info.OutputLibApkPath!);
            }

            if (Args.ExtraSplitApkPaths != null)
            {
                for (int i = 0; i < Args.ExtraSplitApkPaths.Length; i++)
                {
                    string from = Args.ExtraSplitApkPaths[i];
                    string to = Info.OutputExtraApkPaths![i];

                    if (!File.Exists(to))
                    {
                        Logger.Log($"Copying [ {from} ] to [ {to} ]");
                        File.Copy(from, to);
                    }
                }
            }

            Marker("copy-apk", "done");

            (string Id, IPatchStep Step)[] steps =
            [
                ("unity-version", new DetectUnityVersion()),
                ("unity-libs", new ProvideUnityLibs()),
                ("loader-files", new ExtractDependencies()),
                ("manifest", new PatchManifest()),
                ("repack", new RepackAPK()),
                ("certificate", new GenerateCertificate()),
                ("sign", new AlignSign()),
                ("cleanup", new CleanUp()),
            ];

            foreach ((string id, IPatchStep step) in steps)
            {
                current = id;
                Marker(id, "start");

                if (!step.Run(this))
                    throw new Exception($"The \"{id}\" step failed.");

                if (id == "unity-version")
                    Logger.Log($"Unity version: {Args.UnityVersion}");

                Marker(id, "done");
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.Log("[ERROR] " + ex);
            Marker(current, "fail", ex.Message.Replace('\n', ' ').Replace('\r', ' '));
            return false;
        }
    }
}
