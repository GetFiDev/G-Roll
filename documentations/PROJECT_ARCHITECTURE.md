# G-Roll Project Architecture

**Version:** 2.0
**Date:** January 2026

---

## Overview

G-Roll follows a **Clean Architecture** pattern with **VContainer** for dependency injection and **MessageBus** for decoupled event communication. The architecture is designed for testability, maintainability, and optimistic user experience.

---

## Layer Structure

```
Assets/
├── _Core/                  # Shared foundation
├── Domain/                 # Business logic services
├── Infrastructure/         # External systems (Firebase, platform APIs)
├── Presentation/           # UI layer
├── Gameplay/               # Game mechanics
└── _Game Assets/           # Unity assets, scenes, legacy code (deprecating)
```

---

## 1. Core Layer (`Assets/_Core/`)

The foundation layer containing shared types, interfaces, and utilities.

### Structure
```
_Core/
├── Bootstrap/              # Application startup
├── Common/                 # Enums, constants, helpers
├── DI/                     # LifetimeScope configurations
├── Events/                 # MessageBus and message types
├── Interfaces/             # All service interfaces
├── Optimistic/             # Optimistic operation patterns
└── SceneManagement/        # Scene flow system
```

### Key Components

**MessageBus** (`Events/MessageBus.cs`)
Central publish-subscribe system for all cross-module communication.

```
Publisher → MessageBus → Subscriber(s)
```

**OperationResult** (`Optimistic/OperationResult.cs`)
Standard result wrapper for async operations with success/failure states.

**ISnapshotable** (`Optimistic/ISnapshotable.cs`)
Interface for services that support state rollback.

---

## 2. Domain Layer (`Assets/Domain/`)

Business logic services that orchestrate operations.

### Structure
```
Domain/
├── Application/            # App-level services (flow, state)
├── DI/Installers/          # VContainer registrations
├── Economy/                # Currency, inventory
├── Gameplay/               # Energy, session, speed
├── Progression/            # Tasks, achievements
├── Shop/                   # Shop operations
└── Social/                 # Leaderboard, referrals
```

### Key Services

| Service | Responsibility |
|---------|----------------|
| `SessionService` | Game session lifecycle |
| `AchievementService` | Achievement tracking and claiming |
| `LeaderboardService` | Score submission and ranking |
| `GameStateService` | Global game state management |
| `ReferralService` | Referral code system |

### Service Pattern

Every domain service follows this pattern:

1. **Local State** - In-memory cache for immediate reads
2. **Optimistic Updates** - UI changes before server confirmation
3. **Remote Sync** - Background server communication
4. **Rollback Support** - Revert on failure

---

## 3. Infrastructure Layer (`Assets/Infrastructure/`)

External system integrations.

### Structure
```
Infrastructure/
├── Caching/                # Local cache utilities
├── Firebase/               # Firebase integration
│   ├── Interfaces/         # Remote service contracts
│   ├── Services/           # Remote service implementations
│   └── Utilities/          # Firebase helpers
├── Logging/                # Logger implementation
├── Persistence/            # Local storage
└── Services/               # Platform services (ads, haptics)
```

### Firebase Gateway

Central entry point for all Firebase operations:

```
Domain Service → IRemoteService → FirebaseGateway → Firebase
```

### Remote Services

Each domain concept has a corresponding remote service:

| Remote Service | Purpose |
|----------------|---------|
| `SessionRemoteService` | Session API calls |
| `AchievementRemoteService` | Achievement sync |
| `LeaderboardRemoteService` | Leaderboard API |
| `ReferralRemoteService` | Referral system |
| `PlayerStatsRemoteService` | Player statistics |
| `StreakRemoteService` | Daily streak tracking |
| `IAPRemoteService` | In-app purchase verification |
| `ElitePassRemoteService` | Premium subscription |
| `AutopilotRemoteService` | Idle earnings system |

---

## 4. Presentation Layer (`Assets/Presentation/`)

UI components and navigation.

### Structure
```
Presentation/
├── Auth/                   # Auth scene UI
├── Core/                   # UIScreenBase, UIPopupBase
├── Meta/                   # Meta scene UI
├── Navigation/             # NavigationService, BackButtonHandler
├── Components/             # Reusable UI components
├── Services/               # DialogService, FeedbackService
└── DI/                     # UI scope registrations
```

### Screen vs Popup

| Type | Use Case | Behavior |
|------|----------|----------|
| **Screen** | Full-page navigation | Stacked, back navigates |
| **Popup** | Modal overlays | Stackable, returns result |

### Base Classes

**UIScreenBase**
- Lifecycle: `OnScreenEnterAsync` → User interaction → `OnScreenExitAsync`
- Auto-subscribes to MessageBus
- Handles animation and transitions

**UIPopupBase**
- Lifecycle: `OnPopupShowAsync` → User action → `CloseWithResult<T>`
- Modal with backdrop
- Generic result return

### NavigationService

Manages screen stack and popup overlays:

```
PushScreenAsync<T>()     # Add screen to stack
PopScreenAsync()         # Remove top screen
ReplaceScreenAsync<T>()  # Replace current screen
ShowPopupAsync<T>()      # Show modal popup
```

---

## 5. Gameplay Layer (`Assets/Gameplay/`)

Game mechanics and runtime systems.

