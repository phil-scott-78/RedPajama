using System.Diagnostics;
using System.Text;
using LLama;
using LLama.Common;
using LLama.Sampling;
using Spectre.Console;

namespace RedPajama.ConsoleTest;

public class VarietyOfScenarios
{
    private const string basePrompt = """
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
                                         - Dietary: gluten free
                                      2. Green Curry (Extra Spicy) - $18.99 x 1
                                         - Sides: rice
                                         - Dietary: dairy free, nut free
                                      
                                      Total: $50.97
                                      Payment: credit card
                                      ```
                                      """;
    
    private (LLamaWeights Model, ModelParams Parameters)? _modelInfo = null;


    private async Task Prompt(string prompt, string? grammar = null)
    {
        _modelInfo ??= await ConfigureModel();

        var (model, parameters) = _modelInfo.Value;
        
        var executor = new StatelessExecutor(model, parameters)
        {
            ApplyTemplate = true,
            // SystemMessage = "Return the results as JSON only, no formatting."
        };
        var inferenceParams = grammar != null
            ? new InferenceParams { SamplingPipeline = new GreedySamplingPipeline() { Grammar = new Grammar(grammar, "root"), }, }
            : new InferenceParams { SamplingPipeline = new GreedySamplingPipeline() };


        var stopwatch = new Stopwatch();
        stopwatch.Start();
        var sb = new StringBuilder();
        await foreach(var s in executor.InferAsync(prompt, inferenceParams))
        {
            Console.Write(s);
            sb.Append(s);
        }
        stopwatch.Stop();
        Console.WriteLine();
        Console.WriteLine(stopwatch.Elapsed);
        Console.WriteLine();
    }

    async Task<(LLamaWeights model, ModelParams parameters)> ConfigureModel()
    {
        var parameters = new ModelParams(@"b:\models\gemma-2-2b-it-Q4_K_M.gguf")
        {
            ContextSize = 4000,
            GpuLayerCount = -1,
        };
            
        var model = await LLamaWeights.LoadFromFileAsync(parameters);
        return (model, parameters);
    }

    public async Task RunAsync()
    {
        await NoGuidanceAsync();
        await SampleJson();
        await WithOverlySimpleGbnf();
        await WithJsonGbnf();
        await WithCompleteJsonGbnf();
    }
    
    

    async Task NoGuidanceAsync()
    {
        Console.WriteLine("No Guidance");
        Console.WriteLine("==========");
        await Prompt(basePrompt + 
                     """
                     
                     Return results as valid JSON
                     """);
    }
    
    async Task SampleJson()
    {
        Console.WriteLine("SampleJson");
        Console.WriteLine("==========");
        await Prompt(basePrompt + 
                     """

                     Return results as valid JSON in the following format:
                     {
                         "OrderId": "⟨string value of OrderId⟩",
                         "OrderTime": "⟨ISO 8601 format date value of OrderTime⟩",
                         "Status": "⟨New|Preparing|ReadyForPickup|Delivered|Cancelled⟩",
                         "CustomerName": "⟨string value of CustomerName⟩",
                         "PhoneNumber": "⟨string value of PhoneNumber⟩",
                         "DeliveryAddress": {
                             "StreetAddress": "⟨string value of StreetAddress⟩", // The full street address, including street number, street name, and any apartment or suite number
                             "City": "⟨string value of City⟩",
                             "State": "⟨CA|NY|NY|TX⟩", // Allowed values: CA or NY or NY or TX
                             "ZipCode": "⟨string value of ZipCode⟩"
                         },
                         "Items": [{
                         "Name": "⟨string value of Name⟩", // The name of the menu item, excluding spice level.
                         "Price": ⟨decimal value of Price⟩,
                         "Quantity": ⟨integer value of Quantity⟩,
                         "SpicePreference": "⟨Mild|Medium|Hot|ExtraHot⟩",
                         "Sides": ["⟨string value of String_1⟩", "⟨String_2⟩", "⟨String_N⟩"],
                         "DietaryRestrictions": ["⟨string value of String_1⟩", "⟨String_2⟩", "⟨String_N⟩"]
                     }, MenuItem_2, MenuItem_N],
                         "TotalAmount": ⟨decimal value of TotalAmount⟩,
                         "PaymentMethod": "⟨cash|credit|debit⟩" // Allowed values: cash or credit or debit
                     }
                     
                     Replace all placeholders, ⟨...⟩, in the format with the actual values extracted from the text. Do not return placeholders in the final output.
                     """);
    }

