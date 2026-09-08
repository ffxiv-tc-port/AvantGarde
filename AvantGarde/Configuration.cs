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
/// Dalamud 的 <c>PluginConfigurations.SerializeConfig</c> 只設了
/// <c>TypeNameAssemblyFormatHandling</c> 與 <c>TypeNameHandling</c>，
/// <b>沒有</b> <c>DefaultValueHandling</c> ⇒ <c>[DefaultValue]</c> 對序列化完全沒有作用，
/// 它只是文件註記。
///
/// 📌 反序列化那側同樣沒有設 <c>MissingMemberHandling</c>（走 Newtonsoft 預設的
/// <c>Ignore</c>），也沒有 <c>Populate</c>：
/// <list type="bullet">
/// <item>既有使用者的 JSON 裡<b>沒有</b>新欄位的鍵 ⇒ 反序列化不碰那個欄位 ⇒ 吃到欄位初始式。</item>
/// <item>所以「新欄位的初始式」＝「既有使用者更新後拿到的值」，
///       這正是我們要的：把初始式設成<b>現行行為</b>，更新不會改變任何人的體驗。</item>
/// <item>反過來說，日後想改變既有使用者的行為<b>不能靠改初始式</b>——他們的檔案裡已經有那個鍵了。</item>
/// </list>
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
    ///
    /// 📌 Lifestream 那側已經做過「這個區域到底能不能飛」的判斷
    /// （<c>TaskGotoDestination.IsFlyingUnlockedIn</c>：區域用途要是 1、要有風脈旗標組、
    /// 而且 <c>PlayerState.IsAetherCurrentZoneComplete</c> 為真），不能飛就自動退回地面路線，
    /// <b>不會因此失敗</b>。所以這個開關真正的用途是「我就是不想飛」，
    /// 而不是拿來繞過不可飛區域。
    /// </remarks>
    public bool FlyToVendor { get; set; } = true;

    public void Save() => Service.PluginInterface.SavePluginConfig(this);
}
