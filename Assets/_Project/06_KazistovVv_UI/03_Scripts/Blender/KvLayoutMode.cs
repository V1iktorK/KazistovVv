using System;
using UnityEngine;

namespace KazistovVvUI
{
    /// <summary>Режим раскладки интерфейса.</summary>
    public enum KvLayoutShellMode
    {
        /// <summary>Классика: существующая раскладка FreeCAD (не трогаем).</summary>
        Classic = 0,
        /// <summary>Новая утверждённая: Blender-раскладка + стеклянные окна.</summary>
        Blender = 1
    }

    /// <summary>
    /// ПЕРЕКЛЮЧАТЕЛЬ РАСКЛАДКИ «Классика / Blender» (жёсткое правило владельца: старая
    /// раскладка ОСТАЁТСЯ, новая включается командой `ui.layout.blender` из группы «ui»).
    ///
    /// По умолчанию — Классика: существующие тесты и самопрогон видят прежние элементы и
    /// их идентификаторы. Переключение мгновенное: Blender строит свою оболочку поверх
    /// (отдельный канвас) и прячет классическую, обратное — наоборот.
    /// Выбор хранится в PlayerPrefs и переживает перезапуск.
    /// </summary>
    public static class KvLayoutMode
    {
        /// <summary>PlayerPrefs-ключ режима раскладки.</summary>
        public const string PrefsKey = "KazistovVv.Shell.Mode";

        /// <summary>Id команды-переключателя (группа «ui»).</summary>
        public const string CommandId = "ui.layout.blender";

        /// <summary>Раскладка изменилась (оболочки переключаются сами, событие — для подписчиков).</summary>
        public static event Action Changed;

        /// <summary>Текущий режим.</summary>
        public static KvLayoutShellMode Mode
        {
            get
            {
                return (KvLayoutShellMode)Mathf.Clamp(PlayerPrefs.GetInt(PrefsKey, 0), 0, 1);
            }
        }

        /// <summary>Включена ли новая раскладка Blender.</summary>
        public static bool IsBlender
        {
            get { return Mode == KvLayoutShellMode.Blender; }
        }

        /// <summary>Задать режим (мгновенно, с сохранением выбора).</summary>
        public static void SetMode(KvLayoutShellMode mode, bool save = true)
        {
            if (save)
            {
                PlayerPrefs.SetInt(PrefsKey, (int)mode);
                PlayerPrefs.Save();
            }
            if (mode == KvLayoutShellMode.Blender)
            {
                KvBlenderShell.Ensure();
                // Классическая оболочка прячется, но НЕ разбирается: её элементы и
                // идентификаторы остаются доступными (правило владельца).
                KazistovVvUIManager.SetUiVisible(false);
            }
            else
            {
                KvBlenderShell.TearDown();
                KazistovVvUIManager.SetUiVisible(true);
            }
            if (Changed != null) Changed();
        }

        /// <summary>Переключить Классика ↔ Blender.</summary>
        public static void Toggle()
        {
            SetMode(IsBlender ? KvLayoutShellMode.Classic : KvLayoutShellMode.Blender);
        }
    }

    /// <summary>
    /// КОМАНДЫ РАСКЛАДКИ BLENDER. Регистрируются в общем реестре `KvCommands` — и в палитре
    /// Ctrl+K, и в тулбаре они работают как любые команды. Вызывается из
    /// `KazistovVvUIManager.RegisterCommands` и из тестов (идемпотентно).
    /// </summary>
    public static class KvBlenderCommands
    {
        /// <summary>Id команды режима крупных иконок тулбара.</summary>
        public const string LargeIconCommandId = "ui.toolbar.large";

        public static void Register()
        {
            KvCommands.Register(new KvCommand
            {
                Id = "ui.layout.blender",
                Title = "Раскладка: Классика/Blender",
                Description = "Переключает раскладку интерфейса: классическая или новая Blender со стеклянными окнами и доками",
                Icon = "layout",
                MenuPath = "Вид/Раскладка: Классика/Blender",
                Execute = LayoutToggle,
                IsChecked = LayoutIsBlender
            });

            KvCommands.Register(new KvCommand
            {
                Id = "ui.toolbar.large",
                Title = "Крупные иконки тулбара",
                Description = "Режим крупных иконок тулбара (крупные цели нажатия) в раскладке Blender",
                Icon = "expand",
                MenuPath = "Вид/Крупные иконки тулбара",
                Execute = LargeIconsToggle,
                IsChecked = LargeIconsOn
            });
        }

        private static void LayoutToggle()
        {
            KvLayoutMode.Toggle();
        }

        private static bool LayoutIsBlender()
        {
            return KvLayoutMode.IsBlender;
        }

        private static void LargeIconsToggle()
        {
            KvBlenderToolbar toolbar = KvBlenderShell.Instance != null
                ? KvBlenderShell.Instance.Toolbar : null;
            bool next = toolbar == null
                ? PlayerPrefs.GetInt(KvBlenderToolbar.LargeIconsPrefKey, 0) == 0
                : !toolbar.LargeIcons;
            if (toolbar != null) toolbar.LargeIcons = next;
            else
            {
                PlayerPrefs.SetInt(KvBlenderToolbar.LargeIconsPrefKey, next ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        private static bool LargeIconsOn()
        {
            return PlayerPrefs.GetInt(KvBlenderToolbar.LargeIconsPrefKey, 0) != 0;
        }
    }
}
