using JetBrains.Annotations;
using LLama;
using LLama.Abstractions;

namespace RedPajama.ConsoleTest.TestRoutines;

internal class ParseTags : ITestRoutine
{
    [UsedImplicitly(ImplicitUseTargetFlags.Members)]
    public class BlogPost
    {
        public required string Title { get; init; }
        public required string[] Tags { get; init; }
    }

    public async Task Run(LLamaWeights model, IContextParams parameters)
    {
        const string prompt = """
                              Extract the title and tags from this blog post:
                              ```
                              Understanding Machine Learning
                              Tags: #ai #programming #python #tutorial
                              ```
                              """;

        var post = await model.InferAsync<BlogPost>(parameters, prompt, JsonContext.Default, TypeModelContext.Default);

        post.ShouldAllBe([
            p => p.Title.ShouldBe("Understanding Machine Learning"),
            p => p.Tags.ShouldBe(["ai", "programming", "python", "tutorial"])
        ]);
    }
}