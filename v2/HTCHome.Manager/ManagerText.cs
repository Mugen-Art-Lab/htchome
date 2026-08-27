using System;
using System.Globalization;

namespace HTCHome.Manager
{
    internal static class ManagerText
    {
        private static bool russian;

        public static string Language { get { return russian ? "ru-RU" : "en-US"; } }

        public static void SetLanguage(string language)
        {
            russian = !string.IsNullOrWhiteSpace(language) &&
                      language.StartsWith("ru", StringComparison.OrdinalIgnoreCase);
        }

        public static string DetectLanguage()
        {
            return string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "ru", StringComparison.OrdinalIgnoreCase)
                ? "ru-RU"
                : "en-US";
        }

        public static string WindowTitle { get { return "HTC Home Mugen Manager"; } }
        public static string Header { get { return "HTC Home Mugen Manager"; } }
        public static string Subtitle { get { return russian ? "Несколько независимых экземпляров HTC Home из одной папки" : "Multiple independent HTC Home instances from one folder"; } }
        public static string LanguageLabel { get { return russian ? "Язык:" : "Language:"; } }
        public static string NameHeader { get { return russian ? "Имя" : "Name"; } }
        public static string StatusHeader { get { return russian ? "Статус" : "Status"; } }
        public static string Add { get { return russian ? "+ Добавить" : "+ Add"; } }
        public static string Rename { get { return russian ? "Переименовать" : "Rename"; } }
        public static string Start { get { return russian ? "Запустить" : "Start"; } }
        public static string Stop { get { return russian ? "Остановить" : "Stop"; } }
        public static string Delete { get { return russian ? "Удалить" : "Delete"; } }
        public static string Running { get { return russian ? "Запущен" : "Running"; } }
        public static string Stopped { get { return russian ? "Остановлен" : "Stopped"; } }
        public static string NewInstancePrompt { get { return russian ? "Имя нового экземпляра:" : "New instance name:"; } }
        public static string NewInstanceDefault { get { return russian ? "Новый экземпляр" : "New instance"; } }
        public static string RenamePrompt { get { return russian ? "Новое имя экземпляра:" : "New instance name:"; } }
        public static string StopBeforeDelete { get { return russian ? "Сначала остановите экземпляр." : "Stop the instance before deleting it."; } }
        public static string ExecutableNotFound { get { return russian ? "HTCHome.exe не найден рядом с Manager." : "HTCHome.exe was not found next to Manager."; } }

        public static string DeleteQuestion(string name)
        {
            return russian ? "Удалить профиль «" + name + "»?" : "Delete profile \"" + name + "\"?";
        }
    }
}
