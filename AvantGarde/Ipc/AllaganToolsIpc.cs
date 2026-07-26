using System;
using System.Linq;
using Dalamud.Plugin.Ipc;

namespace AvantGarde.Ipc;

// Soft integration with InventoryTools ("Allagan Tools"): used only to check whether an
// item is filed in the player's Glamour Dresser, since that storage can't be read directly
// without the in-game dresser UI being open. Everything else ownership-related (bags,
// armory chest, equipped gear, armoire) is checked locally without needing this plugin.
public static class AllaganToolsIpc
{
    private const uint GlamourChestInventoryType = 2501;

    private static ICallGateSubscriber<bool>? _isInitialized;
    private static ICallGateSubscriber<uint, bool, uint[], uint>? _itemCountOwned;
    private static bool _subscribed;

    private static bool IsPluginLoaded =>
        Service.PluginInterface.InstalledPlugins.Any(p => p.IsLoaded && (p.InternalName == "InventoryTools" || p.InternalName == "Allagan Tools"));

    public static bool IsInGlamourDresser(uint itemId)
    {
        if (!IsPluginLoaded) return false;

        try
        {
            EnsureSubscribed();
            if (_isInitialized is null || _itemCountOwned is null || !_isInitialized.InvokeFunc())
                return false;

            return _itemCountOwned.InvokeFunc(itemId, true, [GlamourChestInventoryType]) > 0;
        }
        catch (Exception e)
        {
            Service.PluginLog.Verbose(e, "AllaganToolsIpc: ItemCountOwned call failed");
            return false;
        }
    }

    private static void EnsureSubscribed()
    {
        if (_subscribed) return;

        _isInitialized = Service.PluginInterface.GetIpcSubscriber<bool>("AllaganTools.IsInitialized");
        _itemCountOwned = Service.PluginInterface.GetIpcSubscriber<uint, bool, uint[], uint>("AllaganTools.ItemCountOwned");
        _subscribed = true;
    }
}
