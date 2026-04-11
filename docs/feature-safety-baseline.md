# Cloud's FrameBoost Safety Baseline

This document turns the initial feature list into a safe implementation baseline.

Goal:
- Improve game performance without disabling critical Windows functionality.
- Avoid crashes, hangs, boot issues, anti-cheat conflicts, driver instability, or BSODs.
- Make every optimization reversible and scoped to the current session whenever possible.

## Safety Principles

FrameBoost should follow these rules for every feature:

1. Prefer per-process changes over system-wide changes.
2. Prefer temporary suspension over disabling startup configuration.
3. Never touch kernel, driver, storage, memory-manager, security, or networking core services.
4. Never touch virtualization drivers, hypervisors, virtual switch components, VM worker services, or host-side VM processes.
5. Never apply undocumented registry tweaks by default.
6. Never use `REALTIME_PRIORITY_CLASS`.
7. Never force hard CPU affinity masks unless the user explicitly opts in and the game is known to behave well with it.
8. Never unload drivers, restart GPUs, flush kernel memory structures, or call unsupported cleanup tools.
9. Every action must support preflight checks, rollback, timeout-based recovery, and failure logging.
10. If a dependency graph is unclear, do not stop the service.
11. If anti-cheat protected games are detected, use compatibility mode and skip invasive process tuning.

## Feature Review

### 1. Smart Game Detection

Safe baseline:
- Detect launch through process monitoring and a user-maintained game allowlist.
- Add optional integrations for Steam, Epic, and Xbox launch detection later.
- Require confidence checks before boost mode is applied:
  - executable path
  - signed publisher when available
  - user-approved profile or known game catalog match

Do not:
- Inject DLLs into game processes.
- Hook graphics APIs.
- Interfere with anti-cheat protected processes.

Risk level:
- Low, if detection stays read-only.

### 2. Background Service Killer

This is the riskiest area in the current feature list.

Safe baseline:
- Do not market this as a broad "service killer."
- Replace it with a constrained "Background Activity Manager."
- Start with user-mode apps and non-critical tray processes before touching services.
- Only allow temporary stop/suspend for a strict allowlist after dependency checks pass.

Potentially safe as opt-in, with restore support:
- Xbox Game Bar related user-facing components
- selected vendor updaters
- selected launchers after the game is already running

Do not disable or stop:
- Windows Defender or security services
- Windows Update stack services
- Hyper-V services
- virtual switch and virtualization platform services
- WSL/virtual machine compute services
- RPC-related services
- DCOM infrastructure
- Plug and Play
- Task Scheduler
- Event Log
- WMI
- BITS
- DHCP, DNS, NLA, or core network stack services
- audio services
- graphics driver services
- storage, filesystem, encryption, or virtualization services
- anything marked critical, protected, driver-backed, or with dependent services we do not fully understand

Design rule:
- Use Service Control Manager queries to inspect service state, start type, and dependencies before any action.
- If dependency inspection fails, skip the service.

Recommendation:
- Version 1 should avoid stopping Windows services entirely unless we have a short vetted allowlist and strong restore logic.

Risk level:
- High, unless heavily restricted.

### 3. Dynamic Power Mode Switching

Safe baseline:
- Support switching between the current user plan and a user-selected gaming plan.
- Default choices should be `Balanced` and `High performance`.
- If `Ultimate Performance` is available, expose it as optional rather than default.

Do:
- Save the prior active scheme GUID before any switch.
- Restore the exact previous plan on exit.
- Revert on crash or forced close through a watchdog process.

Do not:
- Modify hidden processor power settings by default.
- Write aggressive registry power tweaks.
- Assume Ultimate Performance exists on every system.

GPU-specific tuning:
- Keep this out of the first release unless implemented through official vendor tooling with explicit user opt-in.
- Do not write undocumented driver registry keys.

Risk level:
- Low to medium.

### 4. CPU Core and Priority Optimizer

Safe baseline:
- Raise the game process to `HIGH_PRIORITY_CLASS` only when appropriate.
- Lower selected background processes to `BELOW_NORMAL_PRIORITY_CLASS` or apply EcoQoS where supported.
- Prefer CPU Sets or soft placement hints over strict hard affinity masks.

