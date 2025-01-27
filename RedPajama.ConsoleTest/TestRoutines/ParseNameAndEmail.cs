using JetBrains.Annotations;
using LLama;
using LLama.Abstractions;

namespace RedPajama.ConsoleTest.TestRoutines;

internal class ParseNameAndEmail : ITestRoutine
{
    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    private class Person
    {
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public required string Email { get; init; }
    }
    
    public async Task Run(LLamaWeights model, IContextParams parameters)
    {
        var executor = new StatelessExecutor(model, parameters);

        var person = await executor.InferAsync<Person>("""
                                                       Extract the first name, last name and e-mail from this text:
                                                       ```
                                                       Hey, this is Marcus Smith. When you give me a chance, e-mail me at marcus.smith@gmail.com so we can get this all sorted out.");
                                                       ```
                                                       """);

        person.ShouldAllBe([
            p=>p.FirstName.ShouldBe("Marcus"),
            p=>p.LastName.ShouldBe("Smith"),
            p=>p.Email.ShouldBe("marcus.smith@gmail.com")
        ]);
    }
}