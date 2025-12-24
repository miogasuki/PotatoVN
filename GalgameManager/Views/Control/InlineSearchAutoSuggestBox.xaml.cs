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
    private const int AnimationDurationMs = 200;
    private const int FadeDurationMs = 150;
    private readonly ObservableCollection<string> _searchSuggestions = new();
    private DateTime _lastSearchTime = DateTime.Now;
    private Storyboard? _currentStoryboard;
    private bool _isExpanded;
    private Button? _clearButton;

    public InlineSearchAutoSuggestBox()
    {
        InitializeComponent();
        Loaded += InlineSearchAutoSuggestBox_Loaded;
    }

    public ObservableCollection<string> SearchSuggestions => _searchSuggestions;

    private void InlineSearchAutoSuggestBox_Loaded(object sender, RoutedEventArgs e)
    {
        SearchLabelText.Text = "Search".GetLocalized();
        SearchButton.Opacity = 1;
        SearchBoxHost.Opacity = 0;
        SearchBox.Width = 0;
        SearchBoxHost.IsHitTestVisible = false;
        VisualStateManager.GoToState(SearchButton, "LabelOnRight", false);
    }

    private void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        Expand();
    }

    private void Expand()
    {
        if (_isExpanded)
        {
            return;
        }

        _isExpanded = true;
        SearchButton.IsHitTestVisible = false;
        SearchBoxHost.IsHitTestVisible = true;
        Animate(SearchBox.Width, ExpandedWidth, SearchButton.Opacity, 0, SearchBoxHost.Opacity, 1);
        SearchBox.Focus(FocusState.Programmatic);
    }

    private void Collapse()
    {
        if (!_isExpanded)
        {
            return;
        }

        _isExpanded = false;
        SearchBox.IsSuggestionListOpen = false;
        SearchBoxHost.IsHitTestVisible = false;
        SearchButton.IsHitTestVisible = true;
        Animate(SearchBox.Width, 0, SearchButton.Opacity, 1, SearchBoxHost.Opacity, 0);
    }

    private void SearchBox_OnLoaded(object sender, RoutedEventArgs e)
    {
        AttachClearButton();
    }

    private void SearchBox_OnUnloaded(object sender, RoutedEventArgs e)
    {
        DetachClearButton();
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
        if (!_isExpanded)
        {
            return;
        }

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
        if (_clearButton == null)
        {
            return;
        }

        _clearButton.Visibility = Visibility.Visible;
        _clearButton.Opacity = 1;
        _clearButton.IsHitTestVisible = true;
    }

    private void ScheduleClearButtonVisible()
    {
        if (_clearButton == null)
        {
            return;
        }

        _ = DispatcherQueue.TryEnqueue(EnsureClearButtonVisible);
    }

    private void Animate(double fromWidth, double toWidth, double fromButtonOpacity, double toButtonOpacity, double fromBoxOpacity, double toBoxOpacity)
    {
        _currentStoryboard?.Stop();

        var storyboard = new Storyboard();

        var widthAnimation = new DoubleAnimation
        {
            From = fromWidth,
            To = toWidth,
            Duration = new Duration(TimeSpan.FromMilliseconds(AnimationDurationMs)),
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(widthAnimation, SearchBox);
        Storyboard.SetTargetProperty(widthAnimation, "Width");
        storyboard.Children.Add(widthAnimation);

        var buttonAnimation = new DoubleAnimation
        {
            From = fromButtonOpacity,
            To = toButtonOpacity,
            Duration = new Duration(TimeSpan.FromMilliseconds(FadeDurationMs))
        };
        Storyboard.SetTarget(buttonAnimation, SearchButton);
        Storyboard.SetTargetProperty(buttonAnimation, "Opacity");
        storyboard.Children.Add(buttonAnimation);

        var boxAnimation = new DoubleAnimation
        {
            From = fromBoxOpacity,
            To = toBoxOpacity,
            Duration = new Duration(TimeSpan.FromMilliseconds(FadeDurationMs))
        };
        Storyboard.SetTarget(boxAnimation, SearchBoxHost);
        Storyboard.SetTargetProperty(boxAnimation, "Opacity");
        storyboard.Children.Add(boxAnimation);

        _currentStoryboard = storyboard;
        storyboard.Begin();
    }

    private static T? FindDescendant<T>(DependencyObject root, string name) where T : FrameworkElement
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T element && element.Name == name)
            {
                return element;
            }

            var found = FindDescendant<T>(child, name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private async void AutoSuggestBox_OnTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        ScheduleClearButtonVisible();

        if (string.IsNullOrEmpty(SearchKey))
        {
            SearchCommand?.Execute(SearchKey);
            SearchSuggestions.Clear();
            return;
        }

        _ = Task.Run((async Task () =>
        {
            _lastSearchTime = DateTime.Now;
            DateTime tmp = _lastSearchTime;
            await Task.Delay(SearchDelay);
            if (tmp == _lastSearchTime)
            {
                await UiThreadInvokeHelper.InvokeAsync(() =>
                {
                    SearchCommand?.Execute(SearchKey);
                });
            }
        })!);

        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
        {
            return;
        }

        SearchSuggestions.Clear();

        if (SearchKey == string.Empty)
        {
            return;
        }

        if (SearchSuggestionsProvider != null &&
            await SearchSuggestionsProvider.GetSearchSuggestionsAsync(SearchKey) is { } result)
        {
            foreach (var suggestion in result)
            {
                SearchSuggestions.Add(suggestion);
            }
        }
    }

    private void AutoSuggestBox_OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion != null)
        {
            SearchKey = args.ChosenSuggestion.ToString();
        }
        else
        {
            SearchKey = args.QueryText;
        }

        if (string.IsNullOrEmpty(SearchKey))
        {
            return;
        }

        SearchCommand?.Execute(SearchKey);
        SearchSubmitCommand?.Execute(SearchKey);
    }
}
