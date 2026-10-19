using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

using AvantGarde.Data;

namespace AvantGarde;

internal sealed class Service
{
    [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] public static IGameGui GameGui { get; private set; } = null!;
    [PluginService] public static IDataManager DalamudDataManager { get; private set; } = null!;
    [PluginService] public static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] public static IPluginLog PluginLog { get; private set; } = null!;
    [PluginService] public static IClientState ClientState { get; private set; } = null!;

    public static DataManager DataManager { get; set; } = null!;

    /// <summary>
    /// 使用者設定。由 <see cref="Plugin"/> 的建構子在 <c>Create&lt;Service&gt;()</c> 之後立刻填上，
    /// 所以任何繪製路徑走到這裡時它一定不是 null。
    /// </summary>
    public static Configuration Config { get; set; } = null!;

    /// <summary>
    /// 設定視窗。<see cref="UI.SlotWindow"/> 標題列的齒輪要靠它開窗，
    /// 而那裡是靜態繪製路徑，所以擺在這裡當單一入口。
    /// </summary>
    public static UI.ConfigWindow ConfigWindow { get; set; } = null!;

    public Service()
    {
        DataManager = new DataManager();
    }
}
