namespace APITester.Core.Models;

public class ExecutionSummary
{
    public string OutputFile { get; init; } = string.Empty;
    public long TotalElapsedMs { get; init; }
    public int TotalRequests { get; init; }
    public int SuccessfulRequests { get; init; }
    public int FailedRequests { get; init; }
}
