using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace AvantGarde.Utils;

public static class GuiUtilities
{
    public static Vector2 IconSize => new(ImGui.GetTextLineHeight() * 2f);

    // 每一列有兩行：第一行是道具名稱，第二行是「哪裡拿」（NPC 商人＋地名＋「前往」）。
    // 寬度從原本的 30 個字加到 48 個字就是為了讓第二行放得下——放不下的部分兩行都會
    // 縮成「…」並把完整內容留在 tooltip 裡，所以再窄也不會顯示錯的東西，只是看不到細節。
    public static Vector2 SlotWindowSize => new(ImGui.CalcTextSize("A").X * 48f, (IconSize.Y + ImGui.GetStyle().ItemSpacing.Y) * 6f);

    /// <summary>
    /// 把文字裁到指定寬度以內並補上「…」。
    /// </summary>
    /// <remarks>
    /// 🔴 用二分搜尋而不是逐字量：每一列每一幀都會走這裡，逐字量在長名稱上是 O(n) 次
    /// <c>CalcTextSize</c>。放得下時直接原樣回傳，連二分都不做。
    /// ⚠️ 切點若落在代理對（surrogate pair）中間會產生半個字元，所以切完往回退一格。
    /// </remarks>
    public static string Ellipsize(string text, float maxWidth)
    {
        const string Ellipsis = "…";

        if (string.IsNullOrEmpty(text)) return text;
        if (maxWidth <= 0f) return text;
        if (ImGui.CalcTextSize(text).X <= maxWidth) return text;

        var low = 0;
        var high = text.Length;
        while (low < high)
        {
            var mid = (low + high + 1) / 2;
            if (ImGui.CalcTextSize(string.Concat(text.AsSpan(0, mid), Ellipsis)).X <= maxWidth)
                low = mid;
            else
                high = mid - 1;
        }

        if (low > 0 && char.IsHighSurrogate(text[low - 1])) low--;

        return low <= 0 ? Ellipsis : string.Concat(text.AsSpan(0, low), Ellipsis);
    }

    public static bool IconButton(FontAwesomeIcon icon, Vector2 size = default, string? tooltip = null, bool small = false)
    {
        var label = icon.ToIconString();

        ImGui.PushFont(UiBuilder.IconFont);
        bool res = small ? ImGui.SmallButton(label) : ImGui.Button(label, size);
        ImGui.PopFont();

        if (tooltip != null && ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(tooltip);
        }

        return res;
    }
}
