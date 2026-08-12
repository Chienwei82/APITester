namespace APITester.Core.Services;

public interface ILogger
{
    void Info(string message);
    void Warn(string message);
    void LogError(string message);
    void Debug(string message);
}
