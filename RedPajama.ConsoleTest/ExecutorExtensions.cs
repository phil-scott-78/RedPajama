using System.Buffers;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LLama;
using LLama.Abstractions;
using LLama.Common;
using LLama.Native;
using LLama.Sampling;

namespace RedPajama.ConsoleTest;

public static class LlamaWeightExtensions
{
    public static bool IsThinkingModel(this LLamaWeights weights)
    {
        return weights.Metadata["tokenizer.chat_template"].Contains("</think>"); 
    }
}

public static class ExecutorExtensions
{
    
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true, 
        WriteIndented = true, 
        Converters = { new JsonStringEnumConverter() }, 
        ReadCommentHandling = JsonCommentHandling.Skip
    };
    
    public static async Task<(T Result, string Thoughts)> InferWithThoughtsAsync<T>(this ILLamaExecutor executor, string prompt)
    {
        var responseSb = new StringBuilder();
        var thoughtsSb = new StringBuilder();
        await foreach (var s in InferInternalAsync<T>(executor, prompt, true))
        {
            if (s.responseType == ResultType.Response)
            {
                responseSb.Append(s.Value);
            }
            else
            {
                thoughtsSb.Append(s.Value);
            }
        }

        var json = responseSb.ToString();
        var o = JsonSerializer.Deserialize<T>(json, JsonSerializerOptions) ?? throw new InvalidOperationException("Couldn't deserialize result");
        return (o, thoughtsSb.ToString());
    }
    
    public static async Task<T> InferAsync<T>(this ILLamaExecutor executor, string prompt)
    {
        var sb = new StringBuilder();
        await foreach (var s in InferInternalAsync<T>(executor, prompt, false))
        {
            sb.Append(s.Value);
        }

        var json = sb.ToString().Replace("```json", string.Empty).Replace("```", string.Empty);
        return JsonSerializer.Deserialize<T>(json, JsonSerializerOptions) ?? throw new InvalidOperationException("Couldn't deserialize result");
    }

    private enum ResultType{Thinking, Response}

    private static async IAsyncEnumerable<(string Value, ResultType responseType)> InferInternalAsync<T>(ILLamaExecutor executor,
        string prompt, bool isThinkingModel)
    {
        var typeModelBuilder = new TypeModelBuilder<T>().Build();
        var gbnfGenerator = new GbnfGenerator();
        var larkGenerator = new LlGuidanceGenerator();
        var jsonSampleGenerator = new JsonSampleGenerator();
        
        var gbnf = gbnfGenerator.Generate(typeModelBuilder);
        /*
        lark support is in common, but not the base llama.cpp so no go
        var lark = larkGenerator.Generate(typeModelBuilder);
        */ 
        var jsonSample = jsonSampleGenerator.Generate(typeModelBuilder);
        var sampleInstructions = jsonSampleGenerator.SampleInstructions();

        InferenceParams inferenceParams;
        if (isThinkingModel)
        {
            var gbnfWithThinking = """
                                   root-with-thinking        ::= "<think>" think-content "</think>" space root

                                   # Think content rules (any characters except </think>)
                                   think-content ::= ( normal-char | safe-lt ){1,4086}
                                   normal-char    ::= [^<]
                                   safe-lt        ::= "<" ( [^/] | "/" [^t] | "/t" [^h] | "/th" [^i] | "/thi" [^n] | "/thin" [^k] | "/think" [^>] )

                                   """ + gbnf;
            var grammar = new Grammar(gbnfWithThinking, "root-with-thinking");
            inferenceParams = new InferenceParams
            {
                SamplingPipeline = new DefaultSamplingPipeline {Grammar = grammar, Seed = 1229, Temperature = 0.7f, TopK = 40}
                
            };
        }
        else
        {
            var grammar = new Grammar(gbnf, "root");
            inferenceParams = new InferenceParams
            {
                SamplingPipeline = new DefaultSamplingPipeline {Grammar = grammar, Seed = 1229, Temperature = 0.7f, TopK = 40}
            };

        }


        var promptWithSample = $"""
                                {prompt}

                                Return results as valid JSON in the following format:
                                {jsonSample}

                                {sampleInstructions}
                                """;

        var isThinking = false;
        await foreach (var s in executor.InferAsync(promptWithSample, inferenceParams))
        {
            if (s.Contains("<think>"))
            {
                isThinking = true;
            }

            if (s.Contains("</think>"))
            {
                isThinking = false;
            }

            var resultType = isThinking ? ResultType.Thinking : ResultType.Response;
            var value = s.Replace("<think>", string.Empty).Replace("</think>", string.Empty);
            if (string.IsNullOrEmpty(value) == false)
            {
                Debug.Write(value);
                yield return (value, resultType);
            }
        }
    }
}
