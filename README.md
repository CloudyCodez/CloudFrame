# CloudFrame

CloudFrame is a Windows desktop booster focused on safe, reversible game-session optimizations.

## Included In This Build

- profile-based game detection
- automatic or manual boost activation
- temporary power-plan switching
- game process priority boost
- selected background app priority reduction
- automatic restore when the game exits
- startup recovery if the last session ended unexpectedly
- manual DirectX shader cache cleanup
- anti-cheat compatibility mode for known vendors
- modernized dashboard and profile workflow
- toggleable top-left live overlay for FPS, CPU, and GPU telemetry

## Safety Rules In This Build

- no Windows service killing
- no RAM purge tools
- no undocumented VRAM cleanup
- no network driver rewrites
- no driver auto-install logic
- no hard CPU affinity pinning
- known anti-cheat environments automatically fall back to power-plan-only compatibility mode
- in anti-cheat compatibility mode, CloudFrame disables FPS capture and keeps the overlay telemetry-only for safety
- no touching virtualization drivers, services, or common Hyper-V/VMware/VirtualBox/WSL host processes

## Test Build

Published executable:

- `FrameBoost\bin\Release\net10.0-windows\win-x64\publish\CloudFrame.exe`

## Quick Start

1. Launch `CloudFrame.exe`
2. Open the `Profiles` tab
3. Click `Add From EXE`
4. Pick a game executable
5. Choose a power plan and priority level
6. Save the profile
7. Go back to `Dashboard`
8. Start monitoring
9. Launch the game, or use `Boost Selected Game` if it is already running

## Notes

- Running FrameBoost as administrator may improve its ability to tune processes launched with elevated permissions.
- The app is designed around one active boosted game session at a time for safer restore behavior.
- Settings, recovery state, and logs are stored under `%LOCALAPPDATA%\CloudsFrameBoost`.
- CloudFrame can check `CloudyCodez/CloudFrame` releases on launch and prompt when a newer public release is available.
- The current public release line is `1.1.2`.
- Anti-cheat detection is heuristic, not exhaustive. When CloudFrame detects common markers for Easy Anti-Cheat, BattlEye, Riot Vanguard, FACEIT Anti-Cheat, or EA AntiCheat, it skips process-priority changes and keeps the session in compatibility mode.
- FPS capture uses PresentMon when it is available on the system. On this machine, NVIDIA FrameView's bundled `PresentMon_x64.exe` is available and can feed the overlay without injecting into the game.
- CloudFrame also treats virtualization infrastructure as protected. It does not target virtualization drivers, and it will avoid reprioritizing common Hyper-V, WSL, VMware, VirtualBox, Parallels, and QEMU host processes.
