using NexSpace.Domain;

namespace NexSpace.UnitTests;

public sealed class ContainerRulesTests
{
    [Fact]
    public void Display_name_limit_allows_valid_long_file_labels_but_container_name_stays_compact()
    {
        var label = new string('a', 180);
        Assert.Equal(label, ContainerRules.NormalizeDisplayName(label));
        Assert.Throws<ArgumentException>(() => ContainerRules.NormalizeName(label));
    }

    [Fact]
    public void System_resources_are_restricted_to_the_allowlist()
    {
        Assert.True(ContainerRules.IsAllowedSystemResource("shell:Downloads"));
        Assert.False(ContainerRules.IsAllowedSystemResource("file:///C:/unexpected"));
    }
}
