using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GalgameManager.Models;

public class KeyMapping : ObservableObject
{
    private List<int> _from = new();
    public List<int> From
    {
        get => _from;
        set => SetProperty(ref _from, value);
    }

    private List<int> _to = new();
    public List<int> To
    {
        get => _to;
        set => SetProperty(ref _to, value);
    }

    private string _remark = string.Empty;
    public string Remark
    {
        get => _remark;
        set => SetProperty(ref _remark, value);
    }

    private bool _isEnabled = true;
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    private bool _isGlobal;
    public bool IsGlobal
    {
        get => _isGlobal;
        set => SetProperty(ref _isGlobal, value);
    }
}