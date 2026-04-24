using System.Windows;

namespace MyApp;

public partial class MainWindow : Window
{
    private readonly UpdateService _updateService = new();

    public MainWindow()
    {
        InitializeComponent();
        VersionLabel.Text = $"MyApp v{UpdateService.CurrentVersion}";
        Loaded += async (_, _) => await CheckUpdateSilent();
    }

    private async Task CheckUpdateSilent()
    {
        try
        {
            var info = await _updateService.CheckForUpdateAsync();
            if (info != null)
                new UpdateDialog(info) { Owner = this }.ShowDialog();
        }
        catch { /* 네트워크 오류 등 무시 */ }
    }

    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var info = await _updateService.CheckForUpdateAsync();
            if (info == null)
            {
                MessageBox.Show("최신 버전입니다.", "업데이트", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            new UpdateDialog(info) { Owner = this }.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"업데이트 확인 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
