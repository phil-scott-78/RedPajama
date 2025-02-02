using System.Runtime.CompilerServices;

namespace RedPajama.ConsoleTest;

internal static class Tester
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ShouldBe(this string actual, string expected, [CallerArgumentExpression(nameof(actual))] string? callerArgumentExpression = null)
    {
        ArgumentNullException.ThrowIfNull(actual);
        
        if (!actual.Trim().Equals(expected.Trim(), StringComparison.CurrentCultureIgnoreCase))
        {
            throw new NotEqualException(callerArgumentExpression ?? "something", expected, actual);
        }
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ShouldBe<T>(this T actual, T expected, [CallerArgumentExpression(nameof(actual))] string? callerArgumentExpression = null)
    {
        ArgumentNullException.ThrowIfNull(actual); 
        ArgumentNullException.ThrowIfNull(expected);
        
        if (!actual.Equals(expected))
        {
            throw new NotEqualException(callerArgumentExpression ?? "something", expected.ToString() ?? string.Empty , actual.ToString()?? string.Empty);
        }
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ShouldBe(this string[] actual, string[] expected, [CallerArgumentExpression(nameof(actual))] string? callerArgumentExpression = null)
    {
        ArgumentNullException.ThrowIfNull(actual);
        ArgumentNullException.ThrowIfNull(expected);

        var arrayEquals = actual.Select(t => t.Trim()).OrderBy(t => t).SequenceEqual(actual.Select(t => t.Trim()).OrderBy(t => t));
        if (arrayEquals) return;
        
        var actualString = string.Join(", ", actual.Select(t => t.ToString()));
        var expectedString = string.Join(", ", expected.Select(t => t.ToString()));
        throw new NotEqualException(callerArgumentExpression ?? "something", expectedString , actualString);
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ShouldBe<T>(this T[] actual, T[] expected, [CallerArgumentExpression(nameof(actual))] string? callerArgumentExpression = null)
    {
        ArgumentNullException.ThrowIfNull(actual);

        var arrayEquals = actual.OrderBy(t => t).SequenceEqual(actual.OrderBy(t => t));
        if (arrayEquals) return;
        
        var actualString = string.Join(", ", actual.Select(t => t?.ToString() ?? "null"));
        var expectedString = string.Join(", ", expected.Select(t => t?.ToString() ?? "null"));
        throw new NotEqualException(callerArgumentExpression ?? "something", expectedString , actualString);
    }
    
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ShouldAllBe<T>(this T item, Action<T>[] actions, [CallerArgumentExpression(nameof(item))] string? callerArgumentExpression = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        
        List<NotEqualException> errors = new();
        foreach (var action in actions)
        {
            try
            {
                action(item);

            }
            catch (NotEqualException e)
            {
                errors.Add(e);
            }
        }

        if (errors.Count > 0)
        {
            throw new BunchOfNotEqualException(callerArgumentExpression ?? "something", errors.ToArray());
        }
    }
}

internal class BunchOfNotEqualException(string caller, NotEqualException[] exception) : Exception
{
    public string Caller { get; } = caller;
    public NotEqualException[] Exception { get; } = exception;
}

internal class NotEqualException(string caller, string expected, string actual) : Exception
{
    public string Caller { get; } = caller;
    public string Expected { get; } = expected;
    public string Actual { get; } = actual;
}