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
    if (a is LLamaLogLevel.Info or LLamaLogLevel.Debug or LLamaLogLevel.None or LLamaLogLevel.Continue)
    {
        return;
    }

    // Debug.WriteLine($"[{a}] - {b.Trim()} ");
});

AnsiConsole.WriteLine();

var path = args.Length == 1
    ? args[0]
    : AnsiConsole.Prompt(new TextPrompt<string>("Path to GGUF model file or folder containing multiple models: "));

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
    var pattern = Path.GetFileName(path);

    var matcher = new Matcher();
    matcher.AddInclude(pattern);
    var result = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(directory)));

    models.AddRange(result.Files.Select(i => Path.Combine(directory, i.Path)));
}

// let's shuffle them up
models = models
    .Select(x => (x, Random.Shared.Next()))
    .OrderBy(tuple => tuple.Item2)
    .Select(tuple => tuple.Item1)
    .ToList();

var tests = new List<ITestRoutine>()
{
    new BatchedOperation(),
    new ParseComplexRestaurantOrder(),
    new ParseEmailAndExtractGuid(),
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
        ContextSize = 2000,
        GpuLayerCount = -1,
    };

    AnsiConsole.MarkupLineInterpolated($"[blue]{Path.GetFileNameWithoutExtension(modelFile)}[/]");
    using var model = await LLamaWeights.LoadFromFileAsync(parameters);
    // let's mix it up on the spinner for thinking models
    var spinner = model.IsThinkingModel() ? Spinner.Known.Dots8Bit : Spinner.Known.Dots2;

    foreach (var r in tests)
    {
        var stopWatch = new Stopwatch();
        stopWatch.Start();
        try
        {
            AnsiConsole.MarkupInterpolated($"  {r.GetType().Name}: ");
            await r.Run(model, parameters).Spinner(spinner);
            AnsiConsole.MarkupLine($"[green]Ok[/] [grey]{stopWatch.ElapsedMilliseconds}ms[/]");
        }
        catch (NotEqualException e)
        {
            AnsiConsole.MarkupLine(
                $"[red]Failed testing {e.Caller.EscapeMarkup()}[/] [grey]{stopWatch.ElapsedMilliseconds}ms[/]");
            AnsiConsole.Write(new Panel(SpectreHelpers.CreateExceptionRows(e)).Padding(4, 0, 0, 0).NoBorder());
        }
        catch (BunchOfNotEqualException e)
        {
            AnsiConsole.MarkupLine(
                $"[red]Failed testing {e.Caller.EscapeMarkup()}[/] [grey]{stopWatch.ElapsedMilliseconds}ms[/]");
            AnsiConsole.Write(new Panel(SpectreHelpers.CreateExceptionRows(e)).Padding(4, 0, 0, 0).NoBorder());
        }
        catch (Exception e)
        {
            AnsiConsole.MarkupLineInterpolated(
                $"[red]Failed {e.Message}[/] [grey]{stopWatch.ElapsedMilliseconds}ms[/]");
            Debug.WriteLine(e.ToString());
        }
    }

    AnsiConsole.WriteLine();
}