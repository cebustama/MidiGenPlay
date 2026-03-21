# MIDI Jamplay --- Runtime MIDI Input & Composition Pipeline Design

## Objective

Enable real-time MIDI input from USB controllers inside Unity, routed
through MIDI Player Toolkit Pro (MPTK), processed by MIDI Jamplay, and
exported using DryWetMIDI.

Pipeline: MIDI Controller → MPTK MidiInReader → Jamplay Router → Preview
Synth → Recorder → MIDI Asset

## Design Principles

-   Vendor decoupling (MPTK isolated via adapters)
-   SOLID-friendly architecture
-   Runtime + Editor compatibility
-   Extensible for future tooling and AI composition
-   Deterministic recording and reproducibility

## Layered Architecture

### 1. Input Layer

Responsible for capturing MIDI input.

Interface:

    IMidiInputSource
    - event OnMidiEvent(MidiEventData)

Adapter:

    MptkMidiInputAdapter : MonoBehaviour, IMidiInputSource

### 2. Core Routing Layer

Central orchestration of events.

    JamplayMidiRouter
    - Filters
    - Channel routing
    - Quantization
    - Fan-out to Preview, Recorder, Analysis

### 3. Preview Layer

Uses MPTK MidiStreamPlayer to synthesize audio using SF2 soundfonts.

### 4. Recording Layer

Buffers MidiEventData and exports to SMF via DryWetMIDI.

### 5. Analysis / Theory Layer

Optional hooks for harmonic analysis, scale detection, AI suggestions.

## Data Model

    enum MidiEventType
    struct MidiEventData
    {
        MidiEventType Type;
        int Channel;
        int Note;
        int Velocity;
        int Control;
        int Value;
        double Time;
    }

## Example Flow

1.  Controller sends NoteOn.
2.  MPTK receives event.
3.  Adapter converts to MidiEventData.
4.  Router applies filters.
5.  Event forwarded to:
    -   Preview Synth
    -   Recorder
    -   Theory Analyzer

## MVP Implementation Plan

Phase 1: Scene setup with MidiInReader prefab. Phase 2: Adapter
implementation. Phase 3: Router + Preview wiring. Phase 4: Recorder +
MIDI export.

Estimated effort: 1--2 afternoons.

## Future Extensions

-   Piano roll editor
-   Quantization grid UI
-   Clip looping
-   Generative harmony assistants
-   In-game composition tools
-   Multiplayer jam sessions

## Licensing & Open Source

Recommended: MIT or Apache 2.0 for Jamplay Core, vendor adapters
isolated.

------------------------------------------------------------------------

Author: Claudio Bustamante
