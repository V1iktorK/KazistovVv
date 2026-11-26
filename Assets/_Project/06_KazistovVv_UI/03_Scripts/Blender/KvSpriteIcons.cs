using System.Collections.Generic;
using UnityEngine;

namespace KazistovVvUI
{
    /// <summary>
    /// СПРАЙТЫ ИКОНОК ТУЛБАРА ИЗ ФАЙЛОВ (раскладка Blender).
    ///
    /// ОТКУДА. Владелец утвердил дизайн «Blender-раскладка + стеклянные окна» и передал
    /// 126 спрайтов в `Assets/Resources/UI/Icons/Toolbar/`: имя файла = id команды, в котором
    /// точки заменены подчёркиваниями (`robot.playpause` → `robot_playpause.png`). Светлые
    /// глифы рассчитаны на тёмную тему; в светлой теме глиф тонируется тёмным цветом
    /// (`Image.color` умножает спрайт), поэтому файлы не дублируются.
    ///
    /// ПОЧЕМУ НЕ `KvIcons`. `KvIcons` рисует иконки кодом (монохромные штрихи) — это встроенный
    /// запасной набор. Здесь — НАСТОЯЩИЕ спрайты владельца; `KvIcons` остаётся фолбэком,
    /// если файла для id нет (новые команды без спрайта не остаются без кнопки).
    /// </summary>
    public static class KvSpriteIcons
    {
        /// <summary>Путь к спрайтам ОТ папки `Resources` (формат `Resources.Load`).</summary>
        public const string Folder = "UI/Icons/Toolbar";

        /// <summary>Сколько спрайтов в папке (владелец передал 126; для диагностики).</summary>
        public const int ExpectedCount = 126;

        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        /// <summary>Имя файла/ресурса для id команды: точки → подчёркивания.</summary>
        public static string ResourceName(string commandId)
        {
            return string.IsNullOrEmpty(commandId) ? "" : commandId.Replace('.', '_');
        }

        /// <summary>
        /// Спрайт иконки команды (null — файла нет). Спрайты кэшируются: `Resources.Load`
        /// возвращает тот же объект, но поиск по строке каждый раз стоит времени.
        /// </summary>
        public static Sprite Load(string commandId)
        {
            if (string.IsNullOrEmpty(commandId)) return null;
            string key = ResourceName(commandId);
            if (string.IsNullOrEmpty(key)) return null;
            Sprite cached;
            if (cache.TryGetValue(key, out cached)) return cached;
            Sprite sp = Resources.Load<Sprite>(Folder + "/" + key);
            cache[key] = sp;
            return sp;
        }

        /// <summary>Есть ли файл-спрайт для этой команды.</summary>
        public static bool Has(string commandId)
        {
            return Load(commandId) != null;
        }

        /// <summary>Сколько спрайтов реально загружено (диагностика/тесты).</summary>
        public static int LoadedCount
        {
            get
            {
                int n = 0;
                foreach (KeyValuePair<string, Sprite> kv in cache)
                    if (kv.Value != null) n++;
                return n;
            }
        }

        /// <summary>Сбросить кэш (тесты/смена набора иконок).</summary>
        public static void ClearCache()
        {
            cache.Clear();
        }
    }
}
