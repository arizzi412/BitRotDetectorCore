namespace BitRotDetectorCore;

public interface IConcurrentLoggerService : IDisposable
{
    void Enqueue(string message);
}