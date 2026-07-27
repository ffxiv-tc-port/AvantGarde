using System.Collections.Generic;
using System.Linq;
using Lumina.Excel.Sheets;

using AvantGarde.Ipc;

namespace AvantGarde.Data;

public enum AcquisitionSourceKind
{
    Unknown,
    Vendor,
    Craftable,
}

public readonly record struct MapLocation(uint TerritoryId, uint MapId, float X, float Y);

public readonly struct AcquisitionSourceInfo
{
    public required AcquisitionSourceKind Kind { get; init; }
    public string? ZoneName { get; init; }
    public string? JobAbbreviation { get; init; }
    public MapLocation? Location { get; init; }
}

// Best-effort "how do I get this" lookup. Vendor/achievement/quest/shop sources are deferred
// to ItemVendorLocation via IPC (see ItemVendorLocationIpc); crafting is resolved locally from
// the Recipe sheet since that's plain game data. Everything else (dungeon/raid drops,
// gathering, desynthesis, etc.) isn't covered yet and falls back to Unknown.
public static class AcquisitionSource
{
    private const uint FirstCraftingClassJobRow = 8;

    private static Dictionary<uint, uint>? _craftableItemToCraftType;

    private static Dictionary<uint, uint> CraftableItemToCraftType => _craftableItemToCraftType ??=
        Service.DalamudDataManager.GetExcelSheet<Recipe>()!
            .Where(recipe => recipe.ItemResult.RowId != 0)
            .GroupBy(recipe => recipe.ItemResult.RowId)
            .ToDictionary(g => g.Key, g => g.First().CraftType.RowId);

    public static AcquisitionSourceInfo Resolve(uint itemId)
    {
        if (ItemVendorLocationIpc.TryGetSource(itemId, out var vendors))
        {
            string? zoneName = null;
            MapLocation? location = null;
            if (vendors.Count > 0)
            {
                var (_, territoryId, (x, y)) = vendors.First();
                var territory = Service.DalamudDataManager.GetExcelSheet<TerritoryType>()!.GetRowOrDefault(territoryId);
                zoneName = territory?.PlaceName.ValueNullable?.Name.ExtractText();
                if (territory is not null)
                    location = new MapLocation(territoryId, territory.Value.Map.RowId, x, y);
            }
            return new AcquisitionSourceInfo { Kind = AcquisitionSourceKind.Vendor, ZoneName = zoneName, Location = location };
        }

        if (CraftableItemToCraftType.TryGetValue(itemId, out var craftTypeRow))
        {
            var job = Service.DalamudDataManager.GetExcelSheet<ClassJob>()!.GetRowOrDefault(craftTypeRow + FirstCraftingClassJobRow);
            return new AcquisitionSourceInfo { Kind = AcquisitionSourceKind.Craftable, JobAbbreviation = job?.Abbreviation.ExtractText() };
        }

        return new AcquisitionSourceInfo { Kind = AcquisitionSourceKind.Unknown };
    }
}
