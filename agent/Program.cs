using LemonAgent;
using MelonLoader.Installer.Core;
using MelonLoader.Installer.Core.PatchSteps;
using System.Runtime.InteropServices;
using UnityVersionType = AssetRipper.Primitives.UnityVersionType;

// Commands the website runs on the headset:
//   lemon-agent hello
//   lemon-agent detect --apk <base.apk>
//   lemon-agent patch --apk <base.apk> [--apk <split.apk> ...] --package <name> --work <dir> --melon-data <zip> --libunity <file>
//
// Output the site reads:
//   @step <id> <start|done|fail> [detail]   progress
//   @unity <version> <global|china>          result of "detect"
//   @output <dir>                            where "patch" left the patched APKs
//   anything else                            plain log text

static void Step(string id, string state, string detail = "") =>
    Console.WriteLine(detail.Length > 0 ? $"@step {id} {state} {detail}" : $"@step {id} {state}");

static Dictionary<string, List<string>> ParseOptions(string[] a)
{
    Dictionary<string, List<string>> options = new();
    for (int i = 0; i < a.Length; i++)
    {
        if (!a[i].StartsWith("--"))
            continue;

        string key = a[i][2..];
        string value = i + 1 < a.Length && !a[i + 1].StartsWith("--") ? a[++i] : "";

        if (!options.TryGetValue(key, out List<string>? list))
            options[key] = list = new List<string>();
        list.Add(value);
    }
    return options;
}

static string? Get(Dictionary<string, List<string>> options, string key) =>
    options.TryGetValue(key, out List<string>? list) && list.Count > 0 ? list[0] : null;

static List<string> All(Dictionary<string, List<string>> options, string key) =>
    options.TryGetValue(key, out List<string>? list) ? list : new List<string>();

static int Hello(string[] a)
{
    Step("info", "start");
    Console.WriteLine("lemon-agent");
    Console.WriteLine($"OS: {RuntimeInformation.OSDescription}");
    Console.WriteLine($"Architecture: {RuntimeInformation.ProcessArchitecture}");
    Console.WriteLine($"Runtime: {RuntimeInformation.FrameworkDescription}");
    Console.WriteLine($"Arguments: {string.Join(" ", a)}");
    Step("info", "done");
    return 0;
}

static int Detect(Dictionary<string, List<string>> options)
{
    Step("unity-version", "start");

    string? apk = Get(options, "apk");
    if (apk == null || !File.Exists(apk))
    {
        Step("unity-version", "fail", "The APK file wasn't found.");
        return 1;
    }

    var version = UnityVersionDetector.Detect(apk, new ConsoleLogger());
    if (version == null)
    {
        Step("unity-version", "fail", "Couldn't read the Unity version.");
        return 1;
    }

    string kind = version.Value.Type == UnityVersionType.China ? "china" : "global";
    Console.WriteLine($"@unity {version.Value.ToStringWithoutType()} {kind}");
    Step("unity-version", "done");
    return 0;
}

static int Patch(Dictionary<string, List<string>> options)
{
    List<string> apks = All(options, "apk");
    string? package = Get(options, "package");
    string? work = Get(options, "work");
    string? melonData = Get(options, "melon-data");
    string libUnity = Get(options, "libunity") ?? "";
    string unityDeps = Get(options, "unity-deps") ?? "";

    if (apks.Count == 0 || package == null || work == null || melonData == null)
    {
        Console.WriteLine("Missing arguments for patch.");
        Console.WriteLine("@result fail");
        return 2;
    }

    foreach (string apk in apks)
    {
        if (!File.Exists(apk))
        {
            Console.WriteLine($"APK not found: {apk}");
            Console.WriteLine("@result fail");
            return 2;
        }
    }

    // Always start clean, so a leftover patched APK is never patched twice.
    if (Directory.Exists(work))
        Directory.Delete(work, true);

    string temp = Path.Combine(work, "temp");
    string output = Path.Combine(work, "output");
    Directory.CreateDirectory(output);

    // Same split handling as the installer app.
    string target = apks[0];
    string library = apks.FirstOrDefault(p => p.Contains("arm64")) ?? "";
    string[] extras = apks.Skip(1).Where(p => !p.Contains("arm64")).ToArray();
    bool isSplit = apks.Count > 1;

    PatchArguments arguments = new(target, library, extras, output, temp, melonData, unityDeps, null, package, isSplit, libUnity);
    Patcher patcher = new(arguments, new ConsoleLogger());

    if (!patcher.Run())
    {
        Console.WriteLine("@result fail");
        return 1;
    }

    Console.WriteLine($"@output {output}");
    Console.WriteLine("@result ok");
    return 0;
}

string command = args.Length > 0 ? args[0] : "hello";
var options = ParseOptions(args.Skip(1).ToArray());

try
{
    return command switch
    {
        "hello" => Hello(args),
        "detect" => Detect(options),
        "patch" => Patch(options),
        _ => Fail($"Unknown command: {command}"),
    };
}
catch (Exception ex)
{
    Console.WriteLine(ex.ToString());
    Console.WriteLine("@result fail");
    return 1;
}

static int Fail(string message)
{
    Console.WriteLine(message);
    return 2;
}
