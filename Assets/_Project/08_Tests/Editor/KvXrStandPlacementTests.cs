using NUnit.Framework;
using UnityEngine;
using KazistovVvXr;
using TrajectoryCore;

namespace KazistovVvTests
{
    /// <summary>
    /// EditMode-тесты РАССТАНОВКИ СТЕНДОВ И ПОКАЗА ИХ В ШЛЕМЕ (сессия §35).
    ///
    /// ЧТО СЛУЧИЛОСЬ. Владелец надел шлем 16.10.2026 и увидел помещение ангара — но
    /// **«столов или роботов не видно»**, хотя журнал шлема утверждал, что стенды перенесены
    /// на (−1,5; 0; 3,2) и (1,5; 0; 3,2) и ничем не загорожены (проверено отдельным замером:
    /// ни оператор, ни стенды не внутри геометрии зала, луч до столов свободен на всех 6 м).
    ///
    /// НАШЛИСЬ ДВЕ ПРИЧИНЫ, и обе проверяются здесь числами:
    ///   1) СТОЛЫ БЫЛИ СПРЯТАНЫ. Компонент MR-постановки гасил «виртуальные стенды» ВСЕГДА,
    ///      хотя это задумано только для MR. При поле `hideVirtualStands = true` и выключенном
    ///      тумблере условное выражение давало ложь — и в VR, где столы и есть главное
    ///      содержимое, они не рисовались.
    ///   2) ОПЕРАТОР СТОЯЛ ВНУТРИ СТОЛОВ. Столешница проекта — 14,4 × 9,6 м, половина глубины
    ///      4,8 м, а стенды ставились на 3,2 м от оператора: ближняя кромка оказывалась на 1,6 м
    ///      ПОЗАДИ него. Это ровно то, что владелец запретил словами «я не внутри робота».
    /// </summary>
    public class KvXrStandPlacementTests
    {
        /// <summary>Габарит столешницы проекта — из кода, а не из пересказа.</summary>
        private static float TableDepth { get { return StandBuilder.TableDepth; } }

        /// <summary>
        /// ПОЛОВИНА ГЛУБИНЫ СТОЛА — ключевое число: любое расстояние до стендов меньше него
        /// означает, что оператор стоит ВНУТРИ стола.
        /// </summary>
        [Test]
        public void HalfTableDepth_IsAlmostFiveMeters()
        {
            Assert.AreEqual(4.8f, TableDepth * 0.5f, 0.01f,
                "половина глубины столешницы проекта (9,6 м) — 4,8 м; это и есть граница, " +
                "ближе которой оператор оказывается внутри стола");
        }

        /// <summary>
        /// ПРЕЖНЯЯ КОНСТАНТА 3,2 М БЫЛА НЕВЕРНА, и это записано проверкой: 3,2 &lt; 4,8,
        /// то есть ближняя кромка стола оказывалась ПОЗАДИ оператора.
        /// </summary>
        [Test]
        public void OldConstant_WouldPutOperatorInsideTable()
        {
            const float oldStandDistance = 3.2f;
            float nearEdge = oldStandDistance - TableDepth * 0.5f;

            Assert.Less(nearEdge, 0f,
                "при расстоянии 3,2 м ближняя кромка стола оказывалась на " + nearEdge.ToString("0.00") +
                " м, то есть ПОЗАДИ оператора — он стоял внутри обоих столов");
        }

        /// <summary>НОВОЕ ПРАВИЛО: ближняя кромка столов впереди оператора, и запас положительный.</summary>
        [Test]
        public void NewRule_KeepsBenchInFrontOfOperator()
        {
            var host = new GameObject("KvXrSceneContentTestHost");
            try
            {
                var content = host.AddComponent<KvXrSceneContent>();

                Assert.Greater(content.standNearEdgeMeters, 0f,
                    "ближняя кромка столов обязана быть ВПЕРЕДИ оператора: отрицательное или нулевое " +
                    "значение снова поставило бы его внутрь стола");

                // Центр скамьи при новом правиле: расстояние до кромки + полуглубина пары стендов.
                float benchHalfDepth = 10.1f;                 // измеренная полудлина скамьи из двух столов
                float benchCenter = content.standNearEdgeMeters + benchHalfDepth;
                float nearEdge = benchCenter - benchHalfDepth;

                Assert.Greater(nearEdge, 0f, "скамья обязана начинаться впереди оператора");
                Assert.Less(nearEdge, 1.5f,
                    "скамья не должна отодвигаться далеко: владелец должен стоять У неё, а не искать её");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        /// <summary>
        /// СТОЛЫ ПРЯЧУТСЯ ТОЛЬКО В MR. Это и была причина «столов не видно»: в VR компонент
        /// MR-постановки гасил их при выключенном тумблере.
        /// </summary>
        [Test]
        public void VirtualStands_AreHiddenOnlyInMr()
        {
            // VR (passthrough выключен), поле hideVirtualStands = true, тумблер выключен:
            // РАНЬШЕ здесь получалась ложь и столы пропадали. Должно быть — НЕ прятать.
            Assert.IsFalse(KvXrMrRoom.HidesVirtualStands(false, true, false),
                "в VR стенды обязаны быть ВИДНЫ всегда: в шлеме они и есть главное содержимое");

            Assert.IsFalse(KvXrMrRoom.HidesVirtualStands(false, true, true),
                "в VR со включённым тумблером стенды тем более видны");

            // MR включён, поле задано, тумблер выключен — только здесь стенды гасятся.
            Assert.IsTrue(KvXrMrRoom.HidesVirtualStands(true, true, false),
                "в MR виртуальные стенды гасятся: иначе они перекрывают реальную комнату");

            // MR включён, но оператор сам включил слой — уважаем его выбор.
            Assert.IsFalse(KvXrMrRoom.HidesVirtualStands(true, true, true),
                "если оператор включил слой «виртуальные стенды» вручную, гасить их нельзя");

            // MR включён, но гашение не задано полем.
            Assert.IsFalse(KvXrMrRoom.HidesVirtualStands(true, false, false),
                "без явного указания гасить стенды нельзя");
        }

        /// <summary>
        /// РОБОТ СТАВИТСЯ НА «РЕАЛЬНЫЙ СТОЛ» ТОЛЬКО В MR. В VR постановка уводила его в начало
        /// координат — туда, где стоит оператор, и робот становился невидимым.
        /// </summary>
        [Test]
        public void Robot_GoesToRealTableOnlyInMr()
        {
            Assert.IsFalse(KvXrMrRoom.ShouldPlaceRobotOnRealTable(false),
                "в VR реального стола нет: постановка «на реальный стол» перенесла бы робота " +
                "в начало координат, то есть в место, где стоит оператор");

            Assert.IsTrue(KvXrMrRoom.ShouldPlaceRobotOnRealTable(true),
                "в MR робот обязан вставать на реальный стол — иначе MR теряет смысл");
        }
    }
}