    async Task WithOverlySimpleGbnf()
    {
        var grammar = """
                      root   ::= "{" content
                      content ::= .*
                      """;
        
        Console.WriteLine("WithSimpleGbnf");
        Console.WriteLine("==========");
        await Prompt(basePrompt + 
                     """
                     
                     Return results as valid JSON in the following format:
                     {
                         "OrderId": "⟨string value of OrderId⟩",
                         "OrderTime": "⟨ISO 8601 format date value of OrderTime⟩",
                         "Status": "⟨New|Preparing|ReadyForPickup|Delivered|Cancelled⟩",
                         "CustomerName": "⟨string value of CustomerName⟩",
                         "PhoneNumber": "⟨string value of PhoneNumber⟩",
                         "DeliveryAddress": {
                             "StreetAddress": "⟨string value of StreetAddress⟩", // The full street address, including street number, street name, and any apartment or suite number
                             "City": "⟨string value of City⟩",
                             "State": "⟨CA|NY|NY|TX⟩", // Allowed values: CA or NY or NY or TX
                             "ZipCode": "⟨string value of ZipCode⟩"
                         },
                         "Items": [{
                         "Name": "⟨string value of Name⟩", // The name of the menu item, excluding spice level.
                         "Price": ⟨decimal value of Price⟩,
                         "Quantity": ⟨integer value of Quantity⟩,
                         "SpicePreference": "⟨Mild|Medium|Hot|ExtraHot⟩",
                         "Sides": ["⟨string value of String_1⟩", "⟨String_2⟩", "⟨String_N⟩"],
                         "DietaryRestrictions": ["⟨string value of String_1⟩", "⟨String_2⟩", "⟨String_N⟩"]
                     }, MenuItem_2, MenuItem_N],
                         "TotalAmount": ⟨decimal value of TotalAmount⟩,
                         "PaymentMethod": "⟨cash|credit|debit⟩" // Allowed values: cash or credit or debit
                     }
                     
                     Replace all placeholders, ⟨...⟩, in the format with the actual values extracted from the text. Do not return placeholders in the final output.
                     
                     
                     """, grammar);
        
    }
    
