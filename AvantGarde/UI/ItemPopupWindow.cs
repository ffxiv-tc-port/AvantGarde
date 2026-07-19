using System.Diagnostics;
using System.Numerics;
using ImGuiNET;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.Sheets;

using AvantGarde.Utils;

namespace AvantGarde.UI;

public static class ItemPopupWindow
{
    private const ushort SourceTypeIconGil          = 061758;
    private const ushort SourceTypeIconAchievement  = 061767;
    private const ushort SourceTypeIconDungeon      = 061801;
    private const ushort SourceTypeIconRaid         = 061802;
    private const ushort SourceTypeIconTrial        = 061804;
    private const ushort SourceTypeIconQuestionMark = 061807;
    private const ushort SourceTypeIconTreasure     = 061808;
    private const ushort SourceTypeIconFate         = 061809;
    private const ushort SourceTypeIconTribe        = 061814;
    private const ushort SourceTypeIconGathering    = 061815;
    private const ushort SourceTypeIconCrafting     = 061816;
    private const ushort SourceTypeIconHunt         = 061819;
    private const ushort SourceTypeIconMGP          = 061820;
    private const ushort SourceTypeIconMogstore     = 061831;
    private const ushort SourceTypeIconFieldOps     = 061837;
    private const ushort SourceTypeIconQuest        = 061839;
    private const ushort SourceTypeIconExchange     = 061843;
    private const ushort SourceTypeIconIsland       = 061847;

    public static unsafe void Draw(Item item)
    {
        using var popup = ImRaii.Popup($"##avantgarde-item-popup-{item.RowId}");
        if (!popup) return;

        ImGui.TextUnformatted(item.Name.ExtractText());
        ImGui.Separator();

        ImGui.Text($"可裝備職業：{item.ClassJobCategory.Value.Name}");
        ImGui.Spacing();

        DrawGameIcon(SourceTypeIconQuestionMark, GuiUtilities.IconSize);
        ImGui.SameLine();
        ImGui.Text("來源未知！\n開發中...");
        ImGui.Spacing();

        if (ImGui.Selectable("試穿"))
            AgentTryon.TryOn(0, item.RowId);

        if (ImGui.Selectable("搜尋道具"))
            ItemFinderModule.Instance()->SearchForItem(item.RowId, true);

        if (ImGui.Selectable("連結"))
            LinkItem(item);

        if (ImGui.Selectable("複製名稱"))
            ImGui.SetClipboardText(item.Name.ExtractText());

        if (ImGui.Selectable("在 Garland Tools 中開啟"))
            Process.Start(new ProcessStartInfo { FileName = $"https://garlandtools.org/db/#item/{item.RowId}", UseShellExecute = true });
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"https://garlandtools.org/db/#item/{item.RowId}");

        // TODO: Possible item {EQUIPABLE GEAR} sources:
        //      * Crafting
        //      * Exchange (Special Shop - non-gil-currencies, raids tokens, tomestones, etc.)
        //      * Gil Shops (Gridania/Uldah/Limsa etc.)
        //      * Marketboard (e.g. old Starlight Celebration set)
        //      * Desynth (e.g. rare fishing drops)
        //      * Instance (Dungeon, Raid Coffers, AR, etc.) Drops
        //      * Achievement Claim
        //      * Retainer Ventures (Rare ARR gear exclusive to Ventures)
        //      * Eureka & Bozja Lockboxes
    }

    private static unsafe void LinkItem(Item item)
    {
        var agentChatLog = AgentChatLog.Instance();

        agentChatLog->LinkedItem.ItemId = item.RowId;
        agentChatLog->LinkedItem.Quantity = 1;
        agentChatLog->LinkedItemName.SetString(item.Name.ExtractText());
        // NOTE: InventoryItem.LinkedItemQuality doesn't exist in TC's FFXIVClientStructs
        // generation (no equivalent field on this struct here); dropped.

        // 1096 is the ID for <item>
        agentChatLog->InsertTextCommandParam(1096, true);
    }

    private static void DrawGameIcon(ushort id, Vector2 size)
    {
        var icon = Service.TextureProvider.GetFromGameIcon(new(id));

        if (icon.TryGetWrap(out var texture, out _))
        {
            ImGui.Image(texture.ImGuiHandle, size, Vector2.Zero, Vector2.One, Vector4.One);
        }
    }
}
