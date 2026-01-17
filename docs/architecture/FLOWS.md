# G-Roll System Flows

**Version:** 2.0
**Last Updated:** 2026-01-14
**Corrected By:** Developer (Çağıl) Feedback

**⚠️ NOTE:** This file contains flows based on code analysis. Critical corrections from developer feedback have been noted inline. See commit history for detailed changes.

**Related**: [COMPONENTS.md](./COMPONENTS.md), [CRITICAL_SURFACES.md](../qa/CRITICAL_SURFACES.md)

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

### 2.2 New User Creation (Two-Stage Process)

**⚠️ DEVELOPER NOTE (Çağıl):** User creation is a **two-step process**:
1. **createUser** - Creates base document (without nickname/profile)
2. **completeUserProfile** - Finalizes profile after user inputs nickname and optional referral code

```
FirebaseLoginHandler          user.functions.ts (createUser)        Firestore
        │                              │                                │
        │  New UID detected            │                                │
        │──────────────────────────────│                                │
        │                              │                                │
        │  httpsCallable("createUser") │                                │
        │─────────────────────────────>│                                │
        │                              │                                │
        │                              │  Create /users/{uid}           │
        │                              │  (BASE DOCUMENT ONLY)          │
        │                              │───────────────────────────────>│
        │                              │                                │
        │                              │  Initialize:                   │
        │                              │  - uid                         │
        │                              │  - createdAt                   │
        │                              │  - stats (zeros)               │
        │                              │  - inventory (starter items)   │
        │                              │  - energy (full)               │
        │                              │  - streak (0)                  │
        │                              │  - profile: INCOMPLETE         │
        │                              │                                │
        │  { success, isNewUser }      │                                │
        │<─────────────────────────────│                                │
        │                              │                                │
        │  Show UI: Nickname entry     │                                │
        │  + optional referral code    │                                │
        │                              │                                │
        │  User enters nickname        │                                │
        │                              │                                │
        │  httpsCallable               │                                │
        │  ("completeUserProfile")     │                                │
        │─────────────────────────────────────────────────────────────>│
        │                              │                                │
        │                              user.functions.ts                │
        │                              (completeUserProfile)            │
        │                              │                                │
        │                              │  Validate nickname unique      │
        │                              │  Generate referral code        │
        │                              │  Update /users/{uid}:          │
        │                              │  - nickname                    │
        │                              │  - referralCode                │
        │                              │  - profileComplete: true       │
        │                              │───────────────────────────────>│
        │                              │                                │
        │  { success, referralCode }   │                                │
        │<─────────────────────────────────────────────────────────────│
        │                              │                                │
        │  Proceed to main menu        │                                │
        │                              │                                │
```

**Cloud Functions Involved:**
- `createUser` (user.functions.ts) - Creates base document
- `completeUserProfile` (user.functions.ts) - Finalizes profile with nickname and referral code

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

**⚠️ DEVELOPER NOTE (Çağıl):** "Transfaktör" feature status unclear - may have been removed. If still active, needs to be added to this flow. **TODO:** Code review required to confirm.

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

### 5.4 Anti-Cheat Validation System

**⚠️ DEVELOPER NOTE (Çağıl):** Anti-cheat includes pen validation, score validation, duplicate check, session token, and additional checks like coin count validation.

#### Minimum Validation Set (Game-Breaking if Removed)

These validations are **CRITICAL**. If any is removed, the game becomes exploitable:

| Validation | Location | What It Prevents | If Broken |
|------------|----------|------------------|-----------|
| **Session Token** | `session.functions.ts` | Replay attacks, forged sessions | Cheaters can submit fake sessions without playing |
| **Duplicate Check** | `session.functions.ts` | Same sessionId submitted twice | Cheaters can replay one good session infinitely |
| **Pen Validation** | `session.functions.ts` | Forged difficulty/map data | Cheaters can claim rewards for easy maps as hard maps |

