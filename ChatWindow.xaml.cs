using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MutsumiPet.Models;
using MutsumiPet.Services;

namespace MutsumiPet;

public partial class ChatWindow : Window
{
    private readonly ChatConversationService _chatConversationService;
    private readonly CancellationToken _shutdownToken;
    private IReadOnlyList<ChatCommandSuggestion> _visibleSuggestions = Array.Empty<ChatCommandSuggestion>();

    /// <summary>
    /// 初始化浮动输入面板。
    /// </summary>
    public ChatWindow(ChatConversationService chatConversationService, CancellationToken shutdownToken)
    {
        InitializeComponent();
        _chatConversationService = chatConversationService;
        _shutdownToken = shutdownToken;
    }

    /// <summary>
    /// 用户提交输入时触发。
    /// </summary>
    public event EventHandler<string>? InputSubmitted;

    /// <summary>
    /// 窗口加载后定位到桌宠下方并聚焦输入框。
    /// </summary>
    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        PositionNearOwner();
        InputTextBox.Focus();
    }

    /// <summary>
    /// 面板失焦时隐藏窗口，避免长期遮挡桌面。
    /// </summary>
    private void Window_Deactivated(object? sender, EventArgs e)
    {
        Hide();
    }

    /// <summary>
    /// 发送按钮触发当前输入。
    /// </summary>
    private void SendButton_Click(object sender, RoutedEventArgs e)
    {
        SubmitCurrentInput();
    }

    /// <summary>
    /// 输入变化时刷新命令补全列表。
    /// </summary>
    private void InputTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshCompletions();
    }

    /// <summary>
    /// 处理键盘发送、换行和补全选择。
    /// </summary>
    private void InputTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (CompletionBorder.Visibility == Visibility.Visible &&
            (e.Key == Key.Up || e.Key == Key.Down))
        {
            MoveCompletionSelection(e.Key == Key.Down ? 1 : -1);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            return;
        }

        if (e.Key is Key.Enter or Key.Return)
        {
            e.Handled = true;
            SubmitCurrentInput();
        }
    }

    /// <summary>
    /// 单击补全项时提交该命令。
    /// </summary>
    private void CompletionListBox_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var item = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
        if (item?.DataContext is not ChatCommandSuggestion suggestion)
        {
            return;
        }

        CompletionListBox.SelectedItem = suggestion;
        SubmitSelectedCompletion();
    }

    /// <summary>
    /// 更新对话窗口标题和占位文字以匹配当前人格。
    /// </summary>
    public void UpdatePersona(string displayName)
    {
        Title = $"与{displayName}聊天";
        PlaceholderTextBlock.Text = $"和{displayName}说点什么吧";
    }

    /// <summary>
    /// 打开或重新激活输入面板。
    /// </summary>
    public new void Activate()
    {
        if (!IsVisible)
        {
            Show();
        }

        PositionNearOwner();
        base.Activate();
        InputTextBox.Focus();
    }

    /// <summary>
    /// 提交当前输入并隐藏面板。
    /// </summary>
    private void SubmitCurrentInput()
    {
        if (CompletionBorder.Visibility == Visibility.Visible &&
            CompletionListBox.SelectedItem is ChatCommandSuggestion suggestion &&
            ShouldUseSelectedCompletion(InputTextBox.Text))
        {
            InputTextBox.Text = suggestion.CommandText;
        }

        var text = InputTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        InputTextBox.Clear();
        HideCompletions();
        Hide();
        InputSubmitted?.Invoke(this, text);
    }

    /// <summary>
    /// 提交当前选中的补全命令。
    /// </summary>
    private void SubmitSelectedCompletion()
    {
        if (CompletionListBox.SelectedItem is not ChatCommandSuggestion suggestion)
        {
            return;
        }

        InputTextBox.Text = suggestion.CommandText;
        SubmitCurrentInput();
    }

    /// <summary>
    /// 根据当前输入刷新补全候选。
    /// </summary>
    private void RefreshCompletions()
    {
        var text = InputTextBox.Text.Trim();
        if (!text.StartsWith("/", StringComparison.Ordinal))
        {
            HideCompletions();
            return;
        }

        _visibleSuggestions = _chatConversationService.GetAvailableCommands()
            .Where(command => command.CommandText.StartsWith(text, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (_visibleSuggestions.Count == 0)
        {
            HideCompletions();
            return;
        }

        CompletionListBox.ItemsSource = _visibleSuggestions;
        CompletionListBox.SelectedIndex = 0;
        CompletionBorder.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// 隐藏补全列表。
    /// </summary>
    private void HideCompletions()
    {
        CompletionBorder.Visibility = Visibility.Collapsed;
        CompletionListBox.ItemsSource = null;
        _visibleSuggestions = Array.Empty<ChatCommandSuggestion>();
    }

    /// <summary>
    /// 移动补全列表中的当前选择。
    /// </summary>
    private void MoveCompletionSelection(int delta)
    {
        if (_visibleSuggestions.Count == 0)
        {
            return;
        }

        var nextIndex = CompletionListBox.SelectedIndex + delta;
        if (nextIndex < 0)
        {
            nextIndex = _visibleSuggestions.Count - 1;
        }
        else if (nextIndex >= _visibleSuggestions.Count)
        {
            nextIndex = 0;
        }

        CompletionListBox.SelectedIndex = nextIndex;
        CompletionListBox.ScrollIntoView(CompletionListBox.SelectedItem);
    }

    /// <summary>
    /// 判断 Enter 时是否应使用当前选中的补全项。
    /// </summary>
    private static bool ShouldUseSelectedCompletion(string input)
    {
        var text = input.Trim();
        return text == "/" || !text.Contains(' ', StringComparison.Ordinal);
    }

    /// <summary>
    /// 从事件源向上查找指定类型的父元素。
    /// </summary>
    private static T? FindAncestor<T>(DependencyObject? current)
        where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T typed)
            {
                return typed;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    /// <summary>
    /// 将输入面板定位到桌宠窗口下方。
    /// </summary>
    private void PositionNearOwner()
    {
        if (Owner is null)
        {
            return;
        }

        var workArea = SystemParameters.WorkArea;
        var panelWidth = ActualWidth > 0 ? ActualWidth : Width;
        var panelHeight = ActualHeight > 0 ? ActualHeight : 148;
        var desiredLeft = Owner.Left + (Owner.Width - panelWidth) / 2;
        var desiredTop = Owner.Top + Owner.Height + 10;

        Left = Math.Clamp(desiredLeft, workArea.Left + 8, workArea.Right - panelWidth - 8);
        Top = desiredTop + panelHeight <= workArea.Bottom - 8
            ? desiredTop
            : Math.Max(workArea.Top + 8, Owner.Top - panelHeight - 10);
    }
}
