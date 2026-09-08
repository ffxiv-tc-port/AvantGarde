using System;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using FFXIVClientStructs.FFXIV.Component.GUI;

using AvantGarde.UI;

namespace AvantGarde
{
    public sealed class Plugin : IDalamudPlugin
    {
        private readonly WindowSystem _windowSystem = new("AvantGarde");
        private readonly ConfigWindow _configWindow;
        private MainWindow _mainWindow;

        public Plugin(IDalamudPluginInterface pluginInterface)
        {
            pluginInterface.Create<Service>();

            // 設定與在地化互不相依，但兩者都必須排在 Create<Service>() 之後
            // （都會用到 Service.PluginInterface／PluginLog）。
            // 📌 使用者的設定檔裡沒有某個鍵時，Newtonsoft 不會去碰那個欄位
            //    （Dalamud 的 DeserializeConfig 沒有設 Populate，MissingMemberHandling
            //    也是預設的 Ignore）⇒ 該欄位保持 Configuration 裡寫的初始式。
            //    ?? new() 只在「第一次安裝」或「舊檔反序列化不出來」時走到。
            var loaded = pluginInterface.GetPluginConfig() as Configuration;
            Service.Config = loaded ?? new Configuration();

            // 使用者回報用的診斷：設定到底是「讀到了」還是「退回預設值」在畫面上看不出來，
            // 但兩者的行為完全一樣（預設值＝現行行為），所以這是唯一分辨得出來的地方。
            Service.PluginLog.Information(
                $"[AvantGarde] 設定載入完成（{(loaded is null ? "沒有既有設定檔，使用預設值" : "讀自既有設定檔")}）："
                + $"前往商人時允許飛行={Service.Config.FlyToVendor}。");

            Localization.Init(pluginInterface.AssemblyLocation.DirectoryName);

            _configWindow = new ConfigWindow();
            _windowSystem.AddWindow(_configWindow);
            Service.ConfigWindow = _configWindow;

            _mainWindow = new();

            Service.PluginInterface.UiBuilder.Draw += this.DrawUI;
            Service.PluginInterface.UiBuilder.OpenConfigUi += this.OpenConfigUI;
        }

        public void Dispose()
        {
            Service.PluginInterface.UiBuilder.OpenConfigUi -= this.OpenConfigUI;
            Service.PluginInterface.UiBuilder.Draw -= this.DrawUI;
            _windowSystem.RemoveAllWindows();
        }

        private void OpenConfigUI() => _configWindow.IsOpen = true;

        private unsafe void DrawUI()
        {
            // 🔴 設定視窗必須畫在時尚品鑑的判斷「之外」：使用者從插件安裝器的齒輪進來時，
            //    FashionCheck 這個原生視窗多半是關著的，畫在 return 之後等於永遠打不開。
            // ⚠️ WindowSystem.Draw() 不可以在外掛這一側包 try/catch——ImGui 的堆疊沒有
            //    finally 可以還原。它內部對每個視窗自己有防護。
            _windowSystem.Draw();

            var addon = Service.GameGui.GetAddonByName("FashionCheck");
            if (addon != IntPtr.Zero)
            {
                var baseNode = (AtkUnitBase*)addon.Address;
                if (baseNode->RootNode != null && baseNode->RootNode->IsVisible())
                {
                    _mainWindow.Draw(baseNode);
                }
            }
        }
    }
}