**Impact:** If these fail → **Revenue loss, leaderboard fraud, currency inflation**

#### Additional Validations (Heuristics, Not Blockers)

These detect suspicious behavior but don't block submissions:

| Validation | Location | What It Checks | Action on Failure |
|------------|----------|----------------|-------------------|
| **Time Validation** | `session.functions.ts` | Session duration realistic (not 1 second for 1000m) | Flag user, log anomaly |
| **Score Validation** | `session.functions.ts` | Score matches time/distance ratio | Cap rewards at reasonable max |
| **Coin Count Validation** | `session.functions.ts` | Collected coins <= max possible for map | Cap at map maximum |

**Impact:** If these fail → **Suspicious activity logged, rewards capped, but game still playable**

---

## 6. User Data Sync Flow

### 6.1 Initial Load (UID-Based State Hydration)

**⚠️ DEVELOPER NOTE (Çağıl):** UserDatabaseManager does **NOT** fetch session history. It only fetches the main user document. Session history is managed separately by Cloud Functions.

```
UserDatabaseManager                      Firestore
        │                                    │
        │  Initialize(uid)                   │
        │                                    │
        │  Fetch /users/{uid}                │
        │  (SINGLE DOCUMENT)                 │
        │───────────────────────────────────>│
        │                                    │
        │  { profile, stats, inventory,      │
        │    energy, coins, diamonds,        │
        │    equippedItemIds, statsJson }    │
        │<───────────────────────────────────│
        │                                    │
        │  Hydrate local state:              │
        │  - CurrencyManager.SetCoins()      │
        │  - InventoryManager.LoadItems()    │
        │  - StatsManager.LoadStats()        │
        │  - EnergyManager.SetEnergy()       │
        │                                    │
        │  Notify listeners (data ready)     │
        │                                    │
```

**State Hydration Flow:**
1. **UID obtained** from Firebase Auth
2. **Single Firestore fetch** to `/users/{uid}`
3. **Local managers hydrate** from document fields
4. **Cloud Functions handle** all server-side logic (session creation, result processing, achievements, etc.)

**What is NOT fetched on startup:**
- ❌ Session history (only fetched by Cloud Functions during result submission)
- ❌ Achievement progress details (fetched on-demand when UI opens)
- ❌ Leaderboard data (fetched on-demand)
- ❌ Task history (fetched on-demand)

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

**⚠️ DEVELOPER NOTE (Çağıl):** Elite Pass does **NOT** provide faster energy regen.

| Method | Cost | Result |
|--------|------|--------|
| Wait | Free | +1 per X minutes (natural regen) |
| Watch Ad | Free | +Y energy (instant) |
| Diamonds | Z diamonds | Full refill (instant) |

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

### 8.2 Equip/Unequip Item (⚠️ HIGH-RISK: Stat Corruption)

**⚠️ DEVELOPER NOTE (Çağıl):** Equip/Unequip modifies user stats. This area **frequently breaks** due to stat recomputation errors. **Stats must be recalculated** on EVERY equip/unequip operation.

```
UIInventory                    InventoryRemoteService              shop.functions.ts
      │                              │                                │
      │  EquipItem(itemId)           │                                │
      │─────────────────────────────>│                                │
      │                              │                                │
      │                              │  httpsCallable("equipItem")    │
      │                              │───────────────────────────────>│
      │                              │                                │
      │                              │  ┌──────────────────────────┐ │
      │                              │  │ CRITICAL STEPS:          │ │
      │                              │  │ 1. Verify ownership      │ │
      │                              │  │ 2. Add to equipped[]     │ │
      │                              │  │ 3. **RECOMPUTE STATS**   │ │
      │                              │  │    (base + all equipped) │ │
      │                              │  │ 4. Save statsJson        │ │
      │                              │  └──────────────────────────┘ │
      │                              │                                │
      │                              │  { success, newStatsJson }     │
      │                              │<───────────────────────────────│
      │                              │                                │
      │  Update local stats          │                                │
      │<─────────────────────────────│                                │
      │                              │                                │
```

