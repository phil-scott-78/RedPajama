using LLama;
using LLama.Abstractions;

namespace RedPajama.ConsoleTest.TestRoutines;

internal class ParseOrderStatus : ITestRoutine
{
    enum OrderStatus
    {
        Pending,
        Processing,
        Shipped,
        Delivered,
        Cancelled
    }

    class Order
    {
        public required string OrderId { get; init; }
        public required OrderStatus Status { get; init; }
        public required DateTimeOffset LastUpdated { get; init; }
        public required decimal Balance { get; init; }
    }
    
    public async Task Run(LLamaWeights model, IContextParams parameters)
    {
        var executor = new StatelessExecutor(model, parameters);

        var order = await executor.InferAsync<Order>("""
                                                     Extract the order ID, status and last update time from this notification:
                                                     ```
                                                     Order #A12345 status changed to Processing on January 25, 2024 at 3:30 PM UTC. Balance due is $95.05
                                                     ```
                                                     """);

        order.ShouldAllBe([
            o => o.OrderId.ShouldBe("A12345"),
            o => o.Status.ShouldBe(OrderStatus.Processing),
            o => o.LastUpdated.ShouldBe(new DateTimeOffset(2024, 1, 25, 15, 30, 0, TimeSpan.Zero)),
            o => o.Balance.ShouldBe(95.05M),
        ]);
    }
}

internal class ParseTags : ITestRoutine
{
    class BlogPost
    {
        public required string Title { get; init; }
        public required string[] Tags { get; init; }
    }
   
    public async Task Run(LLamaWeights model, IContextParams parameters)
    {
        var executor = new StatelessExecutor(model, parameters);

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