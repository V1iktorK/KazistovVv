# Политика безопасности / Security Policy

**Контакт / Contact: milovanovv30@gmail.com**
Тема письма / Subject: `KazistovVv security`

---

## Русская версия

### Поддерживаемые версии

| Версия | Поддержка |
|---|---|
| Ветка `main` (0.1.x) | получает исправления безопасности |
| Ветка `Develop` | активная разработка, исправления попадают сюда первыми |
| Любые иные ветки, форки, теги | **не поддерживаются** |

### Как сообщить об уязвимости

1. Напишите на **milovanovv30@gmail.com** с темой `KazistovVv security`.
2. **Не** открывайте публичный issue и **не** публикуйте детали до согласования срока раскрытия.
3. Укажите: версию или коммит, платформу (Windows / Android / шлем), шаги воспроизведения,
   влияние и, если есть, минимальный пример.

Ответ о получении — в течение **7 рабочих дней**. Срок устранения согласуется
индивидуально в зависимости от тяжести. После выпуска исправления раскрытие деталей
приветствуется; по желанию заявитель указывается в благодарностях.

### Что относится к безопасности

- Исполнение кода вне ожидаемого контура: загрузка данных (URDF / STEP / конфигурации)
  с возможностью исполнения произвольного кода
- Обход `SafetyGate` или `MotionExecutor`: любая возможность отправить команду движения
  в обход проверок лимитов, коллизий и зон
- Утечка ключей подписи, токенов, сертификатов через репозиторий или артефакты сборки
- Сетевые функции: UDP-совместная работа, веб-дашборд, ROS-коннектор — приём
  неаутентифицированных команд движения
- Подмена данных локализации или настроек, приводящая к исполнению кода
- Уязвимости в прямой зависимости, влияющие на проект (сообщите версию пакета)

### Что к безопасности не относится

- Ошибки планировщика и кинематики без последствий для безопасности (это обычные дефекты —
  см. [`CONTRIBUTING.md`](CONTRIBUTING.md))
- Требования, связанные с сертификацией промышленной безопасности: ПО **не** является
  сертифицированным средством управления и не заявляется таковым
- Отсутствие функций, которые проект и не обещал (см. [`Docs/PROBLEMS.md`](Docs/PROBLEMS.md))

### Важное ограничение

**ПО не предназначено для прямого управления реальным оборудованием без внешнего
контура безопасности** (аппаратные ограничители, СБЗ, независимая система аварийного
останова). Ответственность за ввод в эксплуатацию лежит на эксплуатирующей организации.

---

## English version

### Supported versions

| Version | Supported |
|---|---|
| `main` branch (0.1.x) | receives security fixes |
| `Develop` branch | active development, fixes land here first |
| Any other branch, fork or tag | **not supported** |

### Reporting a vulnerability

1. Email **milovanovv30@gmail.com** with the subject `KazistovVv security`.
2. **Do not** open a public issue and **do not** publish details before a disclosure
   timeline is agreed.
3. Include: version or commit, platform (Windows / Android / headset), reproduction steps,
   impact and a minimal example where possible.

You will receive an acknowledgement within **7 business days**. The fix timeline is agreed
individually depending on severity. After a fix ships, disclosure is welcome; credit is
given on request.

### In scope

- Code execution outside the expected boundary: parsing of data (URDF / STEP / configs)
  that allows arbitrary code execution
- Bypassing `SafetyGate` or `MotionExecutor`: any way to issue a motion command that skips
  limit, collision or zone checks
- Leakage of signing keys, tokens or certificates via the repository or build artefacts
- Network features: UDP collaboration, web dashboard, ROS connector accepting
  unauthenticated motion commands
- Tampering with localisation or settings data that leads to code execution
- Vulnerabilities in a direct dependency that affect the project (include the package version)

### Out of scope

- Planner and kinematics defects with no safety impact (ordinary bugs — see
  [`CONTRIBUTING.md`](CONTRIBUTING.md))
- Industrial-safety certification requirements: the software is **not** a certified control
  device and does not claim to be
- Missing features the project never promised (see [`Docs/PROBLEMS.md`](Docs/PROBLEMS.md))

### Important limitation

**The software is not intended to directly control real equipment without an external
safety layer** (hard limiters, safety-rated PLC, independent emergency stop). Commissioning
responsibility rests with the operating organisation.
