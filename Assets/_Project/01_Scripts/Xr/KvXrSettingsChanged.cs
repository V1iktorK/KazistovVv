using System;

namespace KazistovVvXr
{
    /// <summary>
    /// ОПОВЕЩЕНИЕ О СМЕНЕ XR-НАСТРОЕК (сессия §28).
    ///
    /// ЗАЧЕМ. Настройки XR — статическое хранилище (PlayerPrefs): у него нет ни событий,
    /// ни владельца. А применять их надо немедленно: переключил «плавный ход» — метка
    /// телепорта обязана исчезнуть в тот же кадр, а не после перезапуска. Ровно на такой
    /// несогласованности в проекте уже были дефекты (§23: «настройки не открывались»,
    /// «курсор не восстанавливался»), поэтому здесь одна точка оповещения на весь XR-слой.
    ///
    /// Кто подписывается: локомоция, панель интерфейса, слои MR.
    /// Кто вызывает: панель настроек (и диагностика).
    /// </summary>
    public static class KvXrSettingsChanged
    {
        /// <summary>Настройки XR изменились — перечитать и применить немедленно.</summary>
        public static event Action SettingsChanged;

        /// <summary>Сообщить об изменении настроек. Безопасно вызывать, когда слушателей нет.</summary>
        public static void Notify()
        {
            Action handler = SettingsChanged;
            if (handler != null) handler();
        }

        /// <summary>Сколько слушателей подписано — для диагностики (проверка, что подписка жива).</summary>
        public static int ListenerCount
        {
            get
            {
                Action handler = SettingsChanged;
                return handler != null ? handler.GetInvocationList().Length : 0;
            }
        }

        /// <summary>Снять всех слушателей — вызывается при выходе из PlayMode и в тестах.</summary>
        public static void Clear()
        {
            SettingsChanged = null;
        }
    }
}
