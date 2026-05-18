using APITester.Core.Services;

namespace APITester.Tests;

public class RetryPolicyTests
{
    [Fact]
    public async Task None_DoesNotRetry_ThrowsOnFirstFailure()
    {
        var policy = RetryPolicy.None;
        var attempts = 0;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await policy.ExecuteAsync<int>(ct =>
            {
                attempts++;
                throw new HttpRequestException("fail");
            }));

        Assert.Equal(1, attempts);
        Assert.Contains("1 intentos", ex.Message);
        Assert.IsType<HttpRequestException>(ex.InnerException);
    }

    [Fact]
    public async Task WithRetries_SucceedsOnSecondAttempt()
    {
        var policy = new RetryPolicy { MaxRetries = 3, DelayMs = 10 };
        var attempts = 0;

        var result = await policy.ExecuteAsync(ct =>
        {
            attempts++;
            return Task.FromResult(attempts >= 2 ? 42 : throw new HttpRequestException("fail"));
        });

        Assert.Equal(42, result);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task WithRetries_ExhaustsAllAttempts()
    {
        var policy = new RetryPolicy { MaxRetries = 2, DelayMs = 10 };
        var attempts = 0;

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await policy.ExecuteAsync<int>(ct =>
            {
                attempts++;
                throw new HttpRequestException("fail");
            }));

        Assert.Equal(3, attempts);
        Assert.Contains("3 intentos", ex.Message);
        Assert.IsType<HttpRequestException>(ex.InnerException);
    }

    [Fact]
    public async Task NonRetryableException_DoesNotRetry()
    {
        var policy = new RetryPolicy { MaxRetries = 3, DelayMs = 10 };
        var attempts = 0;

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await policy.ExecuteAsync<int>(ct =>
            {
                attempts++;
                throw new ArgumentException("bad arg");
            }));

        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task SucceedsFirstAttempt_NoRetries()
    {
        var policy = new RetryPolicy { MaxRetries = 3, DelayMs = 10 };
        var attempts = 0;

        var result = await policy.ExecuteAsync(ct =>
        {
            attempts++;
            return Task.FromResult("success");
        });

        Assert.Equal("success", result);
        Assert.Equal(1, attempts);
    }
}
