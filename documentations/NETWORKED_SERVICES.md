# Networked Services - Firebase Integration

**Version:** 1.0
**Date:** January 2026

---

## Overview

G-Roll uses Firebase as its backend, with a clean separation between client-side domain logic and server communication. This document explains how networked services are structured without diving into implementation details.

---

## Architecture Layers

```
┌─────────────────────────────────────────────────────────────┐
│                      DOMAIN LAYER                           │
│  Business logic, optimistic updates, local state            │
│                                                             │
│  SessionService, AchievementService, LeaderboardService     │
└───────────────────────────┬─────────────────────────────────┘
                            │
                            │ Uses interface
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                   INFRASTRUCTURE LAYER                       │
│  Remote service implementations                              │
│                                                             │
│  SessionRemoteService, AchievementRemoteService, etc.       │
└───────────────────────────┬─────────────────────────────────┘
                            │
                            │ Calls Firebase
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                    FIREBASE GATEWAY                          │
│  Central Firebase access point                               │
│                                                             │
│  Authentication, Firestore, Cloud Functions                 │
└─────────────────────────────────────────────────────────────┘
```

---

## Firebase Gateway

The `FirebaseGateway` is the single entry point for all Firebase operations. It provides:

- **Authentication State**: Current user, login status
- **Firestore Access**: Database read/write operations
- **Cloud Functions**: Server-side logic execution
- **Error Handling**: Standardized error responses

All remote services use the gateway rather than accessing Firebase directly.

---

## Remote Services

Each domain concept has a corresponding remote service that handles server communication.

### Session Remote Service

**Purpose:** Manage game session lifecycle with the server.

| Operation | Description |
|-----------|-------------|
| Request Session | Get permission to start a game (checks energy) |
| Submit Result | Send session outcome (score, distance, coins) |
| Validate Session | Verify session token is valid |

**Flow:**
```
User taps Play → SessionRemoteService.RequestSession()
                       ↓
              Server checks energy
                       ↓
              Returns session token
                       ↓
              Game starts with valid token
```

---

### Achievement Remote Service

**Purpose:** Sync achievement progress and claims with server.

| Operation | Description |
|-----------|-------------|
| Fetch Definitions | Get all achievement metadata |
| Fetch Progress | Get user's current achievement states |
| Sync Progress | Update progress for achievements |
| Claim Reward | Mark achievement level as claimed |

**Data Structure:**
```
Achievement Definition
├── ID
├── Title
├── Description
├── Icon URL
└── Levels[]
    ├── Target value
    └── Reward amount

Achievement State (per user)
├── Current progress
├── Claimed levels[]
└── Last updated timestamp
```

---

### Leaderboard Remote Service

**Purpose:** Submit scores and retrieve rankings.

| Operation | Description |
|-----------|-------------|
| Submit Score | Send new high score |
| Fetch Weekly | Get weekly rankings |
| Fetch All-Time | Get all-time rankings |
| Fetch User Rank | Get current user's position |

**Leaderboard Types:**
- **Weekly**: Resets every Monday
- **All-Time**: Permanent rankings

---

### Player Stats Remote Service

**Purpose:** Store and retrieve player statistics.

| Operation | Description |
|-----------|-------------|
| Fetch Stats | Get all player statistics |
| Update Stat | Modify a specific stat value |
| Sync All | Full sync of all stats |

**Stats Tracked:**
- Total games played
- Best score
- Total coins collected
- Total distance traveled
- Play time

---

### Referral Remote Service

**Purpose:** Handle referral code system.

| Operation | Description |
|-----------|-------------|
| Generate Code | Create unique referral code for user |
| Apply Code | Use someone else's referral code |
| Fetch Earnings | Get pending referral rewards |
| Claim Earnings | Collect referral rewards |

**Referral Flow:**
```
User A generates code → User B applies code → Both get rewards
```

---

### Streak Remote Service

**Purpose:** Track daily login streaks.

| Operation | Description |
|-----------|-------------|
| Check Streak | Verify current streak status |
| Claim Day | Mark today as played |
| Fetch History | Get streak history |

**Streak Logic:**
- Playing each day extends streak
- Missing a day resets streak
- Milestones give bonus rewards

---

### IAP Remote Service

**Purpose:** Verify in-app purchases with server.

| Operation | Description |
|-----------|-------------|
| Verify Purchase | Validate receipt with server |
| Restore Purchases | Check for previously bought items |
| Fetch Products | Get available products and prices |

**Verification Flow:**
```
User buys item → Platform receipt → Server verification → Grant item
```

---

### Elite Pass Remote Service

**Purpose:** Manage premium subscription status.

| Operation | Description |
|-----------|-------------|
| Check Status | Verify if user has active subscription |
| Activate | Enable premium after purchase |
| Fetch Benefits | Get list of premium perks |

**Elite Benefits:**
- No ads
- Bonus multipliers
- Exclusive items
- Priority features

---

### Autopilot Remote Service

**Purpose:** Handle idle earnings system.

| Operation | Description |
|-----------|-------------|
| Start Session | Begin autopilot earning |
| Check Status | Get current autopilot progress |
| Claim Earnings | Collect accumulated rewards |

**Autopilot Flow:**
```
Start → Timer runs → Earnings accumulate → Claim when ready
```

---

### Currency Remote Service

**Purpose:** Manage currency transactions.

| Operation | Description |
|-----------|-------------|
| Fetch Balances | Get all currency amounts |
| Add Currency | Grant currency (rewards, purchases) |
| Spend Currency | Deduct currency (purchases) |
| Transfer | Move between currency types |

**Currency Types:**
- Coins (primary, earnable)
- Gems (premium, purchasable)
- Tokens (special, limited)

