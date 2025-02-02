using JetBrains.Annotations;
using LLama;
using LLama.Abstractions;

namespace RedPajama.ConsoleTest.TestRoutines;

internal class ParseTags : ITestRoutine
{    
    [UsedImplicitly(ImplicitUseTargetFlags.Members)]
    private class BlogPost
    {
        public required string Title { get; init; }
        public required string[] Tags { get; init; }
    }
   
    public async Task Run(LLamaWeights model, IContextParams parameters)
    {
        var executor = new StatelessExecutor(model, parameters){ApplyTemplate = true};
        var post = await executor.InferAsync<BlogPost>("""
                                                       Extract the title and tags from this blog post:
                                                       ```
                                                       Understanding Machine Learning
                                                       Tags: #ai #programming #python #tutorial
                                                       ```
                                                       """);

        post.ShouldAllBe([
            p => p.Title.ShouldBe("Understanding Machine Learning"),
            p => p.Tags.ShouldBe(["ai", "programming", "python", "tutorial"])
        ]);
    }
}