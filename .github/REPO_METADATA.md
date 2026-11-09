# Метаданные репозитория на GitHub / GitHub repository metadata

GitHub хранит описание («About»), темы и настройки **не в файлах**, а в самом
репозитории. Этот файл — источник правды: здесь записано, что именно применено,
чтобы значения можно было восстановить или проверить.

GitHub stores the description ("About"), topics and settings **outside the files**, in the
repository itself. This file is the source of truth recording what was applied, so the
values can be restored or verified.

---

## Описание / About

**RU (для показа в русскоязычных материалах):**

> Проприетарная платформа управления промышленными роботами на Unity: 6-осевой манипулятор
> и SCARA, планирование траекторий RRT, проверка коллизий и сингулярностей, допуск
> безопасности, экспорт KRL/KAREL/RAPID, интерфейс оператора в VR/MR/AR. Код открыт для
> ознакомления; для любого использования нужна коммерческая лицензия.

**EN (применено в репозитории / applied to the repository):**

> Proprietary Unity platform for industrial robot control: 6-axis arm + SCARA, RRT motion
> planning, collision and singularity checks, safety gate, KRL/KAREL/RAPID export, VR/MR/AR
> operator UI. Source-available for evaluation — a commercial license is required for any use.

## Темы / Topics

Applied to the repository (GitHub allows up to 20, lowercase, hyphens):

```
unity  unity3d  robotics  industrial-robots  robot-arm  six-axis-robot  scara
inverse-kinematics  motion-planning  trajectory-planning  rrt  collision-detection
digital-twin  xr  virtual-reality  mixed-reality  openxr  meta-quest  csharp
proprietary
```

## Прочие настройки / Other settings

| Настройка / Setting | Значение / Value |
|---|---|
| Default branch | `main` |
| Wiki | выключен, документация в `Docs/` / disabled, docs live in `Docs/` |
| Issues | только через контакты из `.github/ISSUE_TEMPLATE/config.yml` |
| Releases | теги `v0.1.x` соответствуют `bundleVersion` в `ProjectSettings.asset` |
| License (GitHub detection) | `LicenseRef-KazistovVv-Proprietary-1.0` — GitHub не распознаёт проприетарные тексты и показывает `NOASSERTION`, это ожидаемо |
| Social preview | логотип из `Assets/_Project/Branding/` |

## Как применить / How to apply

Через веб-интерфейс: **Settings → General → Description / Topics**.

Через API (нужен токен с правом `repo` или `public_repo`):

```bash
# описание
curl -X PATCH https://api.github.com/repos/V1iktorK/KazistovVv \
  -H "Authorization: Bearer $GITHUB_TOKEN" \
  -H "Accept: application/vnd.github+json" \
  -d '{"description":"Proprietary Unity platform for industrial robot control: 6-axis arm + SCARA, RRT motion planning, collision and singularity checks, safety gate, KRL/KAREL/RAPID export, VR/MR/AR operator UI. Source-available for evaluation — a commercial license is required for any use."}'

# темы
curl -X PUT https://api.github.com/repos/V1iktorK/KazistovVv/topics \
  -H "Authorization: Bearer $GITHUB_TOKEN" \
  -H "Accept: application/vnd.github+json" \
  -d '{"names":["unity","unity3d","robotics","industrial-robots","robot-arm","six-axis-robot","scara","inverse-kinematics","motion-planning","trajectory-planning","rrt","collision-detection","digital-twin","xr","virtual-reality","mixed-reality","openxr","meta-quest","csharp","proprietary"]}'
```

## История изменений метаданных / Change log

| Дата / Date | Изменение / Change |
|---|---|
| 2026-11-09 | Лицензия переведена на проприетарную коммерческую (`LicenseRef-KazistovVv-Proprietary-1.0`), добавлены README (EN/RU), `COMMERCIAL_LICENSE.md`, `SECURITY.md`, `CONTRIBUTING.md`, `THIRD_PARTY_NOTICES.md`, `CITATION.cff`; применены описание и 20 тем. |
