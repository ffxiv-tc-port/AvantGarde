using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

using AvantGarde.Ipc;

namespace AvantGarde.UI;

/// <summary>
/// Avant-Garde 唯一的設定視窗。
/// </summary>
/// <remarks>
/// 📌 這個外掛沒有指令、也沒有常駐的主視窗（整個 UI 都貼在時尚品鑑的原生視窗上），
/// 所以設定的入口做了兩個：
/// <list type="number">
/// <item>插件安裝器裡的齒輪（<c>UiBuilder.OpenConfigUi</c>）—— 隨時都進得去；</item>
/// <item>裝備清單視窗標題列右端的齒輪 —— 使用者正要按「前往」時就在手邊。</item>
/// </list>
/// ⚠️ 標題用 <c>###</c> 固定 ImGui 的識別字串：顯示名是翻譯過的，
/// 但視窗位置與大小的記憶不能因為換了語言就變成另一個視窗。
/// </remarks>
public sealed class ConfigWindow : Window
{
    public ConfigWindow()
        : base("Avant-Garde Settings".Loc() + "###avantgarde-config")
    {
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(380f, 160f),
            MaximumSize = new Vector2(900f, 600f),
        };
        this.Size = new Vector2(420f, 190f);
        this.SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        var config = Service.Config;

        var fly = config.FlyToVendor;
        if (ImGui.Checkbox("Use a flying mount when travelling to a vendor".Loc(), ref fly))
        {
            config.FlyToVendor = fly;
            config.Save();
        }

        ImGui.Spacing();
        using (ImRaii.PushIndent())
        using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(0.60f, 0.60f, 0.60f, 1f)))
        {
            ImGui.TextWrapped("Turn this off to always travel on foot. Lifestream already falls back to a ground route by itself where flying is not unlocked, so leaving this on is safe.".Loc());
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // 🔴「這個開關現在有沒有作用」本身要看得見。
        //    Lifestream 沒裝的話「前往」按鈕根本不會畫出來，不講的話使用者會以為開關壞了。
        //    這兩個判斷都是即時查 InstalledPlugins，不吃 SlotWindow 的快取旗標
        //    （那個旗標只有在使用者開過一次裝備清單之後才有意義，設定視窗可以先被打開）。
        if (!LifestreamIpc.IsAvailable)
        {
            using var warn = ImRaii.PushColor(ImGuiCol.Text, new Vector4(0.85f, 0.70f, 0.35f, 1f));
            ImGui.TextWrapped("Lifestream is not installed, so the Go button is hidden and this setting has no effect.".Loc());
        }
        else if (!ItemVendorLocationIpc.IsAvailable)
        {
            using var warn = ImRaii.PushColor(ImGuiCol.Text, new Vector4(0.85f, 0.70f, 0.35f, 1f));
            ImGui.TextWrapped("ItemVendorLocation is not installed, so acquisition sources cannot be looked up.".Loc());
        }
    }
}
