using System.Text;

namespace ThixTunnel.Core.Infrastructure;

/// <summary>A small daily log file in %LocalAppData%\ThixTunnel\logs. Never write passwords or keys into it.</summary>
public static class Log
{
    private static readonly object Gate = new();

    public static void Info(string message) => Write("INFO ", message);

    public static void Warning(string message) => Write("WARN ", message);

    public static void Error(string message, Exception? exception = null) =>
        Write("ERROR", exception is null ? message : $"{message}: {exception}");

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(AppPaths.LogDirectory);
                var path = Path.Combine(AppPaths.LogDirectory, $"thixtunnel-{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(path, $"{DateTime.Now:HH:mm:ss.fff} {level} {message}{Environment.NewLine}", Encoding.UTF8);
            }
        }
        catch (IOException)
        {
            // logging must never break the application
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
