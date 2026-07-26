using System.Collections.Generic;
using System.Linq;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

using AvantGarde.Ipc;

using CabinetSheetRow = Lumina.Excel.Sheets.Cabinet;

namespace AvantGarde.Data;

// Determines whether the player already "has" a given item, for the Fashion Report gear
// list's owned/unowned highlight. Bags, armory chest, equipped gear and the armoire are
// read directly via ClientStructs since they're always resident in memory; the Glamour
// Dresser is not (it's only populated while its own game window is open), so that check
// is deferred to InventoryTools via IPC, which already keeps a persistent scanned copy.
public static unsafe class OwnershipChecker
{
    private static readonly InventoryType[] PersonalStorageContainers =
    [
        InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4,
        InventoryType.EquippedItems,
        InventoryType.ArmoryMainHand, InventoryType.ArmoryOffHand, InventoryType.ArmoryHead, InventoryType.ArmoryBody,
        InventoryType.ArmoryHands, InventoryType.ArmoryWaist, InventoryType.ArmoryLegs, InventoryType.ArmoryFeets,
        InventoryType.ArmoryEar, InventoryType.ArmoryNeck, InventoryType.ArmoryWrist, InventoryType.ArmoryRings,
        InventoryType.ArmorySoulCrystal,
    ];

    private static Dictionary<uint, uint>? _armoireItemToCabinetRow;

    private static Dictionary<uint, uint> ArmoireItemToCabinetRow => _armoireItemToCabinetRow ??=
        Service.DalamudDataManager.GetExcelSheet<CabinetSheetRow>()!
            .Where(row => row.Item.RowId != 0)
            .GroupBy(row => row.Item.RowId)
            .ToDictionary(g => g.Key, g => g.First().RowId);

    public static bool IsOwned(uint itemId)
    {
        return IsInPersonalStorage(itemId) || IsInArmoire(itemId) || AllaganToolsIpc.IsInGlamourDresser(itemId);
    }

    private static bool IsInPersonalStorage(uint itemId)
    {
        var inventoryManager = InventoryManager.Instance();
        if (inventoryManager == null) return false;

        foreach (var containerType in PersonalStorageContainers)
        {
            var container = inventoryManager->GetInventoryContainer(containerType);
            if (container == null || !container->IsLoaded) continue;

            for (var i = 0; i < container->Size; i++)
            {
                if (container->Items[i].ItemId == itemId)
                    return true;
            }
        }

        return false;
    }

    private static bool IsInArmoire(uint itemId)
    {
        if (!ArmoireItemToCabinetRow.TryGetValue(itemId, out var cabinetRow)) return false;

        var uiState = UIState.Instance();
        if (uiState == null || !uiState->Cabinet.IsCabinetLoaded()) return false;

        return uiState->Cabinet.IsItemInCabinet((int)cabinetRow);
    }
}
