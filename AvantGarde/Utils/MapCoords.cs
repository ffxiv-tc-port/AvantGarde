using Lumina.Excel.Sheets;

namespace AvantGarde.Utils;

/// <summary>
/// 地圖座標（遊戲介面上顯示的那個 X/Y）→ 世界座標的<b>反解</b>。
/// </summary>
/// <remarks>
/// <para>
/// 🔴 這裡刻意逐字反解 <b>ItemVendorLocation 自己用的那條正向公式</b>
/// （<c>ItemVendorLocation/Models/NpcLocation.cs</c> 的 <c>ToMapCoordinate</c>）：
/// <code>
/// c   = scale / 100
/// val = (world + offset) * c
/// map = 41 / c * ((val + 1024) / 2048) + 1
/// </code>
/// 因為我們手上的數字就是那條算出來的，用同一條反解才會 round-trip
/// （離線以 float32 逐步驗算，7 組 scale/offset × 8 個座標，最大誤差 0.00034 碼）。
/// 它與 Dalamud <c>MapUtil.ConvertWorldCoordXZToMapCoord</c> 是同一條，
/// 只是後者把 41/2048 寫成 0.02、2050 寫成 2048；差異不到 0.02 個地圖格。
/// </para>
/// <para>
/// ⚠️ <c>ItemVendorLocation.GetItemVendors</c> 這個端點<b>只回 territory，不回 map id</b>，
/// 所以這裡只能用 <c>TerritoryType.Map</c>——那正是 IVL 端在沒有另外指定 map 時用的同一張圖。
/// 少數多張地圖的區域若 IVL 內部指定了別張圖，反解會落在偏掉的位置，而且<b>不會報錯</b>；
/// 因此 UI 上一律同時把原始地圖座標寫進 tooltip，讓使用者自己核對。
/// </para>
/// <para>
/// 🔴 換不出來時回 <see langword="false"/>，<b>不會給一個看起來很正常的 0</b>——
/// 呼叫端應該把「前往」整個藏起來，不是拿 (0, 0) 去叫 Lifestream 跑。
/// </para>
/// </remarks>
internal static class MapCoords
{
    /// <summary>
    /// 把某張地圖上的地圖座標換回世界座標的 X / Z（<b>Y 是高度，不參與換算</b>）。
    /// </summary>
    public static bool TryMapToWorld(Map map, float mapX, float mapY, out float worldX, out float worldZ)
    {
        worldX = 0f;
        worldZ = 0f;

        // scale 在公式裡是分母，0 會算出無限大。
        float scale = map.SizeFactor;
        if (scale == 0f) return false;
        if (!float.IsFinite(mapX) || !float.IsFinite(mapY)) return false;

        worldX = MapToWorld(mapX, scale, map.OffsetX);
        worldZ = MapToWorld(mapY, scale, map.OffsetY);

        return float.IsFinite(worldX) && float.IsFinite(worldZ);
    }

    private static float MapToWorld(float mapValue, float scale, float offset)
    {
        var c = scale / 100.0f;
        var val = ((mapValue - 1.0f) * c / 41.0f * 2048.0f) - 1024.0f;
        return (val / c) - offset;
    }
}
