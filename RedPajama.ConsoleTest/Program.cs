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
    // new BatchedOperation(),
    new ParseComplexRestaurantOrder(),
    new ParseComplexRestaurantOrderInParts(),
    new ParseEmailAndExtractGuid(),
    new ParseNameAndEmail(),
    new ParseOrderStatus(),
    new ParseNestedAddress(),
    new ParseBookCollection(),
    new ParseTags(),
    new ParseAndInferColor()
};


Dictionary<string, Dictionary<string, (bool Success, TimeSpan Elapsed)>> timings = new();
foreach (var modelFile in models)
{
    var parameters = new ModelParams(modelFile)
    {
        ContextSize = 2000,
        GpuLayerCount = -1,
    };

    var modeFileName = Path.GetFileNameWithoutExtension(modelFile);
    AnsiConsole.MarkupLineInterpolated($"[blue]{modeFileName}[/]");
    using var model = await LLamaWeights.LoadFromFileAsync(parameters);
    // let's mix it up on the spinner for thinking models
    var spinner = model.IsThinkingModel() ? Spinner.Known.Dots8Bit : Spinner.Known.Dots2;

    Dictionary<string,  (bool Success, TimeSpan Elapsed)> timing = new(); 
    foreach (var r in tests)
    {
        bool result = false;
        var stopWatch = new Stopwatch();
        stopWatch.Start();
        try
        {
            AnsiConsole.MarkupInterpolated($"  {r.GetType().Name}: ");
            await r.Run(model, parameters).Spinner(spinner);
            AnsiConsole.MarkupLine($"[green]Ok[/] [grey]{stopWatch.ElapsedMilliseconds}ms[/]");
            result = true;
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
        
        stopWatch.Stop();
        timing.Add(r.GetType().Name, (result, stopWatch.Elapsed));
    }
    
    timings.Add(modeFileName, timing);

    AnsiConsole.WriteLine();
}

// Create a table to display the timing results
var table = new Table();
table.Border(TableBorder.Rounded);

// Add the "Model" column first
table.AddColumn(new TableColumn("Model").LeftAligned());

// Get all unique test names
var allTestNames = timings.Values
    .SelectMany(modelTiming => modelTiming.Keys)
    .Distinct()
    .OrderBy(testName => testName)
    .ToList();

// Add columns for each test
foreach (var testName in allTestNames)
{
    table.AddColumn(new TableColumn(testName).RightAligned());
}

// Add rows for each model
foreach (var (modelName, value) in timings.OrderBy(t => t.Key))
{
    var cells = new List<string> { $"[bold]{modelName}[/]" };

    // Add cells for each test
    foreach (var testName in allTestNames)
    {
        if (value.TryGetValue(testName, out var result))
        {
            var elapsed = result.Elapsed.TotalMilliseconds.ToString("F0");
            var color = result.Success ? "green" : "red";
            cells.Add($"[{color}]{elapsed}ms[/]");
        }
        else
        {
            cells.Add("[grey]N/A[/]");
        }
    }

    table.AddRow(cells.ToArray());
}

// Render the table
AnsiConsole.Write(table);