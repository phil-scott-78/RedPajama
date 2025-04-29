using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using LLama;
using LLama.Abstractions;

namespace RedPajama.ConsoleTest.TestRoutines;

internal class ParseComplexRestaurantOrder : ITestRoutine
{

    public class Order
    {
        [Description("The the order identifier")]
        [Format("AAA###")]
        public required string OrderId { get; init; }

        public required DateTime OrderTime { get; init; }
        public required OrderStatus Status { get; init; }
        public required string CustomerName { get; init; }

        [Format("(###) ###-####")]
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
        [Description("The full delivery address Line, made up of the primary address number, predirectional, street name, suffix, postdirectional, secondary address identifier, and secondary address. You must not include City, State, or ZipCode")]
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
        [Format("gbnf:\"\\\"\" [a-zA-Z ]{1,} \"\\\"\" space")]
        public required string Name { get; init; }
        public required decimal Price { get; init; }
        public required int Quantity { get; init; }
        [Description("The spice level of the menu item, indicated following the name in parenthesis. Typically a value of Spicy, Mild, Super Hot, etc")]
        public required SpiceLevel SpicePreference { get; init; }
        [AllowedValues("rice", "noodles", "bread")]
        public required string[] Sides { get; init; }
        [AllowedValues("dairy-free", "gluten-free", "nut-free", "vegan")]
        public required string[] DietaryRestrictions { get; init; }
    }

    public async Task Run(LLamaWeights model, IContextParams parameters)
    {
        var executor = new StatelessExecutor(model, parameters) { ApplyTemplate = true };
        const string prompt = """
                              Parse this restaurant order. When extracting the address, follow these rules:
                              
                              * City, State and Zip are their own fields
                              * FullStreetAddress is the address without City, State and Zip. 
                              
                              ```
                              Alright, I've got order number RTH789 here from Sarah Johnson. She placed it around 6:30 PM on January 27th 2024. It's currently being prepared and will be delivered to her apartment - that's 789 Oak Road, Apartment 4B in San Francisco, 94110. She left her phone number as (555) 123-4567.
                              
                              For the food, she ordered two spicy Pad Thais - those are $15.99 each. She wanted rice and noodles on the side for those, and mentioned they need to be gluten-free. She also got one Green Curry with extra spice for $18.99, just rice on the side for that one. The curry needs to be dairy-free and nut-free.
                              
                              Total comes to $50.97, and she paid with a credit card.
                              ```
                              """;

        Order order;
        if (!model.IsThinkingModel())
        {
            order = await executor.InferAsync<Order>(prompt, JsonContext.Default, TypeModelContext.Default);
        }
        else
        {
            (order, _) = (await executor.InferWithThoughtsAsync<Order>(prompt, JsonContext.Default, TypeModelContext.Default));
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