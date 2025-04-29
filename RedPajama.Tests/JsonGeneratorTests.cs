using JetBrains.Annotations;
using Shouldly;

namespace RedPajama.Tests;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class JsonSampleGeneratorTests
{
    private readonly JsonSampleGenerator _generator = new(new JsonSampleGeneratorSettings {PrettyPrint = true});
    private readonly JsonSampleGenerator _compactGenerator = new(new JsonSampleGeneratorSettings {PrettyPrint = false});
    
    public class PrimitiveTypes
    {
        public string Text { get; set; } = "";
        public int Number { get; set; }
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
    }

    public class ArrayTypes
    {
        public string[] Texts { get; set; } = [];
        public int[] Numbers { get; set; } = [];
    }

    public enum Status
    {
        Active,
        Inactive,
        Pending
    }

    public class EnumType
    {
        public Status Status { get; set; }
    }

    public class DescribedType
    {
        public string Name { get; set; } = "";
    }

    public class RestrictedType
    {
        public string Category { get; set; } = "";
    }

    public class Address
    {
        public string Street { get; set; } = "";
        public string City { get; set; } = "";
    }

    public class Person
    {
        public string Name { get; set; } = "";
        public Address Address { get; set; } = new();
    }

    public class Item
    {
        public string Name { get; set; } = "";
        public decimal Price { get; set; }
    }

    public class Order
    {
        public Item[] Items { get; set; } = [];
    }

    public class SimpleType
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
    }


    public class EmptyType { }
    // ReSharper restore ClassNeverInstantiated.Global

    [Fact]
    public void Generate_PrimitiveTypes_CreatesCorrectJson()
    {
        // Arrange
        var typeModel = new TypeModelBuilder<PrimitiveTypes>().Build();

        // Act
        var json = _generator.Generate(typeModel);

        // Assert
        var expectedJson = """
                           {
                               "Text": "⟨string value⟩",
                               "Number": ⟨integer value⟩,
                               "Amount": ⟨decimal value⟩,
                               "Date": ⟨ISO 8601 date value (YYYY-MM-DDThh:mm:ss.sssZ)⟩
                           }
                           """;
        json.ShouldContainWithoutWhitespace(expectedJson);
    }

    [Fact]
    public void Generate_WithArrays_CreatesCorrectJson()
    {
        // Arrange
        var typeModel = new TypeModelBuilder<ArrayTypes>().Build();

        // Act
        var json = _generator.Generate(typeModel);

        // Assert
        var expectedJson = """
                           {
                               "Texts": ["⟨string value_1⟩", "⟨Texts_2⟩", "⟨Texts_N⟩"],
                               "Numbers": [⟨integer value_1⟩, ⟨Numbers_2⟩, ⟨Numbers_N⟩]
                           }
                           """;
        json.ShouldContainWithoutWhitespace(expectedJson);
    }

    [Fact]
    public void Generate_WithEnum_CreatesCorrectJson()
    {
        // Arrange
        var typeModel = new TypeModelBuilder<EnumType>().Build();

        // Act
        var json = _generator.Generate(typeModel);

        // Assert
        var expectedJson = """
                           {
                               "Status": "⟨Active|Inactive|Pending⟩"
                           }
                           """;
        json.ShouldContainWithoutWhitespace(expectedJson);
    }

    [Fact]
    public void Generate_WithDescriptions_CreatesCorrectJson()
    {
        // Arrange
        var builder = new TypeModelBuilder<DescribedType>();
        var typeModel = builder.Build();
        typeModel = typeModel.WithDescription<DescribedType, string>(x => x.Name, "User's full name");

        // Act
        var json = _generator.Generate(typeModel);

        // Assert
        var expectedJson = """
                           {
                               "Name": "⟨string value⟩" // User's full name
                           }
                           """;
        json.ShouldContainWithoutWhitespace(expectedJson);
    }

    [Fact]
    public void Generate_WithAllowedValues_CreatesCorrectJson()
    {
        // Arrange
        var builder = new TypeModelBuilder<RestrictedType>();
        var typeModel = builder.Build();
        typeModel = typeModel.WithAllowedValues<RestrictedType, string>(x => x.Category, ["A", "B", "C"]);

        // Act
        var json = _generator.Generate(typeModel);

        // Assert
        var expectedJson = """
                           {
                               "Category": "⟨A|B|C⟩"
                           }
                           """;
        json.ShouldContainWithoutWhitespace(expectedJson);
    }

    [Fact]
    public void Generate_WithNestedObject_CreatesCorrectJson()
    {
        // Arrange
        var typeModel = new TypeModelBuilder<Person>().Build();

        // Act
        var json = _generator.Generate(typeModel);

        // Assert
        var expectedJson = """
                           {
                               "Name": "⟨string value⟩",
                               "Address": {
                                   "Street": "⟨string value⟩",
                                   "City": "⟨string value⟩"
                               }
                           }
                           """;
        json.ShouldContainWithoutWhitespace(expectedJson);
    }

    [Fact]
    public void Generate_WithArrayOfObjects_CreatesCorrectJson()
    {
        // Arrange
        var typeModel = new TypeModelBuilder<Order>().Build();

        // Act
        var json = _generator.Generate(typeModel);

        // Assert
        var expectedJson = """
                           {
                               "Items": [{
                                   "Name": "⟨string value⟩",
                                   "Price": ⟨decimal value⟩
                               }, Items_2, Items_N]
                           }
                           """;
        json.ShouldContainWithoutWhitespace(expectedJson);
    }

    [Fact]
    public void Generate_CompactMode_CreatesJsonWithoutFormatting()
    {
        // Arrange
        var typeModel = new TypeModelBuilder<SimpleType>().Build();

        // Act
        var json = _compactGenerator.Generate(typeModel);

        // Assert
        var expectedJson = """
                           {"Name": "⟨string value⟩","Age": ⟨integer value⟩}
                           """;
        json.ShouldBe(expectedJson);
    }

    [Fact]
    public void Generate_EmptyObject_CreatesMinimalJson()
    {
        // Arrange
        var typeModel = new TypeModelBuilder<EmptyType>().Build();

        // Act
        var json = _generator.Generate(typeModel);

        // Assert
        json.ShouldContainWithoutWhitespace("{}");
    }

    [Fact]
    public void Generate_CustomIndentation_UsesProvidedIndent()
    {
        // Arrange
        var generator = new JsonSampleGenerator(new JsonSampleGeneratorSettings()
        {
            PrettyPrint = true,
            Indent = "\t"
        });
        var typeModel = new TypeModelBuilder<SimpleType>().Build();

        // Act
        var json = generator.Generate(typeModel);

        // Assert
        var expectedJson = """
                           {
                           	"Name": "⟨string value⟩",
                           	"Age": ⟨integer value⟩
                           }
                           """;
        json.ShouldBe(expectedJson, StringCompareShould.IgnoreLineEndings);
    }
}