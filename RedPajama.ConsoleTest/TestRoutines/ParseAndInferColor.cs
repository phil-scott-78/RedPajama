using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;
using LLama;
using LLama.Abstractions;

namespace RedPajama.ConsoleTest.TestRoutines;

internal class ParseAndInferColor : ITestRoutine
{
    [UsedImplicitly(ImplicitUseTargetFlags.Members)]
    private class ColorDescription
    {
        public required string Item { get; init; }
        public required string Description { get; init; }
        [AllowedValues("red", "orange", "yellow", "green", "blue", "purple")]
        public required string Color { get; init; }
    }
   
    public async Task Run(LLamaWeights model, IContextParams parameters)
    {
        var executor = new StatelessExecutor(model, parameters);

        var item = await executor.InferAsync<ColorDescription>("""
                                                               Extract the item details and infer the closest rainbow color:
                                                               ```
                                                               Item: Fresh Banana
                                                               Description: A ripe, bright-colored curved fruit with a thick peel
                                                               ```
                                                               """);

        item.ShouldAllBe([
            i => i.Item.ShouldBe("Fresh Banana"),
            i => i.Description.ShouldBe("A ripe, bright-colored curved fruit with a thick peel"),
            i => i.Color.ShouldBe("yellow")  // LLM should infer yellow as the appropriate rainbow color for a banana
        ]);
    }
}