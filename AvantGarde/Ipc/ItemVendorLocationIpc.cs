using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Ipc.Exceptions;

namespace AvantGarde.Ipc;

/// <summary>
/// <c>ItemVendorLocation</c> 回的一筆商人資料的<b>鏡像型別</b>。
/// </summary>
/// <remarks>
/// 🔴 <b>成員名是跨外掛契約，逐字對應 <c>ItemVendorLocation/IPC/VendorLocationInfo.cs</c>。</b>
/// CallGate 在兩邊型別不同時走 JSON 來回轉換（這裡一定不同——是兩個組件裡的兩個類別），
/// 所以名字打錯的失敗形式是<b>那個欄位靜默變成預設值</b>，不是例外。改名要兩邊一起改。
///
/// 📌 缺欄位是安全的（Newtonsoft 忽略多出來的鍵），所以這裡刻意只鏡像本外掛用得到的部分：
/// <c>Costs</c>（要付多少錢）沒有鏡像過來——列上只有一行的空間放不下代價，
/// 要看明細的人按右鍵開 ItemVendorLocation 自己的結果視窗比較清楚。
/// </remarks>
public sealed class VendorLocationInfo
{
    public uint NpcId { get; set; }

    public string NpcName { get; set; } = "";

    public string ShopName { get; set; } = "";

    /// <summary>商店資料的列號；<c>0</c> ＝ ItemVendorLocation 建表時拿不到。</summary>
    /// <remarks>🔴 要配 <see cref="ShopSheetName"/> 才有意義——各張商店表的列號互相會撞。</remarks>
    public uint ShopId { get; set; }

    /// <summary><see cref="ShopId"/> 屬於哪一張表；拿不到時是空字串。</summary>
    public string ShopSheetName { get; set; } = "";

    /// <summary><c>GilShop</c>／<c>SpecialShop</c>／<c>GcShop</c>／<c>Achievement</c> 等等。</summary>
    public string SourceType { get; set; } = "";

    /// <summary>
    /// 這個商人有沒有已知的位置。
    /// </summary>
    /// <remarks>
    /// 🔴 <see langword="false"/> 時底下所有座標欄位都是 0，<b>那個 0 沒有意義</b>。
    /// </remarks>
    public bool HasLocation { get; set; }

    public uint TerritoryTypeId { get; set; }

    public uint MapId { get; set; }

    /// <summary>世界座標 X。</summary>
    public float WorldX { get; set; }

    /// <summary>
    /// 世界座標 Z（<b>不是地圖上的 Y</b>）。<c>Lifestream.GoToMapPoint</c> 要的就是這個。
    /// </summary>
    public float WorldZ { get; set; }

    /// <summary>地圖座標 X（遊戲內地圖上顯示的那組數字），只拿來顯示給使用者核對。</summary>
    public float MapX { get; set; }

    /// <summary>地圖座標 Y。</summary>
    public float MapY { get; set; }

    /// <summary><see cref="MapX"/>／<see cref="MapY"/> 算不算得出來；false 時那兩個是 0。</summary>
    public bool MapCoordinatesKnown { get; set; }
}

// Soft integration with ItemVendorLocation: used to detect whether an item has a known
// acquisition source (gil shop, special shop/exchange, GC shop, achievement, quest reward,
// etc.) and, if so, where to find it. AvantGarde does not attempt to reproduce IVL's own
// source classification; it only asks "do you know how to get this" and defers the detailed
// breakdown to IVL's own results window.
public static class ItemVendorLocationIpc
{
    private static ICallGateSubscriber<uint, bool, HashSet<(uint npcId, uint territory, (float x, float y))>?>? _getItemVendors;
    private static ICallGateSubscriber<uint, List<VendorLocationInfo>?>? _getItemVendorsWorld;
    private static ICallGateSubscriber<uint, object?>? _openVendorResults;
    private static bool _subscribed;

    /// <summary>
    /// 這一輪查詢裡 <c>GetItemVendorsWorld</c> 到底能不能用。
    /// </summary>
    /// <remarks>
    /// 🔴 存在的理由是<b>成本</b>不是正確性：一次 <c>Prefetch</c> 會問掉好幾百件道具，
    /// 舊版 ItemVendorLocation 沒有這支端點時，每一件都要擲一次 <c>IpcNotReadyError</c>。
    /// 探到一次不可用就整輪不再打。
    ///
    /// ⚠️ 所以它必須在每輪查詢開頭被 <see cref="ResetEndpointProbe"/> 清掉——
    /// 否則使用者中途更新或重載 ItemVendorLocation，這一整個 session 都會卡在舊路徑，
    /// 而且完全看不出原因。
    /// </remarks>
    private static bool? _worldEndpointUsable;

    public static bool IsAvailable =>
        Service.PluginInterface.InstalledPlugins.Any(p => p.IsLoaded && p.InternalName == "ItemVendorLocation");

    /// <summary>每輪查詢開始前呼叫一次，讓「新端點能不能用」重新探一遍。</summary>
    public static void ResetEndpointProbe() => _worldEndpointUsable = null;

