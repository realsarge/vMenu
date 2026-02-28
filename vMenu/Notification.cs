using System.Collections.Generic;
using System.Text;

using CitizenFX.Core;

using static CitizenFX.Core.Native.API;
using static CitizenFX.Core.UI.Screen;
using static vMenuClient.CommonFunctions;

namespace vMenuClient
{
    #region Error Templates
    /// <summary>
    /// List of error templates.
    /// </summary>
    public enum CommonErrors
    {
        NoVehicle,
        NeedToBeTheDriver,
        UnknownError,
        NotAllowed,
        InvalidModel,
        InvalidInput,
        InvalidSaveName,
        SaveNameAlreadyExists,
        CouldNotLoadSave,
        CouldNotLoad,
        PlayerNotFound,
        PedNotFound,
        WalkingStyleNotForMale,
        WalkingStyleNotForFemale,
        RightAlignedNotSupported,
    };

    /// <summary>
    /// Gets the formatted error message.
    /// </summary>
    public static class ErrorMessage
    {
        /// <summary>
        /// Returns the formatted error message for the specified error type.
        /// </summary>
        /// <param name="errorType">The error type.</param>
        /// <param name="placeholderValue">An optional string that will be replaced inside the error message (if applicable).</param>
        /// <returns>The error message.</returns>
        public static string Get(CommonErrors errorType, string placeholderValue = null)
        {
            var outputMessage = "";
            var placeholder = placeholderValue != null ? " " + placeholderValue : "";
            outputMessage = errorType switch
            {
                CommonErrors.NeedToBeTheDriver => "You need to be the driver of this vehicle.",
                CommonErrors.NoVehicle => $"You need to be inside a vehicle{placeholder}.",
                CommonErrors.NotAllowed => $"You are not allowed to{placeholder}, sorry.",
                CommonErrors.InvalidModel => $"This model~r~{placeholder} ~s~could not be found, are you sure it's valid?",
                CommonErrors.InvalidInput => $"The input~r~{placeholder} ~s~is invalid or you cancelled the action, please try again.",
                CommonErrors.InvalidSaveName => $"Saving failed because the provided save name~r~{placeholder} ~s~is invalid.",
                CommonErrors.SaveNameAlreadyExists => $"Saving failed because the provided save name~r~{placeholder} ~s~already exists.",
                CommonErrors.CouldNotLoadSave => $"Loading of~r~{placeholder} ~s~failed! Is the saves file corrupt?",
                CommonErrors.CouldNotLoad => $"Could not load~r~{placeholder}~s~, sorry!",
                CommonErrors.PedNotFound => $"The specified ped could not be found.{placeholder}",
                CommonErrors.PlayerNotFound => $"The specified player could not be found.{placeholder}",
                CommonErrors.WalkingStyleNotForMale => $"This walking style is not available for male peds.{placeholder}",
                CommonErrors.WalkingStyleNotForFemale => $"This walking style is not available for female peds.{placeholder}",
                CommonErrors.RightAlignedNotSupported => $"Right aligned menus are not supported for ultra wide aspect ratios.{placeholder}",
                _ => $"An unknown error occurred, sorry!{placeholder}",
            };
            return outputMessage;
        }
    }
    #endregion

    #region Notifications class
    /// <summary>
    /// Notifications class to easilly show messages using cc-chat.
    /// </summary>
    public static class Notify
    {
        private const string VMenuBlue = "#2a6bbe";

        private static readonly Dictionary<string, string> NotificationTranslations = new Dictionary<string, string>()
        {
            ["vMenu"] = "vMenu",
            ["Alert"] = "Внимание",
            ["Error"] = "Ошибка",
            ["Info"] = "Инфо",
            ["Success"] = "Успешно",
            ["Your category description has been changed."] = "Описание категории было изменено.",
            ["Stopped spectating."] = "Наблюдение остановлено.",
            ["Your settings have been saved."] = "Ваши настройки сохранены.",
            ["Fit applied."] = "Одежда применена.",
            ["Fit clipboard cleared."] = "Буфер одежды очищен.",
            ["Teleported to waypoint."] = "Телепортация к метке выполнена.",
            ["Attempting to re-join the session."] = "Попытка повторно подключиться к сессии.",
        };

        private static string StripFormatting(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(text.Length);
            var inTilde = false;
            var inTag = false;

            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];

                if (c == '~')
                {
                    inTilde = !inTilde;
                    continue;
                }

                if (inTilde)
                {
                    continue;
                }

