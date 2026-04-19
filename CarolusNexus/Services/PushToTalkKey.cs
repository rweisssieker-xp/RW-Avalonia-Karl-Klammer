using System;
using System.Globalization;

namespace CarolusNexus.Services;

/// <summary>Mappt <c>PUSH_TO_TALK_KEY</c> aus .env auf einen Win32-VK (GetAsyncKeyState).</summary>
public static class PushToTalkKey
{
    /// <summary>VK_F8</summary>
    public const int DefaultVirtualKey = 0x77;

    public static int ResolveVirtualKey(string? envValue)
    {
        if (string.IsNullOrWhiteSpace(envValue))
            return DefaultVirtualKey;

        var t = envValue.Trim();
        if (t.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(t.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var vkHex)
            && vkHex is > 0 and < 256)
            return vkHex;

        if (int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var vkDec)
            && vkDec is > 0 and < 256)
            return vkDec;

        if (t.Length == 2 && t[0] == 'F' && char.IsDigit(t[1]))
        {
            var n = t[1] - '0';
            if (n is >= 1 and <= 9)
                return 0x70 + (n - 1); // F1..F9
        }

        if (t.Length == 3 && t.StartsWith("F1", StringComparison.OrdinalIgnoreCase) && char.IsDigit(t[2]))
        {
            var n = (t[2] - '0') + 10; // F10=0x79
            if (n is >= 10 and <= 12)
                return 0x70 + (n - 1);
        }

        if (t.Equals("F11", StringComparison.OrdinalIgnoreCase))
            return 0x7A;
        if (t.Equals("F12", StringComparison.OrdinalIgnoreCase))
            return 0x7B;

        if (t.Length >= 2
            && t[0] == 'F'
            && int.TryParse(t.AsSpan(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var fn)
            && fn is >= 13 and <= 24)
            return 0x7C + (fn - 13); // F13..F24

        return DefaultVirtualKey;
    }
}
