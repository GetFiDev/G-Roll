# G-Roll Critical Surfaces: Access Control Levels

> **Document Version**: 1.0
> **Last Updated**: 2025-01-12
> **Purpose**: Define access control levels for AI-assisted development
> **Audience**: AI assistants (Opus 4.5), Developers, Code reviewers
> **Authority**: This document is referenced by CLAUDE.md and enforced in all AI interactions

---

## Overview

This document defines three access levels for code modifications:

| Level | Name | AI Permissions | Human Review |
|-------|------|----------------|--------------|
| **0** | Hard Lock | Read only, report, suggest tests | N/A (no changes) |
| **1** | Guarded | Small changes with strict review | Required before merge |
| **2** | Safe | Normal development workflow | Standard review |

---

## Access Level Definitions

### Level 0: HARD LOCK - "Never Touch"

**Definition**: Files in this category are so critical that AI assistants must NEVER propose direct code changes. These files handle real money, user authentication, or release infrastructure.

**AI Permissions**:
- Read and analyze code
- Report bugs or security issues
- Suggest improvements (as comments only)
- Propose test cases
- Document behavior

**AI Restrictions**:
- NO code modifications
- NO refactoring suggestions with diffs
- NO "quick fixes"
- NO touching any line of code

**Human Override**: Only a designated senior developer may modify these files, and only after explicit discussion.

---

### Level 1: GUARDED - "Very Controlled"

**Definition**: Files that handle economy, monetization logic, or sensitive data. Changes are allowed but require extra scrutiny.

**AI Permissions**:
- Read and analyze
- Propose small, focused changes
- Bug fixes with clear justification
- Performance improvements (no logic changes)
- Add logging/monitoring

**AI Restrictions**:
- Maximum 50 lines changed per PR
- Maximum 1-3 files per PR
- NO changes to core business logic
- NO changes to data contracts/schemas
- Must include rollback plan

**Human Override**: Changes require review from code owner before merge.

---

### Level 2: SAFE - "Development Playground"

**Definition**: Files where AI can work with standard development practices. These are UI, gameplay logic, utilities, and non-critical systems.

**AI Permissions**:
- Full development capabilities
- Refactoring
- New features
- Bug fixes
- Performance optimization
- Test writing

**Restrictions**:
- Standard code review applies
- Follow project coding standards
- Maximum 200-400 lines per PR
- Maximum 5-10 files per PR

---

## Level 0 Files: HARD LOCK

### Authentication & Identity

| File | Path | Reason |
|------|------|--------|
| `FirebaseLoginHandler.cs` | `Assets/_Game Assets/Scripts/Networks/` | Controls user authentication. Compromise = account takeover |
| `firebase.ts` | `functions/src/` | Firebase Admin SDK initialization. Has elevated privileges |

### In-App Purchase (Revenue Critical)

| File | Path | Reason |
|------|------|--------|
| `IAPManager.cs` | `Assets/_Game Assets/Scripts/Managers/` | Unity Purchasing integration. Handles real money transactions |
| `IAPRemoteService.cs` | `Assets/_Game Assets/Scripts/Networks/` | Sends receipts to server. Receipt manipulation = free purchases |
| `iap.functions.ts` | `functions/src/modules/` | Server-side purchase verification. Validates with Store APIs |

### Credentials & Secrets

| File/Pattern | Path | Reason |
|--------------|------|--------|
| `service-account*.json` | `functions/` | Google Cloud credentials. Exposure = full backend access |
| `*.plist` (signing) | Root | iOS signing configuration |
| `exportOptions.plist` | Root | iOS export settings with Team IDs |
| `.env*` | Any | Environment variables (if exists) |
| `*credentials*` | Any | Any credential files |
| `*secret*` | Any | Any secret files |

### Build & Release Pipeline

| File | Path | Reason |
|------|------|--------|
| `build.yml` | `.github/workflows/` | CI/CD pipeline. Tampering = malicious builds |
| `Fastfile` | `fastlane/` | iOS deployment. Controls what goes to App Store |
| `deploy.sh` | `functions/` | Backend deployment script |

### Level 0 Complete Path List

```
# Authentication
Assets/_Game Assets/Scripts/Networks/FirebaseLoginHandler.cs
functions/src/firebase.ts

# IAP (In-App Purchase)
Assets/_Game Assets/Scripts/Managers/IAPManager.cs
Assets/_Game Assets/Scripts/Networks/IAPRemoteService.cs
functions/src/modules/iap.functions.ts

# Credentials (patterns)
functions/src/service-account*.json
functions/**/*credential*
functions/**/*secret*
**/*.p12
**/*.p8
**/*-key.json

# Build & Release
.github/workflows/build.yml
.github/workflows/*.yml
fastlane/Fastfile
fastlane/Appfile
fastlane/Matchfile
functions/deploy.sh
exportOptions.plist

# Unity Signing (if exists)
*.keystore
*.jks
```

