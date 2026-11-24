using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using PresentationWpf.ViewModels;

namespace PresentationWpf.Views;

public partial class ClearanceView : UserControl
{
    public ClearanceView() => InitializeComponent();
    private async void View_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ClearanceViewModel vm) await vm.InitializeAsync();
    }

    private void View_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ClearanceViewModel oldVm)
            PropertyChangedEventManager.RemoveHandler(oldVm, ViewModel_PropertyChanged, "");
        if (e.NewValue is ClearanceViewModel vm)
            PropertyChangedEventManager.AddHandler(vm, ViewModel_PropertyChanged, "");
        QueueSuggestionUpdate();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ClearanceViewModel.ArticleText) or nameof(ClearanceViewModel.ArticleSuggestion)
            or nameof(ClearanceViewModel.CanWork)) QueueSuggestionUpdate();
    }
    private void ArticleBox_TextChanged(object sender, TextChangedEventArgs e) => QueueSuggestionUpdate();
    private void ArticleBox_SelectionChanged(object sender, RoutedEventArgs e) => QueueSuggestionUpdate();
    private void ArticleBox_SizeChanged(object sender, SizeChangedEventArgs e) => QueueSuggestionUpdate();

    private void QueueSuggestionUpdate() => Dispatcher.InvokeAsync(UpdateSuggestionPosition, DispatcherPriority.Loaded);

    private void UpdateSuggestionPosition()
    {
        if (ArticleSuggestionText == null || ArticleBox == null) return;
        ArticleSuggestionText.Visibility = Visibility.Collapsed;
        if (DataContext is not ClearanceViewModel vm || !vm.CanWork ||
            string.IsNullOrEmpty(vm.ArticleSuggestionSuffix) || ArticleBox.SelectionLength != 0 ||
            ArticleBox.CaretIndex != ArticleBox.Text.Length || ArticleBox.Text != vm.ArticleText) return;
        // Use the actual caret rectangle to respect the current font, DPI, padding and horizontal scroll.
        var caret = ArticleBox.GetRectFromCharacterIndex(ArticleBox.Text.Length);
        if (caret.IsEmpty || caret.X < 0 || caret.X >= ArticleBox.ActualWidth - 4) return;
        ArticleSuggestionText.Margin = new Thickness(caret.X, caret.Y, 0, 0);
        ArticleSuggestionText.MaxWidth = Math.Max(0, ArticleBox.ActualWidth - caret.X - 4);
        ArticleSuggestionText.Visibility = Visibility.Visible;
    }

    private void ArticleBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Tab or Key.Right) || Keyboard.Modifiers != ModifierKeys.None ||
            ArticleBox.SelectionLength != 0 || ArticleBox.CaretIndex != ArticleBox.Text.Length ||
            DataContext is not ClearanceViewModel vm || !vm.CanWork || string.IsNullOrEmpty(vm.ArticleSuggestionSuffix)) return;
        var suggestion = vm.ArticleSuggestion;
        ArticleBox.SetCurrentValue(TextBox.TextProperty, suggestion);
        ArticleBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        ArticleBox.CaretIndex = ArticleBox.Text.Length;
        e.Handled = true;
        QueueSuggestionUpdate();
    }
}
