using System.Globalization;

namespace KazistovVvXr
{
    /// <summary>
    /// НАВЕДЕНИЕ ЛУЧА: КОГДА «ПРОЩЁННАЯ» ИКОНКА ЕЩЁ ДЕЙСТВИТЕЛЬНА, А КОГДА НЕТ (карточка Б51).
    ///
    /// ЧТО БЫЛО. `KvXrUiPointer.AimAlongPanel` прощает дрожание руки: при промахе до 0,5 с держится
    /// номер прошлой иконки (`lastHitIndex`). Это нужно, иначе подпись и нажатие срываются от
    /// микродрожания. Но после смены уровня панель пересобрана, номера иконок означают ДРУГОЕ, а
    /// номер в указателе остался. В журнале приёмки §40 это выглядело так (5 раз за сеанс):
    ///   `[KvXr] ПАНЕЛЬ: луч навёл «» — 0.0 см от центра иконки`
    /// — журнал называл наведение, которого не было, и до 0,5 с держал запрет телепорта.
    ///
    /// ЧТО ЗДЕСЬ. Правило «можно ли верить прощённому номеру» вынесено в чистую функцию, чтобы
    /// проверяться тестом без шлема, и построено так, чтобы ЗАБЫТЬ СБРОС БЫЛО НЕЛЬЗЯ: решение
    /// принимается не вызывающим кодом, а самим фактом смены версии пересборки панели.
    /// </summary>
    public static class KvXrAimGuard
    {
        /// <summary>Сколько секунд прощается потеря иконки (как в указателе §39).</summary>
        public const float GraceSeconds = 0.5f;

        /// <summary>
        /// ВЕРИТЬ ЛИ ПРОЩЁННОМУ НОМЕРУ. `true` — можно считать целью `forgivenIndex`;
        /// `false` — цель сбрасывается (луч ни на что не наведён).
        /// </summary>
        /// <param name="realHit">Луч попал в иконку прямо сейчас.</param>
        /// <param name="versionChanged">Панель пересобрана с прошлого шага (номер сменил смысл).</param>
        /// <param name="forgivenIndex">Номер прошлой иконки (−1 — нечего прощать).</param>
        /// <param name="targetCount">Сколько целей у панели СЕЙЧАС.</param>
        /// <param name="targetTitle">Название иконки под этим номером (пусто — номера в новой панели нет).</param>
        /// <param name="lostSeconds">Сколько секунд назад потеряна иконка.</param>
        /// <param name="graceSeconds">
        /// Сколько секунд прощается потеря. Берётся ИЗ УКАЗАТЕЛЯ (`hoverGraceSeconds`, 0,25 с), а не
        /// из константы: иначе правило и то, как оно применяется, жили бы в разных числах.
        /// </param>
        public static bool KeepsForgivenTarget(bool realHit, bool versionChanged, int forgivenIndex,
                                               int targetCount, string targetTitle, float lostSeconds,
                                               float graceSeconds = GraceSeconds)
        {
            // Реальное попадание не зависит ни от версии, ни от прощения.
            if (realHit) return true;

            // Панель пересобрана — номер прошлой иконки в новой панели означает другую кнопку.
            if (versionChanged) return false;

            // Номера нет или он вне списка целей — прощать нечего.
            if (forgivenIndex < 0 || forgivenIndex >= targetCount) return false;

            // Названия под этим номером нет — значит в новой панели такого номера не существует.
            // Именно этот случай и рождал строку «луч навёл «»».
            if (string.IsNullOrEmpty(targetTitle)) return false;

            // Прощение истекло.
            if (lostSeconds > graceSeconds) return false;

            return true;
        }

        /// <summary>
        /// СТРОКА ЖУРНАЛА О НАВЕДЕНИИ. Попали — называем иконку и расстояние до её центра;
        /// промахнулись — так и пишем: `луч ушёл с иконки`. Пустого названия «навёл «»» быть не может.
        /// </summary>
        public static string AimLogLine(bool realHit, string title, float fromCenterMeters,
                                        float alongRayMeters, string source)
        {
            if (!realHit)
                return "[KvXr] ПАНЕЛЬ: луч ушёл с иконки · источник " + source;

            return string.Format(CultureInfo.InvariantCulture,
                "[KvXr] ПАНЕЛЬ: луч навёл «{0}» — {1:0.0} см от центра иконки · луч {2:0.00} м · источник {3}",
                title, fromCenterMeters * 100f, alongRayMeters, source);
        }
    }
}