**Unequip Flow:**
```
UIInventory → httpsCallable("unequipItem")
              │
              ├── Remove from equipped[]
              ├── **RECOMPUTE STATS** (base + remaining equipped items)
              └── Save statsJson
```

**⚠️ COMMON BUG:**
Forgetting to recalculate stats after equip/unequip results in:
- Stats added/removed multiple times (double bonuses)
- Stats not removed when unequipping
- StatsJson out of sync with equippedItemIds

**Stat Recomputation Logic:**
```typescript
// CORRECT approach
const baseStats = getBaseStats(userId);
const equippedItems = await getEquippedItems(userId);
const totalStats = baseStats;

for (const item of equippedItems) {
  totalStats.speed += item.stats.speed;
  totalStats.jump += item.stats.jump;
  // ... apply all stat bonuses
}

await updateUser(userId, { statsJson: JSON.stringify(totalStats) });
```

**Risk Level:** 🟠 HIGH - Stat corruption breaks gameplay

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

### 9.2 Fetch Leaderboard (SeasonID-Based)

**⚠️ DEVELOPER NOTE (Çağıl):** Leaderboard does **NOT** use daily/weekly periods. Instead, it uses **SeasonID** parameter + **all-time** leaderboard.

```
LeaderboardService                    leaderboard.functions.ts
      │                                       │
      │  GetLeaderboard(seasonId, limit)      │
      │  (seasonId: "season-2024-q1" or       │
      │   "all-time")                         │
      │──────────────────────────────────────>│
      │                                       │
      │                       Query top N for │
      │                       specified season│
      │                       Include user    │
      │                       rank if outside │
      │                       top N           │
      │                                       │
      │  { entries[], userRank, seasonId }    │
      │<──────────────────────────────────────│
      │                                       │
```

### 9.3 Leaderboard Types

**⚠️ NO daily/weekly periods**. Only season-based and all-time:

| Type | Parameter | Reset | Prize Distribution |
|------|-----------|-------|-------------------|
| **Seasonal** | `seasonId` (e.g., "season-2024-q1") | When new season starts | End of season |
| **All-Time** | `"all-time"` | Never | - |

**Example SeasonIDs:**
- `"season-2024-q1"` - Q1 2024 season
- `"season-2024-q2"` - Q2 2024 season
- `"all-time"` - Cumulative leaderboard

**Firestore Structure:**
```
/leaderboards/{seasonId}/entries/{uid}
  - score: number
  - rank: number
  - username: string
  - timestamp: Timestamp
```

---

## 10. Achievement & Daily Task Flow

**⚠️ DEVELOPER NOTE (Çağıl):**
- **NO local progress tracking** for coin collection or other in-game events
- **ALL progress updates** happen server-side in `submitSessionResult` (Cloud Functions)
- **Client UI only shows claimable state** (fetched from Firestore)
- **Achievements** and **Daily Tasks** are separate systems but follow similar 3-stage flow

---

### 10.1 Three-Stage Flow (Achievements & Tasks)

Both Achievements and Daily Tasks follow this pattern:

#### Stage 1: Progress Update (Server-Side ONLY)

```
GameplayManager              session.functions.ts (submitSessionResult)      Firestore
      │                                  │                                      │
      │  Player completes session        │                                      │
      │  (collected 100 coins, etc.)     │                                      │
      │──────────────────────────────────│                                      │
      │                                  │                                      │
      │                                  │  FOR EACH ACHIEVEMENT/TASK:          │
      │                                  │  - Check session data contributes    │
      │                                  │  - Update progress counter           │
      │                                  │  - If threshold reached:             │
      │                                  │    Set claimable = true              │
      │                                  │───────────────────────────────────────>│
      │                                  │                                      │
      │                                  │  Progress saved                       │
      │                                  │<──────────────────────────────────────│
      │                                  │                                      │
```

