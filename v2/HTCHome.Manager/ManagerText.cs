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
            russian = !string.IsNullOrWhiteSpace(language) && language.StartsWith("ru", StringComparison.OrdinalIgnoreCase);
        }

        public static string DetectLanguage()
        {
            return string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "ru", StringComparison.OrdinalIgnoreCase) ? "ru-RU" : "en-US";
        }

        public static string WindowTitle { get { return "HTC Home Mugen Manager"; } }
        public static string Header { get { return "HTC Home Mugen Manager"; } }
        public static string Subtitle { get { return russian ? "Несколько независимых экземпляров HTC Home из одной папки" : "Multiple independent HTC Home instances from one folder"; } }
        public static string LanguageLabel { get { return russian ? "Язык:" : "Language:"; } }
        public static string NameHeader { get { return russian ? "Имя" : "Name"; } }
        public static string StatusHeader { get { return russian ? "Статус" : "Status"; } }
        public static string AutoStartHeader { get { return russian ? "Автозапуск" : "Autostart"; } }
        public static string ResumeDiagnosticHeader { get { return russian ? "Resume-эксперимент" : "Resume experiment"; } }
        public static string Add { get { return russian ? "+ Добавить" : "+ Add"; } }
        public static string Rename { get { return russian ? "Переименовать" : "Rename"; } }
        public static string Start { get { return russian ? "Запустить" : "Start"; } }
        public static string Stop { get { return russian ? "Остановить" : "Stop"; } }
        public static string Delete { get { return russian ? "Удалить" : "Delete"; } }
        public static string StartAll { get { return russian ? "Запустить все" : "Start all"; } }
        public static string StopAll { get { return russian ? "Остановить все" : "Stop all"; } }
        public static string ManagerAutoStart { get { return russian ? "Запускать Manager вместе с Windows" : "Start Manager with Windows"; } }
        public static string ProfileAutoStart { get { return russian ? "Автозапуск профиля" : "Profile autostart"; } }
        public static string ResumeDiagnosticLabel { get { return russian ? "Resume-эксперимент:" : "Resume experiment:"; } }
        public static string ResumeDiagnosticHint { get { return russian ? "Окно не скрывается; отличается только задержка возврата WPF-рендера" : "Window stays visible; only WPF render restore delay differs"; } }
        public static string ResumeDiagnosticNormal { get { return russian ? "Baseline — ничего не менять" : "Baseline — no changes"; } }
        public static string ResumeDiagnosticHide { get { return russian ? "HwndTarget Disable — сразу на Resume" : "HwndTarget Disable — restore at Resume"; } }
        public static string ResumeDiagnosticCloak { get { return russian ? "HwndTarget Disable — вернуть через 3 с" : "HwndTarget Disable — restore after 3s"; } }
        public static string ResumeDiagnosticMinimize { get { return russian ? "HwndTarget Disable — вернуть через 12 с" : "HwndTarget Disable — restore after 12s"; } }
        public static string Running { get { return russian ? "Запущен" : "Running"; } }
        public static string Stopped { get { return russian ? "Остановлен" : "Stopped"; } }
        public static string Yes { get { return russian ? "Да" : "Yes"; } }
        public static string No { get { return russian ? "Нет" : "No"; } }
        public static string NewInstancePrompt { get { return russian ? "Имя нового экземпляра:" : "New instance name:"; } }
        public static string NewInstanceDefault { get { return russian ? "Новый экземпляр" : "New instance"; } }
        public static string RenamePrompt { get { return russian ? "Новое имя экземпляра:" : "New instance name:"; } }
        public static string StopBeforeDelete { get { return russian ? "Сначала остановите экземпляр." : "Stop the instance before deleting it."; } }
        public static string ExecutableNotFound { get { return russian ? "HTCHome.exe не найден рядом с Manager." : "HTCHome.exe was not found next to Manager."; } }
        public static string TrayOpen { get { return russian ? "Открыть Manager" : "Open Manager"; } }
        public static string TrayExit { get { return russian ? "Выход" : "Exit"; } }
        public static string TrayTip { get { return russian ? "HTC Home Mugen — управление экземплярами" : "HTC Home Mugen — instance manager"; } }

        public static string NvidiaCompatibility { get { return russian ? "Совместимость NVIDIA" : "NVIDIA Compatibility"; } }
        public static string NvidiaWindowTitle { get { return NvidiaCompatibility; } }
        public static string NvidiaHeader { get { return NvidiaCompatibility; } }
        public static string NvidiaDescription { get { return russian ? "Диагностика интеграции NVIDIA с экземплярами HTC Home. Handles обновляются автоматически каждые 5 секунд; резкий устойчивый рост после гибернации может указывать на утечку ресурсов." : "Diagnostics for NVIDIA integration with HTC Home instances. Handle counts refresh every 5 seconds; a large sustained increase after hibernate may indicate a resource leak."; } }
        public static string NvidiaProfile { get { return russian ? "Профиль" : "Profile"; } }
        public static string NvidiaModule { get { return russian ? "NVIDIA DLL" : "NVIDIA DLL"; } }
        public static string NvidiaDelta { get { return russian ? "Изменение" : "Change"; } }
        public static string NvidiaHealth { get { return russian ? "Оценка" : "Assessment"; } }
        public static string NvidiaNormal { get { return russian ? "Норма" : "Normal"; } }
        public static string NvidiaWatch { get { return russian ? "Наблюдать" : "Watch"; } }
        public static string NvidiaSuspicious { get { return russian ? "Подозрительный рост" : "Suspicious growth"; } }
        public static string NvidiaStopped { get { return russian ? "Остановлен" : "Stopped"; } }
        public static string NvidiaExclusionHeader { get { return russian ? "Исключения NVIDIA FrameView" : "NVIDIA FrameView exclusions"; } }
        public static string NvidiaExclusionsPresent { get { return russian ? "HTCHome.exe уже добавлен в оба списка исключений." : "HTCHome.exe is already present in both exclusion lists."; } }
        public static string NvidiaExclusionsMissing { get { return russian ? "HTCHome.exe ещё не добавлен в оба списка исключений." : "HTCHome.exe is not yet present in both exclusion lists."; } }
        public static string NvidiaExclusionNote { get { return russian ? "Manager изменяет только ExcludeList.overlay.txt и ExcludeList.txt в папке NVIDIA FrameView. Существующие файлы перед первым изменением копируются в .mugen-backup. Это не гарантирует, что nvspcap64.dll перестанет загружаться в процесс." : "Manager changes only ExcludeList.overlay.txt and ExcludeList.txt in the NVIDIA FrameView folder. Existing files are backed up to .mugen-backup before the first change. This does not guarantee that nvspcap64.dll will stop loading into the process."; } }
        public static string NvidiaApplyExclusions { get { return russian ? "Добавить исключения" : "Add exclusions"; } }
        public static string NvidiaRefresh { get { return russian ? "Обновить" : "Refresh"; } }
        public static string NvidiaClose { get { return russian ? "Закрыть" : "Close"; } }
        public static string NvidiaApplyQuestion { get { return russian ? "Добавить HTCHome.exe в оба списка исключений NVIDIA FrameView?" : "Add HTCHome.exe to both NVIDIA FrameView exclusion lists?"; } }
        public static string NvidiaApplySuccess { get { return russian ? "Исключения добавлены. Перезапустите экземпляры HTC Home для чистой проверки." : "Exclusions were added. Restart HTC Home instances for a clean test."; } }
        public static string NvidiaAdminRequired { get { return russian ? "Windows не разрешила изменить файлы NVIDIA. Запустите Manager один раз от имени администратора и повторите действие." : "Windows did not allow the NVIDIA files to be changed. Run Manager as administrator once and try again."; } }
        public static string NvidiaApplyFailed { get { return russian ? "Не удалось применить исключения NVIDIA." : "Could not apply NVIDIA exclusions."; } }
        public static string NvidiaUpdated(DateTime time) { return russian ? "Обновлено: " + time.ToString("HH:mm:ss") : "Updated: " + time.ToString("HH:mm:ss"); }

        public static string ResumeDiagnosticModeText(string mode)
        {
            if (string.Equals(mode, "target0", StringComparison.OrdinalIgnoreCase) || string.Equals(mode, "hide", StringComparison.OrdinalIgnoreCase)) return ResumeDiagnosticHide;
            if (string.Equals(mode, "target3", StringComparison.OrdinalIgnoreCase) || string.Equals(mode, "targetoff", StringComparison.OrdinalIgnoreCase) || string.Equals(mode, "cloak", StringComparison.OrdinalIgnoreCase)) return ResumeDiagnosticCloak;
            if (string.Equals(mode, "target12", StringComparison.OrdinalIgnoreCase) || string.Equals(mode, "minimize", StringComparison.OrdinalIgnoreCase)) return ResumeDiagnosticMinimize;
            return ResumeDiagnosticNormal;
        }

        public static string DeleteQuestion(string name)
        {
            return russian ? "Удалить профиль «" + name + "»?" : "Delete profile \"" + name + "\"?";
        }
    }
}
