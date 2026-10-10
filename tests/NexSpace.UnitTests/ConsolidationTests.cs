using NexSpace.Application;
using NexSpace.Domain;
using NexSpace.Presentation;

namespace NexSpace.UnitTests;

public sealed class ConsolidationTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Drop_only_accepts_storage_items(bool storage, bool expected) => Assert.Equal(expected, ReferenceDropProcessor.CanAccept(storage));

    [Theory]
    [InlineData(-1, 0, 320, 220)]
    [InlineData(int.MaxValue, int.MaxValue, 16384, 16384)]
    [InlineData(420, 340, 420, 340)]
    public void Invalid_dimensions_are_normalized_before_window_use(int width, int height, int expectedWidth, int expectedHeight)
    {
        var placement = ContainerGeometry.Normalize(new("test", int.MinValue, int.MaxValue, width, height));
        Assert.Equal(expectedWidth, placement.Width); Assert.Equal(expectedHeight, placement.Height);
        var display = new DisplayInfo("primary", "Primary", -1920, -100, 1920, 1080, -1920, -100, 1920, 1040, true, 144);
        var safe = PlacementPolicy.KeepFullyVisible(new(placement.MonitorKey, placement.X, placement.Y, placement.Width, placement.Height), display);
        Assert.InRange(safe.X, display.WorkX, display.WorkX + display.WorkWidth - safe.Width);
        Assert.InRange(safe.Y, display.WorkY, display.WorkY + display.WorkHeight - safe.Height);
    }

    [Fact]
    public void View_model_handles_missing_targets_open_failures_and_repository_recovery()
    {
        var repository = new StubRepository();
        var platform = new FailingPlatform();
        var vm = new ContainerViewModel(new ContainerService(repository), platform, repository.Container.Id);
        Assert.True(vm.Reload());
        var item = Assert.Single(vm.Items);
        Assert.Equal("missing", item.Status.Code);
        Assert.Contains("Missing", item.AccessibleName);
        vm.OpenCommand.Execute(item); Assert.Contains("No se pudo abrir", vm.Status);
        platform.ThrowProbe = true;
        Assert.True(vm.Reload()); Assert.Equal("probe-failed", Assert.Single(vm.Items).Status.Code);
        repository.Fail = true;
        Assert.False(vm.Reload()); Assert.Contains("Reintenta", vm.Status);
        repository.Fail = false; platform.ThrowProbe = false;
        Assert.True(vm.Reload());
        Assert.False(vm.OpenCommand.CanExecute(null));
        Assert.False(vm.MoveDownCommand.CanExecute("wrong type"));
    }

    private sealed class FailingPlatform : IContainerItemPlatformService
    {
        public bool ThrowProbe;
        public ResourceStatus Inspect(ContainerItemReference item) => ThrowProbe ? throw new IOException() : new(false, false, "missing", "Missing");
        public OperationResult Open(ContainerItemReference item) => throw new UnauthorizedAccessException();
    }

    private sealed class StubRepository : IContainerRepository
    {
        public bool Fail;
        public NexContainer Container = NexContainer.Create("Test") with { Items = [new(Guid.NewGuid(), ContainerItemKind.File, "C:/isolated/missing.txt", "key", "Reference", 0, DateTimeOffset.UtcNow)] };
        public NexContainer? Get(Guid id) => Fail ? throw new IOException() : Container;
        public IReadOnlyList<NexContainer> GetAll() => [Container];
        public NexContainer Create(NexContainer container) => Container = container;
        public NexContainer Update(NexContainer container) => Container = container;
        public bool Delete(Guid id) => false;
        public ContainerItemReference AddReference(Guid containerId, ContainerItemKind kind, string target, string displayName) => throw new IOException();
        public bool RemoveReference(Guid containerId, Guid itemId) => false;
        public ContainerItemReference UpdateReference(Guid containerId, ContainerItemReference item) => item;
        public bool MoveReference(Guid containerId, Guid itemId, int offset) => false;
    }
}
