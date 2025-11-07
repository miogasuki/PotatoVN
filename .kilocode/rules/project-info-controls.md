# 自定义控件介绍

本文档介绍了应用中使用的各种自定义控件，包括其功能、属性和使用方法。

---

## Setting

一个左侧有上下两行描述（一行Title，一行Description），右侧允许放置一个自定义控件的设置项控件。

**控件属性：**

* `Title` (string): 设置的标题。
* `Description` (string): 设置的描述。
* `Content` (UIElement): 放置在右侧的自定义控件。

**使用样例：**
请注意，样例中的 `x:Uid` 用于国际化，会自动设置 `Title` 属性。`Content` 区域被放置了一个 `NumberBox`。

```xml
<control:Setting x:Uid="SettingsPage_Other_AutoExport_Interval"  
                 Description="{x:Bind ViewModel.AutoExportIntervalDescription, Mode=OneWay}"  
                 Visibility="{x:Bind ViewModel.IsAutoExportEnabled, Mode=OneWay}">  
    <NumberBox Value="{x:Bind ViewModel.AutoExportInterval, Mode=TwoWay}"  
               SpinButtonPlacementMode="Compact"  
               SmallChange="1" LargeChange="24" Minimum="1"  
               Width="100"/>  
</control:Setting>
```

---

## SettingToggleSwitch

一个左侧有标题和描述，右侧固定为一个 `ToggleSwitch` 的快捷设置项控件。

**控件属性：**

* `Title` (string): 设置的标题。
* `Description` (string): 设置的描述。
* `IsOn` (bool): 绑定到右侧 `ToggleSwitch` 的布尔值。

**使用样例：**
此控件用于简单的布尔值开关设置。

```xml
<views:SettingToggleSwitch x:Uid="SettingsPage_Theme"
                             Description="切换应用的主题"
                             IsOn="{x:Bind ViewModel.IsLightThemeEnabled, Mode=TwoWay}" />
```

---

## ComboBoxWithI18N

一个内置了国际化支持的 `ComboBox` 控件。当 `ItemsSource` 为枚举类型时，它会自动使用 `EnumToStringConverter` 来显示本地化的字符串。

**控件属性：**

* `ItemsSource` (object): 控件的数据源。
* `SelectedItem` (object): 当前选中的项。
* `ItemTemplate` (DataTemplate): 可选，用于自定义下拉列表中项目的显示模板。

**控件事件：**

* `SelectedItemChangedEvent`: 当选中项发生变化时触发。

**使用样例：**
该控件简化了在 `ComboBox` 中展示枚举类型或其他需要本地化显示的数据的流程。

```xml
<control:ComboBoxWithI18N ItemsSource="{x:Bind ViewModel.AvailableSortOptions}"
                          SelectedItem="{x:Bind ViewModel.CurrentSortOption, Mode=TwoWay}" />
```

---

## Panel

一个带有预设样式的容器控件，提供圆角、背景色和边框，用于包裹其他UI元素，使其风格统一。

**控件属性：**

* `Content` (UIElement): 放置在容器内部的UI元素。

**使用样例：**
`Panel` 内可以包含任意UI元素，通常用于将一组相关的控件组织在一起。

```xml
<control:Panel>
    <StackPanel Spacing="10">
        <TextBlock Text="这是一个在Panel内部的标题" Style="{ThemeResource SubtitleTextBlockStyle}"/>
        <Button Content="确认"/>
    </StackPanel>
</control:Panel>
```
