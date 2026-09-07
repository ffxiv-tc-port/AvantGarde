using System;
using System.Linq;
using Dalamud.Plugin.Ipc;

namespace AvantGarde.Ipc;

/// <summary>
/// 對 Lifestream 的單向消費端：只有使用者<b>親手按下裝備列上的「前往」</b>才會呼叫，
/// 沒有任何自動化、沒有事件驅動的呼叫鏈，也不會在背景排任何工作。
/// </summary>
/// <remarks>
/// 🔴 下面這個字串是跨外掛的行為契約，對應 <c>Lifestream/Lifestream/IPC/IPCProvider.cs</c>：
/// <code>Lifestream.GoToMapPoint(uint territoryId, float worldX, float worldZ, bool fly) -&gt; bool</code>
/// 參數是<b>世界座標的 X 與 Z</b>（不是地圖上的 X/Y，也不需要高度——Lifestream 抵達後
/// 會向 vnavmesh 問這個 XZ 底下的地板高度）。名字或參數改了要兩邊一起改，
/// 否則失敗形式是「按了沒反應」而不是報錯。
///
/// 🔴 絕不用聊天指令 <c>/li</c> 走這件事：那個指令空參數等於跨世界傳送。
/// </remarks>
public static class LifestreamIpc
{
    private const string LifestreamInternalName = "Lifestream";

    private static ICallGateSubscriber<uint, float, float, bool, bool>? _goToMapPoint;
    private static bool _subscribed;

    /// <summary>
    /// Lifestream 有沒有裝而且載入了。沒有就連 IPC 都不要呼叫，直接把「前往」藏起來。
    /// </summary>
    public static bool IsAvailable =>
        Service.PluginInterface.InstalledPlugins.Any(p => p is { InternalName: LifestreamInternalName, IsLoaded: true });

    /// <summary>
    /// 請 Lifestream 把角色送到指定區域的世界座標（跨區會自動傳送，可選擇用飛行坐騎）。
    /// </summary>
    /// <returns>
    /// <see langword="true"/> 代表 Lifestream 真的排了工作；
    /// <see langword="false"/> 代表<b>它一件事都沒排</b>（未安裝、舊版沒有這個端點、
    /// 正在忙、人不能動、沒有 vnavmesh、目標區域沒有解鎖的乙太之光）——呼叫端不要等。
    /// </returns>
    public static bool TryGoToMapPoint(uint territoryId, float worldX, float worldZ, bool fly)
    {
        if (territoryId == 0) return false;
        if (!IsAvailable) return false;

        try
        {
            EnsureSubscribed();
            var accepted = _goToMapPoint!.InvokeFunc(territoryId, worldX, worldZ, fly);

            Service.PluginLog.Information(
                $"[AvantGarde] 要求 Lifestream 前往 territory {territoryId} 的 ({worldX:F1}, {worldZ:F1})，" +
                $"允許飛行={fly}，Lifestream 回應={accepted}。");

            return accepted;
        }
        catch (Exception e)
        {
            // 舊版 Lifestream 沒有這個端點時走到這裡（IpcNotReadyError）：不崩、不做事。
            Service.PluginLog.Information(
                e, "[AvantGarde] 呼叫 Lifestream.GoToMapPoint 失敗（可能是 Lifestream 版本太舊或尚未就緒）。");
            return false;
        }
    }

    private static void EnsureSubscribed()
    {
        if (_subscribed) return;

        _goToMapPoint = Service.PluginInterface.GetIpcSubscriber<uint, float, float, bool, bool>("Lifestream.GoToMapPoint");
        _subscribed = true;
    }
}
