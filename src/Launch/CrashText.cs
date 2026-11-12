using System;

namespace OpenSettlers.Launch;

/// <summary>The crash of the asset browser's state, caught and worded.</summary>
public static class CrashText
{
    /// <summary>
    /// <c>subject: crashed: exception type name: message</c>. The subject is the selected asset's file, else the
    /// selected kind.
    /// </summary>
    public static string Of(string? subject, Exception e) => $"{subject}: crashed: {e.GetType().Name}: {e.Message}";

    /// <summary>
    /// Runs <paramref name="action"/>. Returns null when it completes. Where it throws, passes the log line
    /// <c>asset browser crashed: exception</c> to <paramref name="log"/> and returns <see cref="Of"/> over the
    /// subject <paramref name="subject"/> gives, read after the crash.
    /// </summary>
    public static string? Guard(Action action, Func<string?> subject, Action<string> log)
    {
        try
        {
            action();

            return null;
        }
        catch (Exception e)
        {
            log($"asset browser crashed: {e}");

            return Of(subject(), e);
        }
    }
}
