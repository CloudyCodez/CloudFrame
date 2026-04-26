# CloudFrame 2.0 Roadmap

## Vision
CloudFrame 2.0 should feel less like a collection of good utilities and more like a polished gaming performance platform:

- fast and trustworthy
- modern and commercial-grade
- honest about what it can and cannot safely change
- focused on measurable gains, not placebo tweaks
- flexible enough for beginners and deep enough for power users

The goal is not to imitate every "game booster" on the market. The goal is to beat them on trust, clarity, and the quality of the improvements we can safely deliver.

## Product Pillars

### 1. Performance Science, Not Marketing
CloudFrame should speak in metrics that serious gamers actually care about:

- average FPS
- 1% low FPS
- frame pacing / frametime stability
- before/after comparison
- session reports that explain what changed

CloudFrame should prefer measured improvements over estimated ones wherever possible.

### 2. Safe, Supported Windows Tuning
CloudFrame should keep leaning into supported Windows mechanisms instead of risky service-killing or kernel tricks:

- power plan orchestration
- background processing mode
- memory priority shaping
- EcoQoS for non-game work
- recurring maintenance only when it helps
- borderless/window presentation helpers

CloudFrame should stay reversible by default.

### 3. Session-Aware Optimization
CloudFrame should behave like a live session manager:

- recognize launcher handoff
- recognize engine quirks
- adapt during a session
- back off when cleanup starts hurting smoothness
- remember what worked for a specific title

### 4. Commercial UX Quality
The app should feel fast, deliberate, and stable:

- snappy startup
- no janky tab transitions
- no surprise popups where inline UX will do
- clear status language
- clean control hierarchies
- strong defaults with deeper optional controls

### 5. Honest Experimental Lab
CloudFrame can explore advanced ideas, but it should label them correctly:

- external frame generation experiments
- resolution and borderless control
- technology-fit guidance for DLSS / FSR / XeSS
- anti-cheat-safe defaults and hard blocks where needed

## Research-Backed Direction

These are the working methods worth building further because they are either officially documented or grounded in known external tooling patterns:

### Supported Windows Performance Controls
- `powercfg` and power policy orchestration for gaming plans:
  - https://learn.microsoft.com/en-us/windows-hardware/design/device-experiences/powercfg-command-line-options
- background processing mode via `SetPriorityClass` / `PROCESS_MODE_BACKGROUND_BEGIN`:
  - https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-setpriorityclass
- memory priority and EcoQoS through `SetProcessInformation`:
  - https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-setprocessinformation
  - https://learn.microsoft.com/en-us/windows/win32/procthread/quality-of-service
- working-set trimming for safe background pressure relief:
  - https://learn.microsoft.com/en-us/windows/win32/api/psapi/nf-psapi-emptyworkingset

### Presentation / Graphics Improvements
- Windows 11 "Optimizations for windowed games":
  - https://support.microsoft.com/en-us/windows/optimizations-for-windowed-games-in-windows-11-3f006843-2c7e-4ed0-9a5e-f9389e535952
- Desktop Duplication as the foundation for external capture experiments:
  - https://learn.microsoft.com/en-us/windows/win32/direct3ddxgi/desktop-dup-api

### FPS / Benchmark Telemetry
- PresentMon remains the right low-risk telemetry foundation for CloudFrame:
  - https://github.com/GameTechDev/PresentMon/blob/main/README-ConsoleApplication.md

### Frame Generation Reality Check
- DLSS Frame Generation is integrated through NVIDIA Streamline, not something an external WinForms app can simply enable:
  - https://developer.nvidia.com/rtx/streamline
- AMD FSR 3 Frame Generation also expects deep game integration and swap-chain access:
  - https://gpuopen.com/presentations/2023/AMD_FidelityFX_Super_Resolution_3-Overview_and_Integration.pdf
- AMD AFMF is driver-level:
  - https://www.amd.com/en/products/software/adrenalin/afmf.html
- NVIDIA Optical Flow is a realistic acceleration path for future external experiments:
  - https://developer.nvidia.com/optical-flow-sdk

## What 2.0 Should Ship

## A. Performance Lab
- benchmark-quality average FPS, 1% low FPS, and frame pacing metrics
- guided before/after comparisons
- per-session performance report cards
- per-game memory of what actually helped
- quick benchmark workflow for a selected title

## B. Adaptive Boost Engine
- stronger offender scoring using CPU, memory, and sustained presence
- recurring maintenance that backs off when smoothness degrades
- preset logic tuned from real session outcomes, not static assumptions
- better launcher-to-game process handoff across Steam / EA / Epic / Riot / Ubisoft style wrappers

## C. Professional Game Workspace
- tighter Dashboard / Profiles / Tools / Resolution / Frame Gen Lab flow
- richer process scan onboarding
- better profile studio layout and inline editing
- clearer beginner path with smart defaults
- advanced mode that still feels fast

## D. Resolution and Presentation Control
- reliable resize + borderless prep
- restore-safe window style handling
- better borderless recommendations per engine and game shape
- per-profile window presets

## E. External Frame Gen Research Track
- hardware/backend readiness checks
- borderless-only prototype sessions
- anti-cheat hard blocking by default
- latency-first defaults
- explicit "experimental" labeling everywhere

## F. Reliability and Support
- first-run diagnostics
- issue export with richer session data
- self-update with change notes
- crash-safe restore and better recovery language

## Explicit No-Go List
CloudFrame 2.0 should not ship any of these as standard behavior:

- force-killing critical Windows services
- touching Defender / Windows Update / virtualization infrastructure
- `REALTIME_PRIORITY_CLASS`
- hidden driver installs
- kernel-mode tweaks
- injecting into anti-cheat games
- pretending DLSS / FSR / XeSS can be externally turned on for arbitrary games

## 2.0 Execution Phases

### Phase 1: Measurement Foundation
- richer FPS analytics
- 1% low and pacing-aware session reports
- better recommendation inputs

### Phase 2: Sharper Session Orchestration
- offender scoring
- smarter maintenance cadence
- better launcher/engine handoff
- per-game tuning memory upgrades

### Phase 3: UX and Workflow Polish
- smoother tab/view transitions
- cleaner tool hierarchy
- faster first-use and profile onboarding
- benchmark and report surfaces

### Phase 4: Experimental Lab Expansion
- stricter eligibility checks
- prototype capture/presentation session flow
- external frame-gen experimentation for non-anti-cheat titles only

## Success Criteria
CloudFrame 2.0 should be able to say all of this honestly:

- it starts fast and feels snappy
- it gives gamers better session insight than generic boosters
- it improves smoothness, not just headline FPS
- it explains what it changed
- it remembers what worked
- it stays inside safer technical boundaries than most "optimizer" utilities
