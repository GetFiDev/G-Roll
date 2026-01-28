# Renovation Log - January 28, 2026

**Starting Point:** Build v1.1.0
**Status:** Migration Complete

---

## Executive Summary

Complete architectural overhaul from legacy singleton-based code to modern Clean Architecture with VContainer dependency injection and MessageBus event system. This renovation transformed a tightly-coupled, untestable codebase into a modular, maintainable architecture.

---

## Major Changes

### 1. Architecture Migration

**Before:**
- 15+ Singleton managers (Instance pattern)
- God classes (1000+ lines each)
- Direct cross-module dependencies
- Mixed event systems (ScriptableObject events, C# events, direct calls)

**After:**
- VContainer dependency injection
- Interface-based dependencies
- Single responsibility classes
- Unified MessageBus for all events

---

### 2. New Folder Structure

```
Created:
├── Assets/_Core/              # Foundation layer
│   ├── Bootstrap/
│   ├── Common/
│   ├── DI/
│   ├── Events/
│   ├── Interfaces/
│   ├── Optimistic/
│   └── SceneManagement/
│
├── Assets/Domain/             # Business logic
│   ├── Application/
│   ├── DI/
│   ├── Economy/
│   ├── Gameplay/
│   ├── Progression/
│   ├── Shop/
│   └── Social/
│
├── Assets/Infrastructure/     # External systems
│   ├── Caching/
│   ├── Firebase/
│   ├── Logging/
│   ├── Persistence/
│   └── Services/
│
├── Assets/Presentation/       # UI layer
│   ├── Auth/
│   ├── Core/
│   ├── Meta/
│   ├── Navigation/
│   ├── Components/
│   └── Services/
│
└── Assets/Gameplay/           # Game mechanics
    ├── DI/
    ├── Player/
    ├── Session/
    ├── Scoring/
    ├── Spawning/
    └── Services/
```

---

### 3. VContainer Integration

**New LifetimeScopes:**

| Scope | Location | Lifetime |
|-------|----------|----------|
| RootLifetimeScope | Boot Scene | App lifetime (DontDestroyOnLoad) |
| GameSceneLifetimeScope | Game Scene | Scene lifetime |
| AuthSceneLifetimeScope | Auth Scene | Scene lifetime |
| MetaSceneLifetimeScope | Meta Scene | Scene lifetime |
| GameplayLifetimeScope | Gameplay Scene | Scene lifetime |

**Lazy Injection Pattern:**
Introduced `Lazy<T>` injection to resolve circular dependencies between services.

---

### 4. MessageBus System

**New Message Types:**

| Category | Messages |
|----------|----------|
| App Flow | `AppPausedMessage`, `AppResumedMessage`, `SceneTransitionMessage` |
| Auth | `AuthStateChangedMessage`, `ProfileUpdatedMessage` |
| Camera | `CameraSpeedChangedMessage` |
| Currency | `CurrencyChangedMessage`, `CurrencyCollectedMessage` |
| Gameplay | `SessionStartedMessage`, `SessionEndedMessage`, `SpeedChangedMessage`, `BoosterActivatedMessage` |
| Leaderboard | `LeaderboardUpdatedMessage`, `NewHighScoreMessage` |
| Player | `PlayerDiedMessage`, `PlayerStateChangedMessage` |
| Stats | `PlayerStatsUpdatedMessage` |
| Referral | `ReferralCodeAppliedMessage`, `ReferralEarningsClaimedMessage` |
| Profile | `UsernameChangedMessage`, `AvatarChangedMessage` |

---

### 5. Scene System Overhaul

**Before:**
```
Boot Scene → Game Scene (everything mixed)
```

**After:**
```
Boot Scene → Auth Scene → Meta Scene ←→ Gameplay Scene
                              ↓
                       Loading Scene (additive)
```

**New Scene Components:**
- `SceneFlowManager` - Central scene orchestration
- `SceneRegistry` - Scene name/index mapping
- `LoadingSceneController` - Transition UI
- `BootSceneController` - Startup flow
- `AuthSceneController` - Login flow
- `MetaSceneController` - Main menu
- `GameplaySceneController` - Game session
- `EditorSceneBootstrapper` - Always start from Boot in editor

---

### 6. Service Layer

**New Domain Services:**

| Service | Replaces |
|---------|----------|
| SessionService | GameplayManager (session parts) |
| AchievementService | Old AchievementService (Networks) |
| LeaderboardService | LeaderboardManager |
| GameStateService | GameManager, AppFlowManager |
| ReferralService | ReferralManager |

**New Infrastructure Services:**

| Service | Purpose |
|---------|---------|
| FirebaseGateway | Central Firebase access |
| SessionRemoteService | Session API |
| AchievementRemoteService | Achievement sync |
| LeaderboardRemoteService | Leaderboard API |
| PlayerStatsRemoteService | Stats sync |
| ReferralRemoteService | Referral API |
| StreakRemoteService | Daily streak |
| IAPRemoteService | Purchase verification |
| ElitePassRemoteService | Premium subscription |
| AutopilotRemoteService | Idle earnings |

---

### 7. UI Navigation System

**New Base Classes:**
- `UIScreenBase` - Full screen navigation
- `UIPopupBase` - Modal overlays with result return

**NavigationService:**
- Stack-based screen management
- Popup overlay system
- Back button handling
- Transition animations

**New Components:**
- `BottomNavigation`
- `CurrencyDisplay`
- `EnergyDisplay`
- `TaskCard`
- `AchievementCard`
- `ShopItemCard`
- `LeaderboardEntry`
- `BoosterFillBar`
- `SpeedIndicator`

---

### 8. Interface Definitions

**Core Interfaces Created:**

| Interface | Location |
|-----------|----------|
| `ISessionService` | Core/Interfaces/Services |
| `IAchievementService` | Core/Interfaces/Services |
| `ILeaderboardService` | Core/Interfaces/Services |
| `IAuthService` | Core/Interfaces/Services |
| `IAdService` | Core/Interfaces/Services |
| `IAppFlowService` | Core/Interfaces/Services |
| `IAudioService` | Core/Interfaces/Services |
| `ICameraService` | Core/Interfaces/Services |
| `IFeedbackService` | Core/Interfaces/UI |
| `IGameStateService` | Core/Interfaces/Services |
| `IHapticService` | Core/Interfaces/Services |
| `IIAPService` | Core/Interfaces/Services |
| `IItemService` | Core/Interfaces/Services |
| `IPlayerStatsService` | Core/Interfaces/Services |
| `IReferralService` | Core/Interfaces/Services |
| `IUserProfileService` | Core/Interfaces/Services |
| `INavigationService` | Core/Interfaces/UI |
| `IDialogService` | Core/Interfaces/UI |
| `IGRollLogger` | Core/Interfaces/Infrastructure |
| `IGameplaySpeedService` | Core/Interfaces/Services |

---

### 9. Optimistic Pattern Implementation

**New Infrastructure:**
- `ISnapshotable<T>` interface
- `OperationResult` wrapper
- Rollback support in all domain services
- `OperationRolledBackMessage` for UI notification

---

### 10. Files Deleted

**Managers (Replaced by Services):**
- AdManager.cs
- AppFlowManager.cs
- AppManager.cs
- AudioManager.cs
- BackgroundMusicManager.cs
- BootManager.cs
- DataManager.cs
- GameManager.cs
- GameplayLogicApplier.cs
- GameplayManager.cs (old)
- GameplayVisualApplier.cs
- GameplayCameraManager.cs
- HapticManager.cs
- IAPManager.cs
- NetworkConnectionMonitor.cs
- NotificationBadgeManager.cs
- NotificationManager.cs
- ReviewManager.cs
- ShopItemManager.cs
- TouchManager.cs
- UIManager.cs
- UserStatManager.cs

**Network Services (Moved to Infrastructure):**
- AchievementService.cs (old)
- AutopilotService.cs
- ElitePassService.cs
- ElitePassValidator.cs
- FirebaseLoginHandler.cs
- FirestoreRemoteFetcher.cs
- IAPRemoteService.cs (old)
- InventoryDebugPanel.cs
- InventoryRemoteService.cs (old)
- ItemLocalDatabase.cs
- LeaderboardManager.cs
- LeaderboardService.cs (old)
- MapManager.cs
- PlayerStatsRemoteService.cs (old)
- ReferralManager.cs
- ReferralRemoteService.cs (old)
- RemoteAppDataService.cs
- RemoteItemService.cs (old)
- SessionRemoteService.cs (old)
- SessionResultRemoteService.cs
- StreakService.cs (old)
- TaskService.cs (old)
- UserDataEditHandler.cs
- UserDatabaseManager.cs
- UserEnergyService.cs
- UserInventoryManager.cs

**UI (Replaced or Refactored):**
- Many legacy UI panels marked for removal (see UI Migration Plan)
- CollectibleNotifier.cs (MessageBus replaces)
- Event channels (VoidEventChannelSO, PhaseEventChannelSO)

**Player (Consolidated):**
- PlayerController.cs (old - moved to new location)
- PlayerAnimator.cs
- PlayerCollision.cs

**Utilities:**
- MonoSingleton.cs (VContainer replaces)
- Various event channels

---

### 11. Assembly Definitions

**Created:**
- GRoll.Core.asmdef
- GRoll.Domain.asmdef (via folder)
- GRoll.Infrastructure.asmdef
- GRoll.Presentation.asmdef (via folder)
- GRoll.Gameplay.asmdef (via folder)
- GRoll.Entities.asmdef
- GRoll.MapDesignerTools.asmdef

---

### 12. Namespace Reorganization

**Old:**
```
(root namespace, scattered)
```

**New:**
```
GRoll.Core
GRoll.Core.Events
GRoll.Core.Interfaces.Services
GRoll.Core.Interfaces.UI
GRoll.Core.Interfaces.Infrastructure
GRoll.Core.Optimistic

GRoll.Domain.Application
GRoll.Domain.Economy
GRoll.Domain.Gameplay
GRoll.Domain.Progression
GRoll.Domain.Shop
GRoll.Domain.Social

GRoll.Infrastructure.Firebase
GRoll.Infrastructure.Logging
GRoll.Infrastructure.Services

GRoll.Presentation.Core
GRoll.Presentation.Navigation
GRoll.Presentation.Services
GRoll.Presentation.Components

GRoll.Gameplay.Player
GRoll.Gameplay.Services
```

---

### 13. Build Fixes Applied

| Issue | Fix |
|-------|-----|
| Missing GameplayLogicApplier | Created IGameplaySpeedService |
| Missing TouchManager | Input handled by PlayerInputHandler |
| Missing SwipeDirection | Added to Core/Common/Enums.cs |
| Missing CollectibleNotifier | Replaced with MessageBus |
| Wrong PlayerController namespace | Updated to GRoll.Gameplay.Player.Core |
| IFeedbackService wrong namespace | Moved to Core/Interfaces/UI |
| Circular dependencies | Resolved with Lazy<T> injection |

---

## Migration Statistics

| Metric | Value |
|--------|-------|
| Files Created | ~80 |
| Files Deleted | ~100 |
| Files Modified | ~60 |
| New Interfaces | 20+ |
| New Services | 25+ |
| New Messages | 25+ |
| New Scenes | 4 (Auth, Meta, Gameplay, Loading) |

---

## What's Left (Future Work)

1. **Legacy UI Migration** - Replace remaining UI panels with new Screen/Popup system
2. **Player State Machine** - Complete state implementations
3. **Scene Prefab Setup** - Configure Unity scene hierarchies
4. **Integration Testing** - Full flow tests

---

## Commit Reference

**Starting Commit:** `9b4ce74` (build v1.1.0)
**Current State:** Domain and Infrastructure service layer complete

---

*This renovation establishes a solid foundation for scalable mobile game development.*
