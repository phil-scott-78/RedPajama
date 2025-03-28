using Spectre.Console;
using Spectre.Console.Rendering;

namespace RedPajama.ConsoleTest;

internal static class SpectreHelpers
{
    public static Rows CreateExceptionRows(BunchOfNotEqualException exception)
    {
        // Create a Rows object as the root container
        var rows = exception.NotEqualExceptions.Select(NotEqualExceptionToMarkup).ToList();
    
        // Add all nested BunchOfNotEqualExceptions as Trees
        foreach (var nestedEx in exception.BunchOfNotEqualExceptions)
        {
            var nestedTree = new Tree($"{nestedEx.Caller.EscapeMarkup()}");
            PopulateTree(nestedTree, nestedEx);
            rows.Add(nestedTree);
        }
    
        return new Rows(rows);
    }

    private static void PopulateTree(IHasTreeNodes parentNode, BunchOfNotEqualException exception)
    {
        // First add all NotEqualExceptions
        foreach (var notEqualEx in exception.NotEqualExceptions)
        {
            parentNode.AddNode(NotEqualExceptionToMarkup(notEqualEx));
        }
    
        // Then recursively add all nested BunchOfNotEqualExceptions
        foreach (var nestedEx in exception.BunchOfNotEqualExceptions)
        {
            var bunchNode = parentNode.AddNode($"[blue]{nestedEx.Caller.EscapeMarkup()}[/]");
            PopulateTree(bunchNode, nestedEx);
        }
    }

    public static IRenderable CreateExceptionRows(NotEqualException exception)
    {
        return NotEqualExceptionToMarkup(exception);
    }
    
    private static IRenderable NotEqualExceptionToMarkup(NotEqualException notEqualEx)
    {
        return new Markup($"[blue]{notEqualEx.Caller.EscapeMarkup()}[/] Expected [green]{notEqualEx.Expected.EscapeMarkup()}[/], Actual [red]{notEqualEx.Actual.EscapeMarkup()}[/]");
    }
}