        // ================================================================== Б2: источники вариантов

        /// <summary>
        /// ОТСЕЧЬ ЛИШНИЕ ИСТОЧНИКИ ВАРИАНТОВ ШЕЙДЕРОВ (карточка Б2, Этап А).
        ///
        /// ЗАЧЕМ. Сборка §30 шла 46 часов и не дошла до конца: из ~122 000 вариантов шейдеров
        /// ~105 000 приходилось на постобработку (`UberPost` — **92 160 вариантов на каждый
        /// из двух проходов**), ещё **17 472** — на шейдеры ТЕРРЕЙНА, которого в проекте нет
        /// ни одного (замер: проход `ForwardLit` — 13 824 варианта, 18 836 с). Это не «слабый
        /// компьютер»: это две настройки проекта, и обе видны в логе сборки.
        ///
        /// ЧТО ДЕЛАЕТСЯ ЗДЕСЬ:
        ///   1) выключаются шейдеры террейна (`URPTerrainShaderSetting.includeTerrainShaders`);
        ///   2) из профиля объёма по умолчанию убирается тестовый мусор Unity — компоненты
        ///      с потерянным скриптом и компоненты из тестовых сборок (`…Tests`).
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
        /// ВЫКЛЮЧИТЬ ШЕЙДЕРЫ ТЕРРЕЙНА. Возвращает строку-доказательство для журнала.
        /// Опора на факт: в сценах сборки нет ни одного террейна (проверено и закреплено тестом
        /// `UrpGlobalSettings_TerrainShadersAreOff_AndProjectHasNoTerrain`).
        /// </summary>
        public static string DisableTerrainShaders()
        {
            URPTerrainShaderSetting terrain;
            if (!GraphicsSettings.TryGetRenderPipelineSettings<URPTerrainShaderSetting>(out terrain))
                return "настройка URP не найдена (другая версия пакета?) — ничего не изменено";

            bool before = terrain.includeTerrainShaders;
            if (!before) return "уже выключены (правка не нужна)";

            terrain.includeTerrainShaders = false;
            AssetDatabase.SaveAssets();

            // ПЕРЕЧИТЫВАНИЕ: подтверждаем фактом, а не намерением.
            URPTerrainShaderSetting check;
            bool after = GraphicsSettings.TryGetRenderPipelineSettings<URPTerrainShaderSetting>(out check)
                ? check.includeTerrainShaders
                : true;

            return after
                ? "НЕ ВЫКЛЮЧИЛОСЬ (осталось включённым) — проверьте вручную"
                : "выключены: было " + before + " → стало " + after +
                  " (террейнов в сценах сборки нет — им нечего рисовать)";
        }

        /// <summary>
        /// УБРАТЬ ИЗ ПРОФИЛЯ ОБЪЁМА ПО УМОЛЧАНИЮ ТЕСТОВЫЙ МУСОР. В профиле лежат компоненты
        /// из тестовых сборок Unity (`Unity.RenderPipelines.Core.Editor.Tests`) и компоненты
        /// с потерянной ссылкой на скрипт (`m_Script: {fileID: 0}`) — в настройках проекта это
        /// мусор: он ничего не делает, но путает и мешает читать профиль.
        /// </summary>
        public static string CleanDefaultVolumeProfile()
        {
            const string path = "Assets/Settings/DefaultVolumeProfile.asset";

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null) return "профиль не найден: " + path;

            int before = profile.components.Count;
            var removed = new List<string>();

            for (int i = profile.components.Count - 1; i >= 0; i--)
            {
                VolumeComponent component = profile.components[i];

                if (component == null)
                {
                    removed.Add("компонент с потерянным скриптом");
                    profile.components.RemoveAt(i);
                    continue;
                }

                string type = component.GetType().FullName ?? string.Empty;
                if (type.IndexOf(".Tests", StringComparison.Ordinal) < 0) continue;

                removed.Add(component.GetType().Name);
                profile.components.RemoveAt(i);
                UnityEngine.Object.DestroyImmediate(component, true);
            }

            if (removed.Count == 0) return "мусора нет (компонентов " + before + ")";

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var check = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            int after = check != null ? check.components.Count : -1;
            return string.Format("убрано {0} (было {1}, стало {2}): {3}",
                removed.Count, before, after, string.Join(", ", removed.ToArray()));
        }
