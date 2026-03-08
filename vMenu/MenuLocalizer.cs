using System;
using System.Collections.Generic;
using System.Linq;

using MenuAPI;

using Newtonsoft.Json;

namespace vMenuClient
{
    internal static class MenuLocalizer
    {
        internal sealed class LocalizationConfigFile
        {
            public Dictionary<string, string> menu { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
            public Dictionary<string, string> notifications { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        private const string ConfirmationLabelText = "Are you sure?";
        private const string DangerPrefix = "~r~";
        private static readonly Dictionary<string, string> MenuTranslations = new(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> NotificationTranslations = new(StringComparer.Ordinal);

        internal static void SetTranslations(string jsonData)
        {
            MenuTranslations.Clear();
            NotificationTranslations.Clear();

            if (string.IsNullOrWhiteSpace(jsonData))
            {
                return;
            }

            try
            {
                var config = JsonConvert.DeserializeObject<LocalizationConfigFile>(jsonData) ?? new LocalizationConfigFile();
                ReplaceTranslations(MenuTranslations, config.menu);
                ReplaceTranslations(NotificationTranslations, config.notifications);
            }
            catch (JsonException)
            {
            }
        }

        private static void ReplaceTranslations(Dictionary<string, string> target, Dictionary<string, string> source)
        {
            if (source == null)
            {
                return;
            }

            foreach (var entry in source)
            {
                if (string.IsNullOrWhiteSpace(entry.Key) || string.IsNullOrWhiteSpace(entry.Value))
                {
                    continue;
                }

                target[entry.Key] = entry.Value;
            }
        }

        internal static void LocalizeAllMenus()
        {
            foreach (var menu in MenuController.Menus)
            {
                LocalizeMenu(menu);
            }
        }

        internal static void LocalizeMenuInstance(Menu menu)
        {
            LocalizeMenu(menu);
        }

        private static void LocalizeMenu(Menu menu)
        {
            if (menu == null)
            {
                return;
            }

            var menuTitle = menu.MenuTitle;
            var menuSubtitle = menu.MenuSubtitle;
            var translatedMenuTitle = TranslateMenuText(menuTitle);
            var translatedMenuSubtitle = TranslateMenuText(menuSubtitle);
            var keepDescriptions =
                IsMpCharacterMenu(menuTitle) ||
                IsMpCharacterMenu(menuSubtitle) ||
                IsMpCharacterMenu(translatedMenuTitle) ||
                IsMpCharacterMenu(translatedMenuSubtitle);

            menu.MenuTitle = translatedMenuTitle;
            menu.MenuSubtitle = translatedMenuSubtitle;

            foreach (var item in menu.GetMenuItems())
            {
                item.Text = TranslateMenuText(item.Text);
                item.Label = TranslateLabel(item.Label);
                if (!keepDescriptions)
                {
                    item.Description = string.Empty;
                }

                if (item is MenuListItem listItem)
                {
                    for (var i = 0; i < listItem.ListItems.Count; i++)
                    {
                        listItem.ListItems[i] = TranslateMenuText(listItem.ListItems[i]);
                    }
                }
            }
        }

        internal static string TranslateNotificationText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            if (NotificationTranslations.TryGetValue(text, out var translated))
            {
                return translated;
            }

            const string spectatingPrefix = "You are now spectating ";
            if (text.StartsWith(spectatingPrefix, StringComparison.Ordinal) && text.EndsWith(".", StringComparison.Ordinal))
            {
                var playerName = text.Substring(spectatingPrefix.Length).TrimEnd('.');
                return $"Р’С‹ С‚РµРїРµСЂСЊ РЅР°Р±Р»СЋРґР°РµС‚Рµ Р·Р° {playerName}.";
            }

            return text;
        }

        internal static string GetConfirmationLabel(bool danger = false)
        {
            var translated = TranslateMenuText(ConfirmationLabelText);
            return danger ? $"{DangerPrefix}{translated}" : translated;
        }

        internal static bool IsConfirmationLabel(string text, bool danger = false)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            var original = danger ? $"{DangerPrefix}{ConfirmationLabelText}" : ConfirmationLabelText;
            return string.Equals(text, original, StringComparison.Ordinal) ||
                   string.Equals(text, GetConfirmationLabel(danger), StringComparison.Ordinal);
        }

        internal static string TranslateMenuText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            if (MenuTranslations.TryGetValue(text, out var translated))
            {
                return translated;
            }

            if (text.StartsWith("Voice Chat Proximity (", StringComparison.Ordinal))
            {
                return "Р”Р°Р»СЊРЅРѕСЃС‚СЊ РіРѕР»РѕСЃРѕРІРѕРіРѕ С‡Р°С‚Р° (" + text.Substring("Voice Chat Proximity (".Length);
            }

            return text;
        }

        private static string TranslateLabel(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            if (string.Equals(text, ConfirmationLabelText, StringComparison.Ordinal))
            {
                return GetConfirmationLabel();
            }

            if (string.Equals(text, $"{DangerPrefix}{ConfirmationLabelText}", StringComparison.Ordinal))
            {
                return GetConfirmationLabel(true);
            }

            return text;
        }

        private static bool IsMpCharacterMenu(string text)
        {
            return MatchesMenuName(text, "MP Ped Customization")
                   || MatchesMenuName(text, "Create A New Character")
                   || MatchesMenuName(text, "Manage Saved Characters")
                   || MatchesMenuName(text, "Character Inheritance Options")
                   || MatchesMenuName(text, "Character Appearance Options")
                   || MatchesMenuName(text, "Character Face Shape Options")
                   || MatchesMenuName(text, "Character Tattoo Options")
                   || MatchesMenuName(text, "Character Clothing Options")
                   || MatchesMenuName(text, "Character Props Options")
                   || MatchesMenuName(text, "Manage MP Character")
                   || MatchesMenuName(text, "I get updated at runtime!")
                   || MatchesMenuName(text, "Outfit Presets");
        }

        private static bool MatchesMenuName(string text, string englishName)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            return string.Equals(text, englishName, StringComparison.Ordinal)
                   || string.Equals(text, TranslateMenuText(englishName), StringComparison.Ordinal);
        }
    }
}
