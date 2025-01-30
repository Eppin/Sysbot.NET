using System;
using System.ComponentModel;
using System.Globalization;

namespace SysBot.Pokemon;

public class TargetFlawlessIVsConverter(Type type) : EnumConverter(type)
{
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (value == null) return base.ConvertTo(context, culture, value, destinationType);

        var name = Enum.GetName(type, value);
        if (string.IsNullOrWhiteSpace(name))
            return value.ToString();

        var fieldInfo = type.GetField(name);
        if (fieldInfo == null)
            return value.ToString();

        return Attribute.GetCustomAttribute(fieldInfo, typeof(DescriptionAttribute)) is DescriptionAttribute dna
            ? dna.Description
            : value.ToString();
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        foreach (var fieldInfo in type.GetFields())
        {
            if (Attribute.GetCustomAttribute(fieldInfo, typeof(DescriptionAttribute)) is DescriptionAttribute dna && (string)value == dna.Description)
                return Enum.Parse(type, fieldInfo.Name);
        }

        return Enum.Parse(type, (string)value);
    }
}
