# Optimistic Client Philosophy

**Version:** 1.0
**Date:** January 2026

---

## Core Philosophy

> "Assume success, prepare for failure."

The client behaves as if every operation will succeed. UI updates happen **immediately** when the user takes action, before any server confirmation. If the server later rejects the operation, the client gracefully rolls back to the previous state.

---

## Why Optimistic?

### Traditional (Pessimistic) Approach

```
User taps "Buy" → Loading spinner → 500-2000ms wait → UI updates
```

**Problems:**
- App feels slow and unresponsive
- Users notice every network delay
- Frustrating experience on slow connections

### Optimistic Approach

```
User taps "Buy" → UI updates INSTANTLY → Background sync
                                            ↓
                              Success: Done (no UI change needed)
                              Failure: Rollback + subtle notification
```

**Benefits:**
- App feels instant and responsive
- Network latency is invisible to users
- Users can continue interacting immediately

---

## The Four-Step Pattern

Every optimistic operation follows these steps:

### Step 1: Snapshot

Before any change, capture the current state.

```
┌─────────────────────────────────────┐
│           CREATE SNAPSHOT           │
├─────────────────────────────────────┤
│                                     │
│  Save current state:                │
│  - Currency balances                │
│  - Inventory items                  │
│  - Task progress                    │
│  - Any data that might change       │
│                                     │
│  Example:                           │
│  snapshot = { coins: 500, gems: 50 }│
│                                     │
└─────────────────────────────────────┘
```

### Step 2: Optimistic Update

Immediately update the UI as if the operation succeeded.

```
┌─────────────────────────────────────┐
│        OPTIMISTIC UPDATE            │
├─────────────────────────────────────┤
│                                     │
│  Update local state immediately:    │
│  - Deduct currency                  │
│  - Add item to inventory            │
│  - Update progress bar              │
│                                     │
│  Publish message:                   │
│  CurrencyChangedMessage {           │
│    isOptimistic: true               │
│  }                                  │
│                                     │
│  User sees: 500 → 400 coins         │
│  (INSTANT - no loading)             │
│                                     │
└─────────────────────────────────────┘
```

### Step 3: Server Request

Send the operation to the server in the background.

```
┌─────────────────────────────────────┐
│         BACKGROUND SYNC             │
├─────────────────────────────────────┤
│                                     │
│  While user continues using app:    │
│                                     │
│  - Fire async request to Firebase   │
│  - User doesn't see any loading     │
│  - Other interactions still work    │
│                                     │
│  await remoteService.Purchase(...)  │
│                                     │
└─────────────────────────────────────┘
```

### Step 4: Handle Result

Process the server response.

```
┌─────────────────────────────────────┐
│           ON SUCCESS                │
├─────────────────────────────────────┤
│                                     │
│  Server confirmed:                  │
│  - Do nothing (UI already correct)  │
│  - Optionally sync if server value  │
│    differs (bonus applied, etc.)    │
│                                     │
└─────────────────────────────────────┘

┌─────────────────────────────────────┐
│           ON FAILURE                │
├─────────────────────────────────────┤
│                                     │
│  Server rejected:                   │
│                                     │
│  1. Restore snapshot                │
│     coins: 400 → 500 (animated)     │
│                                     │
│  2. Publish message:                │
│     CurrencyChangedMessage {        │
│       isOptimistic: false           │
│     }                               │
│                                     │
│  3. Show subtle feedback:           │
│     - Small toast notification      │
│     - Brief haptic vibration        │
│                                     │
└─────────────────────────────────────┘
```

---

## ISnapshotable Interface

Services that support optimistic operations implement this interface:

```
ISnapshotable<TState>
├── CreateSnapshot() → TState     # Capture current state
└── RestoreSnapshot(TState)       # Revert to captured state
```

### Snapshotable Services

| Service | State Captured |
|---------|----------------|
| CurrencyService | All currency balances |
| InventoryService | Owned items, equipped items |
| TaskService | Task progress, claimed rewards |
| AchievementService | Achievement progress, claimed levels |
| EnergyService | Current energy, timer state |

---

## When to Use Optimistic vs Pessimistic

### Use Optimistic

| Operation | Why |
|-----------|-----|
| Currency transactions | Fast feedback crucial |
| Item equipping | Visual change should be instant |
| Collecting coins in-game | Can't interrupt gameplay |
| Task progress updates | Frequent, non-critical |
| Achievement claiming | Immediate reward feeling |
| Profile updates | Quick feedback expected |

### Use Pessimistic (with loading)

| Operation | Why |
|-----------|-----|
| Real money purchases | Financial security |
| Account deletion | Irreversible, critical |
| Session start (energy check) | Must verify eligibility |
| Leaderboard submission | Must confirm acceptance |

### Use Semi-Pessimistic

Some operations show brief loading but don't block the entire UI:

| Operation | Approach |
|-----------|----------|
| Starting a game session | Show transition, validate in parallel |
| Submitting high score | Show result screen, sync background |

---

## Batched Updates

For frequent operations (like collecting coins during gameplay), individual server calls are inefficient. Use **batching**:

```
┌─────────────────────────────────────┐
│          BATCHED UPDATES            │
├─────────────────────────────────────┤
│                                     │
│  Coin 1 collected:                  │
│  - UI: 0/100 → 1/100                │
│  - Queue: { task_id: +1 }           │
│  - NO server call yet               │
│                                     │
│  Coin 2-10 collected (rapidly):     │
│  - UI updates each time             │
│  - Queue: { task_id: +10 }          │
│                                     │
│  After 2 seconds (batch timer):     │
│  - Send: { task_id: +10 }           │
│  - Single server request            │
│                                     │
└─────────────────────────────────────┘
```

