using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;
using LLama;
using LLama.Abstractions;
using LLama.Common;
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
    public static async Task<(T Result, string Thoughts)> InferWithThoughtsAsync<[MeansImplicitUse(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature, ImplicitUseTargetFlags.WithMembers)] T>(
        this ILLamaExecutor executor, 
        string prompt,
        JsonSerializerContext jsonContext,
        PajamaTypeModelContext? typeModelContext = null
    ) where T : class
    
    {
        var responseSb = new StringBuilder();
        var thoughtsSb = new StringBuilder();
        await foreach (var s in InferInternalAsync<T>(executor, prompt, true, typeModelContext))
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
        var o = JsonSerializer.Deserialize(json, typeof(T), jsonContext) as T ?? throw new InvalidOperationException("Couldn't deserialize result");
        return (o, thoughtsSb.ToString());
    }
    
    public static async Task<T> InferAsync<[MeansImplicitUse(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature, ImplicitUseTargetFlags.WithMembers)] T>(this ILLamaExecutor executor, string prompt,
        JsonSerializerContext jsonContext,
        PajamaTypeModelContext? typeModelContext = null
        ) where T : class
    {
        var sb = new StringBuilder();
        await foreach (var s in InferInternalAsync<T>(executor, prompt, false, typeModelContext))
        {
            if (s.responseType == ResultType.Response)
            {
                sb.Append(s.Value);    
            }
        }

        var json = sb.ToString();
        return JsonSerializer.Deserialize(json, typeof(T), jsonContext) as T ?? throw new InvalidOperationException("Couldn't deserialize result");
    }

    private enum ResultType{Thinking, Response}

    private static async IAsyncEnumerable<(string Value, ResultType responseType)> InferInternalAsync<T>(
        ILLamaExecutor executor,
        string prompt, 
        bool isThinkingModel,
        PajamaTypeModelContext? typeModelContext = null
        )
    {
        var typeModelBuilder = GetTypeModelBuilder<T>(typeModelContext);
        
        var gbnfGenerator = new GbnfGenerator();
        var jsonSampleGenerator = new JsonSampleGenerator();
        
        var gbnf = gbnfGenerator.Generate(typeModelBuilder);
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
                SamplingPipeline = new DefaultSamplingPipeline {Grammar = grammar, Seed = 1229, Temperature = 0.7f, GrammarOptimization = DefaultSamplingPipeline.GrammarOptimizationMode.Extended}
                
            };
        }
        else
        {
            var grammar = new Grammar(gbnf, "root");
            inferenceParams = new InferenceParams
            {
                SamplingPipeline = new DefaultSamplingPipeline {Grammar = grammar, Seed = 1229, RepeatPenalty = 1.1f,FrequencyPenalty = 0, Temperature = 0.1f, TopP = 0.9f, GrammarOptimization = DefaultSamplingPipeline.GrammarOptimizationMode.Extended}
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
                yield return (value, resultType);
            }
        }
    }

    private static TypeModel GetTypeModelBuilder<T>(PajamaTypeModelContext? typeModelContext)
    {
        if (typeModelContext == null && IsDynamicCodeSupported)
        {
            return new TypeModelBuilder<T>().Build();
        }

        if (typeModelContext != null)
        {
            return typeModelContext.Get<T>();
        }

        throw new Exception("TypeModelContext must be passed if running in AOT");

    }

#pragma warning disable IL4000
    [FeatureGuard(typeof(RequiresUnreferencedCodeAttribute))]
    private static bool IsDynamicCodeSupported => RuntimeFeature.IsDynamicCodeSupported;
#pragma warning restore IL4000
}
