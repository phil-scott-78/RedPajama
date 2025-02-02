using System.Text;
using LLama;
using LLama.Common;
using LLama.Native;
using RedPajama.ConsoleTest;
using RedPajama.ConsoleTest.TestRoutines;
using Spectre.Console;
using Spectre.Console.Extensions;

Console.OutputEncoding = Encoding.UTF8;

NativeLogConfig.llama_log_set((_, _) => { });

// await new VarietyOfScenarios().RunAsync();
// Environment.Exit(0);

AnsiConsole.WriteLine();
var path = AnsiConsole.Prompt(new TextPrompt<string>("Path to file or folder: "));

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
    AnsiConsole.MarkupLineInterpolated($"[red]Error:[/] could not find file or folder {path}");
    return -1;
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
    };
    
    AnsiConsole.MarkupLineInterpolated($"[blue]{Path.GetFileNameWithoutExtension(modelFile)}[/]");
    using var model = await LLamaWeights.LoadFromFileAsync(parameters);

    foreach (var r in tests)
    {
        try
        { 
            AnsiConsole.MarkupInterpolated($"  {r.GetType().Name}: ");
            await r.Run(model, parameters).Spinner(Spinner.Known.Dots2);
            AnsiConsole.MarkupLineInterpolated($"[green]Ok[/]");
        }
        catch (BunchOfNotEqualException e)
        {
            var msg = string.Join(Environment.NewLine,
                e.Exception.Select(i =>
                    $"   * [blue]{i.Caller.EscapeMarkup()}[/] expected to be {i.Expected.EscapeMarkup()}, received {i.Actual.EscapeMarkup()}"));
            AnsiConsole.MarkupLine($"[red]Failed testing {e.Caller.EscapeMarkup()}[/].{Environment.NewLine}{msg}");
        }
        catch (Exception e)
        {
            AnsiConsole.MarkupLine($"[red]Failed {e.Message.EscapeMarkup()}[/]");

        }
    }
    
    AnsiConsole.WriteLine();
}

return 0;