### Structure
```
Gameplay/
├── DI/                     # GameplayLifetimeScope
├── Player/                 # Player systems
│   ├── Core/               # PlayerContext, PlayerController
│   ├── Input/              # PlayerInputHandler
│   └── StateMachine/       # State machine and states
├── Session/                # Session controller
├── Scoring/                # Score manager
├── Spawning/               # Player spawn controller
└── Services/               # GameplaySpeedService
```

### Player State Machine

```
┌────────┐     ┌─────────┐     ┌──────────┐
│  IDLE  │ ──► │ RUNNING │ ◄──►│ JUMPING  │
└────────┘     └────┬────┘     └──────────┘
                    │
           ┌────────┼────────┐
           ▼        ▼        ▼
      ┌─────────┐       ┌──────────┐
      │ FROZEN  │       │ COASTING │
      └─────────┘       └──────────┘
```

States:
- **Idle**: Pre-game, waiting for intro
- **Running**: Normal movement
- **Jumping**: Airborne state
- **Frozen**: Hit obstacle, temporary stun
- **Coasting**: Slowing down
- **Teleporting**: Portal transition

---

## 6. Scene System

### Scene Flow

```
Boot Scene → Auth Scene → Meta Scene ←→ Gameplay Scene
                             ↓
                      Loading Scene (additive)
```

### Scene Responsibilities

| Scene | Purpose |
|-------|---------|
| **Boot** | Firebase init, core services, auth check |
| **Auth** | Login, profile setup |
| **Meta** | Main menu, shop, leaderboard |
| **Gameplay** | Game session, player, obstacles |
| **Loading** | Transition overlay |

### LifetimeScope Hierarchy

```
RootLifetimeScope (Boot - DontDestroyOnLoad)
    ├── MessageBus, AuthService, FirebaseGateway
    │
    ├── AuthSceneLifetimeScope (Auth)
    │       └── Login controllers
    │
    ├── MetaSceneLifetimeScope (Meta)
    │       └── NavigationService, Domain services
    │
    └── GameplayLifetimeScope (Gameplay)
            └── Session, Score, Player systems
```

---

## 7. Dependency Injection (VContainer)

### Injection Pattern

```csharp
public class MyService
{
    [Inject] private IMessageBus _messageBus;
    [Inject] private IRemoteService _remoteService;
}
```

### Registration Types

| Type | Lifetime | Use Case |
|------|----------|----------|
| `Singleton` | App lifetime | MessageBus, Auth, Firebase |
| `Scoped` | Scene lifetime | Domain services, Navigation |
| `Transient` | Per-request | Factories, short-lived objects |

### Lazy Injection for Circular Dependencies

```csharp
[Inject] private Lazy<IRelatedService> _relatedService;
```

---

## 8. Event System (MessageBus)

### Message Categories

**Game Flow**
- `GamePhaseChangedMessage`
- `AppPausedMessage`
- `AppResumedMessage`

**Gameplay**
- `PlayerDiedMessage`
- `SessionStartedMessage`
- `SessionEndedMessage`
- `SpeedChangedMessage`
- `BoosterActivatedMessage`

**Economy**
- `CurrencyChangedMessage`
- `InventoryChangedMessage`
- `CurrencyCollectedMessage`

**Progression**
- `TaskProgressMessage`
- `AchievementChangedMessage`

**Operations**
- `OperationRolledBackMessage`

### Usage Pattern

```csharp
// Subscribe
_subscriptions.Add(_messageBus.Subscribe<CurrencyChangedMessage>(OnCurrencyChanged));

// Publish
_messageBus.Publish(new CurrencyChangedMessage(type, oldValue, newValue, isOptimistic));

// Cleanup (in OnDestroy)
_subscriptions.Dispose();
```

---

## 9. Assembly Definitions

```
GRoll.Core           # _Core/ - no dependencies
GRoll.Domain         # Domain/ - depends on Core
GRoll.Infrastructure # Infrastructure/ - depends on Core, Domain
GRoll.Presentation   # Presentation/ - depends on Core, Domain
GRoll.Gameplay       # Gameplay/ - depends on Core, Domain
```

---

## 10. Key Patterns

### 1. Interface Segregation
All external dependencies are abstracted behind interfaces in `_Core/Interfaces/`.

### 2. Single Responsibility
Each class has one reason to change. Large classes are split into focused units.

### 3. Dependency Inversion
High-level modules depend on abstractions, not concrete implementations.

### 4. Optimistic UX
User sees immediate feedback; errors are gracefully handled with rollback.

### 5. Message-Driven
Cross-module communication happens through MessageBus, not direct calls.

---

## Quick Reference

### Adding a New Feature

1. Define interface in `_Core/Interfaces/`
2. Implement service in `Domain/` or `Infrastructure/`
3. Register in appropriate LifetimeScope
4. Create messages if cross-module communication needed
5. Build UI in `Presentation/`

### Adding a New Screen

1. Create class extending `UIScreenBase`
2. Implement lifecycle methods
3. Register in NavigationService
4. Call `PushScreenAsync<T>()` to show

### Adding a New Popup

1. Create class extending `UIPopupBase`
2. Define result type if needed
3. Register in NavigationService
4. Call `ShowPopupAsync<T>()` to display

---

*This architecture enables testable, maintainable, and scalable mobile game development.*
