using System;
using System.Diagnostics;

namespace Tonono2;

public static class DebugLogger
{
    [Conditional("DEBUG")]
    public static void Log(string message) => Console.Error.WriteLine($"[Tonono2] {message}");
}
