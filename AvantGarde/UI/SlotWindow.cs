using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Lumina.Excel.Sheets;

using AvantGarde.Data;
using AvantGarde.Ipc;
using AvantGarde.Utils;

namespace AvantGarde.UI;

public class SlotWindow
{
    private static ImGuiWindowFlags WindowFlags => ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize;

    private List<Item> _itemsFiltered;
    private ItemSlot _slot;
    private Vector2 _position = new();
    private bool _isOpen = false;

    public SlotWindow()
    {
        _itemsFiltered = Service.DataManager.Items;
    }

    public void Update(ItemSlot slot, List<int>? itemIDs, Vector2 windowPos, float buttonSize)
    {
        if (slot == _slot && _isOpen)
            _isOpen = false;
        else
            _isOpen = true;

        _itemsFiltered = [];
        if (_isOpen)
        {
            _slot = slot;
            _position = windowPos;
            _position.X += slot >= ItemSlot.Ears ? buttonSize : -GuiUtilities.SlotWindowSize.X;

            if (itemIDs is not null)
            {
                _itemsFiltered = Service.DataManager.Items
                    .Where(item => slot.IsMatchingSlot(item) && itemIDs.Contains((int)item.RowId) == true).ToList();
            }

            // 🔴 取得方式的跨外掛查詢只發生在這裡（＝使用者點了槽位按鈕的那一下），
            //    繪製迴圈只讀快取。放進 Draw() 會變成每一列每一幀一次 IPC。
            AcquisitionHints.Prefetch(_itemsFiltered);
        }
    }

