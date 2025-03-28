using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using LLama;
using LLama.Abstractions;

namespace RedPajama.ConsoleTest.TestRoutines;

internal class ParseComplexRestaurantOrder : ITestRoutine
{
    class Order
    {
        [Description("The the order identifier, in the format XYZ123.")]
        public required string OrderId { get; init; }

        public required DateTime OrderTime { get; init; }
        public required OrderStatus Status { get; init; }
        public required string CustomerName { get; init; }

        [Description("Should be in the format (XXX) XXX-XXXX")]
        public required string PhoneNumber { get; init; }

        public required Address DeliveryAddress { get; init; }
        public required MenuItem[] Items { get; init; }
        public required decimal TotalAmount { get; init; }

        [AllowedValues("cash", "credit", "debit")]
        public required string PaymentMethod { get; init; }
    }
    
    
    public enum OrderStatus
    {
        New,
        Preparing,
        ReadyForPickup,
        Delivered,
        Cancelled
    }

    public enum SpiceLevel
    {
        Mild,
        Medium,
        Spicy,
        ExtraSpicy
    }

    public class Address
    {
        [Description("The full delivery address Line, made up of the primary address number, predirectional, street name, suffix, postdirectional, secondary address identifier, and secondary address.")]
        public required string FullStreetAddress { get; init; }
        public required string City { get; init; }
        [AllowedValues("CA", "NY", "TX")] 
        public required string State { get; init; }
        public required string ZipCode { get; init; }
    }

    public class MenuItem
    {
        [Description("The line number of the order item in the request")]
        public required int LineNumber { get; init; }
        [Description("The name of the menu item and only the food item. Do not include spice level within parenthesis.")]
        public required string Name { get; init; }
        public required decimal Price { get; init; }
        public required int Quantity { get; init; }
        [Description("The spice level of the menu item, indicated following the name in parenthesis")]
        public required SpiceLevel SpicePreference { get; init; }
        [AllowedValues("rice", "noodles", "bread")]
        public required string[] Sides { get; init; }
        [AllowedValues("dairy-free", "gluten-free", "nut-free", "vegan")]
        public required string[] DietaryRestrictions { get; init; }
    }

    public async Task Run(LLamaWeights model, IContextParams parameters)
    {
        var executor = new StatelessExecutor(model, parameters);
        const string prompt = """
                              Parse this restaurant order.
                              
                              ```
                              Order #RTH789 - Placed at 2024-01-27 18:30:00
                              Status: Being prepared
                              Customer: Sarah Johnson
                              Phone: (555) 123-4567
                              Delivery to: 789 Oak Road, Apartment 4B, San Francisco, CA 94110

                              Items:
                              1. Pad Thai (Spicy) - $15.99 x 2
                                 - Sides: rice, noodles
                                 - Dietary: gluten-free
                              2. Green Curry (Extra Spicy) - $18.99 x 1
                                 - Sides: rice
                                 - Dietary: dairy-free, nut-free

                              Total: $50.97
                              Payment: credit card
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
            o => o.OrderId.ShouldBe("RTH789"),
            o => o.OrderTime.ShouldBe(new DateTime(2024, 1, 27, 18, 30, 0)),
            o => o.Status.ShouldBe(OrderStatus.Preparing),
            o => o.CustomerName.ShouldBe("Sarah Johnson"),
            o => o.PhoneNumber.ShouldBe("(555) 123-4567"),
            o => o.DeliveryAddress.FullStreetAddress.ShouldBe("789 Oak Road, Apartment 4B"),
            o => o.DeliveryAddress.City.ShouldBe("San Francisco"),
            o => o.DeliveryAddress.State.ShouldBe("CA"),
            o => o.DeliveryAddress.ZipCode.ShouldBe("94110"),
            o => o.TotalAmount.ShouldBe(50.97m),
            o => o.PaymentMethod.ShouldBe("credit"),
            o => o.Items.Length.ShouldBe(2),
            o => o.Items[0].ShouldAllBe([
                item => item.Name.ShouldBe("Pad Thai"),
                item => item.Price.ShouldBe(15.99m),
                item => item.Quantity.ShouldBe(2),
                item => item.SpicePreference.ShouldBe(SpiceLevel.Spicy),
                item => item.Sides.Length.ShouldBe(2),
                item => item.Sides[0].ShouldBe("rice"),
                item => item.Sides[1].ShouldBe("noodles"),
                item => item.DietaryRestrictions.Length.ShouldBe(1),
                item => item.DietaryRestrictions[0].ShouldBe("gluten-free"),
            ]),
            o => o.Items[1].ShouldAllBe([
                item => item.Name.ShouldBe("Green Curry"),
                item => item.Price.ShouldBe(18.99m),
                item => item.Quantity.ShouldBe(1),
                item => item.SpicePreference.ShouldBe(SpiceLevel.ExtraSpicy),
                item => item.Sides.Length.ShouldBe(1),
                item => item.Sides[0].ShouldBe("rice"),
                item => item.DietaryRestrictions.Length.ShouldBe(2),
                item => item.DietaryRestrictions[0].ShouldBe("dairy-free"),
                item => item.DietaryRestrictions[1].ShouldBe("nut-free"),
            ]),
        ]);
    }
}