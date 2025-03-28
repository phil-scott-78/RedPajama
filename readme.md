# RedPajama

RedPajama is a C# library that generates structured JSON samples and GBNF (Generalized Backus-Naur Form) grammars from C# types. It's designed to enhance Large Language Model (LLM) interactions by providing type-safe, well-structured data generation and parsing capabilities.

## Features

- Generate sample JSON templates from C# types
- Create corresponding GBNF grammars for type-safe LLM responses
- Support for complex type hierarchies and nested objects
- Custom placeholder delimiters for clear template visualization
- Configurable string length constraints (defaults to 1-512 characters)
- Property descriptions and metadata support
- String validation with AllowedValues, MinLength, and MaxLength attributes

## Usage

### Basic Example

```csharp
class Address
{
    public required string Street { get; init; }
    public required string City { get; init; }
    [Description("Digits only")]
    public required string ZipCode { get; init; }
}

class Customer 
{
    public required string Name { get; init; }
    public required Address ShippingAddress { get; init; }
    public required Address BillingAddress { get; init; }
}

// Generate type model, GBNF grammar, and JSON sample
var typeModel = new TypeModelBuilder<Customer>().Build();
var gbnfGenerator = new GbnfGenerator();
var jsonSampleGenerator = new JsonSampleGenerator();

var gbnf = gbnfGenerator.Generate(typeModel);
var jsonSample = jsonSampleGenerator.Generate(typeModel);
// default instructions on how to build the response, including describing the delimters.
var sampleInstructions = jsonSampleGenerator.SampleInstructions();

var prompt =    """
                Extract the customer name and addresses from this order:
                ```
                Customer: John Smith
                Ship to: 123 Main St, Boston, MA 02108
                Bill to: 456 Park Ave, New York, NY 10022
                ```
                """;

// use the json sample
var promptWithSample = $"""
                        {prompt}

                        Return results as valid JSON in the following format:
                        {jsonSample}

                        {sampleInstructions}
                        """;

var executor = new StatelessExecutor(model, parameters);

var inferenceParams = new InferenceParams
{
    SamplingPipeline = new DefaultSamplingPipeline
    {
        Grammar = new Grammar(gbnf, "root")
    },
};

await foreach (var s in executor.InferAsync(promptWithSample, inferenceParams))
{
    sb.Append(s);
}

var json = sb.ToString();
var customer =  JsonSerializer.Deserialize<Customer>(json, JsonSerializerOptions) ?? throw new InvalidOperationException("Couldn't deserialize result");

customer.ShouldAllBe([
    c => c.Name.ShouldBe("John Smith"),
    c => c.ShippingAddress.Street.ShouldBe("123 Main St"),
    c => c.ShippingAddress.City.ShouldBe("Boston"),
    c => c.ShippingAddress.ZipCode.ShouldBe("02108"),
    c => c.BillingAddress.Street.ShouldBe("456 Park Ave"),
    c => c.BillingAddress.City.ShouldBe("New York"), 
    c => c.BillingAddress.ZipCode.ShouldBe("10022")
]);


```

### Advanced Configuration

```csharp
// Configure type model with descriptions and allowed values
var typeModel = new TypeModelBuilder<Person>()
    .WithDescription(p => p.Name, "Full name of the person")
    .WithAllowedValues(p => p.Name, new[] { "John", "Jane" })
    .Build();

// Customize generator settings
var settings = new JsonSampleGeneratorSettings
{
    OpeningDelimiter = '⟨',
    ClosingDelimiter = '⟩',
    PrettyPrint = true,
    Indent = "    "
};

var jsonSampleGenerator = new JsonSampleGenerator(settings);
```

## Configuration Options

### JsonSampleGeneratorSettings

- `OpeningDelimiter`: Character used to open placeholder values (default: '⟨')
- `ClosingDelimiter`: Character used to close placeholder values (default: '⟩')
- `PrettyPrint`: Enable/disable formatted JSON output (default: true)
- `Indent`: Indentation string for pretty printing (default: "    ")

### GbnfGeneratorSettings

- `DefaultMinLength`: Minimum length for string values (default: 1)
- `DefaultMaxLength`: Maximum length for string values (default: 512)
- `OpeningDelimiter`: Character used to open placeholder values (default: '⟨')
- `ClosingDelimiter`: Character used to close placeholder values (default: '⟩')
- `PrettyPrint`: Enable/disable formatted GBNF output
- `Indent`: Indentation string for pretty printing

## Features in Detail

### Type Support

RedPajama supports the following C# types:
- Primitive types (string, int, long, decimal, float, double, bool)
- DateTime and DateTimeOffset
- Guids
- Arrays
- Enums
- Complex objects with nested properties

### Validation Attributes

- `[Description]`: Add descriptive comments to JSON samples
- `[MinLength]`: Specify minimum string length
- `[MaxLength]`: Specify maximum string length
- `[AllowedValues]`: Define allowed string values

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## License

[MIT License](LICENSE)