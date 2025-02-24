using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using DependencyPropertyGenerator;
using GalgameManager.Models;

namespace GalgameManager.Views.GalgamePagePanel;

[DependencyProperty<Galgame>("Game")]
[ContentProperty(Name = "Content")]
public partial class GamePanelBase : UserControl
{
    partial void OnGameChanged()
    {
        Update();
    }

    /// <summary>
    /// Galgame类中并非所有字段都是自带更新提醒的，部分更新由GalgameViewModel手动告知各个panel，
    /// 若本panel使用的字段全是自动更新的，可忽略这个函数。
    /// </summary>
    protected virtual void Update() { }
}
