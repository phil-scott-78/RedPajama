using System.Diagnostics.CodeAnalysis;
using System.ComponentModel.DataAnnotations;

namespace RedPajama.IntegrationTests;


[Collection("Model collection")]
[SuppressMessage("Usage", "xUnit1004:Test methods should not be skipped")]
[SuppressMessage("ReSharper", "UnusedMember.Local")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
public class GbnfTests(ModelFixture modelFixture) : IClassFixture<ModelFixture>
{
    private const string SkipReason = "Only run when we have a model";
    public static bool ModelDoesntExist { get; } = !File.Exists(ModelFixture.ModelFileName);

    [Fact(Skip = SkipReason, SkipWhen = nameof(ModelDoesntExist))]
    public async Task CanDoSimpleTest()
    {
        var person = await modelFixture.TestGrammarAsync<Person>("Parse this person's name: Phil Scott");
        Assert.NotEmpty(person.FirstName);
        Assert.NotEmpty(person.LastName);
    }

    [Fact(Skip = SkipReason, SkipWhen = nameof(ModelDoesntExist))]
    public async Task CanParseStringWithFormatting()
    {
        var formattedData = await modelFixture.TestGrammarAsync<FormattedStrings>(
            "Extract the information: Email: user@example.com, Phone: 555-123-4567, ZIP: 90210");

        Assert.NotNull(formattedData);
        Assert.NotNull(formattedData.Email);
        Assert.NotNull(formattedData.Phone);
        Assert.NotNull(formattedData.ZipCode);
    }

    [Fact(Skip = SkipReason, SkipWhen = nameof(ModelDoesntExist))]
    public async Task CanParseNumericTypes()
    {
        var numbers = await modelFixture.TestGrammarAsync<NumericTypes>(
            "Extract these numbers: Age: 25, Price: 99.99, Discount: -10.5");

        Assert.NotNull(numbers);
        Assert.True(numbers.Age >= 0);
        Assert.True(numbers.Price != 0);
    }

    [Fact(Skip = SkipReason, SkipWhen = nameof(ModelDoesntExist))]
    public async Task CanParseBooleanValues()
    {
        var flags = await modelFixture.TestGrammarAsync<BooleanTypes>(
            "Is the customer active? Yes. Is the account paid? No.");

        Assert.NotNull(flags);
    }

    [Fact(Skip = SkipReason, SkipWhen = nameof(ModelDoesntExist))]
    public async Task CanParseDateValues()
    {
        var dates = await modelFixture.TestGrammarAsync<DateTypes>(
            "The event is scheduled for 2025-06-15T14:30:00Z and ends on 2025-06-15T16:30:00Z");

        Assert.NotNull(dates);
        Assert.NotEqual(default, dates.EventDate);
        Assert.NotEqual(default, dates.EndDate);
    }

    [Fact(Skip = SkipReason, SkipWhen = nameof(ModelDoesntExist))]
    public async Task CanParseGuidValues()
    {
        var ids = await modelFixture.TestGrammarAsync<GuidTypes>(
            "User ID: 12345678-1234-1234-1234-123456789abc, Transaction ID: 98765432-9876-9876-9876-987654321def");

        Assert.NotNull(ids);
        Assert.NotEqual(Guid.Empty, ids.UserId);
        Assert.NotEqual(Guid.Empty, ids.TransactionId);
    }

    [Fact(Skip = SkipReason, SkipWhen = nameof(ModelDoesntExist))]
    public async Task CanParseArrays()
    {
        var arrays = await modelFixture.TestGrammarAsync<ArrayTypes>(
            "Put all string in the StringArray, all ints in the intArray. Tags: red, blue, green. Scores: 85, 92, 78.");

        Assert.NotNull(arrays);
        Assert.NotEmpty(arrays.StringArray);
        Assert.NotEmpty(arrays.IntArray);
        Assert.True(arrays.StringArray.Length > 0);
        Assert.True(arrays.IntArray.Length > 0);
    }

    [Fact(Skip = SkipReason, SkipWhen = nameof(ModelDoesntExist))]
    public async Task CanParseNestedObjects()
    {
        var order = await modelFixture.TestGrammarAsync<Order>(
            "Customer: John Doe, Email: john@example.com. Shipping Address: 123 Main St, New York, NY 10001. " +
            "Items: T-shirt ($25.99), Jeans ($45.50), Hat ($15.75)");

        Assert.NotNull(order);
        Assert.NotNull(order.Customer);
        Assert.NotNull(order.ShippingAddress);
        Assert.NotEmpty(order.Items);
        Assert.True(order.Items.Length > 0);
    }

    [Fact(Skip = SkipReason, SkipWhen = nameof(ModelDoesntExist))]
    public async Task CanParseEnumValues()
    {
        var statusObj = await modelFixture.TestGrammarAsync<StatusObject>(
            "Order Status: Shipped. Payment Status: Paid.");

        Assert.NotNull(statusObj);
    }

    [Fact(Skip = SkipReason, SkipWhen = nameof(ModelDoesntExist))]
    public async Task CanParseComplexStructure()
    {
        var restaurant = await modelFixture.TestGrammarAsync<Restaurant>(
            "Restaurant: Bella Italia\n" +
            "Rating: 4.7\n" +
            "Location: 123 Italian St., Rome\n" +
            "Menu Items:\n" +
            "- Spaghetti Carbonara, $15.99, Vegetarian\n" +
            "- Margherita Pizza, $12.50, Vegetarian\n" +
            "- Tiramisu, $8.75, Contains alcohol");

        Assert.NotNull(restaurant);
        Assert.NotNull(restaurant.Name);
        Assert.NotNull(restaurant.Location);
        Assert.NotEmpty(restaurant.MenuItems);
        Assert.True(restaurant.MenuItems.Length > 0);
    }

    [Fact(Skip = SkipReason, SkipWhen = nameof(ModelDoesntExist))]
    public async Task CanParseAllowedValuesString()
    {
        var colorObject = await modelFixture.TestGrammarAsync<ColorObject>(
            "Select a color: blue");

        Assert.NotNull(colorObject);
        Assert.NotNull(colorObject.Color);
    }

    [Fact(Skip = SkipReason, SkipWhen = nameof(ModelDoesntExist))]
    public async Task CanParseArrayWithLengthConstraints()
    {
        var data = await modelFixture.TestGrammarAsync<ArrayLengthConstrainedTypes>(
            "Provide three strings for LimitedStrings: apple, banana, cherry. Provide one integer for ExactOneInt: 100.");

        Assert.NotNull(data);
        Assert.NotNull(data.LimitedStrings);
        Assert.Equal(3, data.LimitedStrings.Length);


        Assert.NotNull(data.ExactOneInt);
        Assert.Single(data.ExactOneInt);
    }

    // Test Models
    private class Person
    {
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
    }

    private class FormattedStrings
    {
        [Format("gbnf:\"\\\"\" [a-zA-Z0-9._%+-]+ \"@\" [a-zA-Z0-9.-]+ \".\" [a-zA-Z]{2,} \"\\\"\" space")]
        public required string Email { get; init; }

        [Format("###-###-####")] public required string Phone { get; init; }

        [Format("numeric")] public required string ZipCode { get; init; }
    }

    private class NumericTypes
    {
        public int Age { get; init; }
        public decimal Price { get; init; }
        public double Discount { get; init; }
    }

    private class BooleanTypes
    {
        public bool IsActive { get; init; }
        public bool IsPaid { get; init; }
    }

    private class DateTypes
    {
        public DateTime EventDate { get; init; }
        public DateTime EndDate { get; init; }
    }

    private class GuidTypes
    {
        public Guid UserId { get; init; }
        public Guid TransactionId { get; init; }
    }

    private class ArrayTypes
    {
        public string[] StringArray { get; init; } = Array.Empty<string>();
        public int[] IntArray { get; init; } = Array.Empty<int>();
    }

    private class Customer
    {
        public required string Name { get; init; }
        public required string Email { get; init; }
    }

    private class Address
    {
        public required string Street { get; init; }
        public required string City { get; init; }
        public required string State { get; init; }
        public required string PostalCode { get; init; }
    }

    private class OrderItem
    {
        public required string ProductName { get; init; }
        public decimal Price { get; init; }
    }

    private class Order
    {
        public required Customer Customer { get; init; }
        public required Address ShippingAddress { get; init; }
        public OrderItem[] Items { get; init; } = Array.Empty<OrderItem>();
    }

    private enum OrderStatus
    {
        Pending,
        Processing,
        Shipped,
        Delivered,
        Cancelled
    }

    private enum PaymentStatus
    {
        Pending,
        Authorized,
        Paid,
        Refunded,
        Failed
    }

    private class StatusObject
    {
        public OrderStatus OrderStatus { get; init; }
        public PaymentStatus PaymentStatus { get; init; }
    }

    private class MenuItem
    {
        public required string Name { get; init; }
        public decimal Price { get; init; }
        public required string DietaryInfo { get; init; }
    }

    private class Restaurant
    {
        public required string Name { get; init; }
        public double Rating { get; init; }
        public required string Location { get; init; }
        public MenuItem[] MenuItems { get; init; } = Array.Empty<MenuItem>();
    }

    private class ColorObject
    {
        [Format("gbnf:(\"\\\"red\\\"\" | \"\\\"green\\\"\" | \"\\\"blue\\\"\") space")]
        public required string Color { get; init; }
    }

    private class ArrayLengthConstrainedTypes
    {
        [MinLength(2)]
        [MaxLength(4)]
        public required string[] LimitedStrings { get; init; }

        [MinLength(1)]
        [MaxLength(1)]
        public required int[] ExactOneInt { get; init; }
    }
}