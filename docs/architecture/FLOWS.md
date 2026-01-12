# G-Roll Architecture: Flow Diagrams

> **Document Version**: 1.0
> **Last Updated**: 2025-01-12
> **Purpose**: Detailed flow diagrams for all critical system operations
> **Related**: [COMPONENTS.md](./COMPONENTS.md)

---

## Table of Contents

1. [App Startup Flow](#1-app-startup-flow)
2. [Authentication Flow](#2-authentication-flow)
3. [IAP Purchase Flow](#3-iap-purchase-flow) - CRITICAL
4. [Ad Reward Flow](#4-ad-reward-flow)
5. [Session & Gameplay Flow](#5-session--gameplay-flow)
6. [User Data Sync Flow](#6-user-data-sync-flow)
7. [Energy System Flow](#7-energy-system-flow)
8. [Shop Purchase Flow](#8-shop-purchase-flow)
9. [Leaderboard Flow](#9-leaderboard-flow)
10. [Achievement & Task Flow](#10-achievement--task-flow)
11. [Streak System Flow](#11-streak-system-flow)
12. [Elite Pass Flow](#12-elite-pass-flow)
13. [Referral System Flow](#13-referral-system-flow)
14. [Map Loading Flow](#14-map-loading-flow)
15. [Error Handling Patterns](#15-error-handling-patterns)

---

## 1. App Startup Flow

### 1.1 Overview

The app follows a strict initialization sequence to ensure all systems are ready before gameplay.

### 1.2 Sequence Diagram

```
┌─────────┐     ┌─────────────┐     ┌─────────────────┐     ┌────────────────────┐     ┌────────────────────────┐
│  Unity  │     │ BootManager │     │  AppFlowManager │     │ FirebaseLoginHandler│     │  UserDatabaseManager   │
└────┬────┘     └──────┬──────┘     └────────┬────────┘     └──────────┬─────────┘     └───────────┬────────────┘
     │                 │                      │                        │                           │
     │  Scene Load     │                      │                        │                           │
     │────────────────>│                      │                        │                           │
     │                 │                      │                        │                           │
     │                 │  Initialize SDKs     │                        │                           │
     │                 │  (Firebase, Ads)     │                        │                           │
     │                 │──────────────────────│                        │                           │
     │                 │                      │                        │                           │
     │                 │  StartAuthFlow()     │                        │                           │
     │                 │─────────────────────>│                        │                           │
     │                 │                      │                        │                           │
     │                 │                      │  TryAutoLogin()        │                           │
     │                 │                      │───────────────────────>│                           │
     │                 │                      │                        │                           │
     │                 │                      │                        │  Check cached credentials │
     │                 │                      │                        │──────────────────────────>│
     │                 │                      │                        │                           │
     │                 │                      │                        │  Firebase Auth            │
     │                 │                      │                        │  ───────────────────────  │
     │                 │                      │                        │  (async)                  │
     │                 │                      │                        │                           │
     │                 │                      │  OnLoginSuccess(uid)   │                           │
     │                 │                      │<───────────────────────│                           │
     │                 │                      │                        │                           │
     │                 │                      │  Initialize(uid)       │                           │
     │                 │                      │───────────────────────────────────────────────────>│
     │                 │                      │                        │                           │
     │                 │                      │                        │           Fetch user doc  │
     │                 │                      │                        │           Load inventory  │
     │                 │                      │                        │           Load stats      │
     │                 │                      │                        │           Load energy     │
     │                 │                      │                        │                           │
     │                 │                      │  OnDataReady()         │                           │
     │                 │                      │<───────────────────────────────────────────────────│
     │                 │                      │                        │                           │
     │                 │  OnAppReady()        │                        │                           │
     │                 │<─────────────────────│                        │                           │
     │                 │                      │                        │                           │
     │                 │  Transition to       │                        │                           │
     │                 │  Main Menu           │                        │                           │
     │                 │──────────────────────│                        │                           │
     │                 │                      │                        │                           │
```

### 1.3 Components Involved

| Component | Role | File |
|-----------|------|------|
| BootManager | Entry point, SDK initialization | `/Managers/BootManager.cs` |
| AppFlowManager | Orchestrates auth and data loading | `/Managers/AppFlowManager.cs` |
| FirebaseLoginHandler | Firebase Authentication | `/Networks/FirebaseLoginHandler.cs` |
| UserDatabaseManager | User data loading | `/Networks/UserDatabaseManager.cs` |
| GameManager | Receives ready signal | `/Managers/GameManager.cs` |

### 1.4 Initialization Checklist

```
[ ] Firebase SDK initialized
[ ] Appodeal SDK initialized
[ ] Analytics initialized
[ ] Crashlytics initialized
[ ] Authentication complete (or guest mode)
[ ] User document loaded
[ ] Inventory loaded
[ ] Energy state loaded
[ ] Remote config fetched
[ ] Item catalog fetched
[ ] UI ready
```

### 1.5 Error Scenarios

| Error | Handling |
|-------|----------|
| No internet | Show offline mode prompt, retry button |
| Firebase init failed | Crash with error log |
| Auth failed | Show login screen |
| User doc not found | Create new user |
| Timeout | Retry with exponential backoff |

---

## 2. Authentication Flow

### 2.1 Login Options

```
┌──────────────────────────────────────────────────────┐
│                   Login Options                       │
├──────────────────────────────────────────────────────┤
│                                                      │
│   ┌─────────────┐    ┌─────────────┐                │
│   │   Email/    │    │   Google    │                │
│   │  Password   │    │   OAuth     │                │
│   └──────┬──────┘    └──────┬──────┘                │
│          │                  │                        │
│          └────────┬─────────┘                        │
│                   │                                  │
│                   ▼                                  │
│          ┌───────────────┐                          │
│          │ Firebase Auth │                          │
│          └───────┬───────┘                          │
│                  │                                  │
│                  ▼                                  │
│          ┌───────────────┐                          │
│          │   Get UID     │                          │
│          └───────┬───────┘                          │
│                  │                                  │
│                  ▼                                  │
│      ┌───────────────────────┐                      │
│      │ UserDatabaseManager   │                      │
│      │ Initialize(uid)       │                      │
│      └───────────────────────┘                      │
│                                                      │
└──────────────────────────────────────────────────────┘
```

### 2.2 New User Creation

```
FirebaseLoginHandler                    user.functions.ts                    Firestore
        │                                       │                                │
        │  New UID detected                     │                                │
        │───────────────────────────────────────│                                │
        │                                       │                                │
        │  httpsCallable("createUser")          │                                │
        │──────────────────────────────────────>│                                │
        │                                       │                                │
        │                                       │  Create /users/{uid}           │
        │                                       │───────────────────────────────>│
        │                                       │                                │
        │                                       │  Initialize default values:    │
        │                                       │  - profile (empty)             │
        │                                       │  - stats (zeros)               │
        │                                       │  - inventory (starter items)   │
        │                                       │  - energy (full)               │
        │                                       │  - streak (0)                  │
        │                                       │                                │
        │                                       │  Generate referral code        │
        │                                       │───────────────────────────────>│
        │                                       │                                │
        │  { success: true, isNewUser: true }   │                                │
        │<──────────────────────────────────────│                                │
        │                                       │                                │
        │  Show onboarding / name entry         │                                │
        │                                       │                                │
```

### 2.3 Token Refresh

Firebase handles token refresh automatically. Client-side:
- Token expires after 1 hour
- SDK auto-refreshes before expiry
- All `httpsCallable` requests include valid token

---

## 3. IAP Purchase Flow

> **CRITICAL**: This is the most revenue-sensitive flow. Changes require Level 0 approval.

### 3.1 Complete Purchase Sequence

```
┌────────────┐  ┌────────────┐  ┌──────────────────┐  ┌─────────────────┐  ┌────────────────┐  ┌────────────┐
│    User    │  │  UIShop    │  │   IAPManager     │  │ IAPRemoteService│  │ iap.functions  │  │ Store API  │
└─────┬──────┘  └─────┬──────┘  └────────┬─────────┘  └────────┬────────┘  └───────┬────────┘  └─────┬──────┘
      │               │                  │                     │                   │                 │
      │  Tap "Buy"    │                  │                     │                   │                 │
      │──────────────>│                  │                     │                   │                 │
      │               │                  │                     │                   │                 │
      │               │  BuyProduct(id)  │                     │                   │                 │
      │               │─────────────────>│                     │                   │                 │
      │               │                  │                     │                   │                 │
      │               │                  │  InitiatePurchase() │                   │                 │
      │               │                  │─────────────────────────────────────────────────────────>│
      │               │                  │                     │                   │                 │
      │               │                  │                     │                   │    Store UI     │
      │               │                  │                     │                   │    (native)     │
      │<──────────────────────────────────────────────────────────────────────────────────────────────│
      │               │                  │                     │                   │                 │
      │  Confirm      │                  │                     │                   │                 │
      │──────────────────────────────────────────────────────────────────────────────────────────────>│
      │               │                  │                     │                   │                 │
      │               │                  │  OnPurchaseComplete │                   │                 │
      │               │                  │  (receipt)          │                   │                 │
      │               │                  │<─────────────────────────────────────────────────────────│
      │               │                  │                     │                   │                 │
      │               │                  │  VerifyPurchase     │                   │                 │
      │               │                  │  (receipt, productId)                   │                 │
      │               │                  │────────────────────>│                   │                 │
      │               │                  │                     │                   │                 │
      │               │                  │                     │  verifyPurchase() │                 │
      │               │                  │                     │──────────────────>│                 │
      │               │                  │                     │                   │                 │
      │               │                  │                     │                   │  Validate with  │
      │               │                  │                     │                   │  Store API      │
      │               │                  │                     │                   │────────────────>│
      │               │                  │                     │                   │                 │
      │               │                  │                     │                   │  { valid: true }│
      │               │                  │                     │                   │<────────────────│
      │               │                  │                     │                   │                 │
      │               │                  │                     │                   │  Grant          │
      │               │                  │                     │                   │  entitlements   │
      │               │                  │                     │                   │  (Firestore)    │
      │               │                  │                     │                   │                 │
      │               │                  │                     │  { rewards }      │                 │
      │               │                  │                     │<──────────────────│                 │
      │               │                  │                     │                   │                 │
      │               │                  │  OnVerified(rewards)│                   │                 │
      │               │                  │<────────────────────│                   │                 │
      │               │                  │                     │                   │                 │
      │               │                  │  ConfirmPending     │                   │                 │
      │               │                  │  Purchase()         │                   │                 │
      │               │                  │─────────────────────────────────────────────────────────>│
      │               │                  │                     │                   │                 │
      │               │                  │  Update local state │                   │                 │
      │               │                  │  (currency, items)  │                   │                 │
      │               │                  │                     │                   │                 │
      │               │  OnPurchaseSuccess                     │                   │                 │
      │               │<─────────────────│                     │                   │                 │
      │               │                  │                     │                   │                 │
      │  Show reward  │                  │                     │                   │                 │
      │  animation    │                  │                     │                   │                 │
      │<──────────────│                  │                     │                   │                 │
      │               │                  │                     │                   │                 │
```

### 3.2 Critical Checkpoints

| Checkpoint | Location | Must Verify |
|------------|----------|-------------|
| Receipt received | `IAPManager.OnPurchaseComplete` | Receipt not null/empty |
| Server verification | `iap.functions.verifyPurchase` | Store API returns valid |
| Entitlements granted | `iap.functions.verifyPurchase` | Firestore write success |
| Purchase confirmed | `IAPManager.ConfirmPendingPurchase` | Store acknowledged |
| Local state updated | `IAPManager` | Currency/inventory synced |

### 3.3 Product Types & Handling

| Type | Products | Server Action |
|------|----------|---------------|
| Consumable | Diamonds (5-1000) | Add currency to user |
| Non-Consumable | Remove Ads | Set `removeAds: true` |
| Subscription | Elite Pass | Set `elitePass.isActive: true`, `expiresAt` |

### 3.4 Pending Purchase Recovery

On app restart, `IAPManager` checks for pending purchases:

```
App Start
    │
    ▼
IAPManager.Initialize()
    │
    ▼
Check pending transactions
    │
    ├── No pending → Continue normal flow
    │
    └── Has pending →
            │
            ▼
        VerifyPurchase(pending.receipt)
            │
            ├── Success → ConfirmPendingPurchase()
            │
            └── Failure → Log error, keep pending
```

### 3.5 Error Handling

| Error | Client Action | User Message |
|-------|---------------|--------------|
| Store unavailable | Retry later | "Store not available" |
| Purchase cancelled | No action | None |
| Receipt invalid | Log, don't confirm | "Purchase failed" |
| Server verify failed | Keep pending, retry | "Verifying purchase..." |
| Network error | Keep pending, retry | "Check connection" |

### 3.6 Security Considerations

- **NEVER** grant entitlements client-side
- **NEVER** log full receipts
- **ALWAYS** verify with Store API before granting
- **ALWAYS** use server timestamp for subscription expiry
- **NEVER** trust client-provided product IDs for pricing

---

## 4. Ad Reward Flow

### 4.1 Rewarded Video Sequence

```
┌────────────┐  ┌────────────┐  ┌────────────┐  ┌────────────────┐  ┌──────────────┐
│    User    │  │  UIAdPanel │  │ AdManager  │  │    Appodeal    │  │ ad.functions │
└─────┬──────┘  └─────┬──────┘  └─────┬──────┘  └───────┬────────┘  └──────┬───────┘
      │               │               │                 │                  │
      │  Tap "Watch"  │               │                 │                  │
      │──────────────>│               │                 │                  │
      │               │               │                 │                  │
      │               │  CheckAdReady │                 │                  │
      │               │──────────────>│                 │                  │
      │               │               │                 │                  │
      │               │               │  IsRewardedReady()                 │
      │               │               │────────────────>│                  │
      │               │               │                 │                  │
      │               │               │  true           │                  │
      │               │               │<────────────────│                  │
      │               │               │                 │                  │
      │               │  ShowRewardedVideo()            │                  │
      │               │──────────────>│                 │                  │
      │               │               │                 │                  │
      │               │               │  Show()         │                  │
      │               │               │────────────────>│                  │
      │               │               │                 │                  │
      │               │               │                 │  [Ad Plays]      │
      │<──────────────────────────────────────────────────────────────────>│
      │               │               │                 │                  │
      │               │               │  OnRewarded()   │                  │
      │               │               │<────────────────│                  │
      │               │               │                 │                  │
      │               │               │  ClaimAdReward(type)               │
      │               │               │───────────────────────────────────>│
      │               │               │                 │                  │
      │               │               │                 │   Check daily    │
      │               │               │                 │   limit          │
      │               │               │                 │   Grant reward   │
      │               │               │                 │                  │
      │               │               │  { reward }     │                  │
      │               │               │<───────────────────────────────────│
      │               │               │                 │                  │
      │               │               │  Update local   │                  │
      │               │               │  currency       │                  │
      │               │               │                 │                  │
      │               │  OnRewardGranted               │                  │
      │               │<──────────────│                 │                  │
      │               │               │                 │                  │
      │  Show reward  │               │                 │                  │
      │<──────────────│               │                 │                  │
      │               │               │                 │                  │
```

### 4.2 Ad Types

| Type | Trigger | Reward |
|------|---------|--------|
| Rewarded Video | User-initiated | Coins, Energy, Multiplier |
| Interstitial | After X sessions | None (monetization only) |

### 4.3 Daily Limits

```
ad.functions.ts checks:
- Daily rewarded ad count < MAX_DAILY_ADS
- Last ad timestamp + cooldown < now
- User not flagged for abuse
```

### 4.4 Fraud Prevention

- Server validates ad completion
- Rate limiting per user
- Suspicious pattern detection (too fast, too many)

---

## 5. Session & Gameplay Flow

### 5.1 Session Lifecycle

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           SESSION LIFECYCLE                                  │
└─────────────────────────────────────────────────────────────────────────────┘

    ┌─────────┐         ┌─────────┐         ┌─────────┐         ┌─────────┐
    │  IDLE   │────────>│ REQUEST │────────>│ ACTIVE  │────────>│  END    │
    └─────────┘         └─────────┘         └─────────┘         └─────────┘
         │                   │                   │                   │
         │                   │                   │                   │
    User taps          Server creates      Gameplay in         Session ends
    "Play"             session doc         progress            (win/lose/quit)
                                                                    │
                                                                    ▼
                                                              Submit results
                                                              to server
```

### 5.2 Request Session

```
UISessionGate                SessionRemoteService              session.functions.ts
      │                              │                                │
      │  RequestSession()            │                                │
      │─────────────────────────────>│                                │
      │                              │                                │
      │                              │  httpsCallable("requestSession")
      │                              │───────────────────────────────>│
      │                              │                                │
      │                              │                   Check energy │
      │                              │                   Deduct energy│
      │                              │                   Create session doc
      │                              │                                │
      │                              │  { sessionId, mapData }        │
      │                              │<───────────────────────────────│
      │                              │                                │
      │  OnSessionReady(data)        │                                │
      │<─────────────────────────────│                                │
      │                              │                                │
      │  Load map, start game        │                                │
      │                              │                                │
```

### 5.3 Submit Session Result

```
GameplayManager              SessionResultRemoteService          session.functions.ts
      │                              │                                │
      │  Player dies / wins          │                                │
      │                              │                                │
      │  SubmitResult(score,         │                                │
      │    coins, distance)          │                                │
      │─────────────────────────────>│                                │
      │                              │                                │
      │                              │  httpsCallable("submitResult") │
      │                              │───────────────────────────────>│
      │                              │                                │
      │                              │              Validate session  │
      │                              │              Calculate rewards │
      │                              │              Update user stats │
      │                              │              Check achievements│
      │                              │              Update leaderboard│
      │                              │                                │
      │                              │  { finalRewards, newRecords }  │
      │                              │<───────────────────────────────│
      │                              │                                │
      │  OnResultProcessed(data)     │                                │
      │<─────────────────────────────│                                │
      │                              │                                │
      │  Show level end screen       │                                │
      │  with rewards                │                                │
      │                              │                                │
```

### 5.4 Anti-Cheat Measures

| Measure | Location | Description |
|---------|----------|-------------|
| Session token | Server | Each session has unique ID |
| Time validation | Server | Session duration must be realistic |
| Score validation | Server | Score must match time/distance ratio |
| Duplicate check | Server | Same session can't submit twice |

---

## 6. User Data Sync Flow

### 6.1 Initial Load

```
UserDatabaseManager                      Firestore
        │                                    │
        │  Initialize(uid)                   │
        │                                    │
        │  Fetch /users/{uid}                │
        │───────────────────────────────────>│
        │                                    │
        │  { profile, stats, inventory... }  │
        │<───────────────────────────────────│
        │                                    │
        │  Fetch /users/{uid}/sessions       │
        │───────────────────────────────────>│
        │                                    │
        │  [session history]                 │
        │<───────────────────────────────────│
        │                                    │
        │  Cache locally                     │
        │  Notify listeners                  │
        │                                    │
```

### 6.2 Real-time Updates

UserDatabaseManager may use Firestore listeners for:
- Currency changes (from other devices)
- Inventory updates
- Energy regeneration sync

### 6.3 Conflict Resolution

| Conflict Type | Resolution |
|---------------|------------|
| Currency mismatch | Server wins (anti-cheat) |
| Inventory mismatch | Server wins |
| Settings mismatch | Last write wins |
| Progress mismatch | Higher value wins |

---

## 7. Energy System Flow

### 7.1 Energy Consumption

```
User taps "Play"
      │
      ▼
Check local energy >= required
      │
      ├── No  → Show "Not enough energy" + refill options
      │
      └── Yes →
              │
              ▼
        RequestSession() → Server deducts energy
              │
              ▼
        Update local energy
              │
              ▼
        Start gameplay
```

### 7.2 Energy Regeneration

```
energy.functions.ts (or client calculation)

lastRegenTime = user.energy.lastRegenTime
currentTime = now()
elapsed = currentTime - lastRegenTime

regenAmount = floor(elapsed / REGEN_INTERVAL)
newEnergy = min(user.energy.current + regenAmount, MAX_ENERGY)

if (regenAmount > 0):
    update user.energy.current = newEnergy
    update user.energy.lastRegenTime = lastRegenTime + (regenAmount * REGEN_INTERVAL)
```

### 7.3 Energy Refill Options

| Method | Cost | Result |
|--------|------|--------|
| Wait | Free | +1 per X minutes |
| Watch Ad | Free | +Y energy |
| Diamonds | Z diamonds | Full refill |
| Elite Pass | Subscription | Faster regen |

---

## 8. Shop Purchase Flow (In-Game Currency)

### 8.1 Purchase Item with Coins/Diamonds

```
UIShopPanel                    InventoryRemoteService              shop.functions.ts
      │                              │                                │
      │  PurchaseItem(itemId)        │                                │
      │─────────────────────────────>│                                │
      │                              │                                │
      │                              │  httpsCallable("purchaseItem") │
      │                              │───────────────────────────────>│
      │                              │                                │
      │                              │              Check user has    │
      │                              │              enough currency   │
      │                              │              Deduct currency   │
      │                              │              Add to inventory  │
      │                              │                                │
      │                              │  { success, newBalance }       │
      │                              │<───────────────────────────────│
      │                              │                                │
      │  OnPurchaseComplete          │                                │
      │<─────────────────────────────│                                │
      │                              │                                │
```

### 8.2 Equip Item

```
UIInventory                    InventoryRemoteService              shop.functions.ts
      │                              │                                │
      │  EquipItem(itemId)           │                                │
      │─────────────────────────────>│                                │
      │                              │                                │
      │                              │  httpsCallable("equipItem")    │
      │                              │───────────────────────────────>│
      │                              │                                │
      │                              │              Verify ownership  │
      │                              │              Update equipped[] │
      │                              │                                │
      │                              │  { success }                   │
      │                              │<───────────────────────────────│
      │                              │                                │
```

---

## 9. Leaderboard Flow

### 9.1 Score Submission

Automatic after each session:

```
session.functions.ts (submitSessionResult)
      │
      │  If score > user.highScore:
      │      Update user.highScore
      │      │
      │      ▼
      │  leaderboard.functions.ts
      │      │
      │      ▼
      │  Update /leaderboards/{period}/entries
      │  Recalculate ranks
```

### 9.2 Fetch Leaderboard

```
LeaderboardService                    leaderboard.functions.ts
      │                                       │
      │  GetLeaderboard(period, limit)        │
      │──────────────────────────────────────>│
      │                                       │
      │                       Query top N     │
      │                       Include user    │
      │                       rank if outside │
      │                                       │
      │  { entries[], userRank }              │
      │<──────────────────────────────────────│
      │                                       │
```

### 9.3 Leaderboard Periods

| Period | Reset | Prize Distribution |
|--------|-------|-------------------|
| Daily | 00:00 UTC | Morning |
| Weekly | Monday 00:00 UTC | Monday morning |
| All-Time | Never | - |

---

## 10. Achievement & Task Flow

### 10.1 Achievement Unlock

```
Client Event (e.g., PlayerCollision)          AchievementService            achievements.functions.ts
              │                                      │                              │
              │  OnCoinCollected()                   │                              │
              │─────────────────────────────────────>│                              │
              │                                      │                              │
              │                                      │  Check local progress        │
              │                                      │                              │
              │                                      │  If threshold reached:       │
              │                                      │  httpsCallable("unlock")     │
              │                                      │─────────────────────────────>│
              │                                      │                              │
              │                                      │             Mark unlocked    │
              │                                      │             Grant reward     │
              │                                      │                              │
              │                                      │  { reward }                  │
              │                                      │<─────────────────────────────│
              │                                      │                              │
              │                                      │  Show achievement popup      │
              │                                      │                              │
```

### 10.2 Daily Task Progress

```
session.functions.ts (submitSessionResult)
      │
      │  For each active task:
      │      Check if session contributes
      │      Update task progress
      │      │
      │      └── If complete: Mark claimable
```

### 10.3 Claim Task Reward

```
UITaskPanel                    TaskService                    tasks.functions.ts
      │                              │                              │
      │  ClaimReward(taskId)         │                              │
      │─────────────────────────────>│                              │
      │                              │                              │
      │                              │  httpsCallable("claimTask")  │
      │                              │─────────────────────────────>│
      │                              │                              │
      │                              │         Verify completed     │
      │                              │         Grant reward         │
      │                              │         Mark claimed         │
      │                              │                              │
      │                              │  { reward }                  │
      │                              │<─────────────────────────────│
      │                              │                              │
```

---

## 11. Streak System Flow

### 11.1 Daily Login Streak

```
App Start
    │
    ▼
Check last login date
    │
    ├── Same day → No action
    │
    ├── Yesterday → Increment streak
    │              │
    │              ▼
    │         streak.functions.ts
    │              │
    │              ▼
    │         Grant streak reward
    │
    └── Older → Reset streak to 1
               │
               ▼
          streak.functions.ts
               │
               ▼
          Grant day 1 reward
```

### 11.2 Streak Rewards

| Day | Reward |
|-----|--------|
| 1 | 100 coins |
| 2 | 150 coins |
| 3 | 200 coins |
| 4 | 250 coins |
| 5 | 300 coins |
| 6 | 350 coins |
| 7 | 500 coins + bonus |

---

## 12. Elite Pass Flow

### 12.1 Subscription Purchase

Uses IAP flow (Section 3) with subscription product type.

### 12.2 Benefits Application

```
UserDatabaseManager.Initialize()
    │
    ▼
Check elitePass.isActive && elitePass.expiresAt > now
    │
    ├── Active:
    │       Set ElitePassService.IsActive = true
    │       Apply benefits:
    │       - 2x coin multiplier
    │       - Faster energy regen
    │       - Exclusive items unlocked
    │       - No interstitial ads
    │
    └── Expired:
            Set ElitePassService.IsActive = false
            Remove benefits
```

### 12.3 Subscription Validation

On each app start and periodically:

```
ElitePassValidator                    iap.functions.ts                    Store API
      │                                      │                                │
      │  ValidateSubscription()              │                                │
      │─────────────────────────────────────>│                                │
      │                                      │                                │
      │                                      │  Check with Store API          │
      │                                      │───────────────────────────────>│
      │                                      │                                │
      │                                      │  { isActive, expiresAt }       │
      │                                      │<───────────────────────────────│
      │                                      │                                │
      │                                      │  Update Firestore              │
      │                                      │                                │
      │  { isActive, expiresAt }             │                                │
      │<─────────────────────────────────────│                                │
      │                                      │                                │
```

---

## 13. Referral System Flow

### 13.1 Generate Referral Code

```
user.functions.ts (createUser)
    │
    ▼
Generate unique code (e.g., "GROLL-XXXX")
    │
    ▼
Store in /referralKeys/{code} → { ownerUid }
    │
    ▼
Store in /users/{uid}/referralCode
```

### 13.2 Apply Referral Code

```
UIReferralPanel               ReferralRemoteService              user.functions.ts
      │                              │                                │
      │  ApplyCode("GROLL-XXXX")     │                                │
      │─────────────────────────────>│                                │
      │                              │                                │
      │                              │  httpsCallable("applyReferral")│
      │                              │───────────────────────────────>│
      │                              │                                │
      │                              │        Validate code exists    │
      │                              │        Check not self-referral │
      │                              │        Check not already used  │
      │                              │        Grant reward to BOTH    │
      │                              │        Mark code as used by    │
      │                              │                                │
      │                              │  { reward }                    │
      │                              │<───────────────────────────────│
      │                              │                                │
```

---

## 14. Map Loading Flow

### 14.1 Fetch Map Data

```
SessionRemoteService                 map.functions.ts                    Firestore
      │                                      │                                │
      │  RequestSession() includes           │                                │
      │  map selection                       │                                │
      │─────────────────────────────────────>│                                │
      │                                      │                                │
      │                                      │  Fetch /maps/{mapId}           │
      │                                      │───────────────────────────────>│
      │                                      │                                │
      │                                      │  { gridData, metadata }        │
      │                                      │<───────────────────────────────│
      │                                      │                                │
      │  { sessionId, mapData }              │                                │
      │<─────────────────────────────────────│                                │
      │                                      │                                │
```

### 14.2 Runtime Map Building

```
MapManager receives mapData
    │
    ▼
MapLoaderJsonAdapter.Parse(json)
    │
    ▼
For each cell in gridData:
    │
    ├── Instantiate prefab from pool
    │
    ├── Position on grid
    │
    └── Configure properties (rotation, triggers)
    │
    ▼
Map ready for gameplay
```

---

## 15. Error Handling Patterns

### 15.1 Network Error Pattern

```csharp
async UniTask<T> CallWithRetry<T>(Func<UniTask<T>> call, int maxRetries = 3)
{
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            return await call();
        }
        catch (NetworkException)
        {
            if (i == maxRetries - 1) throw;
            await UniTask.Delay(TimeSpan.FromSeconds(Math.Pow(2, i)));
        }
    }
}
```

### 15.2 Graceful Degradation

| Scenario | Fallback |
|----------|----------|
| Firestore down | Use cached data |
| Functions timeout | Retry with backoff |
| Ads not loading | Hide ad buttons |
| IAP store unavailable | Hide IAP shop |

### 15.3 User Feedback

| Error Type | User Message |
|------------|--------------|
| Network | "Check your connection" |
| Server | "Something went wrong. Try again." |
| Auth | "Please log in again" |
| Purchase | "Purchase failed. Not charged." |

---

## Appendix: Flow Reference Quick Links

| Flow | Section | Risk Level |
|------|---------|------------|
| App Startup | [1](#1-app-startup-flow) | Medium |
| Authentication | [2](#2-authentication-flow) | High |
| IAP Purchase | [3](#3-iap-purchase-flow) | CRITICAL |
| Ad Reward | [4](#4-ad-reward-flow) | High |
| Session | [5](#5-session--gameplay-flow) | Medium |
| Data Sync | [6](#6-user-data-sync-flow) | High |
| Energy | [7](#7-energy-system-flow) | Medium |
| Shop | [8](#8-shop-purchase-flow) | Medium |
| Leaderboard | [9](#9-leaderboard-flow) | Low |
| Achievements | [10](#10-achievement--task-flow) | Low |
| Streak | [11](#11-streak-system-flow) | Low |
| Elite Pass | [12](#12-elite-pass-flow) | High |
| Referral | [13](#13-referral-system-flow) | Low |
| Map Loading | [14](#14-map-loading-flow) | Low |

---

*End of Document*
