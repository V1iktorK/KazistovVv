        // ================================================================== Б2: источники вариантов

        /// <summary>Путь к ассету глобальных настроек URP (в нём живёт настройка шейдеров террейна).</summary>
        public const string UrpGlobalSettingsPath = "Assets/Settings/UniversalRenderPipelineGlobalSettings.asset";

        /// <summary>Путь к профилю объёма по умолчанию (URP применяет его ко всем камерам).</summary>
        public const string UrpDefaultVolumeProfilePath = "Assets/Settings/DefaultVolumeProfile.asset";

        /// <summary>
        /// ОТСЕЧЬ ЛИШНИЕ ИСТОЧНИКИ ВАРИАНТОВ ШЕЙДЕРОВ (карточка Б2, Этап А).
        ///
        /// ЗАЧЕМ. Сборка §30 шла 46 часов и не дошла до конца: из ~122 000 вариантов шейдеров
        /// ~105 000 приходилось на постобработку (`UberPost` — **92 160 вариантов на каждый
        /// из двух проходов**), ещё **17 472** — на шейдеры ТЕРРЕЙНА, которого в проекте нет
        /// ни одного (замер: проход `ForwardLit` — 13 824 варианта, 18 836 с). Это не «слабый
        /// компьютер»: это настройки проекта, и обе видны в логе сборки.
        ///
        /// ЧТО ДЕЛАЕТСЯ ЗДЕСЬ:
        ///   1) выключаются шейдеры террейна;
        ///   2) из профиля объёма по умолчанию убирается мусор: компоненты из тестовых сборок
        ///      Unity и посторонние объекты с потерянным скриптом.
        ///
        /// ЧЕГО ЗДЕСЬ НЕТ И ПОЧЕМУ. Профили HDRP и демо-сцены не чистятся: на них рабочий стол
        /// (`Assets/HDRPDefaultResources/DefaultSettingsVolumeProfile.asset`), и его правка
        /// изменила бы вид на компьютере — этого владелец не разрешал. Варианты постобработки
        /// отсекает отдельный проход сборки (`KvPostProcessVariantStripper`, `IPreprocessShaders`),
        /// и он срабатывает ТОЛЬКО если в сценах сборки ни одна камера не рисует постобработку.
        ///
        /// Оба действия подтверждаются ПЕРЕЧИТЫВАНИЕМ, а не намерением (правило §29: «правка
        /// сделана только если её видно в сохранённом файле»).
        /// </summary>
        [MenuItem(MenuRoot + "7. Отсечь лишние варианты шейдеров (Б2)", false, 70)]
        public static void StripUnusedVariantSources()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[KvStrip] === ОТСЕЧЕНИЕ ЛИШНИХ ИСТОЧНИКОВ ВАРИАНТОВ (Б2) ===");
            sb.AppendLine("  шейдеры террейна: " + DisableTerrainShaders());
            sb.AppendLine("  профиль объёма по умолчанию: " + CleanDefaultVolumeProfile());
            sb.AppendLine("  варианты постобработки при сборке: " +
                (KvPostProcessVariantStripper.ProjectHasNoPostProcessingCameras()
                    ? "ОТСЕКАЮТСЯ — в сценах сборки постобработку никто не рисует"
                    : "НЕ отсекаются — в сценах сборки есть камера с постобработкой"));
            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// ВЫКЛЮЧИТЬ ШЕЙДЕРЫ ТЕРРЕЙНА. Пробуются ДВА пути, и это не перестраховка: замер
        /// 08.10.2026 показал, что штатный доступ `GraphicsSettings.TryGetRenderPipelineSettings
        /// &lt;URPTerrainShaderSetting&gt;` в этом наборе пакетов возвращает **false** — настройка
        /// в ассете есть (`m_IncludeTerrainShaders`), а через API не находится. Правило проекта:
        /// «в файле написано» и «видно из кода» — разные ответы, и оба надо уметь получать.
        /// </summary>
        public static string DisableTerrainShaders()
        {
            // Путь 1 — штатный доступ к настройке URP.
            URPTerrainShaderSetting terrain;
            if (GraphicsSettings.TryGetRenderPipelineSettings<URPTerrainShaderSetting>(out terrain))
            {
                if (!terrain.includeTerrainShaders) return "уже выключены (правка не нужна)";

                terrain.includeTerrainShaders = false;
                AssetDatabase.SaveAssets();

                URPTerrainShaderSetting check;
                bool stillOn = GraphicsSettings.TryGetRenderPipelineSettings<URPTerrainShaderSetting>(out check) &&
                               check.includeTerrainShaders;
                if (!stillOn) return "выключены через API URP (подтверждено перечитыванием)";
            }

            // Путь 2 — правка самого ассета настроек (как в EnsureAndroidHasNoHdrpLevels).
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(UrpGlobalSettingsPath);
            if (asset == null) return "ассет настроек URP не найден: " + UrpGlobalSettingsPath;

            var so = new SerializedObject(asset);
            SerializedProperty property = FindPropertyByName(so.GetIterator(), "m_IncludeTerrainShaders");
            if (property == null)
                return "в настройках URP нет поля m_IncludeTerrainShaders — проверьте версию пакета";

            bool before = property.boolValue;
            if (!before) return "уже выключены (правка не нужна)";

            property.boolValue = false;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(UrpGlobalSettingsPath, ImportAssetOptions.ForceUpdate);

            var reread = new SerializedObject(AssetDatabase.LoadAssetAtPath<ScriptableObject>(UrpGlobalSettingsPath));
            SerializedProperty after = FindPropertyByName(reread.GetIterator(), "m_IncludeTerrainShaders");
            bool value = after != null && after.boolValue;

            return value
                ? "НЕ ВЫКЛЮЧИЛОСЬ (осталось включённым) — проверьте вручную"
                : "выключены правкой ассета настроек: было " + before + " → стало " + value +
                  " (террейнов в сценах сборки нет — им нечего рисовать)";
        }

        /// <summary>Найти свойство по имени во всём дереве сериализованного объекта.</summary>
        private static SerializedProperty FindPropertyByName(SerializedProperty iterator, string name)
        {
            while (iterator.Next(true))
                if (iterator.name == name) return iterator;
            return null;
        }

        /// <summary>
        /// УБРАТЬ ИЗ ПРОФИЛЯ ОБЪЁМА ПО УМОЛЧАНИЮ МУСОР. Мусор здесь ДВУХ видов, и второй нашёлся
        /// только при работе инструмента (08.10.2026):
        ///   1) компоненты СПИСКА профиля из тестовых сборок Unity (`…Editor.Tests`);
        ///   2) **посторонние объекты в самом ассете** — компоненты с потерянным скриптом
        ///      (`m_Script: {fileID: 0}`). Их НЕТ в списке `components` (в файле 28 блоков,
        ///      а компонентов профиля 19), поэтому чтение профиля их не видит: они лежат
        ///      в настройках проекта мёртвым грузом и находятся только через `LoadAllAssetsAtPath`.
        /// </summary>
        public static string CleanDefaultVolumeProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(UrpDefaultVolumeProfilePath);
            if (profile == null) return "профиль не найден: " + UrpDefaultVolumeProfilePath;

            int before = profile.components.Count;
            var removed = new List<string>();

            // 1. Компоненты списка профиля: убираем пришедшие из тестовых сборок.
            for (int i = profile.components.Count - 1; i >= 0; i--)
            {
                VolumeComponent component = profile.components[i];

                if (component == null)
                {
                    removed.Add("пустая запись в списке компонентов");
                    profile.components.RemoveAt(i);
                    continue;
                }

                string type = component.GetType().FullName ?? string.Empty;
                if (type.IndexOf(".Tests", StringComparison.Ordinal) < 0) continue;

                removed.Add("компонент из тестовой сборки: " + component.GetType().Name);
                profile.components.RemoveAt(i);
                UnityEngine.Object.DestroyImmediate(component, true);
            }

            // 2. Посторонние объекты в ассете (потерянные скрипты) — то, чего не видно в списке.
            var all = AssetDatabase.LoadAllAssetsAtPath(UrpDefaultVolumeProfilePath);
            if (all != null)
            {
                foreach (UnityEngine.Object obj in all)
                {
                    if (obj == null) continue;
                    if (obj is VolumeProfile) continue;
                    if (obj is VolumeComponent) continue;

                    removed.Add("посторонний объект в ассете: " + obj.GetType().Name);
                    UnityEngine.Object.DestroyImmediate(obj, true);
                }
            }

            if (removed.Count == 0) return "мусора нет (компонентов " + before + ")";

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(UrpDefaultVolumeProfilePath, ImportAssetOptions.ForceUpdate);

            var check = AssetDatabase.LoadAssetAtPath<VolumeProfile>(UrpDefaultVolumeProfilePath);
            int after = check != null ? check.components.Count : -1;
            return string.Format("убрано {0} (компонентов было {1}, стало {2}): {3}",
                removed.Count, before, after, string.Join(" · ", removed.ToArray()));
        }
