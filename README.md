<div align="center">

# KazistovVv

**Industrial robot control platform in Unity.**
Point at the workpiece, get **eight candidate trajectories**, inspect the ghost previews,
validate kinematics, collisions, joint limits and singularities — then send the robot on its way.

[![Unity](https://img.shields.io/badge/Unity-6000.5.6f1-000000?logo=unity&logoColor=white)](https://unity.com/)
[![C#](https://img.shields.io/badge/C%23-9-239120?logo=csharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![Render](https://img.shields.io/badge/render-URP%2017.5%20%2B%20HDRP%2017.5-0b6eb8)](Packages/manifest.json)
[![XR](https://img.shields.io/badge/XR-OpenXR%20%7C%20Meta%20Quest%20%7C%20PICO%204%20Ultra-6a1b9a)](Packages/manifest.json)
[![Platforms](https://img.shields.io/badge/platforms-Windows%20%7C%20Android%20(VR%2FAR)-2e7d32)](#build-and-run)
[![Languages](https://img.shields.io/badge/UI%20languages-7%20(ru%2C%20en%2C%20de%2C%20es%2C%20fr%2C%20ja%2C%20zh)-ef6c00)](Assets/StreamingAssets/kazistovvv_i18n)
[![License](https://img.shields.io/badge/license-proprietary%20commercial-c0392b)](LICENSE.md)

**English** · [Русский](README.ru.md)

</div>

> [!WARNING]
> **This is proprietary, closed-source software — not open source.**
> The code is published for **reading and evaluation only**. Any use in a product,
> in operations or in commercial activity requires a **paid commercial license**.
> Read [`LICENSE.md`](LICENSE.md) before you clone, build or run anything, and see
> [`COMMERCIAL_LICENSE.md`](COMMERCIAL_LICENSE.md) for terms. Licensing contact:
> **milovanovv30@gmail.com**.

---

## Table of contents

1. [What this is](#what-this-is)
2. [The operator workflow](#the-operator-workflow)
3. [Capabilities](#capabilities)
4. [Architecture in one screen](#architecture-in-one-screen)
5. [Repository layout](#repository-layout)
6. [Build and run](#build-and-run)
7. [Tests and CI](#tests-and-ci)
8. [Documentation map](#documentation-map)
9. [Project scale](#project-scale)
10. [License](#license)
11. [Third-party components](#third-party-components)

---

## What this is

**KazistovVv** is an engineering environment for industrial manipulators, built as a
Unity application. It is not a CAD viewer and not a replay tool: it is a complete
operator loop — **aim, plan, preview, validate, execute** — with a safety gate in
front of every motion.

| Concern | What is actually implemented |
|---|---|
| **Robots** | Exactly two per scene: a **6-axis arm** (`Робот_6ос_Стенд1`, `SixAxisController`) and a **SCARA** (`SCARA_Стенд2`, `SCARAController`, axes J1 / J2 / Z-prismatic) |
| **Motion** | RRT / bidirectional RRT planner, time-optimal reparameterisation, S-profile execution, constrained planning, path smoothing, energy-optimal variants |
| **Validation** | Forward and inverse kinematics (CCD + Jacobian), joint limits, singularity zones, self-collision, capsule-chain world collisions, keep-out zones, dynamic obstacles |
| **Safety** | A single choke point for every motion (`MotionExecutor`) behind `SafetyGate`; pre-run checks, fault injection, health monitoring, human mannequins in the workspace |
| **Operator UI** | Desktop UI in the spirit of **FreeCAD**, on uGUI, **assembled from code at runtime** — menu bar, icon toolbar, dock panels, model tree, properties, status bar |
| **XR** | Desktop, **VR**, **MR** and **AR** paths: XR Interaction Toolkit 3.5.1, OpenXR, Meta XR, AR Foundation 6.5.0; validated on **PICO 4 Ultra** and Meta Quest headsets |
| **Data exchange** | Export to **KRL** (KUKA), **KAREL** (FANUC), **RAPID** (ABB); import **URDF** and **STEP** |
| **Analysis** | Trajectory metrics (time, length, energy), heat maps, force heat maps, joint graphs, PDF reports, screenshot and video capture |
| **Production features** | Trajectory and pose recording / playback, action log, undo / redo, TCP calibration wizard, payload calculator, gripper, teach pendant, scenario and session managers, UDP collaboration, web dashboard |
| **Runtime AI** | **None.** No LLM and no neural network is involved in motion. The pipeline is deterministic kinematics, collision checking and planning |

The project is deliberately **deterministic and auditable**: every motion decision can be
replayed from the action log, and no generative model sits between the operator and the robot.

---

## The operator workflow

The flow is a state machine owned by `TrajectoryFlowController`:

```
Idle
  └─ red laser (Z) + LMB on a surface ─────────▶ PointSelected
        └─ planning runs ──────────────────────▶ TrajectoriesShown   (8 candidates = 8 ghosts)
              └─ green laser (X) + LMB on a trajectory ▶ PhantomsMoving
                    └─ green laser + LMB on a ghost ──▶ RobotMoving ──FinishMotion──▶ Idle
  Enter ──▶ PointMoveMode   (Q/E, W/S, A/D nudge the point; Enter confirms, Esc cancels)
  Esc   ──▶ ResetAll ──▶ Idle
```

Nothing moves the robot without passing the safety gate, and the executor is the only
entry point for motion in the entire codebase.

---

## Capabilities

<details>
<summary><b>Planning and motion</b></summary>

- RRT / BiRRT sampling planner with time-optimal reparameterisation
- Constrained planning, waypoint constraints, path smoothing
- Energy-optimal and time-optimal variants with a side-by-side comparison bench
- Singularity zones, keep-out zones, dynamic obstacles, collision optimisation
- Trajectory execution over time, trajectory tube visualisation, plan metrics

</details>

<details>
<summary><b>Robot, tooling and calibration</b></summary>

- Denavit–Hartenberg parameters, forward kinematics, CCD inverse kinematics, Jacobian
- Automatic joint discovery for a robot placed in the scene (`SixAxisAutoSetup`)
- Gripper, tool kinematics, TCP calibration wizard, payload calculator
- Pose library and posture selection

</details>

<details>
<summary><b>Safety and verification</b></summary>

- Single motion entry point (`MotionExecutor`) guarded by `SafetyGate`
- Joint, velocity and zone limit checks before any impulse is issued
- Fault simulation and pre-start verification, health monitor
- Human mannequins in the workspace for reach and clearance review

</details>

<details>
<summary><b>Interface and content production</b></summary>

- Runtime-built desktop UI with docking, themes and full keyboard navigation
- Multi-camera work, picture-in-picture, cinematics, scene studio (environment / light / materials)
- Screenshots, video capture via Unity Recorder, PDF reports
- Heat maps, force heat maps, joint graphs
- Virtual teach pendant, quick start, tutorial, presentation mode
- **7 UI languages** out of the box: Russian, English, German, Spanish, French, Japanese, Chinese

</details>

<details>
<summary><b>XR layer</b></summary>

- Desktop, VR, MR and AR operator paths behind one input abstraction
- OpenXR with the Meta X API layer; PICO runtime support for PICO 4 Ultra
- Hand tracking, gaze, voice and foveated input services
- Haptics and spatial audio
- Spatial anchors and calibration tools for placing the robot on a real table

</details>

---

## Architecture in one screen

```
┌──────────────────────────────────────────────────────────────────────┐
│ INPUT      Input/ (keyboard, mouse, gamepad, VR, MR)                 │
│            Core/FreeFlyCameraController · VR/ · Spatial/             │
├──────────────────────────────────────────────────────────────────────┤
│ TRAJECTORY TrajectoryFlowController  ← single source of truth        │
│            Planner → CollisionWorld → SafetyGate → MotionExecutor    │
├──────────────────────────────────────────────────────────────────────┤
│ ROBOT      Core/SixAxisController · SCARAController · IK · DH        │
├──────────────────────────────────────────────────────────────────────┤
│ UI         06_KazistovVv_UI/  dockable shell, panels, themes, i18n   │
├──────────────────────────────────────────────────────────────────────┤
│ FEATURES   01_Scripts/Features/  73 orchestrated capabilities        │
├──────────────────────────────────────────────────────────────────────┤
│ XR         01_Scripts/Xr/ (80 files) · OpenXR · XRI · AR Foundation  │
└──────────────────────────────────────────────────────────────────────┘
```

Full layer-by-layer breakdown: [`Docs/ARCHITECTURE.md`](Docs/ARCHITECTURE.md).

---

## Repository layout

| Path | Purpose |
|---|---|
| `Assets/_Project/` | **everything written for this project**: scenes, scripts, UI, tests |
| `Assets/_Project/00_Scenes/` | `MainScene` (desktop, the only scene in Build Settings) and `XrMain` (VR / MR) |
| `Assets/_Project/01_Scripts/` | the platform code — `Core`, `Trajectory`, `Features`, `Xr`, `VR`, `Ar`, `Input`, `Recording`, `Spatial`, `Editor` |
| `Assets/_Project/06_KazistovVv_UI/` | operator interface: shell, panels, themes, localisation |
| `Assets/_Project/08_Tests/` | EditMode and PlayMode test suites |
| `Assets/StreamingAssets/` | data readable without recompilation: 7 language dictionaries, settings, sample URDF / STEP |
| `Docs/` | engineering documentation and session reports |
| `Tools/` | PowerShell scripts: clean-console gate, builds, test runs |
| `ProjectSettings/`, `Packages/` | Unity project and dependency configuration |
| `PROJECT_CONTEXT.md` | the project's main engineering log |

---

## Build and run

### Requirements

| Requirement | Value |
|---|---|
| Unity | **6000.5.6f1** (Unity 6). Older editors will not open the project — the code uses Unity 6 find APIs |
| Render pipelines | Both **URP 17.5.0** (global default, Android / headset) **and HDRP 17.5.0** (quality level "PC") |
| Windows | Windows x86-64 with Windows Build Support (IL2CPP) |
| Android | Android Build Support, min SDK 32, target SDK 34 |
| XR | OpenXR loader + Meta X layer; PICO runtime for PICO 4 Ultra |
| First import | 5–15 minutes while `Library` is generated (HDRP shaders are slow) |

### Open

1. Unity Hub → **Add** → choose the folder containing `Assets`, `Packages`, `ProjectSettings`.
2. Wait for the import to finish.
3. Open **`Assets/_Project/00_Scenes/MainScene.unity`**.
4. A healthy start prints `[RobotInventory] Роботов в сцене: 2 …` and the UI banner, and
   creates the `KazistovVv_UI` object in the Hierarchy.

### Player settings worth knowing

| Setting | Value |
|---|---|
| `productName` | `KazistovVv` |
| `bundleVersion` | `0.1.0` |
| `applicationIdentifier` | `com.KazistovVv.app` |
| Colour space | Linear |
| Input handler | **Both** (legacy `Input` and the new Input System run together) |
| Scene in build | `MainScene` only |

Step-by-step instructions, including APK builds and identifier checks, are in
[`Docs/DEVELOPER_README.md`](Docs/DEVELOPER_README.md).

---

## Tests and CI

| Suite | Scope |
|---|---|
| **EditMode** | ~1,300 test cases across 122 test files — planning, kinematics, safety, UI, render pipeline, localisation |
| **PlayMode** | full robot scenario on a real scene |
| **E2E** | settings panel and the VR/MR switch, driven through real uGUI |
| **Repo guards** | build artefacts in the index, stray files inside `Assets`, `.meta` without an asset, non-ASCII paths, signing keys in git |

CI runs on GitHub Actions: [`.github/workflows/unity-tests.yml`](.github/workflows/unity-tests.yml)
on every branch and pull request, and [`.github/workflows/nightly.yml`](.github/workflows/nightly.yml)
for the deep nightly pass (PlayMode, E2E, APK build). The pipeline is explicit about what it
could **not** verify: a job that cannot run fails loudly instead of pretending to pass.

---

## Documentation map

| Document | Content |
|---|---|
| [`Docs/DEVELOPER_README.md`](Docs/DEVELOPER_README.md) | the developer handbook: open, build, run, extend, troubleshoot |
| [`Docs/ARCHITECTURE.md`](Docs/ARCHITECTURE.md) | layers, modules, invariants, extension points (historical snapshot) |
| [`Docs/PROJECT_STRUCTURE.md`](Docs/PROJECT_STRUCTURE.md) | where everything lives, subsystem by subsystem |
| [`Docs/API_REFERENCE.md`](Docs/API_REFERENCE.md) | API reference |
| [`Docs/CHANGELOG.md`](Docs/CHANGELOG.md) | change history |
| [`Docs/PROBLEMS.md`](Docs/PROBLEMS.md) | known defects and fixes |
| [`Docs/PLAN_NEXT_STEPS.md`](Docs/PLAN_NEXT_STEPS.md) | roadmap |
| [`PROJECT_CONTEXT.md`](PROJECT_CONTEXT.md) | session-by-session engineering log |
| [`Docs/GIT_AUDIT.md`](Docs/GIT_AUDIT.md) | repository hygiene audit |

---

## Project scale

| Metric | Value |
|---|---|
| C# scripts in `01_Scripts` | **257** files, ~**101,700** lines |
| Interface code | **40** files, ~**17,200** lines |
| Test code | **122** files, ~**34,700** lines, ~**1,300** test cases |
| Capability modules | **73** files in `Features` |
| XR layer | **80** files in `Xr` |
| Scenes | 7 (`MainScene` and `XrMain` are the working ones) |
| UI languages | 7 |

<sub>Counted from the working tree on 9 November 2026.</sub>

---

## License

**Proprietary commercial. All rights reserved.**

- **Permitted free of charge:** reading the code, cloning it, building an unmodified copy
  **for your own evaluation**, quoting short excerpts with attribution.
- **Requires a paid license:** any commercial use, any production use, redistribution,
  derivative works, providing access as a service, AI training on the code, commercial
  public demonstration.
- **Commercial licensing:** see [`COMMERCIAL_LICENSE.md`](COMMERCIAL_LICENSE.md) and write to
  **milovanovv30@gmail.com** with the subject `KazistovVv commercial license`.

Read the binding text in [`LICENSE.md`](LICENSE.md) (Russian is the controlling version,
English is provided for reference).

© 2026 Viktor K. (V1iktorK). Co-author: Ametist298.

---

## Third-party components

The repository includes Unity and third-party packages, asset packs, models, textures and
fonts that are **not** covered by the KazistovVv license and remain under their own terms —
including Unity's own licensing. The list is maintained in
[`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).

Unity is a trademark of Unity Technologies. PICO is a trademark of ByteDance.
Meta and Meta Quest are trademarks of Meta Platforms. All other names are the property
of their respective owners. This project is not affiliated with, endorsed by or sponsored
by any of them.
