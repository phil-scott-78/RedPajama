using System.ComponentModel;
using LLama;
using LLama.Abstractions;

namespace RedPajama.ConsoleTest.TestRoutines;

internal class ParseNestedAddress : ITestRoutine
{
    private class Customer 
    {
        public required string Name { get; init; }
        public required Address ShippingAddress { get; init; }
        public required Address BillingAddress { get; init; }
    }
    
            
    public class Address
    {
        public required string Street { get; init; }
        public required string City { get; init; }
        public required string State { get; init; }
        [Description("Digits only")]
        public required string ZipCode { get; init; }
    }
    

   
    public async Task Run(LLamaWeights model, IContextParams parameters)
    {
        var executor = new StatelessExecutor(model, parameters);
        const string prompt = """
                              Extract the customer name and addresses from this order:
                              ```
                              Customer: John Smith
                              Ship to: 123 Main St, Boston, MA 02108
                              Bill to: 456 Park Ave, New York, NY 10022
                              ```
                              """;
        
        Customer customer;
        if (!model.IsThinkingModel())
        {
            customer = await executor.InferAsync<Customer>(prompt);
        }
        else
        {
            (customer, _) = (await executor.InferWithThoughtsAsync<Customer>(prompt));
        }

        customer.ShouldAllBe([
            c => c.Name.ShouldBe("John Smith"),
            c => c.ShippingAddress.ShouldAllBe([
                address => address.Street.ShouldBe("123 Main St"),
                address => address.City.ShouldBe("Boston"),
                address => address.ZipCode.ShouldBe("02108"),
            ]),
            c => c.BillingAddress.ShouldAllBe([
                address => address.Street.ShouldBe("456 Park Ave"),
                address => address.City.ShouldBe("New York"),
                address => address.ZipCode.ShouldBe("10022"),
            ])
        ]);
    }
}