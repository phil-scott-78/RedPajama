using System.Diagnostics;
using System.Text;
using LLama;
using LLama.Common;
using LLama.Native;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using RedPajama.ConsoleTest;
using RedPajama.ConsoleTest.TestRoutines;
using Spectre.Console;
using Spectre.Console.Extensions;

Console.OutputEncoding = Encoding.UTF8;
NativeLibraryConfig.All.WithCuda();
NativeLogConfig.llama_log_set((a, b) =>
{
    Debug.WriteLine($"[{a}]: {b}");
});

AnsiConsole.WriteLine();

var path = args.Length == 1
    ? args[0]
    : AnsiConsole.Prompt(new TextPrompt<string>("Path to GGUF model file or folder containing multiple: "));

List<string> models = [];

if (File.Exists(path))
{
    models.Add(path);
}
else if (Path.Exists(path))
{
    var directory = new DirectoryInfo(path);
    models.AddRange(directory.GetFiles("*.gguf").Select(i => i.FullName));
}
else
{
    var directory = Path.GetDirectoryName(path) ?? "";
    var pattern = Path.GetFileName(path) ?? "*"; // Default to "*" if no pattern

    var matcher = new Matcher();
    matcher.AddInclude(pattern);
    var result = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(directory)));

    models.AddRange(result.Files.Select(i => Path.Combine(directory, i.Path)));

}

var tests = new List<ITestRoutine>()
{
    new ParseComplexRestaurantOrder(),
    new ParseNameAndEmail(),
    new ParseOrderStatus(),
    new ParseNestedAddress(),
    new ParseBookCollection(),
    new ParseTags(),
    new ParseAndInferColor()
};

foreach (var modelFile in models)
{
    var parameters = new ModelParams(modelFile)
    {
        ContextSize = 4000,
        GpuLayerCount = -1,
        // BatchSize = 2048,
    };

    AnsiConsole.MarkupLineInterpolated($"[blue]{Path.GetFileNameWithoutExtension(modelFile)}[/]");
    using var model = await LLamaWeights.LoadFromFileAsync(parameters);

    foreach (var r in tests)
    {
        var stopWatch = new Stopwatch();
        stopWatch.Start();
        try
        {
            AnsiConsole.MarkupInterpolated($"  {r.GetType().Name}: ");
            var spinner = model.IsThinkingModel() ? Spinner.Known.Dots8Bit : Spinner.Known.Dots2;
            await r.Run(model, parameters).Spinner(spinner);
            AnsiConsole.MarkupLine($"[green]Ok[/] [grey]{stopWatch.ElapsedMilliseconds}ms[/]");
        }
        catch (BunchOfNotEqualException e)
        {
            var msg = string.Join(Environment.NewLine,
                e.Exception.Select(i =>
                    $"   * [blue]{i.Caller.EscapeMarkup()}[/] expected to be {i.Expected.EscapeMarkup()}, received {i.Actual.EscapeMarkup()}"));
            // don't use MarkupInterpolated here because we are using {msg} which contains markup in it, which under the interpolated version
            // would be escaped.
            AnsiConsole.MarkupLine(
                $"[red]Failed testing {e.Caller.EscapeMarkup()}[/] [grey]{stopWatch.ElapsedMilliseconds}ms[/]{Environment.NewLine}{msg}");
        }
        catch (Exception e)
        {
            AnsiConsole.MarkupLineInterpolated(
                $"[red]Failed {e.Message}[/] [grey]{stopWatch.ElapsedMilliseconds}ms[/]");
        }
    }

    AnsiConsole.WriteLine();
}

return 0;