using NexSpace.Application;
using Xunit;
namespace NexSpace.UnitTests;

public sealed class DiagnosticResourceBudgetTests
{
    [Fact] public void Typical_sample_is_safe() => Assert.False(DiagnosticResourceBudget.Exceeded(2500, 128L << 20, 256L << 20, 100, 100, TimeSpan.FromSeconds(30)));
    [Theory]
    [InlineData(5501, 0, 0, 0, 0, 0)]
    [InlineData(0, 536870913, 0, 0, 0, 0)]
    [InlineData(0, 0, 805306369, 0, 0, 0)]
    [InlineData(0, 0, 0, 4001, 0, 0)]
    [InlineData(0, 0, 0, 0, 2001, 0)]
    [InlineData(0, 0, 0, 0, 0, 241)]
    public void Each_limit_stops_the_experiment(int handles, long memory, long workingSet, uint gdi, uint user, int seconds) =>
        Assert.True(DiagnosticResourceBudget.Exceeded(handles, memory, workingSet, gdi, user, TimeSpan.FromSeconds(seconds)));
}
