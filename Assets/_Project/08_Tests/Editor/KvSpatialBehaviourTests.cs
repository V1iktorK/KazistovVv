// ================================================================================================
//  KvSpatialBehaviourTests.cs — ПОВЕДЕНЧЕСКИЕ тесты папки `01_Scripts\Spatial` (P4-8).
//
//  ОТКУДА ЗАДАЧА. Калибровка робота (`CalibrationTool`) и пространственный якорь
//  (`SpatialAnchorManager`) — это ровно те два места, где ошибка НЕ видна в редакторе: она
//  проявляется у наладчика, который калибрует робота по реальному столу, перезапускает приложение
//  и получает робота со сдвигом. Проверялись они поиском строк в исходнике — то есть никак.
//
//  ЧТО ПРОВЕРЯЕТСЯ ЗДЕСЬ (чистые функции + настоящие трансформы, без сцены и без шлема):
//    1) поза «мир ↔ относительно опоры трекинга» переводится ТУДА И ОБРАТНО без потерь;
//    2) при перецентровке мира (опора сдвинулась) относительная поза даёт НОВУЮ мировую — именно
//       ради этого якорь и перевели на относительные координаты (P1-7);
//    3) повторная калибровка НЕ складывает перекос (дефект P1-6: поворот умножался дважды);
//    4) смещение по трём точкам действительно совмещает модель с реальностью.
// ================================================================================================

