using System;

namespace CyberPatriot
{
    public static class Utilities
    {
        public static bool TryParseEnumSpaceless<TEnum>(string value, out TEnum @enum) where TEnum : struct => Enum.TryParse<TEnum>(value.Replace(" ", string.Empty), out @enum);
    }
}
