using System.Windows;

namespace MyApp;

public partial class UpdateDialog : Window
{
    private readonly UpdateInfo _info;
    private readonly UpdateService _service = new();

    public UpdateDialog(UpdateInfo info)
    {
        InitializeComponent();
        _info = info;

        VersionText.Text = $"{UpdateService.CurrentVersion} → {info.Version}";
        ChangelogText.Text = string.IsNullOrEmpty(info.Changelog) ? "(변경 내용 없음)" : info.Changelog;

        if (info.Mandatory)
        {
            SkipBtn.IsEnabled = false;
            TitleText.Text = "필수 업데이트가 있습니다";
        }
    }

    private async void UpdateBtn_Click(object sender, RoutedEventArgs e)
    {
        UpdateBtn.IsEnabled = false;
        SkipBtn.IsEnabled = false;
        ProgressBar.Visibility = Visibility.Visible;

        try
        {
            StatusText.Text = "다운로드 중...";
            var progress = new Progress<double>(p =>
            {
                ProgressBar.Value = p;
                StatusText.Text = $"다운로드 중... {p:F0}%";
            });

            await _service.DownloadUpdateAsync(_info, progress);

            StatusText.Text = "파일 검증 중...";
            if (!UpdateService.VerifyHash(_info))
            {
                MessageBox.Show("파일 검증에 실패했습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "검증 실패";
                UpdateBtn.IsEnabled = true;
                SkipBtn.IsEnabled = !_info.Mandatory;
                return;
            }

            StatusText.Text = "업데이트 적용 중...";
            UpdateService.ApplyUpdate(_info);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"업데이트 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "업데이트 실패";
            UpdateBtn.IsEnabled = true;
            SkipBtn.IsEnabled = !_info.Mandatory;
        }
    }

    private void SkipBtn_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
