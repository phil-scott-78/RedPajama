using System.Collections.Concurrent;
using System.Diagnostics;
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
using LLama.Transformers;
using MinjaSharp;

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
    private static readonly ConcurrentDictionary<string, Template> TemplateCache = new();

    public static async Task<T> InferAsync<[MeansImplicitUse(
            ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature,
            ImplicitUseTargetFlags.WithMembers)]
        T>(
        this LLamaWeights model,
        IContextParams parameters,
        string prompt,
        JsonSerializerContext jsonContext,
        PajamaTypeModelContext? typeModelContext = null,
        Action<string>? thoughtsCallback = null
        ) where T : class
    {
        var executor = new StatelessExecutor(model, parameters);
        
        var typeModelBuilder = GetTypeModelBuilder<T>(typeModelContext);

        var gbnfGenerator = new GbnfGenerator();
        var jsonSampleGenerator = new JsonSampleGenerator();
        var gbnf = gbnfGenerator.Generate(typeModelBuilder);
        var jsonSample = jsonSampleGenerator.Generate(typeModelBuilder);
        var sampleInstructions = jsonSampleGenerator.SampleInstructions();

        var grammar = new Grammar(gbnf, "root");
        var inferenceParams = new InferenceParams
        {
            SamplingPipeline = new DefaultSamplingPipeline
            {
                Grammar = grammar,
                Seed = 1229,
                Temperature = 0.1f,
                RepeatPenalty = 1.1f,
                GrammarOptimization = DefaultSamplingPipeline.GrammarOptimizationMode.Extended
            }
        };


        var promptWithSample = $$"""
                                 {{prompt}}

                                 RESPONSE FORMAT:
                                 {{jsonSample}}

                                 GENERAL INSTRUCTIONS:
                                 {{sampleInstructions}}
                                 """;

        var templatedPrompt = ApplyTemplate(model, promptWithSample);

        var sb = new StringBuilder();
        var results = executor.InferAsync(templatedPrompt, inferenceParams);
        await foreach (var s in results)
        {
            sb.Append(s);
        }
        
        var jsonSpan = sb.ToString().AsSpan().Trim();

        // Check if the string starts with <think>
        if (!jsonSpan.StartsWith("<think>", StringComparison.OrdinalIgnoreCase))
        {
            // if not, deserialize the json
            return JsonSerializer.Deserialize(jsonSpan, typeof(T), jsonContext) as T ??
                   throw new InvalidOperationException("Couldn't deserialize result");
            
        }
        // Find the closing </think> tag
        var endTagIndex = jsonSpan.IndexOf("</think>", StringComparison.OrdinalIgnoreCase);
        if (endTagIndex == -1)
        {
            return JsonSerializer.Deserialize(jsonSpan, typeof(T), jsonContext) as T ??
                   throw new InvalidOperationException("Couldn't deserialize result");
        }
        
        // Extract the content within <think>...</think>
        var thoughtContent = jsonSpan.Slice("<think>".Length, endTagIndex - "<think>".Length);

        // Slice the span to get the JSON content after </think>
        jsonSpan = jsonSpan[(endTagIndex + "</think>".Length)..];

        thoughtsCallback?.Invoke(thoughtContent.ToString());
        
        // Now deserialize as before
        return JsonSerializer.Deserialize(jsonSpan, typeof(T), jsonContext) as T ??
               throw new InvalidOperationException("Couldn't deserialize result");
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


    private static string ApplyTemplate(LLamaWeights model, string prompt)
    {
        try
        {
            const string chatTemplateKey = "tokenizer.chat_template";
            const string modelNameKey = "general.name";

            var modelName = model.Metadata.TryGetValue(modelNameKey, out var value) 
                ? value 
                : model.NativeHandle.ToString();
        
            var template = TemplateCache.GetOrAdd(modelName, _ =>
            {
                var templateContent = model.Metadata[chatTemplateKey];
                return new Template(templateContent);
            });

            var request = new ChatRequest()
            {
                Messages =
                [
                    new ChatMessage()
                    {
                        Role = "user",
                        Content = prompt
                    }
                ],
                AddGenerationPrompt = true
            };

            var templatedPrompt = template.Render(request);
            return templatedPrompt;
        }
        catch (Exception e)
        {
            Debug.WriteLine(e.ToString());

            var lLamaTemplate = new LLamaTemplate(model);
            lLamaTemplate.Add("user", prompt);
            lLamaTemplate.AddAssistant = true;
            
            return PromptTemplateTransformer.ToModelPrompt(lLamaTemplate);
        }
        
    }
    // ReSharper disable UnusedMember.Local
    // ReSharper disable UnusedAutoPropertyAccessor.Local
    private class ChatRequest
    {
        [JsonPropertyName("messages")] public List<ChatMessage> Messages { get; set; } = [];

        [JsonPropertyName("add_generation_prompt")]
        public bool? AddGenerationPrompt { get; set; }

        [JsonPropertyName("enable_thinking")] public bool? EnableThinking { get; set; }
    }

    private class ChatMessage
    {
        [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")] public string Content { get; set; } = string.Empty;

        [JsonPropertyName("reasoning_content")]
        public string? ReasoningContent { get; set; }
    }
    // ReSharper restore UnusedMember.Local
    // ReSharper restore UnusedAutoPropertyAccessor.Local


#pragma warning disable IL4000
    [FeatureGuard(typeof(RequiresUnreferencedCodeAttribute))]
    private static bool IsDynamicCodeSupported => RuntimeFeature.IsDynamicCodeSupported;
#pragma warning restore IL4000
}