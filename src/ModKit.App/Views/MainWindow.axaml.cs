using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ModKit.App.ViewModels;

namespace ModKit.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private MainWindowViewModel? Vm => DataContext as MainWindowViewModel;

    private void OnDragOver(object? sender, DragEventArgs e)
        => e.DragEffects = e.Data.Contains(DataFormats.Files) ? DragDropEffects.Copy : DragDropEffects.None;

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (Vm == null) return;
        var files = e.Data.GetFiles();
        if (files == null) return;
        foreach (var f in files)
        {
            string? p = f.TryGetLocalPath();
            if (p != null) Vm.InstallFrom(p);
        }
    }

    private async void BrowseGame(object? sender, RoutedEventArgs e)
    {
        string? f = await PickFolder("Select the Block Story game folder");
        if (f != null && Vm != null) Vm.GameDir = f;
    }

    private async void BrowseWorkspace(object? sender, RoutedEventArgs e)
    {
        string? f = await PickFolder("Select a folder for your mod projects");
        if (f != null && Vm != null) Vm.WorkspaceDir = f;
    }

    private async void InstallModFile(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Pick a mod .dll or a .zip mod pack to install",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Mod (.dll / .zip)") { Patterns = new[] { "*.dll", "*.zip" } }
            }
        });
        string? path = files.FirstOrDefault()?.TryGetLocalPath();
        if (path != null && Vm != null) Vm.InstallFrom(path);
    }

    private async void InstallModFolder(object? sender, RoutedEventArgs e)
    {
        string? folder = await PickFolder("Pick a folder of mod .dll files to install");
        if (folder != null && Vm != null) Vm.InstallFrom(folder);
    }

    private async void DeleteModClick(object? sender, RoutedEventArgs e)
    {
        if (Vm == null || (sender as Control)?.DataContext is not ModKit.Core.WorkspaceMod mod) return;
        if (await Dialogs.ConfirmAsync(this, "Delete mod source",
                $"Delete the source folder for \"{mod.Name}\"?\n\nThis removes the project files in your workspace. The installed DLL in the game stays (disable it on the Launcher tab if you want).",
                "Delete"))
            Vm.DeleteModCommand.Execute(mod);
    }

    private async void UninstallModClick(object? sender, RoutedEventArgs e)
    {
        if (Vm == null || (sender as Control)?.DataContext is not ModKit.App.ViewModels.ModRow row) return;
        if (await Dialogs.ConfirmAsync(this, "Uninstall mod",
                $"Remove \"{row.Mod.Name}\" from the game's plugins folder?",
                "Uninstall"))
            Vm.UninstallModCommand.Execute(row);
    }

    private async Task<string?> PickFolder(string title)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });
        return folders.FirstOrDefault()?.TryGetLocalPath();
    }
}
