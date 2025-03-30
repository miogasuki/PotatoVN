using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace GalgameManager.Helpers.Converter;

public class ElementThemeToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is ElementTheme theme)
            return ("ElementTheme_" + theme).GetLocalized();
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is string str)
            return Enum.Parse(typeof(ElementTheme), str.Replace("ElementTheme_", ""), true);
        if (value is ElementTheme theme)
            return theme;
        return value;
    }
}