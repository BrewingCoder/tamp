using System.Runtime.CompilerServices;

namespace Tamp;

/// <summary>
/// Build-script-friendly preconditions. Throws <see cref="InvalidOperationException"/>
/// with a clear message when a check fails.
/// </summary>
/// <remarks>
/// Named <c>Verify</c> rather than <c>Assert</c> to dodge the namespace
/// collision with xUnit's <c>Assert</c> in test files. From a build
/// script, <c>Verify.NotNull(...)</c> reads cleanly.
/// </remarks>
public static class Verify
{
    /// <summary>Throws if <paramref name="value"/> is null.</summary>
    public static T NotNull<T>(
        T? value,
        [CallerArgumentExpression(nameof(value))] string? expression = null)
        where T : class
    {
        if (value is null)
            throw new InvalidOperationException($"Expected non-null: {expression ?? "<expression>"}");
        return value;
    }

    /// <summary>Throws if <paramref name="value"/> is null.</summary>
    public static T NotNull<T>(
        T? value,
        [CallerArgumentExpression(nameof(value))] string? expression = null)
        where T : struct
    {
        if (value is null)
            throw new InvalidOperationException($"Expected non-null: {expression ?? "<expression>"}");
        return value.Value;
    }

    /// <summary>Throws if <paramref name="value"/> is null or empty.</summary>
    public static string NotNullOrEmpty(
        string? value,
        [CallerArgumentExpression(nameof(value))] string? expression = null)
    {
        if (string.IsNullOrEmpty(value))
            throw new InvalidOperationException($"Expected non-empty string: {expression ?? "<expression>"}");
        return value;
    }

    /// <summary>Throws if <paramref name="value"/> is null, empty, or whitespace.</summary>
    public static string NotNullOrWhiteSpace(
        string? value,
        [CallerArgumentExpression(nameof(value))] string? expression = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Expected non-blank string: {expression ?? "<expression>"}");
        return value;
    }

    /// <summary>Throws if <paramref name="condition"/> is false.</summary>
    public static void True(
        bool condition,
        string? message = null,
        [CallerArgumentExpression(nameof(condition))] string? expression = null)
    {
        if (!condition)
            throw new InvalidOperationException(message ?? $"Expected true: {expression ?? "<expression>"}");
    }

    /// <summary>Throws if <paramref name="condition"/> is true.</summary>
    public static void False(
        bool condition,
        string? message = null,
        [CallerArgumentExpression(nameof(condition))] string? expression = null)
    {
        if (condition)
            throw new InvalidOperationException(message ?? $"Expected false: {expression ?? "<expression>"}");
    }

    /// <summary>Throws if the sequence is null or empty.</summary>
    public static IEnumerable<T> NotEmpty<T>(
        IEnumerable<T>? sequence,
        [CallerArgumentExpression(nameof(sequence))] string? expression = null)
    {
        if (sequence is null) throw new InvalidOperationException($"Expected non-null sequence: {expression}");
        // Materialise once if it's not already a collection so we can both
        // count and return it.
        var list = sequence as IReadOnlyCollection<T> ?? sequence.ToList();
        if (list.Count == 0)
            throw new InvalidOperationException($"Expected non-empty sequence: {expression}");
        return list;
    }

    /// <summary>Throws if the sequence does not contain exactly one element. Returns that element.</summary>
    public static T Single<T>(
        IEnumerable<T>? sequence,
        [CallerArgumentExpression(nameof(sequence))] string? expression = null)
    {
        if (sequence is null) throw new InvalidOperationException($"Expected non-null sequence: {expression}");
        using var e = sequence.GetEnumerator();
        if (!e.MoveNext())
            throw new InvalidOperationException($"Expected exactly one element, got zero: {expression}");
        var first = e.Current;
        if (e.MoveNext())
            throw new InvalidOperationException($"Expected exactly one element, got more than one: {expression}");
        return first;
    }

    /// <summary>Throws unconditionally with the given message. Useful for unreachable branches.</summary>
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    public static void Fail(string message)
        => throw new InvalidOperationException(message);
}