Do not:
- Use `REALTIME_PRIORITY_CLASS`.
- Force exclusive P-core pinning on all hybrid CPUs.
- Change priorities for system, driver-host, security, audio, or shell-critical processes.

Important:
- Some games, launchers, anti-cheat systems, and hybrid CPU schedulers behave worse with forced affinity.
- Affinity changes should be profile-based, opt-in, and easy to disable.

Risk level:
- Medium if conservative, high if aggressive.

### 5. VRAM and RAM Cleanup

This item needs to be narrowed significantly.

Safe baseline:
- Offer lightweight memory-pressure reduction by closing user-selected background apps.
- Optionally prompt the user to close large launchers or browsers before starting a game.

Do not claim or attempt:
- "clear cached GPU memory" through unsupported methods
- aggressive standby list clearing as a default optimization
- undocumented memory manager manipulation
- repeated forced memory purges during gameplay

Reason:
- These tactics often create placebo gains, can increase stutter, and in some cases destabilize the system.

Recommendation:
- Move "Deep Clean" out of the core product.
- If ever added, it should be hidden under Advanced Labs, off by default, with warnings.

Risk level:
- Medium to high.

### 6. Network Latency Optimizer

This should be handled very carefully.

Safe baseline:
- Detect adapter capabilities and expose only reversible, adapter-specific settings.
- Keep all network tweaks disabled by default until validated on the current adapter and driver.
- Let users benchmark before and after applying any network preset.

Do not:
- Apply a universal low-latency preset to every NIC.
- Modify advanced driver properties blindly.
- Disable features required for stability on Wi-Fi adapters or laptops.

Better first-release version:
- Audit current adapter settings
- identify gaming-relevant capabilities
- offer recommendations rather than auto-applying

Reason:
- Official Microsoft networking guidance notes that tuning interrupt moderation for throughput can hurt response time, and NIC behavior varies by hardware and driver.

Risk level:
- High if automatic, medium if advisory-only.

### 7. Shader Cache Maintenance

Safe baseline:
- Detect known shader cache locations for supported vendors and APIs.
- Show cache size, last-modified timestamps, and a cleanup prompt.
- Only clean when the game is not running.

Do not:
- Auto-delete caches on every launch.
- Remove caches that are still being built or used.

Recommendation:
- Manual or profile-based cleanup is safe enough.

Risk level:
- Low to medium.

### 8. Thermal Stabilization

Safe baseline:
- Monitor CPU and GPU temperature, clocks, utilization, and fan behavior.
- When thresholds are exceeded, reduce FrameBoost's own background work and deprioritize optional helper tasks.
- Notify the user when thermal limits are likely hurting performance.

Do not:
- Attempt direct undervolting, power-limit control, or fan curve writes in the first release.
- Throttle unrelated system services in a way that risks instability.

Recommendation:
- This should be observational plus light background de-prioritization only.

Risk level:
- Low if limited to monitoring and mild background tuning.

### 9. Overlay-Free Mode

Safe baseline:
- Detect common overlays and offer per-overlay toggles or launch-time reminders.
- Disable only when an official switch, startup flag, or supported settings path exists.

Do not:
- Kill arbitrary vendor overlay processes mid-session if they share core components with capture, audio, or GPU software.

Risk level:
- Low to medium.

### 10. Game Profiles

Safe baseline:
- Profiles should store only supported, reversible settings.
- Every profile change should be tagged with:
  - scope
  - time applied
  - restore action
  - verification result

Recommendation:
- This should be a core feature and the main control plane for safety.

Risk level:
- Low.

### 11. Boost Now Button

Safe baseline:
- Trigger only the currently enabled safe actions.
- Show a dry-run summary before first use.

Recommendation:
- Good feature, as long as it respects profile safety levels.

Risk level:
- Low.

### 12. Safe Restore System

This must be a foundational subsystem, not just a feature bullet.

Requirements:
- Transaction-style action log
- restore stack with reverse-order rollback
- crash watchdog process
- startup recovery check if the last session ended unexpectedly
- per-action success and failure telemetry stored locally