**Key Point:** Client does **NOT** track "I collected 50 coins this session". Cloud Functions calculate progress from submitted session data.

---

#### Stage 2: Claimable State Check (Client Fetch)

```
UIAchievementPanel / UITaskPanel        Firestore                AchievementService / TaskService
         │                                  │                                │
         │  User opens Achievements/Tasks   │                                │
         │  panel                           │                                │
         │──────────────────────────────────────────────────────────────────>│
         │                                  │                                │
         │                                  │  Fetch achievements/tasks      │
         │                                  │  where claimable = true        │
         │                                  │<───────────────────────────────│
         │                                  │                                │
         │                                  │  { claimableItems[] }          │
         │                                  │───────────────────────────────>│
         │                                  │                                │
         │  Display "Claim" buttons         │                                │
         │<──────────────────────────────────────────────────────────────────│
         │                                  │                                │
```

**UI shows:** "Collect 1000 coins: ✅ Claim Reward"

---

#### Stage 3: Claim Action (Server Validation)

```
UIAchievementPanel               AchievementService/TaskService      achievements.functions.ts / tasks.functions.ts
      │                                  │                                      │
      │  User taps "Claim"               │                                      │
      │─────────────────────────────────>│                                      │
      │                                  │                                      │
      │                                  │  httpsCallable("claimAchievement")   │
      │                                  │  or ("claimTask")                    │
      │                                  │─────────────────────────────────────>│
      │                                  │                                      │
      │                                  │         SERVER RE-VALIDATES:         │
      │                                  │         - Achievement unlocked?      │
      │                                  │         - Already claimed?           │
      │                                  │         - Grant reward (coins/items) │
      │                                  │         - Mark claimed = true        │
      │                                  │                                      │
      │                                  │  { reward, newBalance }              │
      │                                  │<─────────────────────────────────────│
      │                                  │                                      │
      │  Show reward popup               │                                      │
      │<─────────────────────────────────│                                      │
      │                                  │                                      │
```

---

### 10.2 Achievements vs Daily Tasks (Key Differences)

| Aspect | Achievements | Daily Tasks |
|--------|-------------|-------------|
| **Reset** | Never (permanent) | Daily at 00:00 UTC |
| **Progress** | Cumulative across all sessions | Resets daily |
| **Claimable** | Once per achievement | Once per day per task |
| **Firestore Collection** | `/users/{uid}/achievements` | `/users/{uid}/tasks` |
| **Cloud Function** | `claimAchievement` | `claimTask` |

**Example:**
- **Achievement:** "Collect 10,000 coins total" → Progress accumulates forever
- **Daily Task:** "Collect 500 coins today" → Progress resets at midnight

---

### 10.3 Anti-Cheat Note

**Why server-side progress tracking?**
- Client cannot fake "collected 1000 coins" without submitting a valid session
- All rewards gated behind server validation
- Progress calculated from `submitSessionResult` data (server verifies session legitimacy)

---

## 11. Streak System Flow

### 11.1 Daily Login Streak

**⚠️ SOURCE OF TRUTH:** This flow is based on code analysis. Developer feedback suggests behavior may differ. **TODO:** Verify actual implementation in `streak.functions.ts`.

**Expected Behavior:**

```
App Start (streak.functions.ts or client check)
    │
    ▼
Check lastLoginDate vs today
    │
    ├── Same day (today) → No action (already logged in today)
    │
    ├── Yesterday → Increment streak
    │              │
    │              ├── streak = streak + 1
    │              │
    │              ▼
    │         Grant streak reward (day N)
    │         Update lastLoginDate = today
    │
    └── Older than yesterday → Reset OR Continue?
               │
               ├── OPTION A (Reset): streak = 1, grant day 1 reward
               │
               └── OPTION B (Continue): streak = streak + 1 (developer says no reset, just increment)
                   │
                   ▼
               Update lastLoginDate = today
               Grant reward for current streak day
```

