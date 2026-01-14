# G-Roll Architecture: Component Map

> **Document Version**: 1.0
> **Last Updated**: 2025-01-12
> **Purpose**: Complete architectural reference for G-Roll mobile game
> **Audience**: Developers, AI assistants (Opus 4.5), Code reviewers

---

## Table of Contents

1. [Project Overview](#1-project-overview)
2. [Technology Stack](#2-technology-stack)
3. [Directory Structure](#3-directory-structure)
4. [Client Architecture (Unity)](#4-client-architecture-unity)
5. [Backend Architecture (Firebase)](#5-backend-architecture-firebase)
6. [Data Layer](#6-data-layer)
7. [Third-Party Integrations](#7-third-party-integrations)
8. [Build & Release Pipeline](#8-build--release-pipeline)
9. [Module Dependency Map](#9-module-dependency-map)
10. [Critical Integration Points](#10-critical-integration-points)

---

## 1. Project Overview

### 1.1 What is G-Roll?

G-Roll is a hyper-casual mobile game built with Unity, featuring:
- Endless runner / obstacle avoidance gameplay
- Server-authoritative economy (anti-cheat)
- Cross-platform support (iOS & Android)
- Monetization via IAP + Rewarded Ads
- Social features (Leaderboards, Referrals, Achievements)
- Elite Pass subscription system
- Energy/Stamina system
- Daily tasks and streaks

### 1.2 High-Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        CLIENT (Unity)                           │
├─────────────────────────────────────────────────────────────────┤
│  Boot Layer    │  Manager Layer  │  UI Layer  │  Gameplay Layer │
│  ───────────   │  ─────────────  │  ────────  │  ────────────── │
│  BootManager   │  GameManager    │  60+ UI    │  Player         │
│  AppFlowMgr    │  IAPManager     │  Scripts   │  Entities       │
│                │  AdManager      │            │  Controllers    │
│                │  18 Managers    │            │  Map System     │
├─────────────────────────────────────────────────────────────────┤
│                    NETWORK LAYER (27 Services)                  │
│  UserDatabaseManager │ IAPRemoteService │ SessionRemoteService  │
│  InventoryRemoteService │ EnergyService │ LeaderboardService    │
└───────────────────────────┬─────────────────────────────────────┘
                            │ HTTPS Callable
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│                   BACKEND (Firebase Functions)                  │
├─────────────────────────────────────────────────────────────────┤
│  user │ iap │ session │ shop │ energy │ achievements │ streak   │
│  leaderboard │ tasks │ ad │ autopilot │ map │ content │ scheduler│
└───────────────────────────┬─────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│                      DATA LAYER (Firestore)                     │
│  /users/{uid}  │  /appdata  │  /referralKeys  │  /leaderboards  │
└─────────────────────────────────────────────────────────────────┘
```

### 1.3 Design Principles

| Principle | Implementation |
|-----------|----------------|
| **Server Authority** | All economy operations verified server-side |
| **Event-Driven** | ScriptableObject EventChannels for decoupling |
| **Singleton Managers** | Global services via singleton pattern |
| **Async-First** | UniTask for all async operations |
| **Offline Tolerance** | Local cache with remote sync |

---

## 2. Technology Stack

### 2.1 Client

| Component | Technology | Version |
|-----------|------------|---------|
| Game Engine | Unity | 6000.0.64f1 |
| Render Pipeline | URP (Universal) | 17.0.4 |
| Async Library | UniTask | Latest (git) |
| IAP | Unity Purchasing | 5.1.2 |
| Notifications | Mobile Notifications | 2.4.2 |
| Input | Input System | 1.17.0 |
| Camera | Cinemachine | 3.1.4 |
| Performance | Burst Compiler | 1.8.27 |
| Ads | Appodeal | Latest |
| Analytics | Firebase Analytics | 13.x |
| Auth | Firebase Auth | 13.x |
| Database | Firebase Firestore | 13.x |

### 2.2 Backend

| Component | Technology | Version |
|-----------|------------|---------|
| Runtime | Node.js | 20.x |
| Framework | Firebase Functions | 7.0.2 |
| Database | Firestore | 7.11.4 |
| Admin SDK | firebase-admin | 13.6.0 |
| Google APIs | googleapis | 169.0.0 |
| Language | TypeScript | 5.x |
| Region | us-central1 | - |
| Memory | 512 MiB | Default |

### 2.3 Build & Deploy

| Component | Technology |
|-----------|------------|
| CI/CD | GitHub Actions |
| iOS Build | game-ci/unity-builder |
| iOS Deploy | Fastlane (TestFlight) |
| Backend Deploy | Firebase CLI |
| Version Control | Git + LFS |

---

## 3. Directory Structure

```
G-Roll/
├── Assets/
│   ├── _Game Assets/
│   │   ├── Scripts/
│   │   │   ├── Managers/          # 18 singleton managers
│   │   │   ├── Controllers/       # Game logic controllers
│   │   │   ├── Networks/          # 27 remote services
│   │   │   ├── UI/                # 60+ UI scripts
│   │   │   ├── Player/            # Player systems
│   │   │   ├── Entities/          # Game objects (obstacles, collectibles)
│   │   │   ├── MapDesignerTools/  # Level editor
│   │   │   └── Utility/           # Helpers and utilities
│   │   ├── Prefabs/               # Reusable game objects
│   │   ├── Scenes/                # Game scenes
│   │   ├── Materials/             # Shaders and materials
│   │   ├── Animations/            # Animation assets
│   │   └── ScriptableObjects/     # Data containers
│   ├── Resources/
│   │   └── IAPProductCatalog.json # IAP product definitions
│   ├── Firebase/                  # Firebase SDK
│   ├── Appodeal/                  # Ads SDK
│   ├── Plugins/                   # Native plugins
│   └── Scripts/
│       └── UnityPurchasing/       # Generated IAP code
├── functions/
│   ├── src/
│   │   ├── index.ts               # Function exports
│   │   ├── firebase.ts            # Firebase initialization
│   │   ├── modules/               # 14 function modules
│   │   └── utils/                 # Helpers and constants
│   ├── lib/                       # Compiled JavaScript
│   ├── package.json               # Dependencies
│   └── deploy.sh                  # Smart deploy script
├── fastlane/
│   └── Fastfile                   # iOS deployment config
├── .github/
│   └── workflows/
│       └── build.yml              # CI/CD pipeline
├── ProjectSettings/               # Unity configuration
├── Packages/
│   └── manifest.json              # Unity packages
├── exportOptions.plist            # iOS export settings
├── firebase.json                  # Firebase config
└── CLAUDE.md                      # AI assistant guidelines
```

---

## 4. Client Architecture (Unity)

### 4.1 Boot & Initialization Layer

The application startup follows a strict sequence:

```
App Launch
    │
    ▼
┌──────────────┐
│ BootManager  │  Entry point, initializes core systems
└──────┬───────┘
       │
       ▼
┌──────────────────┐
│ AppFlowManager   │  Handles auth flow, profile loading
└──────┬───────────┘
       │
       ▼
┌──────────────────────┐
│ FirebaseLoginHandler │  Firebase Authentication
└──────┬───────────────┘
       │
       ▼
┌──────────────────────┐
│ UserDatabaseManager  │  Load/sync user data
└──────┬───────────────┘
       │
       ▼
┌──────────────┐
│ GameManager  │  Ready for gameplay
└──────────────┘
```

#### Boot Layer Files

| File | Path | Responsibility |
|------|------|----------------|
| `BootManager.cs` | `/Managers/` | Application entry, SDK init |
| `AppFlowManager.cs` | `/Managers/` | Auth flow orchestration |
| `AppManager.cs` | `/Managers/` | App lifecycle (pause, resume, quit) |

### 4.2 Manager Layer (18 Managers)

All managers follow the **Singleton Pattern** and persist across scenes.

#### Core Game Managers

| Manager | Path | Responsibility | Dependencies |
|---------|------|----------------|--------------|
| `GameManager.cs` | `/Managers/` | Game phase state machine | All managers |
| `GameplayManager.cs` | `/Managers/` | In-game session logic | PlayerController, Map |
| `GameplayCameraManager.cs` | `/Managers/` | Gameplay camera control | Cinemachine |
| `GameplayLogicApplier.cs` | `/Managers/` | Apply game rules | GameplayManager |
| `GameplayVisualApplier.cs` | `/Managers/` | Visual feedback | GameplayManager |

#### Data & State Managers

| Manager | Path | Responsibility | Dependencies |
|---------|------|----------------|--------------|
| `DataManager.cs` | `/Managers/` | Local persistence (PlayerPrefs) | None |
| `UserStatManager.cs` | `/Managers/` | Player statistics tracking | DataManager |
| `CurrencyManager.cs` | `/Utility/` | Multi-currency management | Events |

#### Monetization Managers (CRITICAL)

| Manager | Path | Responsibility | Risk Level |
|---------|------|----------------|------------|
| `IAPManager.cs` | `/Managers/` | Unity Purchasing integration | LEVEL 0 |
| `AdManager.cs` | `/Managers/` | Appodeal ads integration | LEVEL 1 |
| `ShopItemManager.cs` | `/Managers/` | Shop economy logic | LEVEL 1 |

#### UI & UX Managers

| Manager | Path | Responsibility |
|---------|------|----------------|
| `UIManager.cs` | `/Managers/` | UI state, panel navigation |
| `AudioManager.cs` | `/Managers/` | SFX playback |
| `BackgroundMusicManager.cs` | `/Managers/` | Music playback |
| `HapticManager.cs` | `/Managers/` | Vibration feedback |
| `TouchManager.cs` | `/Managers/` | Input handling |

#### System Managers

| Manager | Path | Responsibility |
|---------|------|----------------|
| `ObjectPoolingManager.cs` | `/Managers/` | Object pool for performance |
| `NotificationManager.cs` | `/Managers/` | Local push notifications |
| `ReviewManager.cs` | `/Managers/` | App Store review prompts |
| `ApplicationFrameRateHandler.cs` | `/Managers/` | FPS optimization |

### 4.3 Controller Layer

Controllers handle specific game logic domains.

| Controller | Path | Responsibility |
|------------|------|----------------|
| `LevelController.cs` | `/Controllers/` | Level loading and management |
| `Map.cs` | `/Controllers/` | Map grid representation |
| `MapCell.cs` | `/Controllers/` | Individual grid cell |
| `MapGridCellUtility.cs` | `/Controllers/` | Grid math utilities |
| `PlayerSpawnHandler.cs` | `/Controllers/` | Player instantiation |
| `GamePhase.cs` | `/Controllers/` | Phase enum definition |
| `GameplayStats.cs` | `/Controllers/` | Runtime stats tracking |

#### Event System (ScriptableObject Channels)

| Channel | Path | Purpose |
|---------|------|---------|
| `VoidEventChannelSO.cs` | `/Controllers/` | Parameterless events |
| `PhaseEventChannelSO.cs` | `/Controllers/` | Game phase change events |

### 4.4 Network Services Layer (27 Services)

All remote communication goes through this layer. Services call Firebase Cloud Functions.

#### Core Data Services (HIGH RISK)

| Service | Path | Responsibility | Risk |
|---------|------|----------------|------|
| `UserDatabaseManager.cs` | `/Networks/` | Master user data hub - ALL user ops (60KB! No separate UserProfileManager) | CRITICAL |
| `FirebaseLoginHandler.cs` | `/Networks/` | Firebase Auth | LEVEL 0 |
| `IAPRemoteService.cs` | `/Networks/` | Purchase verification | LEVEL 0 |
| `InventoryRemoteService.cs` | `/Networks/` | Server-authoritative inventory | LEVEL 1 |

#### Session & Gameplay Services

| Service | Path | Responsibility |
|---------|------|----------------|
| `SessionRemoteService.cs` | `/Networks/` | Request/end game sessions |
| `SessionResultRemoteService.cs` | `/Networks/` | Submit session results |
| `PlayerStatsRemoteService.cs` | `/Networks/` | Sync player statistics |
| `MapManager.cs` | `/Networks/` | Fetch map data |

#### Economy Services

| Service | Path | Responsibility |
|---------|------|----------------|
| `UserEnergyService.cs` | `/Networks/` | Energy/stamina system |
| `ElitePassService.cs` | `/Networks/` | Subscription management |
| `ElitePassValidator.cs` | `/Networks/` | Subscription validation |

#### Feature Services

| Service | Path | Responsibility |
|---------|------|----------------|
| `AchievementService.cs` | `/Networks/` | Achievement unlock/progress |
| `TaskService.cs` | `/Networks/` | Daily/weekly tasks |
| `LeaderboardService.cs` | `/Networks/` | Leaderboard operations |
| `LeaderboardManager.cs` | `/Networks/` | Leaderboard display |
| `StreakService.cs` | `/Networks/` | Daily streak tracking |
| `AutopilotService.cs` | `/Networks/` | Autopilot feature (12KB) |
| `ReferralRemoteService.cs` | `/Networks/` | Referral code operations |
| `ReferralManager.cs` | `/Networks/` | Referral UI logic |

#### Content Services

| Service | Path | Responsibility |
|---------|------|----------------|
| `RemoteAppDataService.cs` | `/Networks/` | Remote config (24KB) |
| `RemoteItemService.cs` | `/Networks/` | Item catalog |
| `FirestoreRemoteFetcher.cs` | `/Networks/` | Generic Firestore queries |
| `UserInventoryManager.cs` | `/Networks/` | Client inventory state |
| `ItemLocalDatabase.cs` | `/Networks/` | Local item cache |
| `AchievementIconCache.cs` | `/Networks/` | Achievement asset cache |

### 4.5 UI Layer (60+ Scripts)

UI scripts follow the `UI*.cs` naming convention.

#### Core UI

| Script | Responsibility |
|--------|----------------|
| `UIMainMenu.cs` | Main menu screen |
| `UIGamePlay.cs` | In-game HUD |
| `UILevelEnd.cs` | Level complete screen |
| `UIHomePanel.cs` | Home/lobby panel |

#### Shop & Economy UI

| Script | Responsibility |
|--------|----------------|
| `UIShopPanel.cs` | Main shop interface |
| `UIShopItemDisplay.cs` | Shop item cards |
| `UIIAPShopPanel.cs` | Real money shop |
| `UIIAPProduct.cs` | IAP product display |
| `UIAdProduct.cs` | Ad-rewarded products |
| `UICurrencyDisplay.cs` | Currency counters |
| `UIEnergyDisplay.cs` | Energy bar |

#### Feature Panels

| Script | Responsibility |
|--------|----------------|
| `UIAchievementsPanelController.cs` | Achievements screen |
| `UITaskPanel.cs` | Daily tasks |
| `UILeaderboardDisplay.cs` | Leaderboard view |
| `UIRankingPanel.cs` | Ranking details |
| `UIAutoPilotPanel.cs` | Autopilot settings |
| `UIElitePassPanel.cs` | Elite Pass screen |

#### System Panels

| Script | Responsibility |
|--------|----------------|
| `UISetNamePanel.cs` | Username entry |
| `UILoginPanel.cs` | Login screen |
| `UISessionGate.cs` | Session start gate |
| `UIGameplaySettings.cs` | In-game settings |
| `UIGameplayLoading.cs` | Loading screen |
| `UIOverlay.cs` | Overlay management |
| `UIFadePanel.cs` | Fade transitions |

### 4.6 Player System

| Script | Path | Responsibility |
|--------|------|----------------|
| `PlayerController.cs` | `/Player/` | Main player behavior |
| `PlayerMovement.cs` | `/Player/` | Movement physics |
| `PlayerCollision.cs` | `/Player/` | Collision detection |
| `PlayerAnimator.cs` | `/Player/` | Animation state machine |
| `PlayerStatHandler.cs` | `/Player/` | Buff/debuff effects |

### 4.7 Gameplay Entities

#### Collectibles

| Entity | Path | Behavior |
|--------|------|----------|
| `Coin.cs` | `/Entities/` | Currency pickup |
| `BoosterBase.cs` | `/Entities/` | Base booster class |
| `CameraSpeedBooster.cs` | `/Entities/` | Camera speed buff |
| `PlayerSpeedBooster.cs` | `/Entities/` | Player speed buff |
| `InstantFillBooster.cs` | `/Entities/` | Instant energy fill |
| `RandomBooster.cs` | `/Entities/` | Random effect |
| `PlayerMagnet.cs` | `/Controllers/` | Coin magnet |

#### Obstacles

| Entity | Path | Behavior |
|--------|------|----------|
| `Wall.cs` | `/Entities/` | Static wall |
| `MovingWall.cs` | `/Entities/` | Moving wall |
| `CircularRotatorWall.cs` | `/Entities/` | Rotating circular wall |
| `LimitedRotatorWall.cs` | `/Entities/` | Limited rotation wall |
| `Piston.cs` | `/Entities/` | Piston obstacle |
| `RotatorHammer.cs` | `/Entities/` | Rotating hammer |
| `LaserGate.cs` | `/Entities/` | Laser obstacle |
| `TriggerableDoor.cs` | `/Entities/` | Triggered door |
| `TriggerPushButton.cs` | `/Entities/` | Trigger button |
| `SpeedModifierZone.cs` | `/Entities/` | Speed change zone |

#### Special

| Entity | Path | Behavior |
|--------|------|----------|
| `Fan.cs` | `/Entities/` | Air push mechanic |
| `Teleport.cs` | `/Entities/` | Teleportation |
| `LevelFinishZone.cs` | `/Entities/` | Level end trigger |
| `LevelPart.cs` | `/Entities/` | Modular level piece |

### 4.8 Map Designer Tools

Internal tools for level creation.

| Tool | Responsibility |
|------|----------------|
| `BuildMode.cs` | Map building editor mode |
| `MapDesignerControlsHUD.cs` | Designer UI overlay |
| `GridPlacer.cs` | Grid-based placement |
| `MapSaver.cs` | Map persistence |
| `OrbitCamera.cs` | Designer camera control |
| `DraggableBuildItem.cs` | Drag-drop items |
| `BuildableItem.cs` | Placeable item definition |

### 4.9 Utility Systems

#### Currency System

| File | Responsibility |
|------|----------------|
| `CurrencyManager.cs` | Multi-currency state |
| `CurrencyType.cs` | Currency enum |
| `CurrencyData.cs` | Currency data structure |
| `CurrencyEvents.cs` | Currency change events |

#### Other Utilities

| File | Responsibility |
|------|----------------|
| `EventHub.cs` | Central event bus |
| `PlayerPrefsKeys.cs` | PlayerPrefs key constants |
| `IdUtil.cs` | ID generation |
| `ProbabilityTable.cs` | Weighted random |
| `UpgradeData.cs` | Upgrade system data |
| `ResourcePaths.cs` | Asset path constants |
| `MiniJson.cs` | JSON serialization |
| `MapLoaderJsonAdapter.cs` | Map JSON conversion |

---

## 5. Backend Architecture (Firebase)

### 5.1 Function Modules Overview

All functions are deployed to `us-central1` with 512MiB memory.

```
functions/src/
├── index.ts              # Exports all functions
├── firebase.ts           # Admin SDK initialization
├── modules/
│   ├── user.functions.ts
│   ├── iap.functions.ts        # CRITICAL
│   ├── session.functions.ts
│   ├── achievements.functions.ts
│   ├── streak.functions.ts
│   ├── energy.functions.ts
│   ├── shop.functions.ts
│   ├── content.functions.ts
│   ├── leaderboard.functions.ts
│   ├── scheduler.functions.ts
│   ├── ad.functions.ts
│   ├── autopilot.functions.ts
│   ├── tasks.functions.ts
│   └── map.functions.ts
└── utils/
    ├── helpers.ts
    └── constants.ts
```

### 5.2 Function Details

#### User Management (`user.functions.ts`)

| Function | Trigger | Purpose |
|----------|---------|---------|
| `createUser` | HTTPS Callable | Initialize new user document |
| `updateProfile` | HTTPS Callable | Update display name, avatar |
| `applyReferralCode` | HTTPS Callable | Apply referral and grant rewards |
| `deleteAccount` | HTTPS Callable | GDPR user deletion |

#### IAP Verification (`iap.functions.ts`) - CRITICAL

| Function | Trigger | Purpose |
|----------|---------|---------|
| `verifyPurchase` | HTTPS Callable | Verify receipt with store APIs |

**Security Notes:**
- Uses Google Service Account for Play Billing API
- Validates receipt authenticity
- Grants entitlements only after verification
- Handles both iOS (StoreKit) and Android (Google Play)

#### Session Management (`session.functions.ts`)

| Function | Trigger | Purpose |
|----------|---------|---------|
| `requestSession` | HTTPS Callable | Start new game session |
| `submitSessionResult` | HTTPS Callable | End session, calculate rewards |

#### Achievements (`achievements.functions.ts`)

| Function | Trigger | Purpose |
|----------|---------|---------|
| `unlockAchievement` | HTTPS Callable | Mark achievement complete |
| `updateProgress` | HTTPS Callable | Increment achievement progress |
| `getAchievements` | HTTPS Callable | Fetch user achievements |

#### Streak System (`streak.functions.ts`)

| Function | Trigger | Purpose |
|----------|---------|---------|
| `claimDailyStreak` | HTTPS Callable | Claim daily login reward |
| `getStreakStatus` | HTTPS Callable | Get current streak info |

#### Energy System (`energy.functions.ts`)

| Function | Trigger | Purpose |
|----------|---------|---------|
| `getEnergy` | HTTPS Callable | Get current energy + regen time |
| `consumeEnergy` | HTTPS Callable | Spend energy for session |
| `refillEnergy` | HTTPS Callable | Refill via ad/IAP |

#### Shop (`shop.functions.ts`)

| Function | Trigger | Purpose |
|----------|---------|---------|
| `purchaseItem` | HTTPS Callable | Buy item with in-game currency |
| `equipItem` | HTTPS Callable | Set item as equipped |
| `activateConsumable` | HTTPS Callable | Use consumable item |

#### Content (`content.functions.ts`)

| Function | Trigger | Purpose |
|----------|---------|---------|
| `getItems` | HTTPS Callable | Fetch item catalog |
| `getAppData` | HTTPS Callable | Fetch remote config |

#### Leaderboard (`leaderboard.functions.ts`)

| Function | Trigger | Purpose |
|----------|---------|---------|
| `submitScore` | HTTPS Callable | Submit high score |
| `getLeaderboard` | HTTPS Callable | Fetch leaderboard entries |
| `getUserRank` | HTTPS Callable | Get user's current rank |

#### Scheduler (`scheduler.functions.ts`)

| Function | Trigger | Purpose |
|----------|---------|---------|
| `dailyReset` | Pub/Sub Schedule | Reset daily limits |
| `weeklyReset` | Pub/Sub Schedule | Reset weekly tasks |

#### Ads (`ad.functions.ts`)

| Function | Trigger | Purpose |
|----------|---------|---------|
| `getAdProducts` | HTTPS Callable | Fetch ad reward config |
| `claimAdReward` | HTTPS Callable | Grant reward after ad view |
| `getAdUsage` | HTTPS Callable | Get daily ad view count |

#### Autopilot (`autopilot.functions.ts`)

| Function | Trigger | Purpose |
|----------|---------|---------|
| `startAutopilot` | HTTPS Callable | Begin autopilot session |
| `claimAutopilotRewards` | HTTPS Callable | Collect idle rewards |

#### Tasks (`tasks.functions.ts`)

| Function | Trigger | Purpose |
|----------|---------|---------|
| `getTasks` | HTTPS Callable | Fetch daily/weekly tasks |
| `updateTaskProgress` | HTTPS Callable | Increment task progress |
| `claimTaskReward` | HTTPS Callable | Claim completed task |

#### Map (`map.functions.ts`)

| Function | Trigger | Purpose |
|----------|---------|---------|
| `getMaps` | HTTPS Callable | Fetch available maps |
| `getMapData` | HTTPS Callable | Fetch specific map JSON |

---

## 6. Data Layer

### 6.1 Firestore Data Schema

**⚠️ SCHEMA ACCURACY NOTE**

This schema is derived from **code analysis only**. AI cannot see actual Firestore data without:
- Firestore export/snapshot
- Firebase console screenshots
- Manual data dumps

**Potential Gaps:**
- Fields added by manual scripts (not in code)
- Legacy fields from old versions
- Fields only written by external tools

---

**Database:** `getfi` (custom, not `(default)`)

#### **Collection: `users/{uid}/`**

**Creation:** Auto-created by `createUserProfile` Auth trigger

**Root Fields:**
- `uid`, `email`, `photoUrl`, `username`, `isProfileComplete`
- `rank`, `createdAt`, `updatedAt`, `lastLogin`, `lastLoginLocalDate`, `streak`
- `currency`, `premiumCurrency`, `cumulativeCurrencyEarned`
- `energyCurrent`, `energyMax`, `energyRegenPeriodSec`, `energyUpdatedAt`
- `chapterProgress`, `maxScore`, `seasonalMaxScores` (map)
- `statsJson` (string), `sessionsPlayed`, `totalPlaytimeSec`, `powerUpsCollected`, `maxCombo`, `itemsPurchasedCount`
- `hasElitePass`, `elitePassExpiresAt`
- `referralKey`, `referredByKey`, `referredByUid`, `referralAppliedAt`, `referralCount`, `referralEarnings`
- `removeAds`, `adClaimsJson`, `usernameLastChangedAt`

**Subcollections:**
- `inventory/{itemId}/`: `{ owned, equipped, quantity, acquiredAt, source }`
- `loadout/current/`: `{ equippedItemIds[] }`
- `activeConsumables/{itemId}/`: `{ active, expiresAt, lastActivatedAt }`
- `sessions/{sessionId}/`: `{ state, mode, startedAt, processedAt, earnedCurrency, earnedScore, usedRevives, success }`
- `achievements/{typeId}/`: `{ progress, level, nextThreshold, claimedLevels[], lastClaimedAt }`
- `taskCompletion/{taskId}/`: `{ isCompleted, latestEditTime, rewardGranted }`
- `iaptransactions/{autoId}/`: `{ receipt, productId, platform, deviceId, verifiedAt, verificationResponse, granted }`
- `referralData/pendingReferralEarnings/records/{childUid}/`: `{ amount, childName, updatedAt }`
- `referralData/currentReferralEarnings/records/{childUid}/`: `{ amount, childName, updatedAt }`
- `referralData/referralTransactions/records/{autoId}/`: `{ amount, claimedAt, breakdown[] }`

#### **Collection: `appdata/items/{itemId}/itemdata/`**

**Creation:** **MANUAL SEED** (not auto-created by code)

- `itemName`, `itemDescription`, `itemIconUrl`
- `itemGetPrice`, `itemPremiumPrice`
- `itemIsConsumable`, `itemIsRewardedAd`, `itemReferralThreshold`
- `itemstat_comboPower`, `itemstat_playerSpeed`, `itemstat_coinMultiplierPercent`, etc.

#### **Other Collections:**
- `appdata/achievements/types/{typeId}/`: Achievement definitions (manual seed)
- `appdata/taskDatas/tasks/{taskId}/`: Task definitions (manual seed)
- `chapters/{chapterOrder}/`: Map JSON data (manual seed)
- `usernames/{usernameLower}/`: Username uniqueness enforcement
- `referralKeys/{key}/`: Referral key uniqueness
- `leaderboards/{leaderboardId}/entries/{uid}/`: Auto-synced leaderboard entries
- `seasons/{seasonId}/`: Season definitions (manual seed)

**See `docs/architecture/COMPONENTS.md` for complete field list and data flow.**

### 6.2 Local Storage (Client)

| Storage | Technology | Purpose |
|---------|------------|---------|
| PlayerPrefs | Unity | Settings, cache keys |
| Application.persistentDataPath | File | Large data cache |
| ItemLocalDatabase | In-memory | Runtime item lookup |

### 6.3 Remote Config

Remote configuration is stored in `/appdata/config` and includes:
- Feature flags
- Economy tuning values
- A/B test parameters
- Maintenance mode flag

---

## 7. Third-Party Integrations

### 7.1 Firebase Suite

| Service | Usage | SDK Location |
|---------|-------|--------------|
| Authentication | Email/OAuth login | `/Assets/Firebase/` |
| Firestore | Primary database | `/Assets/Firebase/` |
| Cloud Functions | Backend logic | `/functions/` |
| Analytics | Event tracking | `/Assets/Firebase/` |
| Crashlytics | Crash reporting | `/Assets/Firebase/` |

### 7.2 Appodeal (Ads)

| Component | Location |
|-----------|----------|
| SDK | `/Assets/Appodeal/` |
| Integration | `AdManager.cs` |
| Platforms | Android, iOS |

**Ad Types:**
- Rewarded Video (primary monetization)
- Interstitial (between sessions)
- Banner (optional)

### 7.3 Unity IAP

| Component | Location |
|-----------|----------|
| Package | Unity Purchasing 5.1.2 |
| Integration | `IAPManager.cs` |
| Products | `/Resources/IAPProductCatalog.json` |

**Product Types:**
- Consumable: Diamond packs (5, 10, 25, 60, 150, 400, 1000)
- Non-Consumable: Remove Ads
- Subscription: Elite Pass (Monthly, Annual)

### 7.4 Google Play Games Services

| Setting | Value |
|---------|-------|
| App ID | 926358794548 |
| Bundle ID | com.get.groll |
| Config | `/ProjectSettings/GooglePlayGameSettings.txt` |

---

## 8. CI/CD Pipeline

### 8.1 Current Status (January 2026)

**Platform:** GitHub Actions
**Runner:** Self-hosted (developer's Mac Mini)
**Status:** ⚠️ **CURRENTLY OFFLINE** (runner stopped)

#### **Pipeline Configuration:**

**File:** `.github/workflows/ios-build.yml` (primary workflow)

**Triggers:**
- Push to `main` branch
- Manual workflow dispatch

**Steps:**
1. Checkout repository
2. Install Unity (via Unity Hub)
3. Run Unity build command
4. Export iOS project (Xcode)
5. Build IPA via `xcodebuild`
6. Upload to TestFlight (future)

#### **iOS Export Issues:**

**File:** `exportOptions.plist`

**Problem:**
- Contains old Team ID from previous company (Level 4 security issue)
- Needs manual update to current Apple Developer account

**Action Required:**
- Update `teamID` field in `exportOptions.plist`
- Verify provisioning profiles are current
- Test export locally before re-enabling CI

#### **Android CI:**

**Status:** ❌ **NOT IMPLEMENTED**

**Reason:** No Android build workflow exists yet

**Backlog Item:** Create Android build workflow (similar to iOS)

---

### 8.2 Self-Hosted Runner

**Current Setup:**
- Mac Mini (developer's personal machine)
- Location: Developer's home office
- IP: Dynamic (no static IP)

**Risks:**
- Single point of failure
- Power outage = no builds
- Network issues = job failures
- Security: Mac has production signing certificates

**Alternative Options:**

1. **GitHub-Hosted Runner:**
   - Pros: No maintenance, always available
   - Cons: macOS runners expensive ($0.08/min), limited to 6 hours/job

2. **Cloud Mac (AWS EC2 Mac, MacStadium):**
   - Pros: Always online, scalable
   - Cons: Monthly cost ($50-200/mo)

3. **Hybrid:**
   - Use GitHub-hosted for PR checks (fast feedback)
   - Use self-hosted for release builds (cost savings)

---

## 9. Module Dependency Map

### 9.1 Client Dependencies

```
                    ┌─────────────┐
                    │ BootManager │
                    └──────┬──────┘
                           │
              ┌────────────┼────────────┐
              ▼            ▼            ▼
       ┌────────────┐ ┌─────────┐ ┌──────────┐
       │AppFlowMgr  │ │DataMgr  │ │ AudioMgr │
       └─────┬──────┘ └────┬────┘ └──────────┘
             │              │
             ▼              │
    ┌─────────────────┐     │
    │FirebaseLoginHdlr│     │
    └────────┬────────┘     │
             │              │
             ▼              │
    ┌─────────────────────┐ │
    │ UserDatabaseManager │◄┘
    └──────────┬──────────┘
               │
    ┌──────────┼──────────────────────┐
    ▼          ▼                      ▼
┌────────┐ ┌─────────────┐    ┌──────────────┐
│GameMgr │ │CurrencyMgr  │    │InventoryMgr │
└───┬────┘ └─────────────┘    └──────────────┘
    │
    ├──────────────┬───────────────┐
    ▼              ▼               ▼
┌──────────┐  ┌─────────┐   ┌───────────┐
│IAPManager│  │AdManager│   │GameplayMgr│
└──────────┘  └─────────┘   └───────────┘
```

### 9.2 Backend Dependencies

```
index.ts
    │
    ├── firebase.ts (Admin SDK)
    │
    └── modules/*
            │
            └── utils/helpers.ts
            └── utils/constants.ts
```

### 9.3 Data Flow

```
Client Action
     │
     ▼
Network Service (e.g., IAPRemoteService)
     │
     │ httpsCallable()
     ▼
Firebase Function (e.g., iap.functions.ts)
     │
     │ Firestore operations
     ▼
Firestore Database
     │
     │ Response
     ▼
Client State Update
     │
     ▼
UI Refresh
```

---

## 10. Critical Integration Points

### 10.1 Revenue-Critical Paths

| Path | Components | Risk |
|------|------------|------|
| IAP Purchase | IAPManager -> IAPRemoteService -> iap.functions -> Store API | Highest |
| Ad Reward | AdManager -> Appodeal -> ad.functions -> Currency grant | High |
| Elite Pass | ElitePassService -> iap.functions -> Subscription verify | High |

### 10.2 Data Integrity Points

| Point | Risk if Broken |
|-------|----------------|
| UserDatabaseManager sync | User loses progress |
| Session result submission | Score not recorded |
| Inventory operations | Items lost/duplicated |
| Energy calculations | Exploitable or broken gameplay |

### 10.3 Authentication Chain

```
FirebaseLoginHandler
        │
        ▼
Firebase Auth (UID)
        │
        ▼
UserDatabaseManager (User Doc)
        │
        ▼
All Network Services (Authenticated requests)
```

### 10.4 External API Dependencies

| Dependency | Impact if Down |
|------------|----------------|
| Firebase Auth | Cannot login |
| Firestore | Cannot play (server-authoritative) |
| Cloud Functions | No backend operations |
| Google Play Billing API | IAP verification fails |
| App Store Connect | iOS IAP verification fails |
| Appodeal | No ad revenue |

---

## Appendix A: File Size Reference

Large files that require special attention:

| File | Size | Notes |
|------|------|-------|
| `UserDatabaseManager.cs` | ~60KB | Monolithic, high risk |
| `RemoteAppDataService.cs` | ~24KB | Complex config logic |
| `AutopilotService.cs` | ~12KB | Feature complexity |
| `IAPManager.cs` | ~15KB | Critical, well-structured |

---

## Appendix B: Naming Conventions

| Type | Convention | Example |
|------|------------|---------|
| Managers | `*Manager.cs` | `GameManager.cs` |
| Services | `*Service.cs`, `*RemoteService.cs` | `EnergyService.cs` |
| Controllers | `*Controller.cs` | `LevelController.cs` |
| UI Scripts | `UI*.cs` | `UIShopPanel.cs` |
| Event Channels | `*EventChannelSO.cs` | `VoidEventChannelSO.cs` |
| Data Objects | `*Data.cs`, `*SO.cs` | `CurrencyData.cs` |

---

## Appendix C: Glossary

| Term | Definition |
|------|------------|
| Server-Authoritative | All critical operations validated server-side |
| EventChannel | ScriptableObject-based pub/sub pattern |
| UniTask | Async/await library for Unity |
| httpsCallable | Firebase function invocation method |
| Entitlement | Granted right to use a purchased item |
| Elite Pass | Premium subscription tier |

---

*End of Document*
