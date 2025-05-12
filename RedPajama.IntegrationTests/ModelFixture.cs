using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LLama;
using LLama.Common;
using LLama.Native;
using LLama.Sampling;

namespace RedPajama.IntegrationTests;

[CollectionDefinition("Model collection")]
public class ModelCollection : ICollectionFixture<ModelFixture>
{
    // This class has no code and is never created.
    // Its purpose is simply to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}

public class ModelFixture : IDisposable
{
    static ModelFixture()
    {
        var allFiles = Directory.EnumerateFiles(Environment.CurrentDirectory, "*.gguf").ToArray();
        if (allFiles.Length == 0)
        {
            ModelFileName = "(none)";
        }

        ModelFileName = allFiles.FirstOrDefault() ?? "(none)";
    }

    public static readonly string ModelFileName;

    private static readonly JsonSerializerOptions JsonSerializerOptions = new JsonSerializerOptions()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new JsonStringEnumConverter() }
    };

    private LLamaWeights? _lamaWeights;

    private readonly ModelParams _parameters = new(ModelFileName)
    {
        ContextSize = 512, // tiny context
        GpuLayerCount = -1, // all the gpu we got
        BatchSize = 512, // small batches
        BatchThreads = 512, // line up the batch size with the thread
        Threads = 1, // single threaded
    };

    [ModuleInitializer]
    public static void Init()
    {
        NativeLogConfig.llama_log_set((_, _) => { });
    }

    public async Task<T> TestGrammarAsync<T>(string prompt)
    {
        _lamaWeights ??= await LLamaWeights.LoadFromFileAsync(_parameters);
        var builder = new TypeModelBuilder<T>().Build();
        var gbnfGenerator = new GbnfGenerator(new GbnfGeneratorSettings()
        {
            MaxThinkingLength = 64 // if its thinking we don't care, we only worry about the Gbnf being accurate not the results
        });
        
        var gbnf = gbnfGenerator.Generate(builder);

        var grammar = new Grammar(gbnf, "root");
        var inferenceParams = new InferenceParams
        {
            SamplingPipeline = new DefaultSamplingPipeline
            {
                Grammar = grammar,
                Seed = 1229,
                Temperature = 0f, // no creativity
                TopK = 1, 
                GrammarOptimization = DefaultSamplingPipeline.GrammarOptimizationMode.Extended
            }
        };

        var executor = new StatelessExecutor(_lamaWeights, _parameters);

        var sb = new StringBuilder();
        await foreach (var s in executor.InferAsync(prompt, inferenceParams))
        {
            sb.Append(s);
        }

        var jsonSpan = sb.ToString().AsSpan().Trim();
        var thoughts = new StringBuilder();

        // Check if the string starts with <think>
        if (!jsonSpan.StartsWith("<think>", StringComparison.OrdinalIgnoreCase))
        {
            // if not, deserialize the json
            return JsonSerializer.Deserialize<T>(jsonSpan.ToString(), JsonSerializerOptions) ??
                throw new InvalidOperationException("Couldn't deserialize result");
            
        }
        // Find the closing </think> tag
        var endTagIndex = jsonSpan.IndexOf("</think>", StringComparison.OrdinalIgnoreCase);
        if (endTagIndex == -1)
        {
            return JsonSerializer.Deserialize<T>(jsonSpan.ToString(), JsonSerializerOptions) ??
                throw new InvalidOperationException("Couldn't deserialize result");
        }
        
        // Extract the content within <think>...</think>
        var thoughtContent = jsonSpan.Slice("<think>".Length, endTagIndex - "<think>".Length);
        thoughts.Append(thoughtContent);

        // Slice the span to get the JSON content after </think>
        jsonSpan = jsonSpan[(endTagIndex + "</think>".Length)..];

        // this almost certainly won't work, but let's give it a go
        return JsonSerializer.Deserialize<T>(jsonSpan, JsonSerializerOptions) ??
               throw new InvalidOperationException("Couldn't deserialize result");
    }

    private bool _disposed;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            _lamaWeights?.Dispose();
        }

        _disposed = true;
    }
}