using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Поведение калибровки и пространственного якоря.</summary>
    [Category(KvTestCategories.Fast)]
    public class KvSpatialBehaviourTests
    {
        /// <summary>Опорный трансформ трекинга (XR Origin): сдвинут и повёрнут — как в шлеме.</summary>
        private static GameObject MakeOrigin(Vector3 position, float yawDegrees)
        {
            var go = new GameObject("KvTestOrigin");
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yawDegrees, 0f));
            return go;
        }

        /// <summary>
        /// МИР → ОТНОСИТЕЛЬНО ОПОРЫ → МИР: ПОЗА ВОЗВРАЩАЕТСЯ БЕЗ ПОТЕРЬ.
        ///
        /// ЗАЧЕМ. Якорь сохраняет позу робота ОТНОСИТЕЛЬНО опоры трекинга и при старте пересчитывает
        /// её обратно в мир. Если прямое и обратное преобразование не согласованы (например, поворот
        /// применён в одну сторону), робот встаёт не на свой стол — и это выглядит как «сохранение
        /// не работает», потому что числа в файле есть, а место неверное.
        /// В проверке участвуют И позиция, И ПОВОРОТ: раньше терялся именно поворот.
        /// </summary>
        [Test]
        public void Anchor_WorldToLocalAndBack_RoundTrips()
        {
            GameObject origin = MakeOrigin(new Vector3(2f, 0f, -3f), 35f);
            try
            {
                Vector3 worldPosition = new Vector3(5.5f, 0.75f, 7.25f);
                Quaternion worldRotation = Quaternion.Euler(10f, 80f, -5f);

                Vector3 localPosition;
                Quaternion localRotation;
                SpatialAnchorManager.WorldToLocal(origin.transform, worldPosition, worldRotation,
                    out localPosition, out localRotation);

                Vector3 backPosition;
                Quaternion backRotation;
                SpatialAnchorManager.LocalToWorld(origin.transform, localPosition, localRotation,
                    out backPosition, out backRotation);

                Assert.AreEqual(worldPosition.x, backPosition.x, 1e-4f, "позиция X не вернулась");
                Assert.AreEqual(worldPosition.y, backPosition.y, 1e-4f, "позиция Y не вернулась");
                Assert.AreEqual(worldPosition.z, backPosition.z, 1e-4f, "позиция Z не вернулась");
                Assert.Less(Quaternion.Angle(worldRotation, backRotation), 0.01f,
                    "поворот не вернулся: «сохранённый» робот встанет повёрнутым на другой угол");

                // ВЫРОЖДЕННЫЙ СЛУЧАЙ — ОПОРЫ НЕТ. `trackingOrigin` не задан и у робота нет родителя:
                // без этой ветки `origin.InverseTransformPoint` упал бы по null-ссылке прямо в
                // `Start`, то есть приложение не запустилось бы вовсе, а причина была бы последней
                // строкой журнала. Поза в этом случае возвращается КАК ЕСТЬ (мировые остаются мировыми).
                Vector3 noOriginPosition;
                Quaternion noOriginRotation;
                SpatialAnchorManager.WorldToLocal(null, worldPosition, worldRotation,
                    out noOriginPosition, out noOriginRotation);
                Assert.AreEqual(worldPosition, noOriginPosition, "без опоры позиция обязана остаться мировой");
                Assert.AreEqual(worldRotation, noOriginRotation, "без опоры поворот обязан остаться мировым");

                Vector3 noOriginBack;
                Quaternion noOriginBackRotation;
                SpatialAnchorManager.LocalToWorld(null, worldPosition, worldRotation,
                    out noOriginBack, out noOriginBackRotation);
                Assert.AreEqual(worldPosition, noOriginBack, "без опоры обратное преобразование — тождество");
                Assert.AreEqual(worldRotation, noOriginBackRotation, "без опоры обратный поворот — тождество");
            }
            finally
            {
                Object.DestroyImmediate(origin);
            }
        }

        /// <summary>
        /// ПЕРЕЦЕНТРОВКА МИРА НЕ СДВИГАЕТ РОБОТА (P1-7). Главная причина, по которой поза хранится
        /// относительной.
        /// ЧТО БЫЛО. Поза сохранялась в МИРОВЫХ координатах сцены. Начало отсчёта трекинга задаёт
        /// шлем и меняет его при перецентровке и перезапуске — значит «восстановленный» робот
        /// оказывался не на своём столе, а в произвольной точке комнаты. Ошибка тихая: ни в
        /// интерфейсе, ни в журнале.
        ///
        /// ПРОВЕРЯЕТСЯ ПО РЕЗУЛЬТАТУ: одна и та же СОХРАНЁННАЯ (относительная) поза при сдвинутой
        /// опоре даёт ДРУГУЮ мировую — значит робот поедет вместе с миром и останется на своём столе.
        /// </summary>
        [Test]
        public void Anchor_RecentredOriginMovesWorldPoseWithWorld()
        {
            GameObject first = MakeOrigin(Vector3.zero, 0f);
            GameObject moved = MakeOrigin(new Vector3(4f, 0f, 1f), 0f);
            try
            {
                Vector3 localPosition = new Vector3(1f, 0f, 2f);
                Quaternion localRotation = Quaternion.identity;

                Vector3 before;
                Quaternion rotationBefore;
                SpatialAnchorManager.LocalToWorld(first.transform, localPosition, localRotation,
                    out before, out rotationBefore);

                Vector3 after;
                Quaternion rotationAfter;
                SpatialAnchorManager.LocalToWorld(moved.transform, localPosition, localRotation,
                    out after, out rotationAfter);

                Assert.AreEqual(localPosition.x, before.x, 1e-4f, "при опоре в нуле мир = относительная поза");
                Assert.AreEqual(localPosition.x + 4f, after.x, 1e-4f,
                    "после перецентровки мира робот обязан пересчитаться от НОВОЙ опоры, " +
                    "иначе он останется в старой точке комнаты (P1-7)");
                Assert.AreEqual(localPosition.z + 1f, after.z, 1e-4f,
                    "сдвиг опоры по Z тоже обязан учитываться");
                Assert.Greater(Vector3.Distance(before, after), 1f,
                    "перецентровка не изменила мировую позу — значит поза НЕ привязана к опоре");
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(moved);
            }
        }

        /// <summary>
        /// ПОВТОРНАЯ КАЛИБРОВКА НЕ УМНОЖАЕТ ПОВОРОТ ДВАЖДЫ (дефект P1-6). ГЛАВНЫЙ тест файла.
        ///
        /// ЧТО БЫЛО. Поворот накладывался на текущий (`rotation = rotOffset * rotation`), позиция —
        /// прибавлялась к текущей. Наладчик, который калибровал «ещё раз, чтобы точнее», получал
        /// ВДВОЕ больший перекос и не мог понять, откуда он: сообщений нет, а число в сохранении
        /// выглядит как «одна калибровка».
        ///
        /// ПРОВЕРЯЕТСЯ ПО РЕЗУЛЬТАТУ:
        ///   1) применение одного и того же смещения дважды даёт ТУ ЖЕ позу (идемпотентность);
        ///   2) новая калибровка ЗАМЕЩАЕТ старую позу, а не добавляется к ней (поза абсолютная).
        /// Если вернуть накопление, покраснеет любой из двух пунктов.
        /// </summary>
        [Test]
        public void Calibration_RepeatedRunDoesNotAccumulateOffset()
        {
            Vector3 offsetPosition = new Vector3(0.4f, 0f, -0.15f);
            Quaternion offsetRotation = Quaternion.Euler(0f, 12f, 0f);

            Vector3 position;
            Quaternion rotation;
            CalibrationTool.CalibratedBasePose(Vector3.zero, Quaternion.identity,
                offsetPosition, offsetRotation, out position, out rotation);

            Vector3 secondPosition;
            Quaternion secondRotation;
            CalibrationTool.CalibratedBasePose(position, rotation,
                offsetPosition, offsetRotation, out secondPosition, out secondRotation);

            Assert.AreEqual(position, secondPosition,
                "вторая калибровка сдвинула базу ещё раз — перекос складывается (P1-6)");
            Assert.Less(Quaternion.Angle(rotation, secondRotation), 1e-3f,
                "вторая калибровка повернула базу ещё раз — поворот умножается дважды (P1-6)");

            // НОВАЯ КАЛИБРОВКА ИЗ ДРУГОГО МЕСТА: база встаёт туда, куда указали, независимо от того,
            // где она была. Иначе третья калибровка после второй давала бы сумму всех трёх — то есть
            // робота, уехавшего от стола на метр.
            Vector3 freshPosition;
            Quaternion freshRotation;
            CalibrationTool.CalibratedBasePose(new Vector3(9f, 9f, 9f), Quaternion.Euler(0f, 170f, 0f),
                new Vector3(0.2f, 0f, 0.3f), Quaternion.Euler(0f, -20f, 0f),
                out freshPosition, out freshRotation);

            Assert.AreEqual(0.2f, freshPosition.x, 1e-4f, "база встала не туда, куда её поставила калибровка");
            Assert.AreEqual(0.3f, freshPosition.z, 1e-4f, "база не должна «прибавлять» старую позицию к новой");
            Assert.Less(Quaternion.Angle(freshRotation, Quaternion.Euler(0f, -20f, 0f)), 0.01f,
                "поворот базы обязан быть ровно измеренным, без старого перекоса");
        }

        /// <summary>
        /// СМЕЩЕНИЕ ПО ТРЁМ ТОЧКАМ ДЕЙСТВИТЕЛЬНО СОВМЕЩАЕТ МОДЕЛЬ С РЕАЛЬНОСТЬЮ.
        ///
        /// Проверяются оба слагаемых результата:
        ///   • ОСНОВАНИЕ модели попадает ровно в первую реальную точку (это даёт позиция);
        ///   • НАПРАВЛЕНИЕ «основание → второй сустав» после поворота совпадает с реальным (это даёт
        ///     поворот).
        ///
        /// ПОЧЕМУ НЕ «ВСЕ ТРИ ТОЧКИ СОВПАЛИ». Третья точка в модели задана ПРИМЕРОМ и не проверяется
        /// расчётом (в `GetModelCalibrationPoint` это помечено «пример»), а масштаб калибровка не
        /// восстанавливает вовсе — она выравнивает направление. Требовать совпадения всех трёх точек
        /// значило бы проверять то, чего метод не обещает.
        ///
        /// ЧТО ЛОВИТСЯ: сдвиг без поворота (робот «рядом со столом»), поворот без сдвига (робот
        /// повёрнут правильно, но стоит в другом месте) и перепутанное направление (модель → реальность
        /// вместо реальность → модель, то есть поворот на обратный угол).
        /// </summary>
        [Test]
        public void Calibration_OffsetsAlignModelWithReality()
        {
            Vector3[] real =
            {
                new Vector3(1f, 0.9f, 2f),
                new Vector3(1.5f, 1.3f, 2f),
                new Vector3(1.8f, 1.7f, 2.1f)
            };
            Vector3[] model =
            {
                Vector3.zero,
                new Vector3(0f, 0.4f, 0f),
                new Vector3(0.3f, 0.8f, 0f)
            };

            Vector3 position;
            Quaternion rotation;
            CalibrationTool.ComputeOffsets(real, model, out position, out rotation);

            Vector3 basePoint = position + rotation * model[0];
            Assert.Less(Vector3.Distance(basePoint, real[0]), 1e-3f,
                "основание модели не совпало с реальной точкой — калибровка не совмещает робота со столом");

            Vector3 modelDirection = rotation * (model[1] - model[0]);
            Vector3 realDirection = real[1] - real[0];
            Assert.Less(Vector3.Angle(modelDirection, realDirection), 0.01f,
                "направление «основание → второй сустав» после калибровки не совпало с реальным: " +
                "робот повёрнут не туда (перепутаны «модель → реальность» и обратный поворот)");
        }
    }
}
