# Modular Device Architecture

> **Status**: Design Phase
> **Created**: 2026-01-13
> **Goal**: Enable purchasable, wirable arcade machines with physical cable connections

## Overview

The current system uses singleton patterns for game sessions, making it impossible to run multiple independent sessions. The modular device architecture replaces this with purchasable in-game devices that players can wire together using physical cables.

**Key Vision**:
- Card slots, buttons, and screens are modular devices
- Devices have "jacks" (typed connection points)
- Players run physical 3D cables between jacks
- Cables represent Godot signal connections visually
- Multiple arcade machines = multiple parallel sessions

## Core Abstractions

### IDevice

Base interface for all pluggable hardware. A device is a physical object in the game world that can be purchased, placed, and wired.

```
IDevice
├── UniqueId: string
├── WorldNode: Node3D
├── Jacks: IReadOnlyList<IJack>
├── GetJack(name): IJack?
└── OnCableConnected/Disconnected events
```

### IJack<T>

A typed connection point on a device. Jacks have a direction (input/output) and carry specific data types.

```
IJack<T>
├── Name: string (e.g., "CardInput", "TriggerOut")
├── Direction: JackDirection (Input | Output)
├── ParentDevice: IDevice
├── ConnectedCable: Cable?
├── DataEmitted: event Action<T>    // For outputs
├── Receive(T data): void           // For inputs
└── IsCompatibleWith(IJack other): bool
```

### Cable

Physical + logical connection between two jacks. Handles event forwarding and visual representation.

```
Cable : Node3D
├── SourceJack: IJack (must be Output)
├── DestinationJack: IJack (must be Input)
├── VisualMesh: MeshInstance3D (bezier curve)
├── Connect(): void    // Subscribe to source events
├── Disconnect(): void // Unsubscribe
└── UpdateVisual(): void // Update mesh on jack movement
```

## Data Types

| Type | Description | Flow Direction |
|------|-------------|----------------|
| `CardSignature` | Single 8D signature vector | Slot → Machine |
| `CardSignatureList` | Multiple signatures (daisy-chain) | Slot → Slot → Machine |
| `Trigger` | Fire-and-forget event | Button → Machine |
| `MapState` | Current map snapshot | Machine → Screen |
| `CombatState` | Current combat state | Machine → Screen |
| `LootDrop` | Generated loot cards | Machine → Dispenser |

## Device Definitions

### CardSlot

Holds a single card and outputs its signature.

| Jack | Direction | Type | Purpose |
|------|-----------|------|---------|
| `CardOutput` | Out | `CardSignatureList` | Emits when card inserted |
| `ChainInput` | In | `CardSignatureList` | Chains from another slot |

**Behavior**: On card insert, combines with ChainInput (if connected) and emits full list.

### Button

Simple trigger emitter.

| Jack | Direction | Type | Purpose |
|------|-----------|------|---------|
| `TriggerOutput` | Out | `Trigger` | Emits on press |

### ArcadeMachine

The core session host. Owns map generator, exploration AI, and combat system as instances (not singletons).

| Jack | Direction | Type | Purpose |
|------|-----------|------|---------|
| `MapSeedInput` | In | `CardSignatureList` | Cards for map generation |
| `AbilityInput` | In | `CardSignatureList` | Cards for combat abilities |
| `TriggerInput` | In | `Trigger` | Starts session |
| `MapOutput` | Out | `MapState` | For connected screens |
| `CombatOutput` | Out | `CombatState` | For combat screens |
| `LootOutput` | Out | `CardSignatureList` | Emits on session end |

**Key Change**: Current `GameSessionService` logic moves here. Multiple machines = multiple sessions.

### Screen

Renders connected state data.

| Jack | Direction | Type | Purpose |
|------|-----------|------|---------|
| `DisplayInput` | In | `MapState` or `CombatState` | What to render |

**Variants**:
- Hexagonal (continent/city scale)
- Rectangular (building/room scale)
- Combat-focused

### CardDispenser

Receives cards and drops them into the world.

| Jack | Direction | Type | Purpose |
|------|-----------|------|---------|
| `CardInput` | In | `CardSignatureList` | Cards to dispense |

## Connection Examples

### Basic Setup
```
[CardSlot] ─CardOutput→ ─MapSeedInput─ [ArcadeMachine] ─MapOutput→ ─DisplayInput─ [Screen]
[Button] ───TriggerOutput→ ─TriggerInput─┘
```

### Multi-Card Daisy Chain
```
[CardSlot A] ─CardOutput→ ─ChainInput─ [CardSlot B] ─CardOutput→ ─MapSeedInput─ [Machine]
```

### Hierarchical Screens
```
[Machine Continent] ─MapOutput→ [Hex Screen]
                    └─ZoomOutput→ ─TriggerInput─ [Machine City] ─MapOutput→ [Hex Screen 2]
                                                               └─ZoomOutput→ [Machine Room]...
```

### Loot to Player
```
[ArcadeMachine] ─LootOutput→ ─CardInput─ [CardDispenser] → Physical cards drop
```

## Player Interaction

### Connecting Cables

1. Player picks up a cable (E key, like cards)
2. Player aims at a jack (raycast detection)
3. Compatible jacks highlight
4. Player clicks to attach one end
5. Player moves to destination jack
6. Player clicks to complete connection
7. Cable visual renders between jacks

### Validation Rules

- Output jacks connect to Input jacks only
- Types must be compatible (exact match or covariant)
- One cable per output (fan-out requires splitter device)
- Multiple cables can connect to same input (fan-in)

## Migration Path

### Phase 1: Create Interfaces
- Define `IDevice`, `IJack<T>`, `JackDirection`
- Define data types as classes/records
- No implementation yet

### Phase 2: Implement Cable
- Create `Cable` class with event forwarding
- Basic line mesh for visual (bezier later)
- Test with mock devices

### Phase 3: Refactor ArcadeMachine
- Extract session logic from `GameSessionService`
- Create `ArcadeMachineDevice` implementing `IDevice`
- Wire up jacks for inputs/outputs
- GameSessionService becomes thin orchestrator or deprecated

### Phase 4: Simple Devices
- Implement `CardSlotDevice` (refactor existing CardHolder)
- Implement `ButtonDevice` (refactor InteractableButton)
- Test end-to-end connection

### Phase 5: Screen Device
- Implement `ScreenDevice` with MapState/CombatState rendering
- Support viewport-based rendering

### Phase 6: Visual Polish
- Bezier curve cables
- Jack highlight effects
- Connection/disconnection animations

### Phase 7: Player Interaction
- Cable pickup/place mechanics
- Inventory system for devices
- In-game shop for purchasing devices

## Architecture Benefits

1. **No Singletons**: Each ArcadeMachine owns its session state
2. **Parallel Sessions**: Buy multiple machines, run multiple games
3. **Visual Programming**: Players design their own setups
4. **Extensibility**: New device types just implement IDevice
5. **Hierarchical Play**: Chain screens for zoom-in gameplay
6. **Modular Testing**: Devices testable in isolation

## Open Questions

- How to handle device persistence (save/load wiring)?
- Should cables have limited length or routing constraints?
- Power system? (Some devices might need power cables)
- Wireless connections for convenience?
- Multiplayer: Can players share devices or are they per-player?
