using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Ipc;

namespace AvantGarde.Ipc;

// Soft integration with ItemVendorLocation: used to detect whether an item has a known
// acquisition source (gil shop, special shop/exchange, GC shop, achievement, quest reward,
// etc.) and, if so, where to find it. AvantGarde does not attempt to reproduce IVL's own
// source classification; it only asks "do you know how to get this" and defers the detailed
// breakdown to IVL's own results window.
public static class ItemVendorLocationIpc
{
    private static ICallGateSubscriber<uint, bool, HashSet<(uint npcId, uint territory, (float x, float y))>?>? _getItemVendors;
    private static ICallGateSubscriber<uint, object?>? _openVendorResults;
    private static bool _subscribed;

    public static bool IsAvailable =>
        Service.PluginInterface.InstalledPlugins.Any(p => p.IsLoaded && p.InternalName == "ItemVendorLocation");

    /// <summary>
    /// Returns true if ItemVendorLocation has any known source for this item (a vendor,
    /// an achievement, a quest reward, etc.).
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
        catch (Exception e)
        {
            Service.PluginLog.Error(e, "ItemVendorLocationIpc: OpenVendorResults call failed");
        }
    }

    private static void EnsureSubscribed()
    {
        if (_subscribed) return;

        _getItemVendors = Service.PluginInterface.GetIpcSubscriber<uint, bool, HashSet<(uint npcId, uint territory, (float x, float y))>?>("ItemVendorLocation.GetItemVendors");
        _openVendorResults = Service.PluginInterface.GetIpcSubscriber<uint, object?>("ItemVendorLocation.OpenVendorResults");
        _subscribed = true;
    }
}
