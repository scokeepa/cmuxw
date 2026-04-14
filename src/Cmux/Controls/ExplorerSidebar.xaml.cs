using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Cmux.Services;
using Cmux.ViewModels;
using Cmux.Views;

namespace Cmux.Controls;

public partial class ExplorerSidebar : UserControl
{
    private Point _dragStart;
    private bool _suppressFilterTextChanged;

    public event Action<string>? OpenPathInTerminalRequested;

    private ExplorerViewModel? Vm => DataContext as ExplorerViewModel;

    public ExplorerSidebar()
    {
        InitializeComponent();
        LocalizationManager.Instance.LanguageChanged += OnLanguageChanged;
        Unloaded += (_, _) => LocalizationManager.Instance.LanguageChanged -= OnLanguageChanged;
        Loaded += (_, _) => ApplyLocalizedLabels();
    }

    private void OnLanguageChanged()
    {
        Dispatcher.BeginInvoke(() =>
        {
            ApplyLocalizedLabels();
        });
    }

    private void ApplyLocalizedLabels()
    {
        ExplorerFilterBox.ToolTip = L.T("Filter explorer");
    }

    public void SetToolbarVisible(bool visible)
    {
        ExplorerToolbar.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public void SetFilterText(string? text)
    {
        var value = text ?? string.Empty;
        _suppressFilterTextChanged = true;
        ExplorerFilterBox.Text = value;
        _suppressFilterTextChanged = false;

        if (Vm != null)
            Vm.FilterText = value;
    }

    public string GetFilterText() => ExplorerFilterBox.Text;

    public bool TryPickAndAddRoot()
    {
        if (Vm == null)
            return false;

        var dialog = new OpenFolderDialog
        {
            Title = L.T("Add root"),
            Multiselect = false,
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) != true || string.IsNullOrWhiteSpace(dialog.FolderName))
            return false;

        if (!Vm.TryAddRoot(dialog.FolderName.Trim(), out var error))
        {
            MessageBox.Show(L.T(error), L.T("Explorer"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        return true;
    }

    public void ResetView()
    {
        SetFilterText(string.Empty);
        if (Vm != null)
            _ = Vm.RefreshNodeAsync(null);
    }

    private void ExplorerFilterBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressFilterTextChanged)
            return;

        if (Vm != null)
            Vm.FilterText = ExplorerFilterBox.Text;
    }

    private void AddRoot_Click(object sender, RoutedEventArgs e)
    {
        _ = TryPickAndAddRoot();
    }

    private async void RefreshExplorer_Click(object sender, RoutedEventArgs e)
    {
        if (Vm == null) return;
        await Vm.RefreshNodeAsync(Vm.SelectedItem);
    }

    private void ExplorerTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (Vm != null)
            Vm.SelectedItem = e.NewValue as ExplorerItemViewModel;
    }

    private void NodeContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        LocalizationManager.LocalizeContextMenuHeaders(((ContextMenu)sender).Items);
        if (sender is not ContextMenu cm || cm.PlacementTarget is not FrameworkElement fe)
            return;

