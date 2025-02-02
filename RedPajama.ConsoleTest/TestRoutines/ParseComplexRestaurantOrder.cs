using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;
using LLama;
using LLama.Abstractions;

namespace RedPajama.ConsoleTest.TestRoutines;

internal class ParseComplexRestaurantOrder : ITestRoutine
{
    [UsedImplicitly(ImplicitUseTargetFlags.Members)]
    enum OrderStatus 
    {
        New,
        Preparing,
        ReadyForPickup,
        Delivered,
        Cancelled
    }

    [UsedImplicitly(ImplicitUseTargetFlags.Members)]
    enum SpiceLevel
    {
        Mild,
        Medium,
        Hot,
        ExtraHot
    }

    [UsedImplicitly(ImplicitUseTargetFlags.Members)]
    class Address
    {
        [Description("The full street address, including street number, street name, and any apartment or suite number")]
        public required string StreetAddress  { get; init; }
        public required string City { get; init; }
        [AllowedValues("CA", "NY", "NY", "TX")]
        public required string State { get; init; }
        public required string ZipCode { get; init; }
    }

    [UsedImplicitly(ImplicitUseTargetFlags.Members)]
    class MenuItem
    {
        [Description("The name of the menu item, excluding spice level.")]
        public required string Name { get; init; }
        public required decimal Price { get; init; }
        public required int Quantity { get; init; }
        public required SpiceLevel SpicePreference { get; init; }
        [AllowedValues("rice", "noodles", "bread")]
        public required string[] Sides { get; init; }
        [AllowedValues("dairy-free", "gluten-free", "nut-free", "vegan")]
        public required string[] DietaryRestrictions { get; init; }
    }

    [UsedImplicitly(ImplicitUseTargetFlags.Members)]
    class Order
    {
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
    
    public async Task Run(LLamaWeights model, IContextParams parameters)
    {
        var executor = new StatelessExecutor(model, parameters){ApplyTemplate = true};
        var order = await executor.InferAsync<Order>("""
                                                     Parse this restaurant order:
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
                                                     """);

        order.ShouldAllBe([
            o => o.OrderId.ShouldBe("RTH789"),
            o => o.OrderTime.ShouldBe(new DateTime(2024, 1, 27, 18, 30, 0)),
            o => o.Status.ShouldBe(OrderStatus.Preparing),
            o => o.CustomerName.ShouldBe("Sarah Johnson"),
            o => o.PhoneNumber.ShouldBe("(555) 123-4567"),
            o => o.DeliveryAddress.StreetAddress.ShouldBe("789 Oak Road, Apartment 4B"),
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
                item => item.SpicePreference.ShouldBe(SpiceLevel.Hot),
                item => item.Sides.Length.ShouldBe(2),
                item => item.Sides[0].ShouldBe("rice"),
                item => item.Sides[1].ShouldBe("noodles"),
                item => item.DietaryRestrictions.Length.ShouldBe(1),
                item => item.DietaryRestrictions[0].ShouldBe("gluten-free"),
            ]),
            o => o.Items[1].ShouldAllBe<MenuItem>([
                item => item.Name.ShouldBe("Green Curry"),
                item => item.Price.ShouldBe(18.99m),
                item => item.Quantity.ShouldBe(1),
                item => item.SpicePreference.ShouldBe(SpiceLevel.ExtraHot),
                item => item.Sides.Length.ShouldBe(1),
                item => item.Sides[0].ShouldBe("rice"),
                item => item.DietaryRestrictions.Length.ShouldBe(2),
                item => item.DietaryRestrictions[0].ShouldBe("dairy-free"),
                item => item.DietaryRestrictions[1].ShouldBe("nut-free"),
            ]),
        ]);
    }
}