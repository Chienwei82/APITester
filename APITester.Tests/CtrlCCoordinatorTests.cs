using APITester.Rest;

namespace APITester.Tests;

public class CtrlCCoordinatorTests
{
    [Fact]
    public void Cancel_WithoutRunInCourse_CancelsAppToken()
    {
        using var ctrlC = new CtrlCCoordinator();

        ctrlC.Cancel();

        Assert.True(ctrlC.Token.IsCancellationRequested);
    }

    [Fact]
    public async Task Cancel_DuringRun_CancelsOnlyTheRun()
    {
        using var ctrlC = new CtrlCCoordinator();

        var run = ctrlC.RunCancellableAsync(async token =>
        {
            ctrlC.Cancel();
            await Task.Delay(Timeout.Infinite, token);
            return 0;
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        Assert.False(ctrlC.Token.IsCancellationRequested);
    }

    [Fact]
    public async Task RunCancellableAsync_WithoutCancel_ReturnsRunResult()
    {
        using var ctrlC = new CtrlCCoordinator();

        var exitCode = await ctrlC.RunCancellableAsync(_ => Task.FromResult(7));

        Assert.Equal(7, exitCode);
        Assert.False(ctrlC.Token.IsCancellationRequested);
    }

    [Fact]
    public async Task Cancel_AfterRunFinished_CancelsAppToken()
    {
        using var ctrlC = new CtrlCCoordinator();
        await ctrlC.RunCancellableAsync(_ => Task.FromResult(0));

        ctrlC.Cancel();

        Assert.True(ctrlC.Token.IsCancellationRequested);
    }

    [Fact]
    public async Task HasActiveRun_TracksWhetherARunIsInCourse()
    {
        using var ctrlC = new CtrlCCoordinator();
        Assert.False(ctrlC.HasActiveRun);

        var hasActiveRun = await ctrlC.RunCancellableAsync(async _ =>
        {
            Assert.True(ctrlC.HasActiveRun);
            await Task.Yield();
            return 0;
        });

        Assert.Equal(0, hasActiveRun);
        Assert.False(ctrlC.HasActiveRun);
    }
}
