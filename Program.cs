using System.Runtime.InteropServices;

// The site reads these lines to update its progress list:
//   @step <id> <start|done|fail> [detail]
// Any other line is shown as plain log output.
static void Step(string id, string state, string detail = "") =>
    Console.WriteLine(detail.Length > 0 ? $"@step {id} {state} {detail}" : $"@step {id} {state}");

Step("info", "start");
Console.WriteLine("lemon-agent test build");
Console.WriteLine($"OS: {RuntimeInformation.OSDescription}");
Console.WriteLine($"Architecture: {RuntimeInformation.ProcessArchitecture}");
Console.WriteLine($"Runtime: {RuntimeInformation.FrameworkDescription}");
Console.WriteLine($"Arguments: {string.Join(" ", args)}");
Step("info", "done");
return 0;
