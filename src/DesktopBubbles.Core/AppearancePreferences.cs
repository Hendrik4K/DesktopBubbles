namespace DesktopBubbles.Core;

public static class AppearancePreferences
{
    public static string Color(BubbleSettings settings, BubbleGroup? group)
        => settings.LinkBubbleColors ? settings.BubbleColor : group?.Color ?? settings.BubbleColor;

    public static void SetColor(BubbleState state, BubbleGroup? group, string color)
    {
        color = ColorValue.Normalize(color);
        if (state.Settings.LinkBubbleColors || group is null)
        {
            state.Settings.BubbleColor = state.Settings.WindowColor = color;
            if (state.Settings.LinkBubbleColors) foreach (var item in state.Groups) item.Color = item.WindowColor = null;
        }
        else group.Color = group.WindowColor = color;
    }

    public static void LinkColors(BubbleState state, BubbleGroup? selected, bool enabled)
    {
        string current = Color(state.Settings, selected);
        state.Settings.LinkBubbleColors = enabled;
        if (enabled) SetColor(state, selected, current);
    }

    public static void ResetColors(BubbleState state)
    {
        var defaults = new BubbleSettings();
        state.Settings.AppColor = defaults.AppColor;
        state.Settings.LightModeColor = state.Settings.DarkModeColor = null;
        state.Settings.BubbleColor = state.Settings.WindowColor = defaults.BubbleColor;
        foreach (var group in state.Groups) group.Color = group.WindowColor = null;
    }
}
