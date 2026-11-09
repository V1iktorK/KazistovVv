# Сторонние компоненты / Third-Party Notices

**Обновлено / Updated: 9 ноября 2026 г. / 9 November 2026**

Настоящий файл перечисляет компоненты репозитория **KazistovVv**, которые **не**
принадлежат правообладателям и **не** покрываются лицензией
[`LICENSE.md`](LICENSE.md). Они используются на условиях лицензий их собственных
правообладателей.

This file lists components of the **KazistovVv** repository that are **not** owned by
the Rights Holders and are **not** covered by [`LICENSE.md`](LICENSE.md). They are used
under the terms of their own rights holders' licenses.

> Коммерческая лицензия на KazistovVv **не заменяет** лицензии перечисленных
> компонентов. Проверка условий для конкретного сценария использования (особенно
> при распространении продукта) — обязанность лицензиата.
>
> A commercial license for KazistovVv **does not replace** the licenses of the
> components listed here. Verifying the terms for a specific use case — especially when
> redistributing a product — is the licensee's responsibility.

---

## 1. Движок и пакеты Unity / Unity engine and packages

| Компонент / Component | Версия / Version | Лицензия / License |
|---|---|---|
| Unity Editor & Runtime | 6000.5.6f1 | Unity Terms of Service / редакционная лицензия Unity |
| Universal Render Pipeline (`com.unity.render-pipelines.universal`) | 17.5.0 | Unity Companion License |
| High Definition Render Pipeline (`com.unity.render-pipelines.high-definition`) | 17.5.0 | Unity Companion License |
| XR Interaction Toolkit (`com.unity.xr.interaction.toolkit`) | 3.5.1 | Unity Companion License |
| OpenXR Plugin (`com.unity.xr.openxr`) | 1.15.1 | Unity Companion License |
| Meta OpenXR (`com.unity.xr.meta-openxr`) | 2.4.1 | Unity Companion License |
| AR Foundation (`com.unity.xr.arfoundation`) | 6.5.0 | Unity Companion License |
| ARCore XR Plugin (`com.unity.xr.arcore`) | 6.5.0 | Unity Companion License |
| XR Hands (`com.unity.xr.hands`) | 1.9.0 | Unity Companion License |
| Input System (`com.unity.inputsystem`) | 1.20.0 | Unity Companion License |
| Cinemachine (`com.unity.cinemachine`) | 3.1.7 | Unity Companion License |
| Visual Effect Graph (`com.unity.visualeffectgraph`) | 17.5.0 | Unity Companion License |
| Timeline (`com.unity.timeline`) | 1.8.12 | Unity Companion License |
| uGUI (`com.unity.ugui`) | 2.5.0 | Unity Companion License |
| ProBuilder (`com.unity.probuilder`) | 6.1.2 | Unity Companion License |
| Test Framework (`com.unity.test-framework`) | 1.4.7 | Unity Companion License |
| Code Coverage (`com.unity.testtools.codecoverage`) | 1.3.0 | Unity Companion License |
| AI Inference (`com.unity.ai.inference`) | 2.6.1 | Unity Companion License |
| glTFast (`com.unity.cloud.gltfast`) | 6.20.0 | Unity Companion License |
| Deep Linking (`com.unity.cloud.deep-linking`) | 1.3.0 | Unity Companion License |
| Version Control / Collab Proxy (`com.unity.collab-proxy`) | 2.13.3 | Unity Companion License |
| Rider / Visual Studio integration | 3.0.38 / 2.0.26 | Unity Companion License |
| ROS TCP Connector (`com.unity.robotics.ros-tcp-connector`) | git | Apache License 2.0 |
| Unity Recorder (`com.unity.recorder`) | редакторский пакет / editor package | Unity Companion License |

Полный список зависимостей — [`Packages/manifest.json`](Packages/manifest.json) и
[`Packages/packages-lock.json`](Packages/packages-lock.json).

Unity является торговой маркой Unity Technologies. Условия лицензирования Unity
применяются независимо от настоящего репозитория и могут требовать отдельной
подписки (Unity Personal / Pro / Enterprise) в зависимости от организации и дохода.

Unity is a trademark of Unity Technologies. Unity licensing terms apply independently
of this repository and may require a separate subscription depending on the
organisation and its revenue.

---

## 2. Пакеты ассетов и модели в `Assets/` / Asset packs and models

Следующие каталоги содержат сторонние модели, окружения и материалы. Права на них
принадлежат их авторам; в репозиторий они включены как рабочие ресурсы сцены.

The following folders contain third-party models, environments and materials. Rights
belong to their authors; they are included in the repository as working scene assets.

| Каталог / Folder | Что содержит / Content |
|---|---|
| `Assets/Premises/` | модель помещения (ангар) / hangar environment model |
| `Assets/ROOMS/` | модели помещений / room environments |
| `Assets/TABLES/`, `Assets/WorkTables/` | модели столов и верстаков / tables and workbenches. Верстаки — набор **Kenney**, лицензия **CC0 1.0**, текст приложен: `Assets/WorkTables/Workbenches/Models/Kenney_License_CC0.txt` / workbenches are a **Kenney** pack under **CC0 1.0** |
| `Assets/Robots/` | модели промышленных роботов / industrial robot models |
| `Assets/Samples/` | примеры из пакетов Unity XR / Unity XR package samples |
| `Assets/Plugins/` | нативные библиотеки (Android, Roslyn) / native libraries |
| `Assets/TutorialInfo/` | обучающий пример Unity / Unity tutorial sample |
| `Assets/StreamingAssets/*.urdf`, `*.step` | демонстрационные образцы моделей / demo model samples |

> **Важно для распространителей.** Часть моделей и окружений в `Assets/` получена
> из сторонних наборов ассетов. Перед публикацией или распространением собственного
> продукта на основе этого репозитория **проверьте лицензию каждого набора ассетов**:
> условия магазина ассетов, как правило, запрещают распространение моделей как
> самостоятельных файлов и требуют их «встроенности» в приложение.
>
> **Important for distributors.** Some models and environments under `Assets/` come from
> third-party asset packs. Before publishing or distributing your own product based on
> this repository, **verify the license of every asset pack**: asset store terms usually
> forbid redistributing models as standalone files and require them to be embedded in an
> application.

---

## 3. Шрифты, иконки и прочие ресурсы / Fonts, icons and other resources

Сторонние шрифты, иконки и пиктограммы, если они присутствуют в `Assets/`, используются
на условиях их собственных лицензий (как правило, SIL Open Font License или лицензия
соответствующего набора иконок). Точный перечень определяется по метаданным конкретного
файла.

Third-party fonts, icons and pictograms, where present under `Assets/`, are used under
their own licenses (typically the SIL Open Font License or the respective icon set's
license). The exact terms are determined by the metadata of each file.

---

## 4. Как сообщить о нарушении / Reporting an alleged infringement

Если вы считаете, что какой-либо материал в этом репозитории нарушает ваши права,
напишите на **milovanovv30@gmail.com** с темой `KazistovVv IP notice`, указав:
конкретный файл или каталог, охраняемый результат, основание прав и контакт для ответа.
Обоснованные обращения рассматриваются в приоритетном порядке; спорный материал
снимается до выяснения обстоятельств.

If you believe any material in this repository infringes your rights, email
**milovanovv30@gmail.com** with the subject `KazistovVv IP notice`, stating the specific
file or folder, the protected work, the basis of your rights and a contact address.
Substantiated notices are handled as a priority; the disputed material is removed
pending resolution.
