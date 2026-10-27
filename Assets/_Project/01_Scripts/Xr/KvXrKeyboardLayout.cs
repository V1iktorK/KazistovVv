using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>КАКАЯ РАСКЛАДКА НУЖНА: числовая (быстрый ввод чисел) или текстовая (имена, поиск).</summary>
    public enum KvXrKeyboardKind
    {
        /// <summary>Цифры, точка, минус, отбой, ввод, отмена — крупными ключами.</summary>
        Numeric = 0,

        /// <summary>Буквы, цифры и знаки — мелкими ключами, чтобы влезло всё.</summary>
        Text = 1
    }

    /// <summary>ЧТО ДЕЛАЕТ КЛЮЧ.</summary>
    public enum KvXrKeyKind
    {
        /// <summary>Дописывает символ в строку-кандидат.</summary>
        Symbol = 0,

        /// <summary>Отбой: убирает последний символ.</summary>
        Backspace = 1,

        /// <summary>«Ввод»: передаёт строку в единственную точку сохранения.</summary>
        Enter = 2,

        /// <summary>«Отмена»: закрывает ввод, значение не меняется.</summary>
        Cancel = 3
    }

    /// <summary>
    /// КЛЮЧ КЛАВИАТУРЫ: подпись, что делает и ГДЕ СТОИТ (в метрах от центра панели).
    /// Смещение в метрах, а не в пикселях, потому что попадание луча считается в метрах
    /// (`KvXrGroupPanelLayout.TryHitIcon`).
    /// </summary>
    public struct KvXrKey
    {
        /// <summary>Что написано на ключе: «7», «.», «−», «отбой», «ввод», «отмена», «а».</summary>
        public string Label;

        /// <summary>Какой символ дописывается (для служебных ключей — '\0').</summary>
        public char Symbol;

        /// <summary>Что делает ключ.</summary>
        public KvXrKeyKind Kind;

        /// <summary>Смещение центра ключа от центра панели, метры.</summary>
        public Vector2 OffsetMeters;
    }

    /// <summary>
    /// ГЕОМЕТРИЯ КЛАВИАТУРЫ НА ПАНЕЛИ — ЧИСТАЯ АРИФМЕТИКА, БЕЗ UNITY-ОБЪЕКТОВ.
    ///
    /// РАЗМЕРЫ ИЗ ЗАМЕРОВ ПАНЕЛИ, А НЕ «НА ГЛАЗ». Канвас панели 420 px при 0,0025 м/px = 1,05 м
    /// (`KvXrGroupPanel.canvasSize`, `metersPerCanvasUnit`). Рабочая полоса экрана — по X примерно
    /// ±0,21 м (там стоят кнопки «−»/«+»: ±0,18 м), по Y от +0,15 м (строка значения) до −0,29 м
    /// («Закрыть»).
    ///
    /// РАДИУС ПОПАДАНИЯ ЛУЧОМ = 0,85 × размер иконки (`KvXrGroupPanel.TryAim`). Поэтому ключ мельче
    /// 30 мм делать нельзя: попадание станет лотереей. Числовая раскладка — ключ 60 мм (попадание
    /// 51 мм), текстовая — 40 мм (попадание 34 мм) при шаге 48 мм: соседние цели перекрываются, и
    /// выбирает ближайшая, что уже проверено на веере.
    ///
    /// РЕШЕНИЕ ВЛАДЕЛЬЦА 24.10.2026: числа 4×4 с ключом 60 мм, имена и поиск — с ключом 40 мм.
    /// У текстовой раскладки семь рядов (52 ключа): 33 буквы + 10 цифр + пробел и знаки `.,-()` +
    /// «отбой/ввод/отмена». Иначе не набрать даже пример из задания — «Стенд 1 (левый)».
    /// </summary>
    public static class KvXrKeyboardLayout
    {
        /// <summary>Размер ключа числовой раскладки, метры.</summary>
        public const float NumericKeyMeters = 0.060f;

        /// <summary>Шаг между ключами числовой раскладки, метры.</summary>
        public const float NumericPitchMeters = 0.070f;

        /// <summary>Размер ключа текстовой раскладки, метры.</summary>
        public const float TextKeyMeters = 0.040f;

        /// <summary>Шаг между ключами текстовой раскладки, метры.</summary>
        public const float TextPitchMeters = 0.048f;

        /// <summary>Полуширина рабочей полосы экрана, метры.</summary>
        public const float PanelHalfWidthMeters = 0.21f;

        /// <summary>Верхняя граница клавиатуры (ниже строки значения), метры.</summary>
        public const float TopLimitMeters = 0.135f;

        /// <summary>Нижняя граница клавиатуры (выше кнопки «Закрыть»), метры.</summary>
        public const float BottomLimitMeters = -0.225f;

        /// <summary>Порядок символов текстовой раскладки: буквы, цифры, знаки.</summary>
        private const string TextSymbols = "абвгдеёжзийклмнопрстуфхцчшщъыьэюя0123456789 .,-()";

        private static readonly KvXrKey[] numeric = BuildNumeric();
        private static readonly KvXrKey[] text = BuildText();

        /// <summary>Числовая: четыре столбца, цифры подряд, четвёртый ряд — служебные ключи.</summary>
        private static KvXrKey[] BuildNumeric()
        {
            string[] labels = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", ".", "−" };
            char[] symbols = { '1', '2', '3', '4', '5', '6', '7', '8', '9', '0', '.', '-' };

            var keys = new KvXrKey[15];
            for (int i = 0; i < 12; i++)
                keys[i] = Make(labels[i], symbols[i], KvXrKeyKind.Symbol, i % 4, i / 4, 4, 4,
                    NumericPitchMeters, 0.105f);

            keys[12] = Make("отбой", '\0', KvXrKeyKind.Backspace, 0, 3, 4, 4, NumericPitchMeters, 0.105f);
            keys[13] = Make("ввод", '\0', KvXrKeyKind.Enter, 1, 3, 4, 4, NumericPitchMeters, 0.105f);
            keys[14] = Make("отмена", '\0', KvXrKeyKind.Cancel, 2, 3, 4, 4, NumericPitchMeters, 0.105f);
            return keys;
        }

        /// <summary>Текстовая: восемь столбцов, семь рядов; последний ряд — четыре ключа.</summary>
        private static KvXrKey[] BuildText()
        {
            int total = TextSymbols.Length + 3;
            var keys = new KvXrKey[total];

            for (int i = 0; i < TextSymbols.Length; i++)
            {
                char symbol = TextSymbols[i];
                string label = symbol == ' ' ? "пробел"
                    : symbol == '-' ? "−"
                    : symbol.ToString();
                keys[i] = Make(label, symbol, KvXrKeyKind.Symbol, i % 8, i / 8, 8, 7,
                    TextPitchMeters, 0.115f);
            }

            int last = TextSymbols.Length;
            keys[last] = Make("отбой", '\0', KvXrKeyKind.Backspace, last % 8, last / 8, 8, 7, TextPitchMeters, 0.115f);
            keys[last + 1] = Make("ввод", '\0', KvXrKeyKind.Enter, (last + 1) % 8, (last + 1) / 8, 8, 7,
                TextPitchMeters, 0.115f);
            keys[last + 2] = Make("отмена", '\0', KvXrKeyKind.Cancel, (last + 2) % 8, (last + 2) / 8, 8, 7,
                TextPitchMeters, 0.115f);
            return keys;
        }

        private static KvXrKey Make(string label, char symbol, KvXrKeyKind kind,
                                    int column, int row, int columns, int rows,
                                    float pitch, float topMeters)
        {
            return new KvXrKey
            {
                Label = label,
                Symbol = symbol,
                Kind = kind,
                OffsetMeters = new Vector2(
                    (column - (columns - 1) * 0.5f) * pitch,
                    topMeters - row * pitch)
            };
        }

        /// <summary>Ключи раскладки (для построения и для проверок).</summary>
        public static KvXrKey[] Keys(KvXrKeyboardKind kind)
        {
            return kind == KvXrKeyboardKind.Numeric ? numeric : text;
        }

        /// <summary>Сколько ключей в раскладке.</summary>
        public static int KeyCount(KvXrKeyboardKind kind)
        {
            return Keys(kind).Length;
        }

        /// <summary>Ключ по номеру (порядок = порядок показа).</summary>
        public static KvXrKey Key(KvXrKeyboardKind kind, int index)
        {
            KvXrKey[] keys = Keys(kind);
            if (index < 0 || index >= keys.Length) return new KvXrKey();
            return keys[index];
        }

        /// <summary>Есть ли в раскладке такой символ (и под каким номером; −1 — нет).</summary>
        public static int IndexOfSymbol(KvXrKeyboardKind kind, char symbol)
        {
            KvXrKey[] keys = Keys(kind);
            for (int i = 0; i < keys.Length; i++)
                if (keys[i].Kind == KvXrKeyKind.Symbol && keys[i].Symbol == symbol) return i;
            return -1;
        }

        /// <summary>Размер ключа раскладки, метры.</summary>
        public static float KeySizeMeters(KvXrKeyboardKind kind)
        {
            return kind == KvXrKeyboardKind.Numeric ? NumericKeyMeters : TextKeyMeters;
        }

        /// <summary>Ширина раскладки, метры (от левого края крайнего ключа до правого).</summary>
        public static float WidthMeters(KvXrKeyboardKind kind)
        {
            float pitch = kind == KvXrKeyboardKind.Numeric ? NumericPitchMeters : TextPitchMeters;
            int columns = kind == KvXrKeyboardKind.Numeric ? 4 : 8;
            return (columns - 1) * pitch + KeySizeMeters(kind);
        }

        /// <summary>Высота раскладки, метры.</summary>
        public static float HeightMeters(KvXrKeyboardKind kind)
        {
            float pitch = kind == KvXrKeyboardKind.Numeric ? NumericPitchMeters : TextPitchMeters;
            int rows = kind == KvXrKeyboardKind.Numeric ? 4 : 7;
            return (rows - 1) * pitch + KeySizeMeters(kind);
        }

        /// <summary>
        /// ВСЕ КЛЮЧИ ВЛЕЗАЮТ В РАБОЧУЮ ПОЛОСУ: по X не выходят за ±PanelHalfWidthMeters, по Y лежат
        /// между BottomLimitMeters и TopLimitMeters. Клавиатура, вылезшая за панель или наехавшая на
        /// «Закрыть», — это кнопки, до которых не дотянуться.
        /// </summary>
        public static bool FitsPanel(KvXrKeyboardKind kind)
        {
            KvXrKey[] keys = Keys(kind);
            float half = KeySizeMeters(kind) * 0.5f;

            for (int i = 0; i < keys.Length; i++)
            {
                Vector2 offset = keys[i].OffsetMeters;
                if (Mathf.Abs(offset.x) + half > PanelHalfWidthMeters + 0.0001f) return false;
                if (offset.y + half > TopLimitMeters + 0.0001f) return false;
                if (offset.y - half < BottomLimitMeters - 0.0001f) return false;
            }

            return true;
        }

        /// <summary>ДВА КЛЮЧА НЕ В ОДНОМ МЕСТЕ: иначе луч нажмёт соседний (та же ловушка, что в §40).</summary>
        public static bool HasCollisions(KvXrKeyboardKind kind)
        {
            KvXrKey[] keys = Keys(kind);
            for (int i = 0; i < keys.Length; i++)
                for (int j = i + 1; j < keys.Length; j++)
                    if ((keys[i].OffsetMeters - keys[j].OffsetMeters).sqrMagnitude < 1e-8f) return true;
            return false;
        }
    }
}