        if (fe.DataContext is ExplorerItemViewModel node && Vm != null)
        {
            Vm.SelectedItem = node;
            foreach (var item in cm.Items.OfType<MenuItem>())
            {
                if (item.Name == "RemoveRootMenuItem")
                    item.IsEnabled = node.IsRoot;
                else if (item.Name == "NewFileMenuItem" || item.Name == "NewFolderMenuItem")
                    item.IsEnabled = node.IsDirectory;
                else if (item.Name == "RenameNodeMenuItem" || item.Name == "DeleteNodeMenuItem")
                    item.IsEnabled = !node.IsRoot;
            }
        }
    }

    private async void NewFile_Click(object sender, RoutedEventArgs e)
    {
        if (Vm?.SelectedItem == null) return;
        var prompt = new TextPromptWindow(L.T("New File"), L.T("Enter file name"));
        prompt.Owner = Window.GetWindow(this);
        if (prompt.ShowDialog() != true)
            return;
        if (!Vm.TryCreateFile(Vm.SelectedItem, prompt.ResponseText.Trim(), out _, out var error))
            MessageBox.Show(L.T(error), L.T("Explorer"), MessageBoxButton.OK, MessageBoxImage.Warning);
        await Vm.RefreshNodeAsync(Vm.SelectedItem.IsDirectory ? Vm.SelectedItem : Vm.SelectedItem.Parent);
    }

    private async void NewFolder_Click(object sender, RoutedEventArgs e)
    {
        if (Vm?.SelectedItem == null) return;
        var prompt = new TextPromptWindow(L.T("New Folder"), L.T("Enter folder name"));
        prompt.Owner = Window.GetWindow(this);
        if (prompt.ShowDialog() != true)
            return;
        if (!Vm.TryCreateFolder(Vm.SelectedItem, prompt.ResponseText.Trim(), out _, out var error))
            MessageBox.Show(L.T(error), L.T("Explorer"), MessageBoxButton.OK, MessageBoxImage.Warning);
        await Vm.RefreshNodeAsync(Vm.SelectedItem.IsDirectory ? Vm.SelectedItem : Vm.SelectedItem.Parent);
    }

    private async void RenameNode_Click(object sender, RoutedEventArgs e)
    {
        if (Vm?.SelectedItem == null) return;
        var prompt = new TextPromptWindow(L.T("Rename"), L.T("Enter new name"), Vm.SelectedItem.DisplayName);
        prompt.Owner = Window.GetWindow(this);
        if (prompt.ShowDialog() != true)
            return;
        if (!Vm.TryRename(Vm.SelectedItem, prompt.ResponseText.Trim(), out _, out var error))
            MessageBox.Show(L.T(error), L.T("Explorer"), MessageBoxButton.OK, MessageBoxImage.Warning);
        await Vm.RefreshNodeAsync(Vm.SelectedItem.Parent ?? Vm.SelectedItem);
    }

    private async void DeleteNode_Click(object sender, RoutedEventArgs e)
    {
        if (Vm?.SelectedItem == null) return;

        var msg = string.Format(L.T("Delete '{0}'?"), Vm.SelectedItem.DisplayName);
        if (MessageBox.Show(msg, L.T("Explorer"), MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        var parent = Vm.SelectedItem.Parent;
        if (!Vm.TryDelete(Vm.SelectedItem, out var error))
        {
            MessageBox.Show(L.T(error), L.T("Explorer"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        await Vm.RefreshNodeAsync(parent);
    }

    private void OpenInTerminal_Click(object sender, RoutedEventArgs e)
    {
        if (Vm?.SelectedItem == null)
            return;

        var path = Vm.SelectedItem.IsDirectory
            ? Vm.SelectedItem.FullPath
            : Path.GetDirectoryName(Vm.SelectedItem.FullPath) ?? Vm.SelectedItem.FullPath;

        OpenPathInTerminalRequested?.Invoke(path);
    }

    private void RevealInExplorer_Click(object sender, RoutedEventArgs e)
    {
        if (Vm?.SelectedItem == null)
            return;

        try
        {
            var path = Vm.SelectedItem.FullPath;
            if (Vm.SelectedItem.IsDirectory)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            else
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
        }
        catch
        {
            // no-op
        }
    }

    private void RemoveRoot_Click(object sender, RoutedEventArgs e)
    {
        if (Vm?.SelectedItem == null)
            return;
        Vm.RemoveRoot(Vm.SelectedItem);
    }

    private void ExplorerTree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(ExplorerTree);
    }

    private void ExplorerTree_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || Vm?.SelectedItem == null)
            return;

        var now = e.GetPosition(ExplorerTree);
        if (Math.Abs(now.X - _dragStart.X) < 4 && Math.Abs(now.Y - _dragStart.Y) < 4)
            return;

        var data = BuildDragDataObject(Vm.SelectedItem);
        DragDrop.DoDragDrop(ExplorerTree, data, DragDropEffects.Copy | DragDropEffects.Move);
    }

    private void ExplorerTree_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(ExplorerItemViewModel)))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private async void ExplorerTree_Drop(object sender, DragEventArgs e)
    {
        if (Vm == null || !e.Data.GetDataPresent(typeof(ExplorerItemViewModel)))
            return;

        var sourceNode = (ExplorerItemViewModel?)e.Data.GetData(typeof(ExplorerItemViewModel));
        if (sourceNode == null || sourceNode.IsPlaceholder)
            return;

        var targetNode = FindAncestor<TreeViewItem>((DependencyObject)e.OriginalSource)?.DataContext as ExplorerItemViewModel;
        if (targetNode == null || targetNode == sourceNode || targetNode.IsPlaceholder)
            return;

        if (sourceNode.IsRoot && targetNode.IsRoot)
        {
            Vm.MoveRootBefore(sourceNode, targetNode);
            return;
        }

        if (sourceNode.IsRoot)
            return;

        if (Vm.TryMove(sourceNode, targetNode, out _, out var error))
        {
            await Vm.RefreshNodeAsync(targetNode.IsDirectory ? targetNode : targetNode.Parent);
            await Vm.RefreshNodeAsync(sourceNode.Parent);
        }
        else
        {
            MessageBox.Show(L.T(error), L.T("Explorer"), MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T matched)
                return matched;
            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static DataObject BuildDragDataObject(ExplorerItemViewModel item)
    {
        var data = new DataObject();
        data.SetData(typeof(ExplorerItemViewModel), item);

        if (!item.IsPlaceholder && !string.IsNullOrWhiteSpace(item.FullPath))
        {
            var normalizedPath = Path.GetFullPath(item.FullPath);
            data.SetData(DataFormats.FileDrop, new[] { normalizedPath });
            data.SetData(DataFormats.UnicodeText, normalizedPath);
            data.SetData(DataFormats.Text, normalizedPath);
        }

        return data;
    }
}
