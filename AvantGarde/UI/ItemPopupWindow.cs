using System.Diagnostics;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.Sheets;

using AvantGarde.Data;
using AvantGarde.Ipc;
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

        ImGui.Text("Equippable by: ??".Loc(item.ClassJobCategory.Value.Name));
        ImGui.Spacing();

        DrawAcquisitionSource(item);
        ImGui.Spacing();

        if (ImGui.Selectable("Try On".Loc()))
            AgentTryon.TryOn(0, item.RowId);

        if (ImGui.Selectable("Search Item".Loc()))
            ItemFinderModule.Instance()->SearchForItem(item.RowId, true);

        if (ImGui.Selectable("Link".Loc()))
            LinkItem(item);

        if (ImGui.Selectable("Copy Name".Loc()))
            ImGui.SetClipboardText(item.Name.ExtractText());

        if (ImGui.Selectable("Open in Garland Tools".Loc()))
            Process.Start(new ProcessStartInfo { FileName = $"https://garlandtools.org/db/#item/{item.RowId}", UseShellExecute = true });
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"https://garlandtools.org/db/#item/{item.RowId}");

        if (ItemVendorLocationIpc.IsAvailable && ImGui.Selectable("Show in Item Vendor Location".Loc()))
            ItemVendorLocationIpc.OpenResults(item.RowId);

        // Not yet resolved by DrawAcquisitionSource (no known vendor/achievement/quest source
        // via Item Vendor Location, and not craftable via Recipe): marketboard-only items
        // (e.g. old Starlight Celebration sets), desynth drops, dungeon/raid/instance drops,
        // and Eureka & Bozja lockboxes still fall back to "Unknown Source".
    }

    private static void DrawAcquisitionSource(Item item)
    {
        var source = AcquisitionSource.Resolve(item.RowId);
        switch (source.Kind)
        {
            case AcquisitionSourceKind.Vendor:
                DrawGameIcon(SourceTypeIconExchange, GuiUtilities.IconSize);
                ImGui.SameLine();
                ImGui.TextWrapped(source.ZoneName is not null
                    ? "Available from a vendor near ??.".Loc(source.ZoneName)
                    : "Available from a known source (achievement, quest, or shop).".Loc());

                if (source.Location is { } location && ImGui.Selectable("Show location on map".Loc()))
                {
                    Service.GameGui.OpenMapWithMapLink(new MapLinkPayload(location.TerritoryId, location.MapId, location.X, location.Y));
                }
                break;

            case AcquisitionSourceKind.Craftable:
                DrawGameIcon(SourceTypeIconCrafting, GuiUtilities.IconSize);
                ImGui.SameLine();
                ImGui.TextWrapped(source.JobAbbreviation is not null
                    ? "Craftable by ??.".Loc(source.JobAbbreviation)
                    : "Craftable.".Loc());
                break;

            default:
                DrawGameIcon(SourceTypeIconQuestionMark, GuiUtilities.IconSize);
                ImGui.SameLine();
                ImGui.TextWrapped("Unknown Source!\nWork In Progress...".Loc());
                break;
        }
    }

    private static unsafe void LinkItem(Item item)
    {
        var agentChatLog = AgentChatLog.Instance();

        agentChatLog->LinkedItem.ItemId = item.RowId;
        agentChatLog->LinkedItem.Quantity = 1;
        agentChatLog->LinkedItemName.SetString(item.Name.ExtractText());
        agentChatLog->LinkedItem.LinkedItemQuality = item.Rarity;

        // 1096 is the ID for <item>
        agentChatLog->InsertTextCommandParam(1096, true);
    }

    private static void DrawGameIcon(ushort id, Vector2 size)
    {
        var icon = Service.TextureProvider.GetFromGameIcon(new(id));

        if (icon.TryGetWrap(out var texture, out _))
        {
            ImGui.Image(texture.Handle, size, Vector2.Zero, Vector2.One, Vector4.One);
        }
    }
}