**Clarification Needed:**
- **Does streak reset** if user skips 2+ days, or does it just continue incrementing?
- Developer (Çağıl) mentioned: "her yeni günde +1" (every new day +1), suggesting **no reset**, but this needs code verification.

**Streak Reward Logic:**
- Each login grants reward based on current streak day
- If streak exceeds 7 days, does it loop back to day 1 rewards or continue with day 7 rewards?

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

### 12.2 Benefits Application (Corrected)

**⚠️ DEVELOPER NOTE (Çağıl):** Previous documentation had incorrect benefits. Below are the **actual** Elite Pass benefits.

```
UserDatabaseManager.Initialize()
    │
    ▼
Check elitePass.isActive && elitePass.expiresAt > now
    │
    ├── Active:
    │       Set ElitePassService.IsActive = true
    │       Apply benefits (see table below)
    │
    └── Expired:
            Set ElitePassService.IsActive = false
            Remove benefits
```

**Actual Elite Pass Benefits:**

| Benefit | Description | Implementation Notes |
|---------|-------------|---------------------|
| **Remove Ads** | Removes rewarded video ads (NOT interstitial) | Does this disable ALL rewarded ads or just certain types? **TODO:** Clarify scope. |
| **Exclusive Item Grant (Rent)** | Grants time-limited exclusive items | Uses `acquisitionType: "rent"` with `expiryDate`. If user already owns item, does **NOT** grant duplicate. |
| **Double Life Slot** | Player gets 2 lives instead of 1 | Gameplay mechanic change, not energy-related. |
| **2x Coin Multiplier (Conditional)** | Applies in certain systems (e.g., Autopilot) | **NOT** a universal 2x multiplier. Only specific game modes/features. **TODO:** Document exactly where it applies. |

**What Elite Pass Does NOT Include:**
- ❌ **NO faster energy regeneration**
- ❌ **NO interstitial ad removal** (only rewarded ads affected)
- ❌ **NO permanent items** (exclusive items are rentals with expiry)

---

**Item Grant Logic (Rent Acquisition):**

```typescript
// Elite Pass item grant (Cloud Functions)
if (user.elitePass.isActive) {
  const exclusiveItems = getElitePassItems();

  for (const item of exclusiveItems) {
    const alreadyOwned = user.inventory.includes(item.id);

    if (!alreadyOwned) {
      grantItem(userId, item.id, {
        acquisitionType: "rent",
        expiryDate: user.elitePass.expiresAt
      });
    }
    // If already owned, skip (no duplicate)
  }
}
```

**When Elite Pass Expires:**
- Rented items removed from inventory
- 2x multiplier disabled
- Double life slot reverts to single life
- Rewarded ads re-enabled (if they were disabled)

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

**⚠️ DEVELOPER NOTE (Çağıl):** Error messages are primarily for **developer debugging** (console logs), NOT always shown to players.

| Scenario | Fallback Behavior | Player-Facing UI |
|----------|-------------------|------------------|
| Firestore down | Use cached data | None (silent fallback) |
| Functions timeout | Retry with backoff | Loading spinner continues |
| Ads not loading | Hide ad buttons | Buttons disappear |
| IAP store unavailable | **Block/wait** (does NOT hide shop) | "Store unavailable, please wait" or retry prompt |

**IAP Unavailable Behavior:**
- **NOT** hiding the shop as previously documented
- Instead: **Blocks user flow** or shows "Store not ready" message
- Waits for store initialization before allowing purchases

---

### 15.3 Error Feedback (Two Layers)

**⚠️ DEVELOPER NOTE (Çağıl):** Most errors go to debug console, NOT to player UI.

#### Layer 1: Developer Debug Logs (Console/Crashlytics)

All errors logged for developer troubleshooting:

