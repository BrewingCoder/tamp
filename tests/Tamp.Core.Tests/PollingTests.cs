using System.Diagnostics;
using Xunit;

namespace Tamp.Core.Tests;

public class PollingTests
{
    [Fact]
    public async Task Returns_True_When_Condition_Succeeds_Immediately()
    {
        var attempts = 0;

        var result = await Polling.Until(
            ct => { attempts++; return Task.FromResult(true); },
            timeout: TimeSpan.FromSeconds(5));

        Assert.True(result);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task Returns_True_After_N_Attempts()
    {
        var attempts = 0;

        var result = await Polling.Until(
            ct => { attempts++; return Task.FromResult(attempts >= 3); },
            timeout: TimeSpan.FromSeconds(5),
            backoff: Backoff.Constant(TimeSpan.FromMilliseconds(10)));

        Assert.True(result);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task Returns_False_When_Timeout_Elapses()
    {
        var attempts = 0;
        var sw = Stopwatch.StartNew();

        var result = await Polling.Until(
            ct => { attempts++; return Task.FromResult(false); },
            timeout: TimeSpan.FromMilliseconds(150),
            backoff: Backoff.Constant(TimeSpan.FromMilliseconds(30)));

        sw.Stop();
        Assert.False(result);
        Assert.True(attempts >= 2, $"Expected at least 2 attempts, got {attempts}");
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2), $"Polling overran budget: {sw.Elapsed.TotalMilliseconds}ms");
    }

    [Fact]
    public async Task Non_Positive_Timeout_Allows_One_Check()
    {
        var attempts = 0;

        var result = await Polling.Until(
            ct => { attempts++; return Task.FromResult(true); },
            timeout: TimeSpan.Zero);

        Assert.True(result);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task Cancellation_Propagates_OperationCanceledException()
    {
        using var cts = new CancellationTokenSource();

        var task = Polling.Until(
            async ct => { await Task.Delay(100, ct).ConfigureAwait(false); return false; },
            timeout: TimeSpan.FromSeconds(5),
            backoff: Backoff.Constant(TimeSpan.FromMilliseconds(20)),
            cancellationToken: cts.Token);

        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAsync<TaskCanceledException>(() => task);
    }

    [Fact]
    public async Task Condition_Exception_Propagates()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Polling.Until(
            ct => throw new InvalidOperationException("boom"),
            timeout: TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task Null_Condition_Throws_ArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => Polling.Until(
            null!,
            timeout: TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task Uses_Backoff_Delay_Between_Attempts()
    {
        var stamps = new List<DateTimeOffset>();

        await Polling.Until(
            ct => { stamps.Add(DateTimeOffset.UtcNow); return Task.FromResult(stamps.Count >= 3); },
            timeout: TimeSpan.FromSeconds(5),
            backoff: Backoff.Constant(TimeSpan.FromMilliseconds(50)));

        Assert.Equal(3, stamps.Count);
        // Between attempt 1 and 2, and 2 and 3, expect ~50ms delays (allow drift for thread scheduling).
        var gap12 = stamps[1] - stamps[0];
        var gap23 = stamps[2] - stamps[1];
        Assert.True(gap12 >= TimeSpan.FromMilliseconds(40), $"Expected ~50ms gap, got {gap12.TotalMilliseconds}ms");
        Assert.True(gap23 >= TimeSpan.FromMilliseconds(40), $"Expected ~50ms gap, got {gap23.TotalMilliseconds}ms");
    }

    [Fact]
    public async Task Logger_Receives_Trace_Per_Attempt_And_Warn_On_Timeout()
    {
        using var writer = new StringWriter();
        var logger = new Logger(writer, LogLevel.Trace);

        var result = await Polling.Until(
            ct => Task.FromResult(false),
            timeout: TimeSpan.FromMilliseconds(80),
            backoff: Backoff.Constant(TimeSpan.FromMilliseconds(20)),
            logger: logger);

        Assert.False(result);
        var output = writer.ToString();
        Assert.Contains("[TRACE]", output);
        Assert.Contains("[WARN]", output);
        Assert.Contains("timed out", output);
    }

    [Fact]
    public async Task Logger_Receives_Debug_On_Success()
    {
        using var writer = new StringWriter();
        var logger = new Logger(writer, LogLevel.Debug);

        var result = await Polling.Until(
            ct => Task.FromResult(true),
            timeout: TimeSpan.FromSeconds(1),
            logger: logger);

        Assert.True(result);
        var output = writer.ToString();
        Assert.Contains("[DEBUG]", output);
        Assert.Contains("satisfied", output);
    }

    [Fact]
    public async Task Cancellation_Before_Start_Throws_Immediately()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => Polling.Until(
            ct => Task.FromResult(true),
            timeout: TimeSpan.FromSeconds(5),
            cancellationToken: cts.Token));
    }
}
