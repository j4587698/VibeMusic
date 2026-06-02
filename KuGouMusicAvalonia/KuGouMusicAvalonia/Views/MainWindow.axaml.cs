using Avalonia.Controls;
using LuminaUI.Controls;
using LuminaUI.Services;
using KuGouMusicAvalonia.Services;

namespace KuGouMusicAvalonia.Views;

public partial class MainWindow : LuminaWindow
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private bool _isRealClosing;

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        if (_isRealClosing)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;

        if (!MusicService.HasPromptedMinimizeToTray)
        {
            var result = await LuminaDialogService.Instance.ShowConfirmAsync(
                this, 
                "关闭提示", 
                "你希望在关闭窗口时最小化到系统托盘吗？\n（选“是”则隐藏到托盘保持后台播放，选“否”则彻底退出）",
                "是 (最小化)", 
                "否 (彻底退出)");
            
            MusicService.MinimizeToTrayOnClose = result;
            MusicService.HasPromptedMinimizeToTray = true;
        }

        if (MusicService.MinimizeToTrayOnClose)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => this.Hide());
        }
        else
        {
            _isRealClosing = true;
            Avalonia.Threading.Dispatcher.UIThread.Post(() => this.Close());
        }
    }
}