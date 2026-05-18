# Glyphborn.Echo

> Proprietary Software — DoItBetter Studio

**Glyphborn.Echo** is the audio engine component of the Glyphborn ecosystem.

Development began in August 2025 as part of DoItBetter Studio’s long-term effort to build a modular, deterministic game engine and editor suite.

---

## Overview


Glyphborn.Echo is the dedicated audio engine and editor for the Glyphborn ecosystem, providing deterministic, modular, and extensible audio services for games and tools. It features:

- Windows Forms-based audio editor UI
- WAV file decoding (8/16-bit, mono/stereo, downmixed to signed 8-bit mono PCM)
- Audio playback using WinMM (WinMMAudioPlayer)
- Sound effect management with marker editing (trim, loop points)
- Music streaming and seamless looping
- Audio channel and bus control
- Volume routing and balancing (via PCM data)
- Deterministic runtime audio state (AudioDocument, AudioFlags)
- Project save/load (.gbaud editor files)
- Export to runtime .gbaud format
- Version checking and update mechanism

Echo is designed to operate independently of world structure, rendering, and gameplay logic, ensuring clean separation of concerns and testable audio logic.

---

## Architectural Role

The Glyphborn ecosystem is intentionally modular.

Glyphborn.Echo handles:

✔ Audio playback  
✔ Music and loop control  
✔ Channel management  
✔ Runtime audio coordination  

Glyphborn.Echo does **not** handle:

✖ Rendering  
✖ World or spatial data  
✖ Game rules or combat systems  
✖ Networking  
✖ UI  

Audio logic is isolated to ensure clarity, testability, and clean dependency boundaries.

Glyphborn.Echo integrates with other systems but does not depend on them for core functionality.

---

## Design Principles

Glyphborn.Echo follows the engineering principles established by DoItBetter Studio:

- **Deterministic Audio State** — Predictable behavior across runtime sessions  
- **Modular Architecture** — Independent repository and versioning  
- **Separation of Concerns** — No world or rendering coupling  
- **Extensibility** — Designed for future audio system expansion  
- **Testable Core Logic** — Clear abstraction around playback providers  

---

## Ecosystem Integration

Glyphborn.Echo integrates with:

- Atlas — World and spatial systems  
- Mapper — Tooling and editor workflows  
- Glyphborn — Core runtime and gameplay systems  

Each component is developed independently to allow controlled iteration and long-term scalability.

---

## Project Status


**Initial Release: v1.0.0**

Glyphborn.Echo has reached its first stable release, providing a robust foundation for audio playback, mixing, and runtime management. Further features and integrations are planned for future versions.

The broader Glyphborn engine will eventually be rebranded and released as:

**Damascus — The Steel Editor Suite**

This repository is publicly visible for transparency and portfolio purposes but remains proprietary and not open source.

---

## Release Notes

### v1.0.0 — Initial Release

**Release Date:** 2026-06-XX

**Features:**

- Windows Forms-based audio editor UI (MainForm, SoundListView)
- WAV file decoding (8/16-bit, mono/stereo, downmixed to signed 8-bit mono PCM)
- Audio playback using WinMM (WinMMAudioPlayer)
- Sound effect management with marker editing (trim, loop points)
- Music streaming and seamless looping support
- Audio channel and bus control
- Volume routing and balancing (via PCM data, not mixer UI)
- Deterministic runtime audio state (AudioDocument, AudioFlags)
- Project save/load (.gbaud editor files, Serializer)
- Export to runtime .gbaud format (GbaudExporter)
- Version checking and update mechanism (VersionChecker)
- Modular, testable architecture with clear separation of concerns

This release establishes a robust foundation for future extensibility and integration with other Glyphborn ecosystem components.

---

## Ownership & License

Copyright © 2025–2026 DoItBetter Studio

All rights reserved.

This software and associated documentation are proprietary intellectual property of DoItBetter Studio.

No license is granted to use, copy, modify, distribute, sublicense, reverse engineer, or create derivative works without prior written permission.

DoItBetter Studio reserves the right to relicense this software under an open-source license upon official release.
