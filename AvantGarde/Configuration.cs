using System;
using Dalamud.Configuration;

namespace AvantGarde;

/// <summary>
/// Avant-Garde 的使用者設定。走 Dalamud 標準的
/// <see cref="Dalamud.Plugin.IDalamudPluginInterface.GetPluginConfig"/> /
/// <see cref="Dalamud.Plugin.IDalamudPluginInterface.SavePluginConfig"/>，
/// 檔案落在 <c>pluginConfigs/AvantGarde.json</c>。
/// </summary>
/// <remarks>
/// 🔴 <b>預設值一律寫成欄位初始式，不要寫 <c>[DefaultValue(...)]</c>。</b>
/// Dalamud 存檔時沒有設 <c>DefaultValueHandling</c> ⇒ <c>[DefaultValue]</c> 對序列化完全沒有作用。
/// 📌 反序列化那側走 Newtonsoft 預設：既有使用者的 JSON 裡<b>沒有</b>新欄位的鍵 ⇒ 不碰那個欄位 ⇒ 吃到欄位初始式。
/// 所以「新欄位的初始式」＝「既有使用者更新後拿到的值」：把初始式設成<b>現行行為</b>，更新不會改變任何人的體驗。
/// 反過來說，日後想改變既有使用者的行為<b>不能靠改初始式</b>——他們的檔案裡已經有那個鍵了。
/// </remarks>
[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    /// <summary>
    /// 按下裝備列上的「前往」時，允不允許 Lifestream 用飛行坐騎跑最後一段。
    /// </summary>
    /// <remarks>
    /// 預設 <see langword="true"/>＝<b>維持這個功能上線時的行為</b>（原本是寫死 <c>fly: true</c>）。
    /// 關掉之後一律走地面路線。
    /// 📌 Lifestream 那側會自己判斷「這個區域到底能不能飛」，不能飛就自動退回地面路線，
    /// <b>不會因此失敗</b>。所以這個開關真正的用途是「我就是不想飛」，
    /// 而不是拿來繞過不可飛區域。
    /// </remarks>
    public bool FlyToVendor { get; set; } = true;

    public void Save() => Service.PluginInterface.SavePluginConfig(this);
}
