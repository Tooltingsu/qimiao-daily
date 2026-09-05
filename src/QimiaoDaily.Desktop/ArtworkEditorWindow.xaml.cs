using System.Windows;
using QimiaoDaily.Desktop.ViewModels;

namespace QimiaoDaily.Desktop;

public partial class ArtworkEditorWindow : Window
{
    private readonly ShellViewModel _viewModel;

    public ArtworkEditorWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.SaveArtworkEditCommand.ExecuteAsync(null);
        if (_viewModel.ImportMessage.StartsWith("美图已保存", StringComparison.Ordinal)
            || _viewModel.ImportMessage.StartsWith("美图没有字段变化", StringComparison.Ordinal))
            DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