                if (c == '<')
                {
                    inTag = true;
                    continue;
                }

                if (c == '>' && inTag)
                {
                    inTag = false;
                    continue;
                }

                if (inTag)
                {
                    continue;
                }

                if (c == '^' && i + 1 < text.Length && char.IsDigit(text[i + 1]))
                {
                    i++;
                    continue;
                }

                if (char.IsControl(c))
                {
                    if (c == '\n' || c == '\r' || c == '\t')
                    {
                        builder.Append(' ');
                    }
                    continue;
                }

                builder.Append(c);
            }

            return string.Join(" ", builder.ToString().Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries));
        }

        private static string TranslateNotification(string text)
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
            if (text.StartsWith(spectatingPrefix, System.StringComparison.Ordinal) && text.EndsWith(".", System.StringComparison.Ordinal))
            {
                var playerName = text.Substring(spectatingPrefix.Length).TrimEnd('.');
                return $"Вы теперь наблюдаете за {playerName}.";
            }

            return text;
        }

        private static void SendChatMessage(string title, string message, string color, string icon)
        {
            var cleanedTitle = TranslateNotification(StripFormatting(title));
            var cleanedMessage = TranslateNotification(StripFormatting(message));

            if (string.IsNullOrWhiteSpace(cleanedTitle))
            {
                cleanedTitle = "vMenu";
            }

            if (string.IsNullOrWhiteSpace(cleanedMessage))
            {
                return;
            }

            TriggerEvent("chat:addMessage", new
            {
                templateId = "ccChat",
                multiline = false,
                args = new[] { color, icon, cleanedTitle, "", cleanedMessage, "1.0" }
            });
        }

        public static void Custom(string message, bool blink = true, bool saveToBrief = true)
        {
            SendChatMessage("vMenu", message, VMenuBlue, "fa-solid fa-bars");
        }

        public static void Alert(string message, bool blink = true, bool saveToBrief = true)
        {
            SendChatMessage("Alert", message, VMenuBlue, "fa-solid fa-triangle-exclamation");
        }

        public static void Alert(CommonErrors errorMessage, bool blink = true, bool saveToBrief = true, string placeholderValue = null)
        {
            var message = ErrorMessage.Get(errorMessage, placeholderValue);
            Alert(message, blink, saveToBrief);
        }

        public static void Error(string message, bool blink = true, bool saveToBrief = true)
        {
            SendChatMessage("Error", message, VMenuBlue, "fa-solid fa-circle-exclamation");
            Debug.Write("[vMenu] [ERROR] " + message + "\n");
        }

        public static void Error(CommonErrors errorMessage, bool blink = true, bool saveToBrief = true, string placeholderValue = null)
        {
            var message = ErrorMessage.Get(errorMessage, placeholderValue);
            Error(message, blink, saveToBrief);
        }

        public static void Info(string message, bool blink = true, bool saveToBrief = true)
        {
            SendChatMessage("Info", message, VMenuBlue, "fa-solid fa-circle-info");
        }

        public static void Success(string message, bool blink = true, bool saveToBrief = true)
        {
            SendChatMessage("Success", message, VMenuBlue, "fa-solid fa-circle-check");
        }

        public static void CustomImage(string textureDict, string textureName, string message, string title, string subtitle, bool saveToBrief, int iconType = 0)
        {
            var chatTitle = !string.IsNullOrWhiteSpace(subtitle) ? subtitle : title;
            SendChatMessage(chatTitle, message, VMenuBlue, "fa-solid fa-envelope");
        }
    }
    #endregion

    #region Custom Subtitle class
    /// <summary>
    /// Custom Subtitle class used to display subtitles using preformatted templates.
    /// Optionally you can also use a blank/custom style if you don't want to use an existing template.
    /// </summary>
    public static class Subtitle
    {
        /// <summary>
        /// Custom (white/custom text style subtitle)
        /// </summary>
        /// <param name="message">The message to be displayed.</param>
        /// <param name="duration">(Optional) duration in ms.</param>
        /// <param name="drawImmediately">(Optional) draw the notification immediately or wait for the previous subtitle text to disappear.</param>
        public static void Custom(string message, int duration = 2500, bool drawImmediately = true)
        {
            BeginTextCommandPrint("CELL_EMAIL_BCON"); // 10x ~a~
            foreach (var s in CitizenFX.Core.UI.Screen.StringToArray(message))
            {
                AddTextComponentSubstringPlayerName(s);
            }
            EndTextCommandPrint(duration, drawImmediately);
        }

        /// <summary>
        /// Alert (yellow text subtitle).
        /// </summary>
        /// <param name="message">The message to be displayed.</param>
        /// <param name="duration">(Optional) duration in ms.</param>
        /// <param name="drawImmediately">(Optional) draw the notification immediately or wait for the previous subtitle text to disappear.</param>
        /// <param name="prefix">(Optional) add a prefix to your message, if you use this, only the prefix will be colored. The rest of the message will be left white.</param>
        public static void Alert(string message, int duration = 2500, bool drawImmediately = true, string prefix = null)
        {
            Custom((prefix != null ? "~y~" + prefix + " ~s~" : "~y~") + message, duration, drawImmediately);
        }

        /// <summary>
        /// Error (red text subtitle).
        /// </summary>
        /// <param name="message">The message to be displayed.</param>
        /// <param name="duration">(Optional) duration in ms.</param>
        /// <param name="drawImmediately">(Optional) draw the notification immediately or wait for the previous subtitle text to disappear.</param>
        /// <param name="prefix">(Optional) add a prefix to your message, if you use this, only the prefix will be colored. The rest of the message will be left white.</param>
        public static void Error(string message, int duration = 2500, bool drawImmediately = true, string prefix = null)
        {
            Custom((prefix != null ? "~r~" + prefix + " ~s~" : "~r~") + message, duration, drawImmediately);
        }

        /// <summary>
        /// Info (blue text subtitle).
        /// </summary>
        /// <param name="message">The message to be displayed.</param>
        /// <param name="duration">(Optional) duration in ms.</param>
        /// <param name="drawImmediately">(Optional) draw the notification immediately or wait for the previous subtitle text to disappear.</param>
        /// <param name="prefix">(Optional) add a prefix to your message, if you use this, only the prefix will be colored. The rest of the message will be left white.</param>
        public static void Info(string message, int duration = 2500, bool drawImmediately = true, string prefix = null)
        {
            Custom((prefix != null ? "~b~" + prefix + " ~s~" : "~b~") + message, duration, drawImmediately);
        }

        /// <summary>
        /// Success (green text subtitle).
        /// </summary>
        /// <param name="message">The message to be displayed.</param>
        /// <param name="duration">(Optional) duration in ms.</param>
        /// <param name="drawImmediately">(Optional) draw the notification immediately or wait for the previous subtitle text to disappear.</param>
        /// <param name="prefix">(Optional) add a prefix to your message, if you use this, only the prefix will be colored. The rest of the message will be left white.</param>
        public static void Success(string message, int duration = 2500, bool drawImmediately = true, string prefix = null)
        {
            Custom((prefix != null ? "~g~" + prefix + " ~s~" : "~g~") + message, duration, drawImmediately);
        }
    }
    #endregion

    public static class HelpMessage
    {


        public enum Label
        {
            EXIT_INTERIOR_HELP_MESSAGE
        }

        private static readonly Dictionary<Label, KeyValuePair<string, string>> labels = new()
        {
            [Label.EXIT_INTERIOR_HELP_MESSAGE] = new KeyValuePair<string, string>("EXIT_INTERIOR_HELP_MESSAGE", "Press ~INPUT_CONTEXT~ to exit the building.")
        };



        public static void Custom(string message) => Custom(message, 6000, true);
        public static void Custom(string message, int duration) => Custom(message, duration, true);
        public static void Custom(string message, int duration, bool sound)
        {
            var array = CommonFunctions.StringToArray(message);
            if (IsHelpMessageBeingDisplayed())
            {
                ClearAllHelpMessages();
            }
            BeginTextCommandDisplayHelp("CELL_EMAIL_BCON");
            foreach (var s in array)
            {
                AddTextComponentSubstringPlayerName(s);
            }
            EndTextCommandDisplayHelp(0, false, sound, duration);
        }

        public static void CustomLooped(Label label)
        {
            if (GetLabelText(labels[label].Key) == "NULL")
            {
                AddTextEntry(labels[label].Key, labels[label].Value);
            }
            //string[] array = CommonFunctions.StringToArray(message);
            //if (IsHelpMessageBeingDisplayed())
            //{
            //    ClearAllHelpMessages();
            //}
            //BeginTextCommandDisplayHelp("CELL_EMAIL_BCON");
            //foreach (string s in array)
            //{
            //    AddTextComponentSubstringPlayerName(s);
            //}
            DisplayHelpTextThisFrame(labels[label].Key, true);
            //EndTextCommandDisplayHelp(0, true, false, -1);
        }
    }
}