Recommendation:
- Build this before any invasive optimizer logic.

Risk level:
- Essential.

## Bonus Feature Review

### Performance HUD

Safe baseline:
- Use lightweight polling and supported telemetry APIs.
- Keep it optional and low refresh by default.

Do not:
- Inject into the game render path for v1.

Risk level:
- Low.

### Auto-Update Game Drivers

Recommendation:
- Change this to "Driver Update Checker."
- Notify users about available GPU updates and link to official vendor tools.

Do not:
- Silent-install GPU drivers automatically.

Reason:
- Driver installs are one of the highest-risk actions in the whole product and can absolutely cause crashes or unstable systems.

Risk level:
- High if automatic, low if advisory-only.

### Benchmark Mode

Safe baseline:
- Use existing game or synthetic benchmark launches with user consent.
- Focus on before/after comparison for FrameBoost actions.

Do not:
- Push unsafe overclock-style tuning during benchmark mode.

Risk level:
- Low.

## Safe Default Tiers

FrameBoost should ship with three internal safety tiers.

### Tier 1: Safe Default

Enabled by default:
- smart game detection
- power plan switching between known plans
- profile system
- safe restore system
- optional overlay reminders
- lightweight performance HUD
- manual shader cache cleanup
- background app de-prioritization for user-selected apps only

### Tier 2: Advanced Opt-In

Available only after warnings and compatibility checks:
- selected service stop/suspend actions from a vetted allowlist
- game-specific priority tuning
- CPU set hints
- advisory network tuning

### Tier 3: Experimental Labs

Hidden behind explicit warnings:
- standby memory purges
- deep cleanup routines
- aggressive affinity pinning
- vendor-specific GPU tuning

Tier 3 should not be part of the main marketing promise until it proves stable.

## Features to Remove or Reword

These original items should be changed before we build or market the app:

- "Background Service Killer"
  - Replace with "Background Activity Manager"
- "Clears cached GPU memory before launch"
  - Remove or reword to supported cleanup only
- "Frees standby RAM pages to reduce stutter"
  - Do not make this a default feature claim
- "Adjusts adapter settings for gaming"
  - Reword as detection plus optional recommendations first
- "Auto-Update Game Drivers" with silent install
  - Replace with update detection and official download links
- "Temporarily throttles background tasks instead of the game"
  - Restrict this to user processes and FrameBoost-owned helpers

## Recommended V1 Scope

The safest strong first release is:

- Smart game detection
- Profile system
- Safe restore engine
- Power plan switching
- Overlay management through supported toggles
- Background app manager for user-mode apps
- Conservative process priority tuning
- Manual shader cache maintenance
- Lightweight telemetry and HUD

## V1 Non-Goals

Do not include these in the first release:

- broad Windows service disabling
- automatic NIC advanced property rewriting
- silent driver installs
- undocumented VRAM cleanup
- aggressive RAM purge logic
- hard affinity pinning by default
- any kernel, driver, or firmware tuning

## Suggested Build Order

1. Detection engine
2. Restore and rollback engine
3. Profile model
4. Power switching
5. Background app manager
6. Priority and EcoQoS tuning
7. Overlay controls
8. Shader cache tools
9. Monitoring and HUD
10. Advanced features behind opt-in gates

## References

- Microsoft Learn: `powercfg` command-line options
  - https://learn.microsoft.com/en-us/windows-hardware/design/device-experiences/powercfg-command-line-options
- Microsoft Learn: `SetProcessAffinityMask`
  - https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-setprocessaffinitymask
- Microsoft Learn: `SetPriorityClass`
  - https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-setpriorityclass
- Microsoft Learn: `SetProcessInformation`
  - https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-setprocessinformation
- Microsoft Learn: Quality of Service
  - https://learn.microsoft.com/en-us/windows/win32/procthread/quality-of-service
- Microsoft Learn: Network Adapter Performance Tuning in Windows Server
  - https://learn.microsoft.com/en-us/windows-server/networking/technologies/network-subsystem/net-sub-performance-tuning-nics
- Microsoft Learn: PsService and service dependency inspection
  - https://learn.microsoft.com/en-us/sysinternals/downloads/psservice