---

## Level 1 Files: GUARDED

### Monetization Logic

| File | Path | Reason | Max Changes |
|------|------|--------|-------------|
| `AdManager.cs` | `Assets/_Game Assets/Scripts/Managers/` | Ad integration, reward logic | 50 lines |
| `ad.functions.ts` | `functions/src/modules/` | Ad reward server validation | 30 lines |
| `ShopItemManager.cs` | `Assets/_Game Assets/Scripts/Managers/` | Shop economy logic | 50 lines |
| `shop.functions.ts` | `functions/src/modules/` | Server-side shop operations | 50 lines |

### Economy & Currency

| File | Path | Reason | Max Changes |
|------|------|--------|-------------|
| `CurrencyManager.cs` | `Assets/_Game Assets/Scripts/Utility/` | Multi-currency state | 30 lines |
| `energy.functions.ts` | `functions/src/modules/` | Energy system (affects gameplay pacing) | 30 lines |
| `UserEnergyService.cs` | `Assets/_Game Assets/Scripts/Networks/` | Client energy logic | 30 lines |

### User Data (Large/Complex Files)

| File | Path | Reason | Max Changes |
|------|------|--------|-------------|
| `UserDatabaseManager.cs` | `Assets/_Game Assets/Scripts/Networks/` | 60KB monolith, master data hub | 30 lines |
| `RemoteAppDataService.cs` | `Assets/_Game Assets/Scripts/Networks/` | 24KB, remote config | 30 lines |
| `user.functions.ts` | `functions/src/modules/` | User creation, profile | 30 lines |

### Inventory & Entitlements

| File | Path | Reason | Max Changes |
|------|------|--------|-------------|
| `InventoryRemoteService.cs` | `Assets/_Game Assets/Scripts/Networks/` | Server-authoritative inventory | 50 lines |
| `UserInventoryManager.cs` | `Assets/_Game Assets/Scripts/Networks/` | Client inventory state | 50 lines |

### Subscriptions

| File | Path | Reason | Max Changes |
|------|------|--------|-------------|
| `ElitePassService.cs` | `Assets/_Game Assets/Scripts/Networks/` | Subscription management | 30 lines |
| `ElitePassValidator.cs` | `Assets/_Game Assets/Scripts/Networks/` | Subscription validation | 30 lines |

### Session & Anti-Cheat

| File | Path | Reason | Max Changes |
|------|------|--------|-------------|
| `session.functions.ts` | `functions/src/modules/` | Session validation, anti-cheat | 50 lines |
| `SessionRemoteService.cs` | `Assets/_Game Assets/Scripts/Networks/` | Session requests | 50 lines |
| `SessionResultRemoteService.cs` | `Assets/_Game Assets/Scripts/Networks/` | Result submission | 50 lines |

### Data Contracts & Constants

| File | Path | Reason | Max Changes |
|------|------|--------|-------------|
| `constants.ts` | `functions/src/utils/` | Backend constants, affects all functions | 20 lines |
| `IAPProductCatalog.json` | `Assets/Resources/` | IAP product definitions | 10 lines |

### Level 1 Complete Path List

```
# Monetization
Assets/_Game Assets/Scripts/Managers/AdManager.cs
Assets/_Game Assets/Scripts/Managers/ShopItemManager.cs
functions/src/modules/ad.functions.ts
functions/src/modules/shop.functions.ts

# Economy
Assets/_Game Assets/Scripts/Utility/CurrencyManager.cs
Assets/_Game Assets/Scripts/Networks/UserEnergyService.cs
functions/src/modules/energy.functions.ts

# User Data (Large Files)
Assets/_Game Assets/Scripts/Networks/UserDatabaseManager.cs
Assets/_Game Assets/Scripts/Networks/RemoteAppDataService.cs
functions/src/modules/user.functions.ts

# Inventory
Assets/_Game Assets/Scripts/Networks/InventoryRemoteService.cs
Assets/_Game Assets/Scripts/Networks/UserInventoryManager.cs

# Subscriptions
Assets/_Game Assets/Scripts/Networks/ElitePassService.cs
Assets/_Game Assets/Scripts/Networks/ElitePassValidator.cs

# Sessions
Assets/_Game Assets/Scripts/Networks/SessionRemoteService.cs
Assets/_Game Assets/Scripts/Networks/SessionResultRemoteService.cs
functions/src/modules/session.functions.ts

# Data Contracts
functions/src/utils/constants.ts
Assets/Resources/IAPProductCatalog.json
```