**Benefits:**
- Reduces server load
- Minimizes network traffic
- Gameplay never interrupted

---

## Rollback UX Guidelines

When a rollback happens, the user should barely notice:

### Do

- **Animate** the value change (400 → 500 smooth transition)
- Show a **brief, non-blocking** toast ("Operation failed")
- Use **subtle haptic** feedback (light vibration)
- **Continue** allowing user interactions

### Don't

- Show error dialogs that require dismissal
- Block the UI during rollback
- Display technical error messages
- Make the user feel punished

### Toast Messages

| Scenario | Message |
|----------|---------|
| Purchase failed | "Purchase couldn't complete" |
| Network error | "Connection issue, try again" |
| Validation failed | "Couldn't update, please retry" |

Keep messages:
- Short (under 40 characters)
- Non-technical
- Action-oriented when possible

---

## Message Flow Example

**Scenario:** User buys an item for 100 coins

```
┌──────────────────────────────────────────────────────────────┐
│                     PURCHASE FLOW                             │
├──────────────────────────────────────────────────────────────┤
│                                                              │
│  [User Taps Buy]                                             │
│        │                                                     │
│        ▼                                                     │
│  ┌──────────────────────────────────────┐                    │
│  │ 1. Snapshot                          │                    │
│  │    { coins: 500, inventory: [...] }  │                    │
│  └──────────────────────────────────────┘                    │
│        │                                                     │
│        ▼                                                     │
│  ┌──────────────────────────────────────┐                    │
│  │ 2. Optimistic Update                 │                    │
│  │    coins = 400                       │                    │
│  │    inventory.add(item)               │                    │
│  └──────────────────────────────────────┘                    │
│        │                                                     │
│        ▼                                                     │
│  MessageBus.Publish(CurrencyChangedMessage)   ───────────►   │
│  MessageBus.Publish(InventoryChangedMessage)  ───────────►   │
│        │                                         │           │
│        │                                         ▼           │
│        │                               ┌──────────────────┐  │
│        │                               │ UI Updates       │  │
│        │                               │ - Coin display   │  │
│        │                               │ - Item appears   │  │
│        │                               └──────────────────┘  │
│        ▼                                                     │
│  ┌──────────────────────────────────────┐                    │
│  │ 3. Server Request (background)       │                    │
│  │    await purchaseRemote(item)        │                    │
│  └──────────────────────────────────────┘                    │
│        │                                                     │
│   ┌────┴────┐                                                │
│   ▼         ▼                                                │
│ SUCCESS   FAILURE                                            │
│   │         │                                                │
│   │         ▼                                                │
│   │  ┌──────────────────────────────────┐                    │
│   │  │ 4. Rollback                      │                    │
│   │  │    RestoreSnapshot()             │                    │
│   │  │    coins = 500                   │                    │
│   │  │    inventory.remove(item)        │                    │
│   │  └──────────────────────────────────┘                    │
│   │         │                                                │
│   │         ▼                                                │
│   │  MessageBus.Publish(CurrencyChangedMessage)              │
│   │  MessageBus.Publish(OperationRolledBackMessage)          │
│   │         │                                                │
│   │         ▼                                                │
│   │  ┌──────────────────────────────────┐                    │
│   │  │ Show Toast + Haptic              │                    │
│   │  └──────────────────────────────────┘                    │
│   │                                                          │
│   ▼                                                          │
│  [Done]                                                      │
│                                                              │
└──────────────────────────────────────────────────────────────┘
```

---

## UI Component Guidelines

### OptimisticButton

A button wrapper that handles the async pattern:

1. Disables during operation (prevents double-tap)
2. Shows subtle loading indicator
3. Re-enables after completion or rollback

### OptimisticCurrencyDisplay

Currency display that animates changes:

1. Detects `isOptimistic` flag in message
2. Animates count-up/down smoothly
3. Different animation for rollback (maybe slight shake)

### OptimisticListItem

List items that can be added/removed optimistically:

1. Appears/disappears with animation
2. Rollback removes/restores with reverse animation

---

## Error Classification

Not all errors trigger rollback:

| Error Type | Behavior |
|------------|----------|
| Network timeout | Retry, then rollback if persistent |
| Validation failure | Immediate rollback |
| Server error (500) | Retry, then rollback |
| Conflict (outdated data) | Sync from server, no rollback |

---

## Testing Optimistic Flows

### Scenarios to Test

1. **Happy path**: Operation succeeds, no visible change after initial update
2. **Rollback**: Server rejects, values animate back
3. **Rapid actions**: Multiple quick operations don't conflict
4. **Network failure**: Timeout handling and rollback
5. **Offline mode**: Queue operations for later sync

### Simulating Failures

Use debug flags to force rollback scenarios:
- Force network delay (test loading states)
- Force validation failure (test rollback animation)
- Force timeout (test retry logic)

---

## Summary

| Principle | Implementation |
|-----------|----------------|
| Instant feedback | Update UI before server response |
| Graceful failure | Animated rollback, subtle notification |
| Non-blocking | User can continue during sync |
| Batched updates | Group frequent operations |
| Minimal disruption | Toast, not dialog; haptic, not alert |

The goal: Users should feel the app is fast and reliable, even on slow networks. Failures are rare exceptions, not obstacles.

---

*Optimistic architecture puts user experience first while maintaining data integrity through robust rollback mechanisms.*
