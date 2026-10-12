namespace NexSpace.Application;

/// <summary>Bounded diagnostic experiments only; never a production reclamation strategy.</summary>
public static class DiagnosticResourceBudget
{
    public static bool Exceeded(int handles, long privateBytes, long workingSet, uint gdiObjects, uint userObjects, TimeSpan elapsed) =>
        handles > 5500 || privateBytes > 512L * 1024 * 1024 || workingSet > 768L * 1024 * 1024 ||
        gdiObjects > 4000 || userObjects > 2000 || elapsed > TimeSpan.FromMinutes(4);
}
