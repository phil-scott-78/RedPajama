using System.ComponentModel;
using LLama;
using LLama.Abstractions;

namespace RedPajama.ConsoleTest.TestRoutines;

internal class ParseOrderStatus : ITestRoutine
{
    public class Order
    {
        public required string OrderId { get; init; }
        public required OrderStatus Status { get; init; }
        [Description("Time in UTC")] public required DateTimeOffset LastUpdated { get; init; }
        public required decimal Balance { get; init; }
    }
    
            
    public enum OrderStatus
    {
        Pending,
        Processing,
        Shipped,
        Delivered,
        Cancelled
    }

    public async Task Run(LLamaWeights model, IContextParams parameters)
    {
        var executor = new StatelessExecutor(model, parameters);
        const string prompt = """
                              Extract the order ID, status and last update time from this notification:
                              ```
                              Order #A12345 status changed to Processing on January 25, 2024 at 3:30 PM UTC. Balance due is $95.05
                              ```
                              """;

        Order order;
        if (!model.IsThinkingModel())
        {
            order = await executor.InferAsync<Order>(prompt);
        }
        else
        {
            (order, _) = (await executor.InferWithThoughtsAsync<Order>(prompt));
        }

        order.ShouldAllBe([
            o => o.OrderId.ShouldBe("A12345"),
            o => o.Status.ShouldBe(OrderStatus.Processing),
            o => o.LastUpdated.ShouldBe(new DateTimeOffset(2024, 1, 25, 15, 30, 0, TimeSpan.Zero)),
            o => o.Balance.ShouldBe(95.05M),
        ]);
    }
}