---

### Inventory Remote Service

**Purpose:** Manage user's item collection.

| Operation | Description |
|-----------|-------------|
| Fetch Inventory | Get all owned items |
| Add Item | Grant new item to user |
| Remove Item | Take item from user |
| Equip Item | Set item as active |
| Unequip Item | Remove item from active |

**Item Categories:**
- Skins
- Effects
- Boosters
- Consumables

---

### Energy Remote Service

**Purpose:** Manage play energy system.

| Operation | Description |
|-----------|-------------|
| Fetch Energy | Get current energy and timer |
| Consume Energy | Use one energy to play |
| Add Energy | Grant energy (ads, purchases) |
| Check Timer | Get time until next energy |

**Energy Rules:**
- Maximum capacity (e.g., 6)
- Regeneration time (e.g., 30 minutes each)
- Can exceed max via purchases/ads

---

### Task Remote Service

**Purpose:** Manage daily/weekly tasks.

| Operation | Description |
|-----------|-------------|
| Fetch Tasks | Get active tasks and progress |
| Update Progress | Increment task completion |
| Claim Reward | Collect completed task reward |
| Refresh Tasks | Get new task set |

**Task Types:**
- Daily (reset every day)
- Weekly (reset every week)
- Special (event-based)

---

## Data Flow Patterns

### Read Pattern (Fetch)

```
┌──────────────┐    ┌──────────────┐    ┌──────────────┐
│ Domain       │    │ Remote       │    │ Firebase     │
│ Service      │───►│ Service      │───►│ Gateway      │
│              │    │              │    │              │
│ GetData()    │    │ FetchAsync() │    │ Firestore    │
└──────────────┘    └──────────────┘    └──────────────┘
       ▲                                       │
       │                                       │
       └───────────────────────────────────────┘
                    Returns data
```

### Write Pattern (Update)

```
┌──────────────┐    ┌──────────────┐    ┌──────────────┐
│ Domain       │    │ Remote       │    │ Firebase     │
│ Service      │───►│ Service      │───►│ Gateway      │
│              │    │              │    │              │
│ 1. Snapshot  │    │ UpdateAsync()│    │ Firestore    │
│ 2. Optimistic│    │              │    │              │
│ 3. Request   │    │              │    │              │
└──────────────┘    └──────────────┘    └──────────────┘
       ▲                                       │
       │        Success/Failure                │
       └───────────────────────────────────────┘
```

### Batched Write Pattern

```
Frequent Events → Queue in Domain Service → Batch Timer
                                               ↓
                                    Single Remote Request
                                               ↓
                                    Server processes batch
```

---

## Error Handling

### Error Categories

| Category | Description | Handling |
|----------|-------------|----------|
| Network | Connection lost | Retry with backoff |
| Auth | Session expired | Re-authenticate |
| Validation | Invalid data | Show user message |
| Server | Backend error | Retry, then fail gracefully |
| Rate Limit | Too many requests | Wait and retry |

### Retry Strategy

```
Attempt 1 → Fail → Wait 1s
Attempt 2 → Fail → Wait 2s
Attempt 3 → Fail → Wait 4s
Attempt 4 → Fail → Give up, trigger rollback
```

---

## Offline Handling

When the device is offline:

1. **Read operations**: Return cached data
2. **Write operations**: Queue for later sync
3. **Critical operations**: Inform user, require connection

### Sync Queue

```
Offline writes are queued:
┌─────────────────────────────────┐
│ Queue                           │
├─────────────────────────────────┤
│ 1. Task progress update         │
│ 2. Item equip                   │
│ 3. Currency spend               │
└─────────────────────────────────┘
         │
         │ When online
         ▼
    Process queue in order
```

---

## Security Model

### Client-Side Validation

- Check energy before requesting session
- Validate currency before purchase attempt
- Verify inventory before equip

### Server-Side Validation

- All state changes verified server-side
- Client cannot directly modify balances
- Session tokens prevent replay attacks
- Receipt verification for purchases

### Data Protection

- User data isolated by UID
- Firestore security rules enforce access
- Sensitive operations require Cloud Functions
- No raw database access from client

---

## Service Initialization

### Boot Sequence

```
1. App starts
2. Firebase SDK initializes
3. FirebaseGateway created
4. Auth state checked
5. If authenticated:
   - Remote services become available
   - Initial data fetch begins
6. If not authenticated:
   - Navigate to auth flow
```

### Lazy Loading

Remote services are created on-demand:
- Core services (session, currency) load at start
- Secondary services load when needed
- Reduces startup time

---

## Monitoring and Analytics

### Events Tracked

| Event | Purpose |
|-------|---------|
| Session start/end | Gameplay metrics |
| Purchase completed | Revenue tracking |
| Error occurred | Stability monitoring |
| Feature used | Usage analytics |

### Performance Metrics

- API response times
- Retry rates
- Error rates by type
- Sync success rates

---

## Summary

| Service | Primary Responsibility |
|---------|----------------------|
| Session | Game session management |
| Achievement | Progress tracking |
| Leaderboard | Score rankings |
| Player Stats | User statistics |
| Referral | Invite system |
| Streak | Daily rewards |
| IAP | Purchase verification |
| Elite Pass | Premium subscription |
| Autopilot | Idle earnings |
| Currency | Money management |
| Inventory | Item ownership |
| Energy | Play attempts |
| Task | Daily challenges |

All services follow the same pattern:
1. Interface defined in Core
2. Implementation in Infrastructure
3. Uses FirebaseGateway
4. Returns standardized results
5. Supports offline/retry

---

*Firebase integration is abstracted behind clean interfaces, allowing easy testing and potential backend migration.*
