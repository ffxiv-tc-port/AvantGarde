using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Lumina.Excel.Sheets;

using AvantGarde.Ipc;
using AvantGarde.Utils;

namespace AvantGarde.Data;

/// <summary>
/// 裝備清單「那一列」右下角要顯示的取得方式。刻意只有四種狀態——
/// 詳細的來源分類（成就／任務／部族／點數交換…）留給彈出視窗與 ItemVendorLocation 自己。
/// </summary>
public enum AcquisitionHintKind
{
    /// <summary>查不到已知來源，而且這件也上不了市場。列上畫灰色的 <c>?</c>。</summary>
    Unknown,

    /// <summary>ItemVendorLocation 沒裝／沒載入 ⇒ 這一欄是「查不到」，<b>不是</b>「沒有」。</summary>
    IvlUnavailable,

    /// <summary>NPC 買得到（含交換所、部族商店等 ItemVendorLocation 認得的來源）。</summary>
    Vendor,

    /// <summary>沒有已知的 NPC 來源，但這件可以掛上市場 ⇒ 市場買得到。</summary>
    Market,
}

/// <summary>某一件道具的取得方式；由 <see cref="AcquisitionHints"/> 查一次之後快取起來。</summary>
public readonly struct AcquisitionHint
{
    public required AcquisitionHintKind Kind { get; init; }

    /// <summary>商人名字（<c>ENpcResident.Singular</c>）。查不到時為 <see langword="null"/>。</summary>
    public string? VendorName { get; init; }

    /// <summary>商人所在區域的地名。位置不明時為 <see langword="null"/>。</summary>
    public string? PlaceName { get; init; }

    /// <summary>
    /// 商店名稱（例如「雜貨商人」「詩學神典石交換」）。查不到時為 <see langword="null"/>。
    /// </summary>
    /// <remarks>
    /// 📌 只有新的 <c>GetItemVendorsWorld</c> 端點給得出來；落回舊端點時一律是
    /// <see langword="null"/>。<b>可能含換行</b>（ItemVendorLocation 會把上層選單名和
    /// 店名疊成兩行），所以只放 tooltip，不要放在列上。
    /// </remarks>
    public string? ShopName { get; init; }

    /// <summary>
    /// <see cref="MapX"/>／<see cref="MapY"/> 是不是真的算得出來。
    /// </summary>
    /// <remarks>
    /// 🔴 <see langword="false"/> 時那兩個欄位是 0，<b>不可以當座標顯示</b>——
    /// 畫成 (0.0, 0.0) 會被讀成「商人在地圖左上角」。
    /// </remarks>
    public bool MapCoordinatesKnown { get; init; }

    /// <summary>目標區域的 <c>TerritoryType</c> 列號；0 代表 IVL 認得這件道具但不知道商人在哪。</summary>
    public uint TerritoryId { get; init; }

    /// <summary>ItemVendorLocation 回報的原始<b>地圖</b>座標，只拿來顯示給使用者核對。</summary>
    public float MapX { get; init; }

    /// <summary>ItemVendorLocation 回報的原始<b>地圖</b>座標，只拿來顯示給使用者核對。</summary>
    public float MapY { get; init; }

    /// <summary>
    /// 地圖座標有沒有成功反解成世界座標。<b>false 時「前往」必須整個藏起來</b>——
    /// 拿一個算不出來的 0 去叫 Lifestream 跑，會把人送到地圖角落。
    /// </summary>
    public bool HasWorldPosition { get; init; }

    public float WorldX { get; init; }

    public float WorldZ { get; init; }

    /// <summary>這件同時也掛得上市場（<see cref="AcquisitionHintKind.Vendor"/> 時才有意義）。</summary>
    public bool AlsoOnMarket { get; init; }
}

