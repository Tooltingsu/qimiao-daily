using System.Windows;
using QimiaoDaily.Desktop.ViewModels;

namespace QimiaoDaily.Desktop;

public partial class ImportEditorWindow : Window
{
    private readonly ShellViewModel _viewModel;

    public ImportEditorWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private async void Preview_Click(object sender, RoutedEventArgs e)
        => await _viewModel.ImportCalendarDataCommand.ExecuteAsync(null);

    private async void Confirm_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.ConfirmImportCommand.ExecuteAsync(null);
        if (_viewModel.ImportMessage.StartsWith("已确认导入", StringComparison.Ordinal))
            DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
