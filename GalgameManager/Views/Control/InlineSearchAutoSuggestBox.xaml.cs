using System.Collections.ObjectModel;
using System.Windows.Input;
using DependencyPropertyGenerator;
using GalgameManager.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace GalgameManager.Views.Control;

[DependencyProperty<string>("SearchKey")]
[DependencyProperty<ICommand>("SearchCommand")]
[DependencyProperty<ICommand>("SearchSubmitCommand")]
[DependencyProperty<ISearchSuggestionsProvider>("SearchSuggestionsProvider")]
[DependencyProperty<string>("PlaceholderText")]
[DependencyProperty<double>("ExpandedWidth", DefaultValue = 250d)]
public sealed partial class InlineSearchAutoSuggestBox : UserControl
{
    private const int SearchDelay = 500;
    private readonly ObservableCollection<string> _searchSuggestions = new();
    private bool _isExpanded;
    private Button? _clearButton;
    private CancellationTokenSource? _searchCts;
    private bool _isInitialized;

    public InlineSearchAutoSuggestBox()
    {
        InitializeComponent();
        Loaded += InlineSearchAutoSuggestBox_Loaded;
    }

    public ObservableCollection<string> SearchSuggestions => _searchSuggestions;

    private void InlineSearchAutoSuggestBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isInitialized) return;
        _isInitialized = true;

        SearchLabelText.Text = "Search".GetLocalized();
        // Initial state
        SearchButton.Opacity = 1;
        SearchButton.IsHitTestVisible = true;
        SearchBoxHost.Opacity = 0;
        SearchBoxHost.IsHitTestVisible = false;
        SearchBox.Width = 0;
    }

    private void SearchButton_Click(object sender, RoutedEventArgs e) => Expand();

    private void Expand()
    {
        if (_isExpanded) return;

        _isExpanded = true;

        // Update HitTest state
        SearchButton.IsHitTestVisible = false;
        SearchBoxHost.IsHitTestVisible = true;

        // Implicit Animations handle Opacity
        SearchButton.Opacity = 0;
        SearchBoxHost.Opacity = 1;

        // Manually animate Width (Dependent Animation)
        AnimateWidth(ExpandedWidth);

        SearchBox.Focus(FocusState.Programmatic);
    }

    private void Collapse()
    {
        if (!_isExpanded) return;

        _isExpanded = false;
        SearchBox.IsSuggestionListOpen = false;

        // Update HitTest state
        SearchBoxHost.IsHitTestVisible = false;
        SearchButton.IsHitTestVisible = true;

        // Implicit Animations handle Opacity
        SearchButton.Opacity = 1;
        SearchBoxHost.Opacity = 0;

        // Manually animate Width
        AnimateWidth(0);
    }

    private void SearchBox_OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachClearButton();
        SearchBox.LostFocus += SearchBox_LostFocus;
    }

    private void SearchBox_OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachClearButton();
        SearchBox.LostFocus -= SearchBox_LostFocus;
    }

    private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
    {
        // 如果关键词为空，失去焦点时自动收起
        if (string.IsNullOrEmpty(SearchKey))
        {
            Collapse();
        }
    }

    private void AttachClearButton()
    {
        DetachClearButton();
        _clearButton = FindDescendant<Button>(SearchBox, "DeleteButton")
                       ?? FindDescendant<Button>(SearchBox, "ClearButton");

        if (_clearButton != null)
        {
            _clearButton.Click += ClearButton_Click;
            EnsureClearButtonVisible();
        }
    }

    private void DetachClearButton()
    {
        if (_clearButton != null)
        {
            _clearButton.Click -= ClearButton_Click;
            _clearButton = null;
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isExpanded) return;

        if (!string.IsNullOrEmpty(SearchKey))
        {
            SearchKey = string.Empty;
            SearchBox.Focus(FocusState.Programmatic);
            EnsureClearButtonVisible();
            return;
        }

        Collapse();
    }

    private void EnsureClearButtonVisible()
    {
        if (_clearButton == null) return;
        _clearButton.Visibility = Visibility.Visible;
        _clearButton.Opacity = 1;
        _clearButton.IsHitTestVisible = true;
    }

    private void ScheduleClearButtonVisible() => DispatcherQueue.TryEnqueue(EnsureClearButtonVisible);

    private void AnimateWidth(double toWidth)
    {
        var animation = new DoubleAnimation
        {
            From = SearchBox.Width,
            To = toWidth,
            Duration = new Duration(TimeSpan.FromSeconds(0.2)),
            EnableDependentAnimation = true,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        Storyboard.SetTarget(animation, SearchBox);
        Storyboard.SetTargetProperty(animation, "Width");

        var sb = new Storyboard();
        sb.Children.Add(animation);
        sb.Begin();
    }

    private static T? FindDescendant<T>(DependencyObject root, string name) where T : FrameworkElement
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T element && element.Name == name) return element;
            var found = FindDescendant<T>(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private async void AutoSuggestBox_OnTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        ScheduleClearButtonVisible();

        // 1. Logic consistent with SearchAutoSuggestBox: Handle Empty Key immediately
        if (string.IsNullOrEmpty(SearchKey))
        {
            _searchCts?.Cancel();
            SearchCommand?.Execute(SearchKey);
            SearchSuggestions.Clear();
            return;
        }

        // 2. Logic consistent with SearchAutoSuggestBox: Debounce Search Command
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        // Start the debounce timer (Fire and forget, similar to Task.Run in original)
        _ = DebounceSearchCommand(token);

        // 3. Logic consistent with SearchAutoSuggestBox: Fetch Suggestions Immediately
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            SearchSuggestions.Clear();
            if (SearchSuggestionsProvider != null)
            {
                var result = await SearchSuggestionsProvider.GetSearchSuggestionsAsync(SearchKey);
                // Check token to avoid race conditions (Optimization: Don't show stale results)
                if (result != null && !token.IsCancellationRequested)
                {
                    foreach (var suggestion in result)
                    {
                        SearchSuggestions.Add(suggestion);
                    }
                }
            }
        }
    }

    private async Task DebounceSearchCommand(CancellationToken token)
    {
        try
        {
            await Task.Delay(SearchDelay, token);
            if (!token.IsCancellationRequested)
            {
                SearchCommand?.Execute(SearchKey);
            }
        }
        catch (TaskCanceledException)
        {
            // Ignore cancellation
        }
    }

    private void AutoSuggestBox_OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        _searchCts?.Cancel(); // Cancel any pending search

        SearchKey = args.ChosenSuggestion?.ToString() ?? args.QueryText;

        if (string.IsNullOrEmpty(SearchKey)) return;

        SearchCommand?.Execute(SearchKey);
        SearchSubmitCommand?.Execute(SearchKey);
    }
}
