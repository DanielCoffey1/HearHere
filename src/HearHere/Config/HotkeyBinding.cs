using System.Text.Json.Serialization;
using System.Windows.Input;

namespace HearHere.Config;

public sealed class HotkeyBinding
{
    public ModifierKeys Modifiers { get; set; }
    public Key Key { get; set; }

    [JsonIgnore]
    public string DisplayString
    {
        get
        {
            var parts = new List<string>();
            if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            if (Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
            if (Key != Key.None) parts.Add(Key.ToString());
            return string.Join(" + ", parts);
        }
    }

    public bool IsEmpty => Key == Key.None;
}