    async Task WithJsonGbnf()
    {
        var grammar = """
                      root   ::= object
                      value  ::= object | array | string | number | ("true" | "false" | "null") ws
                      
                      object ::=
                        "{" ws (
                                  string ":" ws value
                          ("," ws string ":" ws value)*
                        )? "}" ws
                      
                      array  ::=
                        "[" ws (
                                  value
                          ("," ws value)*
                        )? "]" ws
                      
                      string ::=
                        "\"" (
                          [^"\\\x7F\x00-\x1F] |
                          "\\" (["\\bfnrt] | "u" [0-9a-fA-F]{4}) # escapes
                        )* "\"" ws
                      
                      number ::= ("-"? ([0-9] | [1-9] [0-9]{0,15})) ("." [0-9]+)? ([eE] [-+]? [0-9] [1-9]{0,15})? ws
                      
                      # Optional space: by convention, applied in this grammar after literal chars when allowed
                      ws ::= | " " | "\n" [ \t]{0,20}
                      """;
        
        Console.WriteLine("JsonGbnf");
        Console.WriteLine("==========");
        await Prompt(basePrompt + 
                     """
                     
                     Return results as valid JSON in the following format:
                     {
                         "OrderId": "⟨string value of OrderId⟩",
                         "OrderTime": "⟨ISO 8601 format date value of OrderTime⟩",
                         "Status": "⟨New|Preparing|ReadyForPickup|Delivered|Cancelled⟩",
                         "CustomerName": "⟨string value of CustomerName⟩",
                         "PhoneNumber": "⟨string value of PhoneNumber⟩",
                         "DeliveryAddress": {
                             "StreetAddress": "⟨string value of StreetAddress⟩", // The full street address, including street number, street name, and any apartment or suite number
                             "City": "⟨string value of City⟩",
                             "State": "⟨CA|NY|NY|TX⟩", // Allowed values: CA or NY or NY or TX
                             "ZipCode": "⟨string value of ZipCode⟩"
                         },
                         "Items": [{
                         "Name": "⟨string value of Name⟩", // The name of the menu item, excluding spice level.
                         "Price": ⟨decimal value of Price⟩,
                         "Quantity": ⟨integer value of Quantity⟩,
                         "SpicePreference": "⟨Mild|Medium|Hot|ExtraHot⟩",
                         "Sides": ["⟨string value of String_1⟩", "⟨String_2⟩", "⟨String_N⟩"],
                         "DietaryRestrictions": ["⟨string value of String_1⟩", "⟨String_2⟩", "⟨String_N⟩"]
                     }, MenuItem_2, MenuItem_N],
                         "TotalAmount": ⟨decimal value of TotalAmount⟩,
                         "PaymentMethod": "⟨cash|credit|debit⟩" // Allowed values: cash or credit or debit
                     }
                     
                     Replace all placeholders, ⟨...⟩, in the format with the actual values extracted from the text. Do not return placeholders in the final output.
                     
                     
                     """, grammar);
        
    }
    
