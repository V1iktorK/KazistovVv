// ================================================================================================
//  KvCenterWindowPreviewTests.cs — EditMode-тесты ПОДСВЕТКИ ПРЕВЬЮ РАЗМЕЩЕНИЯ (сессия S44).
//
//  ОТКУДА ЗАДАЧА. План доработки (5.2) давно отмечал дефект: покраска превью «можно ставить /
//  нельзя ставить» молча пропускалась, если в превью нет рендерера — пустой префаб, модель без
//  мешей, выключенные визуалы. Оператор вёл призрак без цвета и не понимал, допустимо ли место.
//  Задача «сделать тест на пустой префаб» стояла в плане с 13.10.2026 и не была выполнена.
//
//  ЧТО СДЕЛАНО В ПРОДАКШНЕ, ЧТОБЫ ЭТО СТАЛО ПРОВЕРЯЕМЫМ.
//    • `CenterWindow.PreviewColor(valid)` — цвета вынесены в публичный контракт;
//    • `CenterWindow.PaintPreview(renderers, valid)` — краска вынесена из `Update` в статический
//      метод: он не требует сцены и MonoBehaviour, поэтому проверяется прямо;
//    • рендереры превью собираются ВСЕ, а не первый (`CollectPreviewRenderers`).
//
//  ЧЕМ ЭТИ ТЕСТЫ ОТЛИЧАЮТСЯ ОТ «ДЛЯ ГАЛОЧКИ»: проверяются именно те три свойства, которые дали
//  дефект — пустой вход не падает, нулевые элементы массива пропускаются, покрашены ВСЕ рендереры
//  и именно в нужный цвет. Тест «пустой префаб» — тот самый, которого не было.
// ================================================================================================