    public unsafe void Draw()
    {
        if (!_isOpen) { return; }

        ImGui.SetNextWindowSize(GuiUtilities.SlotWindowSize);
        ImGui.SetNextWindowPos(_position);

        if (!ImGui.Begin($"##avantgarde-item-display-{_slot}", WindowFlags))
        {
            ImGui.End();
            return;
        }

        ImGui.Text($"Avant-Garde: {_slot.GetDescription()}");

        // 標題列右端的齒輪＝設定的第二個入口（第一個是插件安裝器裡的齒輪）。
        // ⚠️ 圖示字型下的寬度要在 PushFont 之後才量得準，否則按鈕會被推出視窗右緣。
        //    SmallButton 的水平內距是 FramePadding.X * 2（垂直是 0）。
        ImGui.PushFont(UiBuilder.IconFont);
        var cogWidth = ImGui.CalcTextSize(FontAwesomeIcon.Cog.ToIconString()).X + (ImGui.GetStyle().FramePadding.X * 2f);
        ImGui.PopFont();

        ImGui.SameLine();
        ImGui.SetCursorPosX(ImGui.GetWindowWidth() - cogWidth - ImGui.GetStyle().WindowPadding.X);
        if (GuiUtilities.IconButton(FontAwesomeIcon.Cog, default, "Settings".Loc(), small: true))
        {
            Service.ConfigWindow.IsOpen = true;
        }

        ImGui.Separator();

        if (!_itemsFiltered.Any())
        {
            using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.5f, 1f)))
            {
                ImGui.TextWrapped("This category could be new, and/or is currently empty in the database.".Loc());
                ImGui.Spacing();
                ImGui.TextWrapped("If you wish to help, see the github page for more information.".Loc());
            }

            ImGui.End();
            return;
        }

        ImGuiClip.ClippedDraw(_itemsFiltered, item => DrawItem(item, showIDs: false, canInteract: true), GuiUtilities.IconSize.Y + ImGui.GetStyle().ItemSpacing.Y);

        ImGui.End();
    }

    public static void DrawItem(Item item, bool showIDs, bool canInteract)
    {
        var isOwned = OwnershipChecker.IsOwned(item.RowId);

        // 已經有的裝備不需要「去哪裡拿」，所以只有判定為「沒有」的才查快取。
        var hint = isOwned ? default : AcquisitionHints.Get(item);

        using var ownedColor = ImRaii.PushColor(ImGuiCol.Header, new Vector4(0.20f, 0.55f, 0.20f, 0.55f), isOwned)
                                    .Push(ImGuiCol.HeaderHovered, new Vector4(0.25f, 0.65f, 0.25f, 0.65f), isOwned)
                                    .Push(ImGuiCol.HeaderActive, new Vector4(0.20f, 0.55f, 0.20f, 0.75f), isOwned);

        if (canInteract)
        {
            if (ImGui.Selectable($"##avantgarde-popup-select-{item.RowId}", isOwned, ImGuiSelectableFlags.None, new Vector2(GuiUtilities.SlotWindowSize.X, GuiUtilities.IconSize.Y))
                && (ImGui.IsMouseReleased(ImGuiMouseButton.Left) || ImGui.IsMouseReleased(ImGuiMouseButton.Right)))
            {
                ImGui.OpenPopup($"##avantgarde-item-popup-{item.RowId}");
            }

            // 🔴 這個 Selectable 蓋滿整列，而「前往」是在它之後才送出的。
            //    本 pin 的 ImGui（有 SetItemAllowOverlap、還沒有 SetNextItemAllowOverlap）
            //    在 ItemHoverable 開頭就會擋掉「已經有別的 item 佔住 HoveredId」的後續 item
            //    ⇒ 不呼叫這一行的話，「前往」按鈕會畫得出來但永遠按不到，而且不報錯。
            //    必須緊接在 Selectable 之後呼叫（它讀的是 LastItemData）。
            ImGui.SetItemAllowOverlap();

            if (ImGui.IsItemHovered())
            {
                // 列上放的是掃視得到的短版（含裁掉的「…」），完整內容一律留在這裡。
                using var tooltip = ImRaii.Tooltip();
                ImGui.TextUnformatted(BuildRowTooltip(item, isOwned, hint));
            }
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() - GuiUtilities.IconSize.Y - ImGui.GetStyle().FramePadding.Y);
        }

        // 一列固定佔兩行（圖示剛好是兩行高）：第一行是道具名稱，第二行是取得方式。
        // 兩行都以這個絕對起點定位，所以名稱再長也不會把第二行擠到下一列去
        // ——ClippedDraw 的列高是固定值，靠自然換行會整份錯位。
        var contentStart = ImGui.GetCursorPos();

        if (Service.TextureProvider.GetFromGameIcon(new GameIconLookup { IconId = item.Icon }).TryGetWrap(out var icon, out _))
        {
            if (icon is not null)
            {
                ImGui.Image(icon.Handle, GuiUtilities.IconSize);
                ImGui.SameLine();
            }
        }

        var textStartX = ImGui.GetCursorPosX();
        var textWidth = ImGui.GetContentRegionAvail().X;
        if (textWidth < 1f) textWidth = 1f;

        var itemName = item.Name.ExtractText();
        if (showIDs)
        {
            itemName = $"[{item.RowId}] " + itemName;
        }
        ImGui.TextUnformatted(GuiUtilities.Ellipsize(itemName, textWidth));

        if (!isOwned)
        {
            ImGui.SetCursorPos(new Vector2(textStartX, contentStart.Y + ImGui.GetTextLineHeight()));
            DrawAcquisitionLine(item, hint, textStartX, textWidth);
        }

        ItemPopupWindow.Draw(item);
    }

    /// <summary>
    /// 一列的第二行：「哪裡拿」。NPC 有位置且裝了 Lifestream 時右端補一顆「前往」。
    /// </summary>
    private static void DrawAcquisitionLine(Item item, AcquisitionHint hint, float lineStartX, float lineWidth)
    {
        var showGoButton = hint.Kind == AcquisitionHintKind.Vendor
                           && hint.TerritoryId != 0
                           && hint.HasWorldPosition
                           && AcquisitionHints.LifestreamAvailable;

        var goLabel = "Go".Loc();
        var goWidth = ImGui.CalcTextSize(goLabel).X + (ImGui.GetStyle().FramePadding.X * 2f);

        var textWidth = showGoButton ? lineWidth - goWidth - ImGui.GetStyle().ItemSpacing.X : lineWidth;
        if (textWidth < 1f) textWidth = 1f;

        var (text, colour) = DescribeHint(hint);
        using (ImRaii.PushColor(ImGuiCol.Text, colour))
        {
            ImGui.TextUnformatted(GuiUtilities.Ellipsize(text, textWidth));
        }

        if (!showGoButton) { return; }

        ImGui.SameLine();
        ImGui.SetCursorPosX(lineStartX + lineWidth - goWidth);

        // 🔴 只有使用者親手按下去才會動：沒有任何自動化或事件驅動的呼叫鏈。
        //    Lifestream 回 false 代表它一件事都沒排（忙碌中／人不能動／沒有 vnavmesh），
        //    那不是錯誤，所以這裡不彈訊息，原因寫在 log（Information）。
        if (ImGui.SmallButton($"{goLabel}##avantgarde-goto-{item.RowId}"))
        {
            // 飛行旗標是使用者設定（預設 true＝這個功能上線時的行為）。
            // 這是整個外掛裡唯一一個傳飛行旗標的地方。
            LifestreamIpc.TryGoToMapPoint(hint.TerritoryId, hint.WorldX, hint.WorldZ, Service.Config.FlyToVendor);
        }
        if (ImGui.IsItemHovered())
        {
            using var tooltip = ImRaii.Tooltip();
            // 🔴 算不出地圖座標時不要印 (0.0, 0.0)——那會被讀成「商人在地圖左上角」。
            //    前往本身照樣可以按：世界座標與地圖座標是兩條獨立的資料。
            ImGui.TextUnformatted(hint.MapCoordinatesKnown
                ? "Ask Lifestream to take you there (map ??, ??).".Loc($"{hint.MapX:F1}", $"{hint.MapY:F1}")
                : "Ask Lifestream to take you there.".Loc());
        }
    }

    private static (string Text, Vector4 Colour) DescribeHint(AcquisitionHint hint)
    {
        var normal = new Vector4(0.78f, 0.78f, 0.78f, 1f);
        var dim = new Vector4(0.50f, 0.50f, 0.50f, 1f);

        switch (hint.Kind)
        {
            case AcquisitionHintKind.Vendor:
                var vendorName = hint.VendorName ?? "NPC vendor".Loc();
                return (hint.PlaceName is not null
                    ? "NPC: ?? @ ??".Loc(vendorName, hint.PlaceName)
                    : "NPC: ??".Loc(vendorName), normal);

            case AcquisitionHintKind.Market:
                // 列上空間窄用短的「市場」；tooltip 才用官方全名「市場佈告板」。
                return ("Market".Loc(), normal);

            case AcquisitionHintKind.IvlUnavailable:
                return ("Requires ItemVendorLocation".Loc(), dim);

            default:
                // 🔴「不知道」本身要在列上看得見。畫成 0 或留白會被讀成「沒有來源」。
                return ("?", dim);
        }
    }

    private static string BuildRowTooltip(Item item, bool isOwned, AcquisitionHint hint)
    {
        var lines = new List<string> { item.Name.ExtractText() };

        if (isOwned)
        {
            lines.Add("Already in your possession.".Loc());
            return string.Join("\n", lines);
        }

        switch (hint.Kind)
        {
            case AcquisitionHintKind.Vendor:
                var vendorName = hint.VendorName ?? "NPC vendor".Loc();
                lines.Add(hint.PlaceName is not null
                    ? "NPC: ?? @ ??".Loc(vendorName, hint.PlaceName)
                    : "NPC: ??".Loc(vendorName));

                // 店名可能是兩行（上層選單名＋店名），所以只放 tooltip 不放列上。
                if (hint.ShopName is not null)
                    lines.Add("Shop: ??".Loc(hint.ShopName));

                if (hint.TerritoryId == 0)
                    lines.Add("Location unknown.".Loc());
                else if (hint.MapCoordinatesKnown)
                    lines.Add($"X: {hint.MapX:F1}, Y: {hint.MapY:F1}");
                else
                    // 🔴「不知道」本身要看得見。直接寫 0 會誤導。
                    lines.Add("Map coordinates unavailable.".Loc());

                if (hint.AlsoOnMarket)
                    lines.Add("Also available on the market board.".Loc());

                if (hint.TerritoryId != 0 && hint.HasWorldPosition && !AcquisitionHints.LifestreamAvailable)
                    lines.Add("Install Lifestream to travel there in one click.".Loc());
                break;

            case AcquisitionHintKind.Market:
                lines.Add("Only available on the market board.".Loc());
                break;

            case AcquisitionHintKind.IvlUnavailable:
                lines.Add("ItemVendorLocation is not installed, so acquisition sources cannot be looked up.".Loc());
                break;

            default:
                lines.Add("No known acquisition source.".Loc());
                break;
        }

        return string.Join("\n", lines);
    }
}
