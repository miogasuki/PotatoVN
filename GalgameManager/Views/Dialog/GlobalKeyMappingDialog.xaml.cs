
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Models;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.WinUI;

namespace GalgameManager.Views.Dialog;

public sealed partial class GlobalKeyMappingDialog : ContentDialog, INotifyPropertyChanged
{
    private ObservableCollection<KeyMapping> _mappings = null!;
    private bool _hasMappings;

    public ObservableCollection<KeyMapping> Mappings
    {
        get => _mappings;
        private set
        {
            _mappings = value;
            OnPropertyChanged();
        }
    }

    public bool HasMappings
    {
        get => _hasMappings;
        private set
        {
            if (_hasMappings == value) return;
            _hasMappings = value;
            OnPropertyChanged();
        }
    }

    public List<KeyMapping> ResultMappings => Mappings.ToList();

    public GlobalKeyMappingDialog(IEnumerable<KeyMapping> mappings)
    {
        InitializeComponent();

        // 设置对话框基本信息
        RequestedTheme = App.MainWindow?.Content is Microsoft.UI.Xaml.FrameworkElement element ? element.RequestedTheme : RequestedTheme;
        XamlRoot = App.MainWindow!.Content.XamlRoot;
        DefaultButton = ContentDialogButton.Primary;
        Title = "GlobalKeyMappingDialog_Title".GetLocalized();
        PrimaryButtonText = "GlobalKeyMappingDialog_Button_Save".GetLocalized();
        SecondaryButtonText = "GlobalKeyMappingDialog_Button_Clear".GetLocalized();
        CloseButtonText = "GlobalKeyMappingDialog_Button_Cancel".GetLocalized();

        Mappings = new ObservableCollection<KeyMapping>();
        foreach (var mapping in mappings)
        {
            Mappings.Add(new KeyMapping
            {
                Remark = mapping.Remark,
                From = new List<int>(mapping.From),
                IsEnabled = mapping.IsEnabled,
                IsGlobal = mapping.IsGlobal
            });
        }

        UpdateHasMappings();
        Mappings.CollectionChanged += (_, _) => UpdateHasMappings();

        // 清空按钮逻辑：直接清空并返回Secondary结果，让ViewModel处理保存
        SecondaryButtonClick += (_, _) => Mappings.Clear();
    }

    private void UpdateHasMappings()
    {
        HasMappings = Mappings.Count > 0;
    }

    [RelayCommand]
    private void AddMapping()
    {
        Mappings.Add(new KeyMapping { IsGlobal = true });
    }

    private void RemoveMapping(KeyMapping? mapping)
    {
        if (mapping != null)
        {
            Mappings.Remove(mapping);
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is KeyMapping mapping)
        {
            RemoveMapping(mapping);
        }
    }
    
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
