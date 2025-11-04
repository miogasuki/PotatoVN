
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.Input;
using GalgameManager.Models;
using Microsoft.UI.Xaml.Controls;

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

    [RelayCommand]
    private void RemoveMapping(KeyMapping? mapping)
    {
        if (mapping != null)
        {
            Mappings.Remove(mapping);
        }
    }
    
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
