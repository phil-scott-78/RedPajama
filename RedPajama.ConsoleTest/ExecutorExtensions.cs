using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LLama.Abstractions;
using LLama.Common;
using LLama.Sampling;

namespace RedPajama.ConsoleTest;

public static class ExecutorExtensions
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true, 
        WriteIndented = true, 
        Converters = { new JsonStringEnumConverter() }, 
        ReadCommentHandling = JsonCommentHandling.Skip
    };
    
    public static async Task<T> InferAsync<T>(this ILLamaExecutor executor, string prompt)
    {
        var sb = new StringBuilder();

        var typeModelBuilder = new TypeModelBuilder<T>().Build();
        var gbnfGenerator = new GbnfGenerator();
        var larkGenerator = new LlGuidanceGenerator();
        var jsonSampleGenerator = new JsonSampleGenerator();
        
        var gbnf = gbnfGenerator.Generate(typeModelBuilder);
        var lark = larkGenerator.Generate(typeModelBuilder);
        var jsonSample = jsonSampleGenerator.Generate(typeModelBuilder);
        var sampleInstructions = jsonSampleGenerator.SampleInstructions();
        
        var inferenceParams = new InferenceParams
        {
            SamplingPipeline = new GreedySamplingPipeline()
            {
                Grammar = new Grammar(gbnf, "root"),
            },
        };

        var promptWithSample = $"""
                                {prompt}

                                Return results as valid JSON in the following format:
                                {jsonSample}

                                {sampleInstructions}
                                """;
        
        await foreach (var s in executor.InferAsync(promptWithSample, inferenceParams))
        {
            sb.Append(s);
        }
        
        var json = sb.ToString();
        return JsonSerializer.Deserialize<T>(json, JsonSerializerOptions) ?? throw new InvalidOperationException("Couldn't deserialize result");
    }
}
