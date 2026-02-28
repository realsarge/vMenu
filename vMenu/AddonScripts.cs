using System;
using System.Threading.Tasks;

using CitizenFX.Core;

using MenuAPI;

using static CitizenFX.Core.Native.API;
using static vMenuClient.CommonFunctions;

namespace vMenuClient
{
    public class AddonScripts
    {
        private bool scriptLaunchInProgress;
        private Menu menu;

        private void CreateMenu()
        {
            menu = new Menu(MainMenu.GetCurrentMenuHeaderName(), "Scripts");

            var policeRadar = new MenuItem("Police Radar", "Open the police radar menu.");
            var heliWinch = new MenuItem("Heli Winch", "Open the helicopter winch menu.");
            var k9Menu = new MenuItem("K9 Dog", "Open the K9 dog menu.");
            var worldSpawner = new MenuItem("Spawner (World)", "Open the world-attached object spawner.");
            var vehicleSpawner = new MenuItem("Spawner (Vehicle)", "Open the vehicle-attached object spawner.");
            var deleteSpawnerObject = new MenuItem("Spawner (Delete Object)", "Delete the nearest spawned object.");
            var trafficMenu = new MenuItem("Traffic Control", "Open the traffic control menu.");

            menu.AddMenuItem(policeRadar);
            menu.AddMenuItem(heliWinch);
            menu.AddMenuItem(k9Menu);
            menu.AddMenuItem(worldSpawner);
            menu.AddMenuItem(vehicleSpawner);
            menu.AddMenuItem(deleteSpawnerObject);
            menu.AddMenuItem(trafficMenu);

            menu.OnItemSelect += async (sender, item, index) =>
            {
                if (item == policeRadar)
                {
                    await LaunchExternalMenuAsync(() => TriggerEvent("wk:openRemote"));
                }
                else if (item == heliWinch)
                {
                    await LaunchExternalMenuAsync(() => TriggerEvent("heliwinch:openmenu"));
                }
                else if (item == k9Menu)
                {
                    await LaunchExternalMenuAsync(() => TriggerEvent("k9Menu: OpenMenu"));
                }
                else if (item == worldSpawner)
                {
                    await LaunchExternalMenuAsync(() => ExecuteCommand("spawner w"),
                        "Управление наклоном: NumPad, положением: стрелки, установить объект: Space или Enter, выйти: Del. Пропы: https://gtahash.ru/");
                }
                else if (item == vehicleSpawner)
                {
                    await LaunchExternalMenuAsync(() => ExecuteCommand("spawner v"),
                        "Управление положением: NumPad, установить объект: Space или Enter, выйти: Del.");
                }
                else if (item == deleteSpawnerObject)
                {
                    ExecuteCommand("deleter");
                }
                else if (item == trafficMenu)
                {
                    await LaunchExternalMenuAsync(() => ExecuteCommand("traffic"),
                        "Управление: E, X, Del.");
                }
            };
        }

        private async Task LaunchExternalMenuAsync(Action openAction, string helpText = null)
        {
            if (scriptLaunchInProgress)
            {
                return;
            }

            scriptLaunchInProgress = true;
            try
            {
                MenuController.CloseAllMenus();

                for (var i = 0; i < 20 && MenuController.IsAnyMenuOpen(); i++)
                {
                    await BaseScript.Delay(0);
                }

                await BaseScript.Delay(150);
                openAction?.Invoke();

                if (!string.IsNullOrWhiteSpace(helpText))
                {
                    TriggerEvent("chat:addMessage", new
                    {
                        templateId = "ccChat",
                        multiline = false,
                        args = new[] { "#9c27b0", "fa-solid fa-m", "Меню", "", helpText, "1.0" }
                    });
                }
            }
            finally
            {
                await BaseScript.Delay(250);
                scriptLaunchInProgress = false;
            }
        }

        public Menu GetMenu()
        {
            if (menu == null)
            {
                CreateMenu();
            }

            return menu;
        }
    }
}