    async Task WithCompleteJsonGbnf()
    {
        var grammar = """
                      char ::= [^"\\\x7F\x00-\x1F⟨⟩] | [\\] (["\\bfnrt] | "u" [0-9a-fA-F]{4})
                      space ::= | " " | "\n" [ \t]{0,20}
                      root-orderid-kv ::= "\"OrderId\"" space ":" space "\"" char{1, 512} "\"" space
                      root-ordertime-kv ::= "\"OrderTime\"" space ":" space "\"" [0-9]{4} "-" ([0][1-9]|[1][0-2]) "-" ([0][1-9]|[12][0-9]|[3][01]) "T" ([01][0-9]|[2][0-3]) ":" [0-5][0-9] ":" [0-5][0-9] ("." [0-9]{3})? ("Z"|([+-] ([01][0-9]|[2][0-3]) ":" [0-5][0-9])) "\"" space
                      root-status-kv ::= "\"Status\"" space ":" space ("\"New\""|"\"Preparing\""|"\"ReadyForPickup\""|"\"Delivered\""|"\"Cancelled\"") space
                      root-customername-kv ::= "\"CustomerName\"" space ":" space "\"" char{1, 512} "\"" space
                      root-phonenumber-kv ::= "\"PhoneNumber\"" space ":" space "\"" char{1, 512} "\"" space
                      root-deliveryaddress-streetaddress-kv ::= "\"StreetAddress\"" space ":" space "\"" char{1, 512} "\"" space
                      root-deliveryaddress-city-kv ::= "\"City\"" space ":" space "\"" char{1, 512} "\"" space
                      root-deliveryaddress-state ::= ("\"CA\""|"\"NY\""|"\"NY\""|"\"TX\"") space
                      root-deliveryaddress-state-kv ::= "\"State\"" space ":" space root-deliveryaddress-state
                      root-deliveryaddress-zipcode-kv ::= "\"ZipCode\"" space ":" space "\"" char{1, 512} "\"" space
                      root-deliveryaddress-kv ::= "\"DeliveryAddress\"" space ":" space "{" space root-deliveryaddress-streetaddress-kv "," space root-deliveryaddress-city-kv "," space root-deliveryaddress-state-kv "," space root-deliveryaddress-zipcode-kv "}" space
                      root-items-item-name-kv ::= "\"Name\"" space ":" space "\"" char{1, 512} "\"" space
                      root-items-item-price-kv ::= "\"Price\"" space ":" space ("-"? ([0] | [1-9] [0-9]{0,15}) ("." [0-9]{1,15})?) space
                      root-items-item-quantity-kv ::= "\"Quantity\"" space ":" space ("-"? [0] | [1-9] [0-9]{0,15}) space
                      root-items-item-spicepreference-kv ::= "\"SpicePreference\"" space ":" space ("\"Mild\""|"\"Medium\""|"\"Hot\""|"\"ExtraHot\"") space
                      root-items-item-sides-item ::= "\"" char{1, 512} "\"" space
                      root-items-item-sides-kv ::= "\"Sides\"" space ":" space "[" space (root-items-item-sides-item ("," space root-items-item-sides-item)*)? "]" space
                      root-items-item-dietaryrestrictions-item ::= "\"" char{1, 512} "\"" space
                      root-items-item-dietaryrestrictions-kv ::= "\"DietaryRestrictions\"" space ":" space "[" space (root-items-item-dietaryrestrictions-item ("," space root-items-item-dietaryrestrictions-item)*)? "]" space
                      root-items-item ::= "{" space root-items-item-name-kv "," space root-items-item-price-kv "," space root-items-item-quantity-kv "," space root-items-item-spicepreference-kv "," space root-items-item-sides-kv "," space root-items-item-dietaryrestrictions-kv "}" space
                      root-items-kv ::= "\"Items\"" space ":" space "[" space (root-items-item ("," space root-items-item)*)? "]" space
                      root-totalamount-kv ::= "\"TotalAmount\"" space ":" space ("-"? ([0] | [1-9] [0-9]{0,15}) ("." [0-9]{1,15})?) space
                      root-paymentmethod ::= ("\"cash\""|"\"credit\""|"\"debit\"") space
                      root-paymentmethod-kv ::= "\"PaymentMethod\"" space ":" space root-paymentmethod
                      root ::= "{" space root-orderid-kv "," space root-ordertime-kv "," space root-status-kv "," space root-customername-kv "," space root-phonenumber-kv "," space root-deliveryaddress-kv "," space root-items-kv "," space root-totalamount-kv "," space root-paymentmethod-kv "}" space
                      """;
        
        Console.WriteLine("CompleteGbnf");
        Console.WriteLine("==========");
        await Prompt(basePrompt + 
                     """
                     
                     Return results as valid JSON in the following format:
                     {
                         "OrderId": "⟨string value of OrderId⟩",
                         "OrderTime": "⟨ISO 8601 format date value of OrderTime⟩",
                         "Status": "⟨New|Preparing|ReadyForPickup|Delivered|Cancelled⟩",
                         "CustomerName": "⟨string value of CustomerName⟩",
                         "PhoneNumber": "⟨string value of PhoneNumber⟩",
                         "DeliveryAddress": {
                             "StreetAddress": "⟨string value of StreetAddress⟩", // The full street address, including street number, street name, and any apartment or suite number
                             "City": "⟨string value of City⟩",
                             "State": "⟨CA|NY|NY|TX⟩", // Allowed values: CA or NY or NY or TX
                             "ZipCode": "⟨string value of ZipCode⟩"
                         },
                         "Items": [{
                         "Name": "⟨string value of Name⟩", // The name of the menu item, excluding spice level.
                         "Price": ⟨decimal value of Price⟩,
                         "Quantity": ⟨integer value of Quantity⟩,
                         "SpicePreference": "⟨Mild|Medium|Hot|ExtraHot⟩",
                         "Sides": ["⟨string value of String_1⟩", "⟨String_2⟩", "⟨String_N⟩"],
                         "DietaryRestrictions": ["⟨string value of String_1⟩", "⟨String_2⟩", "⟨String_N⟩"]
                     }, MenuItem_2, MenuItem_N],
                         "TotalAmount": ⟨decimal value of TotalAmount⟩,
                         "PaymentMethod": "⟨cash|credit|debit⟩" // Allowed values: cash or credit or debit
                     }
                     
                     Replace all placeholders, ⟨...⟩, in the format with the actual values extracted from the text. Do not return placeholders in the final output.
                     
                     
                     """, grammar);
        
    }
}