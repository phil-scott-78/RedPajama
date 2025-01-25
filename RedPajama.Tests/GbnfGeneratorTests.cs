using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;
using Shouldly;

namespace RedPajama.Tests;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public class GbnfGeneratorTests
{

    
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

    public class LengthConstrainedType
    {
        [MinLength(5)]
        [MaxLength(10)]
        public string Text { get; set; } = "";
    }

    private readonly GbnfGenerator _generator = new();

    [Fact]
    public void Generate_PrimitiveTypes_CreatesCorrectGrammar()
    {
        // Arrange
        var typeModel = new TypeModelBuilder<PrimitiveTypes>().Build();

        // Act
        var grammar = _generator.Generate(typeModel);

        // Assert
        var expected = """
            char ::= [^"\\\x7F\x00-\x1F⟨⟩] | [\\] (["\\bfnrt] | "u" [0-9a-fA-F]{4})
            space ::= | " " | "\n" [ \t]{0,20}
            root-text-kv ::= "\"Text\"" space ":" space "\"" char{1, 512} "\"" space
            root-number-kv ::= "\"Number\"" space ":" space ("-"? [0] | [1-9] [0-9]{0,15}) space
            root-amount-kv ::= "\"Amount\"" space ":" space ("-"? ([0] | [1-9] [0-9]{0,15}) ("." [0-9]{1,15})?) space
            root-date-kv ::= "\"Date\"" space ":" space "\"" [0-9]{4} "-" ([0][1-9]|[1][0-2]) "-" ([0][1-9]|[12][0-9]|[3][01]) "T" ([01][0-9]|[2][0-3]) ":" [0-5][0-9] ":" [0-5][0-9] ("." [0-9]{3})? ("Z"|([+-] ([01][0-9]|[2][0-3]) ":" [0-5][0-9])) "\"" space
            root ::= "{" space root-text-kv "," space root-number-kv "," space root-amount-kv "," space root-date-kv "}" space
            """;

        grammar.ShouldContainWithoutWhitespace(expected);
    }

    [Fact]
    public void Generate_WithArrays_CreatesCorrectGrammar()
    {
        // Arrange
        var typeModel = new TypeModelBuilder<ArrayTypes>().Build();

        // Act
        var grammar = _generator.Generate(typeModel);

        // Assert
        var expected = """
            char ::= [^"\\\x7F\x00-\x1F⟨⟩] | [\\] (["\\bfnrt] | "u" [0-9a-fA-F]{4})
            space ::= | " " | "\n" [ \t]{0,20}
            root-texts-item ::= "\"" char{1,512} "\"" space
            root-texts-kv ::= "\"Texts\"" space ":" space "[" space (root-texts-item ("," space root-texts-item)*)? "]" space
            root-numbers-item ::= ("-"? [0] | [1-9] [0-9]{0,15}) space
            root-numbers-kv ::= "\"Numbers\"" space ":" space "[" space (root-numbers-item ("," space root-numbers-item)*)? "]" space
            root ::= "{" space root-texts-kv "," space root-numbers-kv "}" space
            """;

        grammar.ShouldContainWithoutWhitespace(expected);
    }

    [Fact]
    public void Generate_WithEnum_CreatesCorrectGrammar()
    {
        // Arrange
        var typeModel = new TypeModelBuilder<EnumType>().Build();

        // Act
        var grammar = _generator.Generate(typeModel);

        // Assert
        var expected = """
            char ::= [^"\\\x7F\x00-\x1F⟨⟩] | [\\] (["\\bfnrt] | "u" [0-9a-fA-F]{4})
            space ::= | " " | "\n" [ \t]{0,20}
            root-status-kv ::= "\"Status\"" space ":" space ("\"Active\""|"\"Inactive\""|"\"Pending\"") space
            root ::= "{" space root-status-kv "}" space
            """;

        grammar.ShouldContainWithoutWhitespace(expected);
    }

    [Fact]
    public void Generate_WithAllowedValues_CreatesCorrectGrammar()
    {
        // Arrange
        var builder = new TypeModelBuilder<RestrictedType>();
        builder.WithAllowedValues(x => x.Category, new[] { "A", "B", "C" });
        var typeModel = builder.Build();

        // Act
        var grammar = _generator.Generate(typeModel);

        // Assert
        var expected = """
            char ::= [^"\\\x7F\x00-\x1F⟨⟩] | [\\] (["\\bfnrt] | "u" [0-9a-fA-F]{4})
            space ::= | " " | "\n" [ \t]{0,20}
            root-category ::= ("\"A\""|"\"B\""|"\"C\"") space
            root-category-kv ::= "\"Category\"" space ":" space root-category
            root ::= "{" space root-category-kv "}" space
            """;

        grammar.ShouldContainWithoutWhitespace(expected);
    }

    [Fact]
    public void Generate_WithNestedObject_CreatesCorrectGrammar()
    {
        // Arrange
        var typeModel = new TypeModelBuilder<Person>().Build();

        // Act
        var grammar = _generator.Generate(typeModel);

        // Assert
        var expected = """
            char ::= [^"\\\x7F\x00-\x1F⟨⟩] | [\\] (["\\bfnrt] | "u" [0-9a-fA-F]{4})
            space ::= | " " | "\n" [ \t]{0,20}
            root-name-kv ::= "\"Name\"" space ":" space "\"" char{1, 512} "\"" space
            root-address-street-kv ::= "\"Street\"" space ":" space "\"" char{1, 512} "\"" space
            root-address-city-kv ::= "\"City\"" space ":" space "\"" char{1, 512} "\"" space
            root-address-kv ::= "\"Address\"" space ":" space "{" space root-address-street-kv "," space root-address-city-kv "}" space
            root ::= "{" space root-name-kv "," space root-address-kv "}" space
            """;

        grammar.ShouldContainWithoutWhitespace(expected);
    }

    [Fact]
    public void Generate_WithLengthConstraints_CreatesCorrectGrammar()
    {
        // Arrange
        var typeModel = new TypeModelBuilder<LengthConstrainedType>().Build();

        // Act
        var grammar = _generator.Generate(typeModel);

        // Assert
        var expected = """
            char ::= [^"\\\x7F\x00-\x1F⟨⟩] | [\\] (["\\bfnrt] | "u" [0-9a-fA-F]{4})
            space ::= | " " | "\n" [ \t]{0,20}
            root-text-kv ::= "\"Text\"" space ":" space "\"" char{5,10} "\"" space
            root ::= "{" space root-text-kv "}" space
            """;

        grammar.ShouldContainWithoutWhitespace(expected);
    }
    
    public class EmptyType { }

    [Fact]
    public void Generate_EmptyObject_CreatesCorrectGrammar()
    {
        // Arrange
        var typeModel = new TypeModelBuilder<EmptyType>().Build();

        // Act
        var grammar = _generator.Generate(typeModel);

        // Assert
        var expected = """
            char ::= [^"\\\x7F\x00-\x1F⟨⟩] | [\\] (["\\bfnrt] | "u" [0-9a-fA-F]{4})
            space ::= | " " | "\n" [ \t]{0,20}
            root ::= "{" space "}" space
            """;

        grammar.ShouldContainWithoutWhitespace(expected);
    }
}