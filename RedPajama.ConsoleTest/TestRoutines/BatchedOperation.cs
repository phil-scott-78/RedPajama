using System.Collections.Concurrent;
using System.Diagnostics;
using LLama;
using LLama.Abstractions;
using LLama.Batched;

namespace RedPajama.ConsoleTest.TestRoutines;

public class BatchedOperation : ITestRoutine
{
    public enum Confidence
    {
        Low, Medium, High
    }

    public class Answer
    {
        public required bool Value { get; init; }
        public required Confidence Confidence { get; init; }
    }
    
    public async Task Run(LLamaWeights model, IContextParams parameters)
    {
        const int count = 100;
        
        using var batchedExecutor = new BatchedExecutor(model, parameters);
        var data = LoadData("assets/train.csv").Take(count).ToArray();

        var prompts = data.Select(i => new KeyValuePair<int, string>(i.Index, GetPrompt(i.Question, i.Passage))).ToArray();
        ConcurrentDictionary<int, (bool IsCorrect, Confidence Confidence)> results = new();
        await foreach (var s in batchedExecutor.ExecuteAsync<int, Answer>(prompts, JsonContext.Default, TypeModelContext.Default))
        {
            var correctResult = data.First(i => i.Index == s.Key).Answer == s.Value.Value;
            results.AddOrUpdate(s.Key, (correctResult, s.Value.Confidence), (_, v) => v);
        }

        // Basic correctness count
        var correct = results.Count(i => i.Value.IsCorrect);
        var incorrect = results.Count(i => i.Value.IsCorrect == false);

        // Track confidence-based metrics
        var confidenceResults = new Dictionary<Confidence, (int Correct, int Incorrect)>
        {
            { Confidence.Low, (0, 0) },
            { Confidence.Medium, (0, 0) },
            { Confidence.High, (0, 0) }
        };

        foreach (var result in results)
        {
            var (isCorrect, confidence) = result.Value;
        
            // Update confidence tracking
            if (isCorrect)
            {
                confidenceResults[confidence] = (confidenceResults[confidence].Correct + 1, confidenceResults[confidence].Incorrect);
            }
            else
            {
                confidenceResults[confidence] = (confidenceResults[confidence].Correct, confidenceResults[confidence].Incorrect + 1);
            }
        }
        
        Debug.WriteLine($"Batched operation completed. Correct: {correct}, Incorrect: {incorrect}");
        Debug.WriteLine($"Confidence breakdown:");
        Debug.WriteLine($"  High: {confidenceResults[Confidence.High].Correct} correct, {confidenceResults[Confidence.High].Incorrect} incorrect");
        Debug.WriteLine($"  Medium: {confidenceResults[Confidence.Medium].Correct} correct, {confidenceResults[Confidence.Medium].Incorrect} incorrect");
        Debug.WriteLine($"  Low: {confidenceResults[Confidence.Low].Correct} correct, {confidenceResults[Confidence.Low].Incorrect} incorrect");

        if (results.Count != count)
        {
            throw new NotEqualException("batched operations count", "100", results.Count.ToString());
        }

        foreach (var p in prompts)
        {
            if (results.ContainsKey(p.Key) == false)
            {
                throw new NotEqualException("missing key", "", p.Key.ToString());
            }
        }
        
        if (correct < 60)
        {
            throw new NotEqualException("batched operations", "100", correct.ToString());
        }
    }
    
    string GetPrompt(string question, string passage)
    {
        return $"""
                Read this passage:
                {passage}

                Based on your reading, return true or false for your answer, and your confidence on your answer:
                {question}
                """;

    }
    
    private static IEnumerable<(int Index, string Question , bool Answer, string Passage)> LoadData(string path)
    {
        var counter = 0;
        foreach (var line in File.ReadLines(path))
        {
            var splits = line.Split(",");

            if (!bool.TryParse(splits[1], out var boolean))
                continue;

            var hint = string.Join(",", splits[2..]);
            hint = hint.Trim('\"');

            yield return (counter, splits[0], boolean, hint);
            counter++;
        }
    }
}