/// <summary>
/// 「這件沒有的裝備要去哪裡拿」的查詢與快取。
/// </summary>
/// <remarks>
/// <para>
/// 🔴 <b>只在視窗開啟的那一下查，每幀不查。</b><see cref="Prefetch"/> 由
/// <c>SlotWindow.Update</c>（＝使用者點了槽位按鈕）呼叫一次，把整份清單解析完丟進快取；
/// 繪製迴圈只會走 <see cref="Get"/>，那是純字典查詢，不碰 IPC。
/// </para>
/// <para>
/// 📌 快取與旗標只有<b>繪製執行緒</b>會碰（<c>UiBuilder.Draw</c> → <c>MainWindow.Draw</c>
/// → <c>SlotWindow</c>），本外掛沒有對外註冊任何 IPC 端點，也沒有背景執行緒，
/// 所以裸 <see cref="Dictionary{TKey, TValue}"/> 在這裡是安全的。
/// <b>日後若真的加了對外端點或背景工作，這張表要先上鎖再說。</b>
/// </para>
/// <para>
/// ⚠️ ItemVendorLocation 沒載入時<b>不寫快取</b>：否則使用者稍後才把它打開，
/// 這一整個 session 都會停在「需要 ItemVendorLocation」而且看不出原因。
/// </para>
/// </remarks>
public static class AcquisitionHints
{
    private static readonly Dictionary<uint, AcquisitionHint> Cache = [];

    private static bool _ivlAvailable;
    private static bool _lifestreamAvailable;

    /// <summary>上一輪 <see cref="Prefetch"/> 有沒有真的用到新端點（只拿來寫診斷）。</summary>
    private static bool _usedWorldEndpoint;

    /// <summary>上一次 <see cref="Prefetch"/> 當下 ItemVendorLocation 在不在。</summary>
    public static bool IvlAvailable => _ivlAvailable;

    /// <summary>上一次 <see cref="Prefetch"/> 當下 Lifestream 在不在（決定要不要畫「前往」）。</summary>
    public static bool LifestreamAvailable => _lifestreamAvailable;

    /// <summary>
    /// 視窗開啟時呼叫一次，把清單裡還沒查過的道具一次解析完。
    /// </summary>
    public static void Prefetch(IReadOnlyList<Item> items)
    {
        _ivlAvailable = ItemVendorLocationIpc.IsAvailable;
        _lifestreamAvailable = LifestreamIpc.IsAvailable;

        // ⚠️ 必須每輪重探：使用者可能在兩次開窗之間更新或重載 ItemVendorLocation，
        //    沿用上一輪的結論會讓新端點永遠不被採用，而且完全看不出原因。
        ItemVendorLocationIpc.ResetEndpointProbe();
        _usedWorldEndpoint = false;

        if (!_ivlAvailable) return;

        var stopwatch = Stopwatch.StartNew();
        int vendor = 0, market = 0, unknown = 0;

        foreach (var item in items)
        {
            if (Cache.ContainsKey(item.RowId)) continue;

            var hint = Resolve(item);
            Cache[item.RowId] = hint;

            switch (hint.Kind)
            {
                case AcquisitionHintKind.Vendor: vendor++; break;
                case AcquisitionHintKind.Market: market++; break;
                default: unknown++; break;
            }
        }

        stopwatch.Stop();

        var resolved = vendor + market + unknown;
        if (resolved > 0)
        {
            Service.PluginLog.Information(
                $"[AvantGarde] 取得方式查詢：新解析 {resolved} 件（NPC {vendor}／市場 {market}／未知 {unknown}），" +
                $"清單共 {items.Count} 件、快取累計 {Cache.Count} 件，耗時 {stopwatch.Elapsed.TotalMilliseconds:F1} ms，" +
                $"來源＝{(_usedWorldEndpoint ? "GetItemVendorsWorld（世界座標，精確）" : "GetItemVendors（地圖座標反解）")}。");
        }
    }

