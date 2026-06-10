using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
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

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Handled || e.Key != Key.Space || ShouldIgnoreSpaceShortcut(e.Source))
        {
            return;
        }

        PlayerService.Instance.TogglePlayPause();
        e.Handled = true;
    }

    private static bool ShouldIgnoreSpaceShortcut(object? source)
    {
        if (source is not Visual visual)
        {
            return false;
        }

        return IsInteractiveSpaceTarget(source)
            || visual.GetVisualAncestors().Any(IsInteractiveSpaceTarget);
    }

    private static bool IsInteractiveSpaceTarget(object? target)
    {
        return target is TextBox
            or Button
            or ToggleButton
            or RepeatButton
            or Slider
            or ComboBox;
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        // 1. 如果是真正的彻底退出阶段，直接交给基类处理并放行
        if (_isRealClosing)
        {
            base.OnClosing(e);
            return;
        }

        // 2. 拦截当前的关闭事件
        e.Cancel = true;

        // 3. 询问用户是否最小化到托盘（首次）
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

        // 4. 根据设置决定是隐藏还是彻底退出
        if (MusicService.MinimizeToTrayOnClose)
        {
            // 仅仅隐藏主窗口，保持托盘运行
            this.Hide();
        }
        else
        {
            // 彻底退出：标记变量并调用 Shutdown
            _isRealClosing = true;
            
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
            else
            {
                this.Close();
            }
        }
    }
}