---

## Level 2 Files: SAFE

### UI Layer (All Safe)

```
Assets/_Game Assets/Scripts/UI/**/*.cs
```

**Includes** (60+ files):
- `UIMainMenu.cs`, `UIGamePlay.cs`, `UILevelEnd.cs`
- `UIShopPanel.cs`, `UIShopItemDisplay.cs`
- `UIAchievementsPanelController.cs`, `UITaskPanel.cs`
- `UILeaderboardDisplay.cs`, `UIRankingPanel.cs`
- `UIAutoPilotPanel.cs`, `UIElitePassPanel.cs`
- `UISetNamePanel.cs`, `UILoginPanel.cs`
- All other UI*.cs files

### Gameplay Entities

```
Assets/_Game Assets/Scripts/Entities/**/*.cs
```

**Includes**:
- `Coin.cs`, `BoosterBase.cs`, `*Booster.cs`
- `Wall.cs`, `MovingWall.cs`, `*Wall.cs`
- `Piston.cs`, `LaserGate.cs`, `Fan.cs`
- `Teleport.cs`, `LevelFinishZone.cs`
- All obstacle and collectible scripts

### Controllers

```
Assets/_Game Assets/Scripts/Controllers/**/*.cs
```

**Includes**:
- `LevelController.cs`
- `Map.cs`, `MapCell.cs`, `MapGridCellUtility.cs`
- `PlayerSpawnHandler.cs`
- `GamePhase.cs`, `GameplayStats.cs`
- Event channels (`*EventChannelSO.cs`)

### Player System

```
Assets/_Game Assets/Scripts/Player/**/*.cs
```

**Includes**:
- `PlayerController.cs`
- `PlayerMovement.cs`
- `PlayerCollision.cs`
- `PlayerAnimator.cs`
- `PlayerStatHandler.cs`

### Map Designer Tools

```
Assets/_Game Assets/Scripts/MapDesignerTools/**/*.cs
```

**Includes**:
- `BuildMode.cs`
- `MapDesignerControlsHUD.cs`
- `GridPlacer.cs`, `MapSaver.cs`
- `OrbitCamera.cs`
- All editor tools

### Core Managers (Non-Monetization)

```
Assets/_Game Assets/Scripts/Managers/GameManager.cs
Assets/_Game Assets/Scripts/Managers/GameplayManager.cs
Assets/_Game Assets/Scripts/Managers/GameplayCameraManager.cs
Assets/_Game Assets/Scripts/Managers/UIManager.cs
Assets/_Game Assets/Scripts/Managers/AudioManager.cs
Assets/_Game Assets/Scripts/Managers/BackgroundMusicManager.cs
Assets/_Game Assets/Scripts/Managers/HapticManager.cs
Assets/_Game Assets/Scripts/Managers/TouchManager.cs
Assets/_Game Assets/Scripts/Managers/ObjectPoolingManager.cs
Assets/_Game Assets/Scripts/Managers/NotificationManager.cs
Assets/_Game Assets/Scripts/Managers/ReviewManager.cs
Assets/_Game Assets/Scripts/Managers/DataManager.cs
Assets/_Game Assets/Scripts/Managers/UserStatManager.cs
Assets/_Game Assets/Scripts/Managers/ApplicationFrameRateHandler.cs
Assets/_Game Assets/Scripts/Managers/GameplayLogicApplier.cs
Assets/_Game Assets/Scripts/Managers/GameplayVisualApplier.cs
```

### Utility Scripts (Except CurrencyManager)

```
Assets/_Game Assets/Scripts/Utility/**/*.cs
# Except: CurrencyManager.cs (Level 1)
```

**Includes**:
- `EventHub.cs`
- `PlayerPrefsKeys.cs`
- `IdUtil.cs`
- `ProbabilityTable.cs`
- `UpgradeData.cs`
- `ResourcePaths.cs`
- `MiniJson.cs`
- `MapLoaderJsonAdapter.cs`

### Non-Critical Network Services

