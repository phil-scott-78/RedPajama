using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;
using LLama;
using LLama.Batched;
using LLama.Native;
using LLama.Sampling;

namespace RedPajama.ConsoleTest;

public static class BatchedExecutorExtensions
{
    public static async IAsyncEnumerable<KeyValuePair<TKey, TValue>> ExecuteAsync<TKey, [MeansImplicitUse] TValue>(
        this BatchedExecutor executor, 
        IEnumerable<KeyValuePair<TKey, string>> prompts,
        JsonSerializerContext jsonSerializerContext,
        PajamaTypeModelContext pajamaTypeModelContext,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) where TKey : notnull where TValue : class
    {
        var typeModelBuilder = pajamaTypeModelContext.Get<TValue>();
        var gbnfGenerator = new GbnfGenerator();
        var jsonSampleGenerator = new JsonSampleGenerator();

        var gbnf = gbnfGenerator.Generate(typeModelBuilder);
        var jsonSample = jsonSampleGenerator.Generate(typeModelBuilder);
        var sampleInstructions = jsonSampleGenerator.SampleInstructions();

        var vocab = executor.Context.NativeHandle.ModelHandle.Vocab;
        var queue = new Queue<KeyValuePair<TKey, string>>(prompts.Select(i =>
            new KeyValuePair<TKey, string>(i.Key, executor.TemplatePrompt(i.Value, jsonSample, sampleInstructions))));

        // Dynamic management of max active conversations based on KV slot availability
        var currentMaxActiveSize = 64; // Start with maximum of 16, but will reduce if we hit NoKvSlot errors
        var activeConversations = ImmutableList.Create<ConversationData<TKey, TValue>>();

        while ((queue.Count > 0 || activeConversations.Any(data => !data.IsComplete)) &&
               cancellationToken.IsCancellationRequested == false)
        {
            // Only add new conversations if we haven't reached KV limits or if we have no active conversations
            if (queue.Count > 0 && activeConversations.Count < currentMaxActiveSize)
            {
                for (var i = activeConversations.Count; i < currentMaxActiveSize; i++)
                {
                    if (queue.Count == 0) continue;
                    var (key, templatedPrompt) = queue.Dequeue();

                    var conversation = executor.Create();
                    conversation.Prompt(executor.Context.Tokenize(templatedPrompt, addBos: true, special: true));

                    var conversationData = new ConversationData<TKey, TValue>
                    {
                        Key = key,
                        Prompt = templatedPrompt,
                        Conversation = conversation,
                        JsonContext = jsonSerializerContext,
                        Sampler = new DefaultSamplingPipeline
                        {
                            Grammar = new Grammar(gbnf, "root"),
                            GrammarOptimization = DefaultSamplingPipeline.GrammarOptimizationMode.Extended
                        },
                        Decoder = new StreamingTokenDecoder(executor.Context)
                    };
                    activeConversations = activeConversations.Add(conversationData);
                }
            }

            var decodeResult = await executor.Infer(cancellationToken);

            if (decodeResult != DecodeResult.Ok && decodeResult != DecodeResult.Error)
            {
                // Reduce the maxActive limit to prevent hitting the limit again
                // Ensure we don't go below 1 as we need at least one conversation
                currentMaxActiveSize = Math.Max(1, activeConversations.Count - 1);


                // Handle the NoKvSlot error by selecting a conversation to dispose
                if (activeConversations.Count > 0)
                {
                    // Find the conversation that has processed the most tokens (likely consuming most KV cache)
                    var conversationToDispose = activeConversations
                        .OrderByDescending(c => c.TokensSampled)
                        .First();

                    // Get the original prompt to add back to the queue
                    var promptToRequeue = new KeyValuePair<TKey, string>(
                        conversationToDispose.Key,
                        conversationToDispose.Prompt);

                    // Add it back to the queue
                    queue.Enqueue(promptToRequeue);

                    // Dispose the conversation
                    conversationToDispose.CleanupResources();

                    // Remove from active conversations list
                    activeConversations = activeConversations.Remove(conversationToDispose);

                    // Continue to next iteration to process the modified active conversations
                    continue;
                }
            }

            if (decodeResult == DecodeResult.Error)
            {
                throw new Exception("Unknown error occurred while inferring.");
            }

            foreach (var conversationData in activeConversations)
            {
                // Completed conversations don't need sampling.
                if (conversationData.IsComplete)
                    continue;

                if (conversationData.Conversation.RequiresSampling == false)
                {
                    // we need to call infer before we sample again.
                    continue;
                }

                // Use the sampling pipeline to choose a single token for this conversation.
                var token = conversationData.Conversation.Sample(conversationData.Sampler);

                // Some special tokens indicate that this sequence has ended. Check if that's what has been chosen by the sampling pipeline.
                if (token.IsEndOfGeneration(vocab))
                {
                    yield return new KeyValuePair<TKey, TValue>(conversationData.Key, conversationData.GetAnswer());
                    conversationData.CleanupResources();
                }
                else
                {
                    // It isn't the end of generation, so add this token to the decoder and then add that to our tracked data
                    conversationData.AppendToken(token);
                }
            }

            if (activeConversations.Any(i => i.IsComplete))
            {
                // remove all completed conversations from the list.
                activeConversations = activeConversations.RemoveAll(i => i.IsComplete);
            }
        }
    }

    private static string TemplatePrompt(this BatchedExecutor executor, string prompt, string jsonSample,
        string sampleInstructions)
    {
        var template = new LLamaTemplate(executor.Context.NativeHandle.ModelHandle);
        template.Add("system",
            "I am a helpful bot that returns short and concise answers. I include a ten word description of my reasoning when I finish.");
        template.Add("user", GetPromptWithSample(prompt, jsonSample, sampleInstructions));
        template.AddAssistant = true;
        return Encoding.UTF8.GetString(template.Apply());
    }

    private static string GetPromptWithSample(string prompt, string jsonSample, string sampleInstructions)
    {
        return $"""
                {prompt}

                Return results as valid JSON in the following format:
                {jsonSample}

                {sampleInstructions}
                """;
    }

    internal class ConversationData<TKey, TValue> where TKey : notnull where TValue : class
    {
        public required TKey Key { get; init; }
        public required string Prompt { get; init; }
        public required Conversation Conversation { get; init; }
        public required BaseSamplingPipeline Sampler { get; init; }
        public required StreamingTokenDecoder Decoder { get; init; }

        public bool IsComplete { get; private set; }
        public int TokensSampled { get; private set; }
        public required JsonSerializerContext JsonContext { get; init; }

        private readonly StringBuilder _stringBuilder = new();

        private void AppendAnswer(string s)
        {
            _stringBuilder.Append(s);
        }

        public void CleanupResources()
        {
            Conversation.Dispose();
            Sampler.Dispose();
            IsComplete = true;
        }

        public TValue GetAnswer()
        {
            var json = _stringBuilder.ToString();
            
            return JsonSerializer.Deserialize(json, typeof(TValue), JsonContext) as TValue ??
                   throw new InvalidOperationException("Couldn't deserialize result");
        }

        public void AppendToken(LLamaToken token)
        {
            TokensSampled++;
            Decoder.Add(token);
            AppendAnswer(Decoder.Read().ReplaceLineEndings(" "));
            // Prompt the conversation with this token, ready for the next round of inference to generate another token
            Conversation.Prompt(token);
        }
    }
}