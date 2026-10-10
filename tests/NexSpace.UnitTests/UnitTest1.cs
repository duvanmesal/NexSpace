namespace NexSpace.UnitTests;

public class RuntimeBaselineTests
{
    [Fact]
    public void Test_host_uses_dotnet_10()
    {
        Assert.StartsWith(".NET 10", System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
    }
}