using System.Text.RegularExpressions;
using KazistovVvUI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace KazistovVvTests
{
    /// <summary>Подсветка превью размещения: пустой префаб, частичный набор, цвет.</summary>
    public class KvCenterWindowPreviewTests
    {
        private GameObject root;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("KvPreviewTestRoot");
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
        }

        // ---------------------------------------------------------------- материалы и рендереры

        /// <summary>
        /// Материал без URP-свойства `_BaseColor`: покраска обязана пройти через `color`, а не упасть.
        /// Именно так ведёт себя новый материал Unity с шейдером `Sprites/Default`.
        /// </summary>
        private static Material NewShaderMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            return new Material(shader);
        }

        /// <summary>
        /// В РЕДАКТОРЕ Unity предупреждает, что `renderer.material` создаёт экземпляр материала
        /// (в PlayMode утечки нет). Проверяемый код работает в PlayMode, поэтому предупреждение
        /// ожидаемо: разрешаем его на время теста, иначе NUnit считает его провалом.
        /// </summary>
        private static void AllowEditModeMaterialWarning(int count)
        {
            for (int i = 0; i < count; i++)
            {
                // `LogAssert.Expect(LogType, Regex)` существует не во всех версиях пакета,
                // поэтому берём перегрузку со строкой-подстрокой: она есть во всех.
                LogAssert.Expect(LogType.Error,
                    new Regex("Instantiating material.*"));
            }
        }

        /// <summary>Рендерер с собственным экземпляром материала — как у настоящего превью.</summary>
        private Renderer NewRenderer(string name, out Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            var mesh = new Mesh();
            mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2 };
            go.AddComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = go.AddComponent<MeshRenderer>();
            material = NewShaderMaterial();
            renderer.sharedMaterial = material;
            return renderer;
        }

        /// <summary>
        /// ЦВЕТА СРАВНИВАЮТСЯ ПО КАНАЛАМ С ДОПУСКОМ. `Color` — это четыре float, и `Assert.AreEqual(Color, Color)`
        /// в Unity сравнивает через `Equals`, который для float не делает допуска: два «одинаковых»
        /// цвета, прошедших через материал, расходятся в последнем разряде и тест падает на ровном
        /// месте. Допуск 1e-3 — заметно меньше шага квантования цвета в 8 бит (1/255 ≈ 0.004).
        /// </summary>
        private static void AssertColor(Color expected, Color actual, string message)
        {
            Assert.AreEqual(expected.r, actual.r, 1e-3f, message + " (R)");
            Assert.AreEqual(expected.g, actual.g, 1e-3f, message + " (G)");
            Assert.AreEqual(expected.b, actual.b, 1e-3f, message + " (B)");
            Assert.AreEqual(expected.a, actual.a, 1e-3f, message + " (A)");
        }

        // ---------------------------------------------------------------- 1. ПУСТОЙ ПРЕФАБ

        /// <summary>
        /// ПУСТОЙ ПРЕФАБ — НЕ ПАДЕНИЕ (тот самый тест из плана 5.2).
        ///
        /// `PaintPreview(null, …)` и пустой массив обязаны вернуть 0 и не бросить исключения:
        /// превью без единого рендерера — законное состояние, а не ошибка вызывающего.
        /// </summary>
        [Test]
        public void PaintPreview_EmptyOrNull_ReturnsZeroWithoutThrowing()
        {
            Assert.DoesNotThrow(() => CenterWindow.PaintPreview(null, true),
                "null-массив рендереров обязан обрабатываться, а не падать");
            Assert.AreEqual(0, CenterWindow.PaintPreview(null, true));

            Assert.DoesNotThrow(() => CenterWindow.PaintPreview(new Renderer[0], false),
                "пустой массив рендереров обязан обрабатываться, а не падать");
            Assert.AreEqual(0, CenterWindow.PaintPreview(new Renderer[0], false));
        }

        /// <summary>
        /// NULL ВНУТРИ МАССИВА пропускается: рендерер мог быть уничтожен между сборкой и покраской
        /// (превью пересоздаётся при смене объекта). Раньше это было бы `NullReferenceException`.
        /// </summary>
        [Test]
        public void PaintPreview_SkipsDestroyedRenderer()
        {
            Material m;
            Renderer alive = NewRenderer("alive", out m);

            var destroyedGo = new GameObject("destroyed");
            destroyedGo.transform.SetParent(root.transform, false);
            var destroyedMesh = destroyedGo.AddComponent<MeshFilter>();
            destroyedMesh.sharedMesh = new Mesh();
            var destroyed = destroyedGo.AddComponent<MeshRenderer>();
            destroyed.sharedMaterial = NewShaderMaterial();
            Object.DestroyImmediate(destroyed);
            Object.DestroyImmediate(destroyedGo);

            AllowEditModeMaterialWarning(1);
            Assert.AreEqual(0, CenterWindow.PaintPreview(new[] { destroyed }, true),
                "массив из одного уничтоженного рендерера — красить нечего, исключения быть не должно");
            int painted = CenterWindow.PaintPreview(new[] { destroyed, alive }, true);
            Assert.AreEqual(1, painted, "покрашен обязан быть ровно один живой рендерер");
            AssertColor(CenterWindow.PreviewColor(true), alive.material.color,
                "живой рендерер обязан получить цвет «можно ставить»");
        }

        // ---------------------------------------------------------------- 2. ВСЕ РЕНДЕРЕРЫ, А НЕ ПЕРВЫЙ

        /// <summary>
        /// КРАСЯТСЯ ВСЕ РЕНДЕРЕРЫ. У модели робота их много: если красить только первый, оператор
        /// видит наполовину зелёный призрак и не понимает, разрешено ли место.
        /// </summary>
        [Test]
        public void PaintPreview_PaintsEveryRenderer()
        {
            Material m1, m2, m3;
            Renderer a = NewRenderer("a", out m1);
            Renderer b = NewRenderer("b", out m2);
            Renderer c = NewRenderer("c", out m3);

            AllowEditModeMaterialWarning(3);
            int painted = CenterWindow.PaintPreview(new[] { a, b, c }, false);
            Assert.AreEqual(3, painted, "обязаны быть покрашены все три рендерера");

            Color expected = CenterWindow.PreviewColor(false);
            AssertColor(expected, a.material.color, "первый рендерер не покрашен");
            AssertColor(expected, b.material.color, "второй рендерер не покрашен");
            AssertColor(expected, c.material.color, "третий рендерер не покрашен");
        }

        /// <summary>
        /// ОРИГИНАЛ НЕ ПОРТИТСЯ: красится ЭКЗЕМПЛЯР материала, а не общий. Иначе материал модели
        /// робота позеленел бы навсегда после первого наведения призрака.
        /// </summary>
        [Test]
        public void PaintPreview_DoesNotTouchSharedMaterial()
        {
            // `renderer.material` в редакторе Unity предупреждает об утечке материалов — это
            // ОЖИДАЕМОЕ поведение проверяемого кода (он работает в PlayMode, где утечки нет).
            // Разрешаем ровно одно такое сообщение и проверяем сами материалы.
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("Instantiating material due to calling renderer.material"));

            Material shared;
            Renderer r = NewRenderer("model", out shared);
            Color before = shared.color;

            CenterWindow.PaintPreview(new[] { r }, true);

            Assert.AreEqual(before, shared.color,
                "общий материал модели изменился — оригинал обязан остаться прежним");
            Assert.AreNotEqual(before, r.material.color, "экземпляр материала обязан быть перекрашен");
        }

        // ---------------------------------------------------------------- 3. КОНТРАКТ ЦВЕТА

        /// <summary>
        /// ЦВЕТ — КОНТРАКТ: зелёный «можно», красный «нельзя», оба полупрозрачные (0,45).
        ///
        /// Полупрозрачность не косметика: призрак не должен закрывать сцену. Если кто-то поменяет
        /// альфу на 1, тест это назовёт.
        /// </summary>
        [Test]
        public void PreviewColor_IsGreenWhenValidAndRedWhenNot()
        {
            Color ok = CenterWindow.PreviewColor(true);
            Color bad = CenterWindow.PreviewColor(false);

            Assert.Greater(ok.g, ok.r, "цвет «можно» обязан быть зелёным (G > R)");
            Assert.Greater(bad.r, bad.g, "цвет «нельзя» обязан быть красным (R > G)");

            Assert.AreEqual(0.45f, ok.a, 1e-4f, "прозрачность «можно» изменилась");
            Assert.AreEqual(0.45f, bad.a, 1e-4f, "прозрачность «нельзя» изменилась");
        }

        /// <summary>
        /// ПЕРЕКРАСКА ТУДА-ОБРАТНО: сначала «нельзя», потом «можно». Без второй покраски призрак
        /// остался бы красным после переноса в допустимую точку — это и есть та ошибка, которую
        /// оператор видит глазами, но которую нечем поймать без теста.
        /// </summary>
        [Test]
        public void PaintPreview_RepaintsOnValidityChange()
        {
            Material m;
            Renderer r = NewRenderer("toggle", out m);

            // Предупреждение редактирования Unity выдаёт ОДИН раз на материал: первая покраска
            // создаёт экземпляр, вторая перекрашивает тот же. Поэтому ожидаем ровно одно.
            AllowEditModeMaterialWarning(1);
            CenterWindow.PaintPreview(new[] { r }, false);
            AssertColor(CenterWindow.PreviewColor(false), r.material.color, "призрак в недопустимой точке");

            CenterWindow.PaintPreview(new[] { r }, true);
            AssertColor(CenterWindow.PreviewColor(true), r.material.color,
                "после переноса в допустимую точку призрак обязан позеленеть");
        }
    }
}
