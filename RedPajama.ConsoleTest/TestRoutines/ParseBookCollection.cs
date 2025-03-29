using System.ComponentModel;
using LLama;
using LLama.Abstractions;

namespace RedPajama.ConsoleTest.TestRoutines;

internal class ParseBookCollection : ITestRoutine
{
    public class Library
    {
        public required string Name { get; init; }
        public required Book[] Books { get; init; }
    }
    
    public class Book
    {    
        public required string Title { get; init; }
        public required string Author { get; init; }
        public required int Year { get; init; }
        [Description("Is this book checked out or available?")]
        public required bool Available { get; init; }
    }
   
    public async Task Run(LLamaWeights model, IContextParams parameters)
    {
        var executor = new StatelessExecutor(model, parameters);

        const string prompt = """
                              Extract the library name and book details:
                              ```
                              Central Library:
                              1. The Great Gatsby by F. Scott Fitzgerald (1925) [checked out]
                              2. 1984 by George Orwell (1949)
                              3. To Kill a Mockingbird by Harper Lee (1960)
                              ```
                              """;
        
        Library library;
        if (!model.IsThinkingModel())
        {
            library = await executor.InferAsync<Library>(prompt, JsonContext.Default, TypeModelContext.Default);
        }
        else
        {
            (library, _) = (await executor.InferWithThoughtsAsync<Library>(prompt, JsonContext.Default, TypeModelContext.Default));
        }


        library.ShouldAllBe([
            l => l.Name.ShouldBe("Central Library"),
            l => l.Books.Length.ShouldBe(3),
            l => l.Books[0].ShouldAllBe([
                b => b.Title.ShouldBe("The Great Gatsby"),
                b => b.Author.ShouldBe("F. Scott Fitzgerald"),
                b => b.Year.ShouldBe(1925),
                b => b.Available.ShouldBe(false),
            ]),
            l => l.Books[1].ShouldAllBe([
                b => b.Title.ShouldBe("1984"),
                b => b.Author.ShouldBe("George Orwell"),
                b => b.Year.ShouldBe(1949),
                b => b.Available.ShouldBe(true),
            ]),
            l => l.Books[2].ShouldAllBe([
                b => b.Title.ShouldBe("To Kill a Mockingbird"),
                b => b.Author.ShouldBe("Harper Lee"),
                b => b.Year.ShouldBe(1960),
                b => b.Available.ShouldBe(true),
            ]),
            
        ]);
    }
}