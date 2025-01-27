using LLama;
using LLama.Common;
using LLama.Native;
using RedPajama.ConsoleTest;
using RedPajama.ConsoleTest.TestRoutines;
using Spectre.Console;

AnsiConsole.WriteLine();
var path = AnsiConsole.Prompt(new TextPrompt<string>("Path to file or folder: "));

List<string> models = new(); 

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

NativeLogConfig.llama_log_set((_, _) => { });

var tests = new List<ITestRoutine>()
{
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
            await r.Run(model, parameters);
            AnsiConsole.MarkupLineInterpolated($" [green]Ok[/]");
        }
        catch (BunchOfNotEqualException e)
        {
            var msg =  string.Join(Environment.NewLine,
                e.Exception.Select(i => $"   * [blue]{i.Caller.EscapeMarkup()}[/] expected to be {i.Expected.EscapeMarkup()}, received {i.Actual.EscapeMarkup()}"));
            AnsiConsole.MarkupLine($"[red]Failed testing {e.Caller.EscapeMarkup()}[/].{Environment.NewLine}{msg}");
        }
    }
    
    AnsiConsole.WriteLine();
}

return 0;