using GalgameManager.Enums;
using Microsoft.UI.Xaml.Data;

namespace GalgameManager.Helpers.Converter;

public class LanguageEnumToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return ("LanguageEnum_" + (LanguageEnum)value).GetLocalized();
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => LanguageEnum.Auto; // 不需要
}