```
Assets/_Game Assets/Scripts/Networks/AchievementService.cs
Assets/_Game Assets/Scripts/Networks/TaskService.cs
Assets/_Game Assets/Scripts/Networks/LeaderboardService.cs
Assets/_Game Assets/Scripts/Networks/LeaderboardManager.cs
Assets/_Game Assets/Scripts/Networks/StreakService.cs
Assets/_Game Assets/Scripts/Networks/AutopilotService.cs
Assets/_Game Assets/Scripts/Networks/ReferralRemoteService.cs
Assets/_Game Assets/Scripts/Networks/ReferralManager.cs
Assets/_Game Assets/Scripts/Networks/RemoteItemService.cs
Assets/_Game Assets/Scripts/Networks/FirestoreRemoteFetcher.cs
Assets/_Game Assets/Scripts/Networks/ItemLocalDatabase.cs
Assets/_Game Assets/Scripts/Networks/AchievementIconCache.cs
Assets/_Game Assets/Scripts/Networks/MapManager.cs
Assets/_Game Assets/Scripts/Networks/PlayerStatsRemoteService.cs
```

### Non-Critical Backend Functions

```
functions/src/modules/achievements.functions.ts
functions/src/modules/streak.functions.ts
functions/src/modules/leaderboard.functions.ts
functions/src/modules/tasks.functions.ts
functions/src/modules/autopilot.functions.ts
functions/src/modules/map.functions.ts
functions/src/modules/content.functions.ts
functions/src/modules/scheduler.functions.ts
functions/src/utils/helpers.ts
```

### Documentation

```
docs/**/*.md
README.md
```

### Test Files (When Added)

```
Assets/Tests/**/*.cs
functions/src/**/*.test.ts
functions/src/**/*.spec.ts
```

---

## Quick Reference Matrix

| Category | Level 0 | Level 1 | Level 2 |
|----------|---------|---------|---------|
| **IAP/Purchase** | IAPManager, IAPRemoteService, iap.functions | - | - |
| **Ads** | - | AdManager, ad.functions | - |
| **Auth** | FirebaseLoginHandler, firebase.ts | - | - |
| **User Data** | - | UserDatabaseManager, user.functions | - |
| **Inventory** | - | InventoryRemoteService | - |
| **Currency** | - | CurrencyManager | Other currency utilities |
| **Energy** | - | energy.functions, UserEnergyService | - |
| **Sessions** | - | session.functions, SessionRemoteService | - |
| **Shop** | - | shop.functions, ShopItemManager | UIShopPanel |
| **Subscriptions** | - | ElitePassService, ElitePassValidator | UIElitePassPanel |
| **Leaderboards** | - | - | All leaderboard files |
| **Achievements** | - | - | All achievement files |
| **Tasks** | - | - | All task files |
| **Streaks** | - | - | All streak files |
| **UI** | - | - | All UI files |
| **Gameplay** | - | - | All gameplay files |
| **Player** | - | - | All player files |
| **Map/Level** | - | - | All map files |
| **Build/CI** | build.yml, Fastfile, deploy.sh | - | - |
| **Credentials** | All credential/secret files | - | - |

---

## Enforcement Rules

### For AI Assistants (Opus 4.5)

When asked to modify code:

1. **Check this document first**
2. **If Level 0**:
   - Respond: "This file is Level 0 (Hard Lock). I cannot modify it directly."
   - Offer: Analysis, bug reports, test suggestions
3. **If Level 1**:
   - Respond: "This file is Level 1 (Guarded). Proceeding with restricted changes."
   - Limit changes to specified maximum
   - Include risk assessment
   - Include rollback plan
4. **If Level 2**:
   - Proceed with standard development
   - Follow normal PR limits (200-400 lines, 5-10 files)

### For Human Reviewers

1. **Any PR touching Level 0**: Auto-reject, escalate to senior
2. **Any PR touching Level 1**: Extra scrutiny, verify limits
3. **Level 2 PRs**: Standard review process

---

## Escalation Path

```
Level 0 Modification Request
         │
         ▼
   Is it urgent security fix?
         │
    ┌────┴────┐
   Yes       No
    │         │
    ▼         ▼
 Contact    Discuss in
 Senior     team meeting
 Dev        before any
 Directly   changes
```

---

## Changelog

| Date | Change | Author |
|------|--------|--------|
| 2025-01-12 | Initial version | AI Pipeline Setup |

---

## Related Documents

- [COMPONENTS.md](../architecture/COMPONENTS.md) - Architecture overview
- [FLOWS.md](../architecture/FLOWS.md) - System flow diagrams
- [CLAUDE.md](../../CLAUDE.md) - AI assistant guidelines
- [SMOKE.md](./SMOKE.md) - Quick test checklist
- [CRITICAL.md](./CRITICAL.md) - Release test checklist

---

*End of Document*