    /// <summary>繪製迴圈用的讀取端：純字典查詢，絕不在這裡補打 IPC。</summary>
    public static AcquisitionHint Get(Item item)
    {
        if (!_ivlAvailable)
            return new AcquisitionHint { Kind = AcquisitionHintKind.IvlUnavailable };

        return Cache.TryGetValue(item.RowId, out var hint)
            ? hint
            : new AcquisitionHint { Kind = AcquisitionHintKind.Unknown };
    }

    private static AcquisitionHint Resolve(Item item)
    {
        var onMarket = IsOnMarketBoard(item);

        // 🔑 新端點優先。它直接給世界座標（ItemVendorLocation 從 LGB 讀來的原始值），
        //    我們這邊完全不必反解地圖座標 ⇒ 一個區域有好幾張圖時不會偏掉。
        //    回 true 代表對方回答了，答案是「沒有商人」時也算回答過，不可以再退去問舊端點。
        if (ItemVendorLocationIpc.TryGetVendorsWorld(item.RowId, out var worldVendors))
        {
            _usedWorldEndpoint = true;

            return worldVendors.Count > 0
                ? ResolveVendorWorld(worldVendors, onMarket)
                : new AcquisitionHint
                {
                    Kind = onMarket ? AcquisitionHintKind.Market : AcquisitionHintKind.Unknown,
                };
        }

        // 退路：對方版本較舊，沒有新端點。座標只能自己從地圖座標反解回世界座標。
        // filterNoLocation: false ⇒ 連「知道是誰賣的、但不知道他站哪」的也拿回來，
        // 這樣至少還能在列上寫出商人名字，而不是整件退成「未知」。
        if (ItemVendorLocationIpc.TryGetSource(item.RowId, out var vendors, filterNoLocation: false) && vendors.Count > 0)
            return ResolveVendor(vendors, onMarket);

        return new AcquisitionHint
        {
            Kind = onMarket ? AcquisitionHintKind.Market : AcquisitionHintKind.Unknown,
        };
    }

    /// <summary>
    /// 新端點（<c>GetItemVendorsWorld</c>）的結果 → 一列要顯示的東西。
    /// </summary>
    /// <remarks>
    /// 🔑 與 <see cref="ResolveVendor"/> 最大的差別是<b>世界座標不必反解</b>：
    /// ItemVendorLocation 手上本來就有 LGB 讀出來的原始 X/Z，直接送給 Lifestream 即可。
    /// 舊路徑是拿地圖座標去套 <c>TerritoryType.Map</c> 的 SizeFactor/Offset 反算，
    /// 碰到一個區域好幾張圖的地方會落在偏掉的位置而且不報錯。
    /// </remarks>
    private static AcquisitionHint ResolveVendorWorld(List<VendorLocationInfo> vendors, bool onMarket)
    {
        // 🔴 回傳順序沒有保證。不固定挑選規則的話，同一件道具每次開視窗可能顯示不同的商人，
        //    使用者會以為資料在跳。規則與舊路徑一致：有位置的優先，再取 npcId 最小的。
        var best = vendors.Where(v => v.HasLocation && v.TerritoryTypeId != 0).OrderBy(v => v.NpcId).FirstOrDefault()
                   ?? vendors.OrderBy(v => v.NpcId).First();

        var hasLocation = best.HasLocation && best.TerritoryTypeId != 0;

        string? placeName = null;
        if (hasLocation)
        {
            var territory = Service.DalamudDataManager.GetExcelSheet<TerritoryType>()!.GetRowOrDefault(best.TerritoryTypeId);
            if (territory is not null)
                placeName = territory.Value.PlaceName.ValueNullable?.Name.ExtractText();
        }

        // 對方給得出名字就用對方的（同一份客戶端語言），給不出來才自己查表。
        var vendorName = string.IsNullOrWhiteSpace(best.NpcName) ? ResolveNpcName(best.NpcId) : best.NpcName;

        return new AcquisitionHint
        {
            Kind = AcquisitionHintKind.Vendor,
            VendorName = string.IsNullOrWhiteSpace(vendorName) ? null : vendorName,
            PlaceName = string.IsNullOrWhiteSpace(placeName) ? null : placeName,
            ShopName = string.IsNullOrWhiteSpace(best.ShopName) ? null : best.ShopName,
            TerritoryId = hasLocation ? best.TerritoryTypeId : 0u,
            MapX = best.MapX,
            MapY = best.MapY,
            MapCoordinatesKnown = hasLocation && best.MapCoordinatesKnown,
            HasWorldPosition = hasLocation,
            WorldX = best.WorldX,
            WorldZ = best.WorldZ,
            AlsoOnMarket = onMarket,
        };
    }

