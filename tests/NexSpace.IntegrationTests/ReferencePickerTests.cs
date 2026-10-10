using NexSpace.Windows;

namespace NexSpace.IntegrationTests;

public sealed class ReferencePickerTests
{
    [Fact]
    public void Native_selection_preserves_shortcut_path_and_spaces()
    {
        var paths = WindowsReferenceFilePicker.ParseSelection("C:\\isolated\\nota acceso.lnk\0\0");
        Assert.Equal(["C:\\isolated\\nota acceso.lnk"], paths);
    }

    [Fact]
    public void Native_multiselect_combines_directory_and_filenames()
    {
        var paths = WindowsReferenceFilePicker.ParseSelection("C:\\isolated\0nota acceso.lnk\0otro archivo.txt\0\0");
        Assert.Equal(["C:\\isolated\\nota acceso.lnk", "C:\\isolated\\otro archivo.txt"], paths);
    }
}