    /// <summary>
    /// 新端點：一次拿到<b>世界座標</b>與商人／商店名／商店列號。
    /// </summary>
    /// <param name="vendors">
    /// 回 <see langword="true"/> 時才有意義；<b>可能是空清單</b>
    /// （＝ItemVendorLocation 認得這件道具但沒有任何商人）。
    /// </param>
    /// <returns>
    /// <see langword="true"/> ＝ ItemVendorLocation 回答了（即使答案是「沒有」）；
    /// <see langword="false"/> ＝ <b>這支端點不能用</b>（沒裝／舊版沒有它／呼叫出錯），
    /// 呼叫端應該落回 <see cref="TryGetSource"/>。
    /// </returns>
    /// <remarks>
    /// 🔑 這兩件事一定要分得開。把「端點不存在」和「查得到但沒有商人」混成同一個回傳值，
    /// 會讓舊版 ItemVendorLocation 的使用者整份清單退成「查無已知來源」——
    /// 那是<b>錯的答案</b>，不是「查不到」。
    /// </remarks>
    public static bool TryGetVendorsWorld(uint itemId, out List<VendorLocationInfo> vendors)
    {
        vendors = [];
        if (!IsAvailable) return false;
        if (_worldEndpointUsable == false) return false;

        try
        {
            EnsureSubscribed();
            var result = _getItemVendorsWorld?.InvokeFunc(itemId);
            _worldEndpointUsable = true;

            // null ＝ ItemVendorLocation 完全不認得這件道具。那是一個有效的答案
            //（＝沒有已知商人），不是「端點壞了」，所以照樣回 true 配一份空清單。
            if (result is not null)
                vendors = result;

            return true;
        }
        catch (IpcNotReadyError)
        {
            // 對方沒裝、還沒註冊，或舊版根本沒有這支端點。這是預期內的情況：
            // 記一次、整輪不再打，讓呼叫端安靜地落回舊端點。
            if (_worldEndpointUsable is null)
            {
                Service.PluginLog.Information(
                    "[AvantGarde] ItemVendorLocation.GetItemVendorsWorld 不可用（多半是對方版本較舊），"
                    + "這一輪改用舊的 GetItemVendors 端點。");
            }

            _worldEndpointUsable = false;
            return false;
        }
        catch (Exception e)
        {
            // 型別對不上、對方實作擲例外之類的意外。一樣落回舊端點，但這個要看得見。
            if (_worldEndpointUsable is null)
            {
                Service.PluginLog.Information(
                    e, "[AvantGarde] 呼叫 ItemVendorLocation.GetItemVendorsWorld 失敗，這一輪改用舊端點。");
            }

            _worldEndpointUsable = false;
            return false;
        }
    }

    /// <summary>
    /// 舊端點。<b>刻意保留</b>：使用者的 ItemVendorLocation 不一定跟得上版本，
    /// 這是 <see cref="TryGetVendorsWorld"/> 不可用時唯一還能用的來源。
    /// </summary>
    /// <param name="filterNoLocation">
    /// true（預設，維持既有呼叫端的行為）＝只回有地圖位置的商人；
    /// false ＝連「知道是誰賣的、但不知道他站在哪」的也回來，那種項目的 territory 與座標是 0。
    /// </param>
    public static bool TryGetSource(uint itemId, out HashSet<(uint npcId, uint territory, (float x, float y))> vendors, bool filterNoLocation = true)
    {
        vendors = [];
        if (!IsAvailable) return false;

        try
        {
            EnsureSubscribed();
            var result = _getItemVendors?.InvokeFunc(itemId, filterNoLocation);
            if (result is null) return false;

            vendors = result;
            return true;
        }
        catch (IpcNotReadyError)
        {
            return false;
        }
        catch (Exception e)
        {
            Service.PluginLog.Verbose(e, "ItemVendorLocationIpc: GetItemVendors call failed");
            return false;
        }
    }

    public static void OpenResults(uint itemId)
    {
        if (!IsAvailable) return;

        try
        {
            EnsureSubscribed();
            _openVendorResults?.InvokeFunc(itemId);
        }
        catch (IpcNotReadyError)
        {
            // 對方還沒註冊端點。不是錯誤，也沒有別的事可以做。
        }
        catch (Exception e)
        {
            Service.PluginLog.Error(e, "ItemVendorLocationIpc: OpenVendorResults call failed");
        }
    }

    private static void EnsureSubscribed()
    {
        if (_subscribed) return;

        _getItemVendors = Service.PluginInterface.GetIpcSubscriber<uint, bool, HashSet<(uint npcId, uint territory, (float x, float y))>?>("ItemVendorLocation.GetItemVendors");
        _getItemVendorsWorld = Service.PluginInterface.GetIpcSubscriber<uint, List<VendorLocationInfo>?>("ItemVendorLocation.GetItemVendorsWorld");
        _openVendorResults = Service.PluginInterface.GetIpcSubscriber<uint, object?>("ItemVendorLocation.OpenVendorResults");
        _subscribed = true;
    }
}