    private static AcquisitionHint ResolveVendor(
        HashSet<(uint npcId, uint territory, (float x, float y))> vendors, bool onMarket)
    {
        // 🔴 HashSet 的列舉順序沒有保證。不排序的話同一件道具每次開視窗可能顯示不同的商人，
        //    使用者會以為資料在跳。固定挑「有位置的、npcId 最小的那一個」。
        var located = vendors.Where(v => v.territory != 0).OrderBy(v => v.npcId).ToList();
        var hasLocation = located.Count > 0;
        var best = hasLocation ? located[0] : vendors.OrderBy(v => v.npcId).First();

        string? placeName = null;
        float worldX = 0f, worldZ = 0f;
        var hasWorld = false;

        if (hasLocation)
        {
            var territory = Service.DalamudDataManager.GetExcelSheet<TerritoryType>()!.GetRowOrDefault(best.territory);
            if (territory is not null)
            {
                placeName = territory.Value.PlaceName.ValueNullable?.Name.ExtractText();

                var map = territory.Value.Map.ValueNullable;
                if (map is not null)
                    hasWorld = MapCoords.TryMapToWorld(map.Value, best.Item3.x, best.Item3.y, out worldX, out worldZ);
            }
        }

        var vendorName = ResolveNpcName(best.npcId);

        return new AcquisitionHint
        {
            Kind = AcquisitionHintKind.Vendor,
            VendorName = string.IsNullOrWhiteSpace(vendorName) ? null : vendorName,
            PlaceName = string.IsNullOrWhiteSpace(placeName) ? null : placeName,
            TerritoryId = hasLocation ? best.territory : 0u,
            MapX = hasLocation ? best.Item3.x : 0f,
            MapY = hasLocation ? best.Item3.y : 0f,
            // 舊端點沒有商店名這個東西；tooltip 那邊會自動略過整行。
            ShopName = null,
            // 舊端點的地圖座標是 ItemVendorLocation 那邊算好才送過來的，
            // 所以「有位置」就等於「座標算得出來」。
            MapCoordinatesKnown = hasLocation,
            HasWorldPosition = hasWorld,
            WorldX = worldX,
            WorldZ = worldZ,
            AlsoOnMarket = onMarket,
        };
    }

    /// <summary>
    /// <c>ItemVendorLocation.GetItemVendors</c> 回的 npcId 是 <c>ENpcResident</c> 的列號
    /// （IVL 端就是拿 <c>ENpcBase.RowId</c> 去查 <c>ENpcResident</c> 的）。
    /// </summary>
    private static string? ResolveNpcName(uint npcId)
    {
        if (npcId == 0) return null;

        var resident = Service.DalamudDataManager.GetExcelSheet<ENpcResident>()!.GetRowOrDefault(npcId);
        return resident?.Singular.ExtractText();
    }

    /// <summary>
    /// 市場的判準：有搜尋分類（＝上得了市場的那張表）而且不是「無法交易」。
    /// <b>刻意不查價</b>——PriceInsight 沒有 IPC，而且查價會變成每列一次網路請求。
    /// </summary>
    private static bool IsOnMarketBoard(Item item)
        => item.ItemSearchCategory.RowId != 0 && !item.IsUntradable;
}