```csharp
Debug.LogError($"[NetworkService] Failed to fetch user data: {ex.Message}");
FirebaseCrashlytics.RecordException(ex);
```

**Examples:**
- `"[IAP] Store initialization timeout (30s)"`
- `"[Firestore] Connection refused: Check your connection"`
- `"[SessionService] submitSessionResult failed: Invalid session token"`

---

#### Layer 2: Player-Facing UI (When Necessary)

Only **critical blockers** shown to player:

| Error Type | When Shown to Player | Message |
|------------|----------------------|---------|
| **Network** | Cannot proceed (e.g., login, session start) | "Check your connection" |
| **Server** | Critical failure after retries | "Something went wrong. Try again." |
| **Auth** | Login failure | "Please log in again" |
| **Purchase** | IAP failed | "Purchase failed. Not charged." |

**Examples of NO player-facing UI:**
- ❌ Leaderboard fetch fails → No error popup, just empty leaderboard
- ❌ Ad fails to load → Ad button disappears silently
- ❌ Achievement fetch fails → Empty achievement list, no error

**Player Should Only See Errors When:**
- Game cannot continue (e.g., session cannot start)
- Purchase flow fails (must inform about payment status)
- Login/authentication required

---

## Appendix: Flow Reference Quick Links

**⚠️ DEVELOPER NOTE (Çağıl):** Risk levels revised based on production impact. "If broken, what happens?"

| Flow | Section | Risk Level | If Broken, Result |
|------|---------|------------|-------------------|
| **App Startup** | [1](#1-app-startup-flow) | 🔴 **CRITICAL** | Game won't launch - users cannot play |
| **Authentication** | [2](#2-authentication-flow) | 🔴 **CRITICAL** | Users can't access accounts - total lockout |
| **IAP Purchase** | [3](#3-iap-purchase-flow) | 🔴 **CRITICAL** | Revenue fraud / chargebacks / revenue loss |
| **Map Loading** | [14](#14-map-loading-flow) | 🔴 **CRITICAL++** | Game unplayable - maps won't load |
| **Data Sync** | [6](#6-user-data-sync-flow) | 🔴 **CRITICAL** | Progress loss - user data corrupted |
| **Energy** | [7](#7-energy-system-flow) | 🟠 **VERY HIGH** | Gameplay gating broken - infinite plays or no plays |
| **Shop** | [8](#8-shop-purchase-flow) | 🟠 **HIGH** | Currency exploits - economy broken |
| **Elite Pass** | [12](#12-elite-pass-flow) | 🟠 **HIGH** | Subscription benefits not applied - refunds |
| **Ad Reward** | [4](#4-ad-reward-flow) | 🟠 **HIGH** | Revenue loss (no ad monetization) |
| **Session** | [5](#5-session--gameplay-flow) | 🟡 **MEDIUM** | Gameplay broken but game launches |
| **Leaderboard** | [9](#9-leaderboard-flow) | 🟡 **MEDIUM** | Rankings wrong - competitive integrity lost |
| **Achievements** | [10](#10-achievement--task-flow) | 🟢 **LOW** | Progress not tracked - annoying but not blocking |
| **Streak** | [11](#11-streak-system-flow) | 🟢 **LOW** | Daily rewards broken - minor inconvenience |
| **Referral** | [13](#13-referral-system-flow) | 🟢 **LOW** | Viral loop broken - growth impacted but not critical |

---

**Risk Level Definitions:**

- 🔴 **CRITICAL** - Game unplayable, users locked out, or revenue fraud
- 🔴 **CRITICAL++** - Even worse than CRITICAL (e.g., map loading = cannot play AT ALL)
- 🟠 **VERY HIGH** - Major gameplay/economy systems broken
- 🟠 **HIGH** - Important features broken, user experience severely degraded
- 🟡 **MEDIUM** - Feature broken but game still playable
- 🟢 **LOW** - Minor feature broken, minimal user impact

---

*End of Document*
