# G-Roll Test Automation Plan

**Version:** 1.0
**Status:** 📝 **BACKLOG ITEM** (Planning Phase)
**Priority:** Medium
**Estimated Effort:** 2-3 weeks (implementation)

---

## Overview

This document outlines the **future test automation strategy** for G-Roll. Currently, all testing is **manual** (see SMOKE.md and CRITICAL.md). This plan describes how to introduce **automated unit tests** for the 27 network services.

**⚠️ IMPORTANT:** This is a **planning document**, NOT an implementation task. The current focus is on **manual testing** until the game reaches stable production.

---

## Current State (2026-01-14)

| Aspect | Status |
|--------|--------|
| **Manual Testing** | ✅ Active (SMOKE.md + CRITICAL.md) |
| **Automated Unit Tests** | ❌ None |
| **Integration Tests** | ❌ None |
| **E2E Tests** | ❌ None |
| **CI/CD Tests** | ❌ None (runner offline) |

---

## Proposed Automation Strategy

### Phase 1: Network Service Unit Tests (27 Services)

**Goal:** Verify each Cloud Function callable is reachable and responds correctly.

**Approach:** "Ping/Pong" validation for all 27 network services.

#### Test Harness Architecture

```csharp
// Test harness for each service
public class NetworkServiceTests
{
    [Test]
    public async Task TestService_PingPong()
    {
        // Arrange
        var service = new TestableSessionRemoteService();
        var testUid = "test-user-123";

        // Act: Call "ping" endpoint (lightweight operation)
        var response = await service.PingAsync(testUid);

        // Assert: Verify "pong" response
        Assert.IsTrue(response.success);
        Assert.AreEqual("pong", response.message);
    }

    [Test]
    public async Task TestService_Timeout()
    {
        // Arrange
        var service = new TestableSessionRemoteService();
        service.SetTimeout(1); // 1ms timeout

        // Act & Assert: Verify timeout handling
        Assert.ThrowsAsync<TimeoutException>(async () => {
            await service.RequestSessionAsync("test-uid");
        });
    }

    [Test]
    public async Task TestService_DuplicatePing()
    {
        // Arrange
        var service = new TestableSessionRemoteService();
        var pingId = "unique-ping-123";

        // Act: Send same ping twice
        var response1 = await service.PingAsync(pingId);
        var response2 = await service.PingAsync(pingId);

        // Assert: Second ping should be rejected (duplicate)
        Assert.IsTrue(response1.success);
        Assert.IsFalse(response2.success);
        Assert.AreEqual("duplicate", response2.errorCode);
    }
}
```

---

### Phase 2: 27 Services Test Coverage

**Services to Test:**

#### Core Data Services (4)
1. `UserDatabaseManager.cs` - Ping/Pong only (CRITICAL - don't test real ops)
2. `FirebaseLoginHandler.cs` - Auth token validation
3. `IAPRemoteService.cs` - Ping/Pong only (LEVEL 0 - no fake purchases)
4. `InventoryRemoteService.cs` - Basic item fetch test

#### Session & Gameplay Services (4)
5. `SessionRemoteService.cs` - Request session timeout test
6. `SessionResultRemoteService.cs` - Submit result validation test
7. `PlayerStatsRemoteService.cs` - Sync stats test
8. `MapManager.cs` - Fetch map data test

#### Economy Services (3)
9. `UserEnergyService.cs` - Energy calculation test
10. `ElitePassService.cs` - Subscription status check
11. `ElitePassValidator.cs` - Validation timeout test

#### Feature Services (9)
12. `AchievementService.cs` - Fetch achievements test
13. `TaskService.cs` - Fetch tasks test
14. `LeaderboardService.cs` - Fetch leaderboard test
15. `LeaderboardManager.cs` - Display logic test
16. `StreakService.cs` - Streak calculation test
17. `AutopilotService.cs` - Autopilot state test
18. `ReferralRemoteService.cs` - Referral code validation
19. `ReferralManager.cs` - UI logic test
20. `AdManager.cs` - Ad availability check

#### Content Services (7)
21. `RemoteAppDataService.cs` - Config fetch test
22. `RemoteItemService.cs` - Item catalog fetch test
23. `FirestoreRemoteFetcher.cs` - Generic query test
24. `UserInventoryManager.cs` - Local inventory state test
25. `ItemLocalDatabase.cs` - Cache hit/miss test
26. `AchievementIconCache.cs` - Icon cache test
27. `SkinManager.cs` - Skin catalog test

---

### Phase 3: Test Implementation Plan

**Step 1: Setup Firebase Emulator Suite**
- Install Firebase Emulator (Firestore + Functions)
- Configure test environment (separate from production)
- Seed test data (users, items, achievements)

**Step 2: Create "Ping" Cloud Functions**
For each service, add a lightweight "ping" endpoint:

```typescript
// functions/src/modules/test.functions.ts
export const pingSession = onCall({ region: "us-central1" }, async (request) => {
  if (!request.auth) {
    throw new HttpsError("unauthenticated", "Must be logged in");
  }

  // Simple health check
  return { success: true, message: "pong", timestamp: Date.now() };
});

export const pingInventory = onCall({ region: "us-central1" }, async (request) => {
  // Same pattern for all 27 services
  return { success: true, message: "pong", service: "inventory" };
});
```

**Step 3: Unity Test Framework Integration**
- Use Unity Test Framework (UTF) or NUnit
- Create `Tests/` directory in Unity project
- Write test classes for each service

**Step 4: CI/CD Integration**
- Add test step to GitHub Actions workflow
- Run tests on every PR
- Block merge if tests fail

---

## Test Coverage Goals

| Test Type | Coverage Target | Priority |
|-----------|----------------|----------|
| **Ping/Pong** | 100% (all 27 services) | 🔴 HIGH |
| **Timeout Handling** | 100% (all 27 services) | 🟠 MEDIUM |
| **Duplicate Request** | 80% (critical services only) | 🟡 LOW |
| **Negative Tests** | 50% (error code validation) | 🟢 FUTURE |

---

## Test Execution Strategy

### Local Development
```bash
# Start Firebase Emulator
firebase emulators:start

# Run Unity tests
Unity -runTests -testPlatform PlayMode -testResults results.xml
```

### CI/CD (GitHub Actions)
```yaml
name: Run Unit Tests

on: [pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v2
      - name: Setup Firebase Emulator
        run: npm install -g firebase-tools
      - name: Start Emulator
        run: firebase emulators:start --only firestore,functions &
      - name: Run Unity Tests
        run: unity-editor -runTests
      - name: Stop Emulator
        run: pkill -f firebase
```

---

## Negative Test Scenarios

### Critical Negative Tests (Must Have)

| Service | Test Case | Expected Behavior |
|---------|-----------|-------------------|
| **SessionRemoteService** | Request session with 0 energy | Return error: "insufficient energy" |
| **IAPRemoteService** | Submit fake receipt | Return error: "invalid receipt" |
| **InventoryRemoteService** | Equip item not owned | Return error: "item not found" |
| **UserEnergyService** | Refill with insufficient diamonds | Return error: "insufficient currency" |
| **LeaderboardService** | Submit invalid sessionId | Return error: "invalid session" |

---

## Test Data Management

### Seed Data (Firebase Emulator)

**Test Users:**
```json
{
  "users": {
    "test-user-1": {
      "uid": "test-user-1",
      "nickname": "TestUser1",
      "coins": 1000,
      "diamonds": 100,
      "energy": 5,
      "inventory": ["item-001", "item-002"],
      "equippedItemIds": ["item-001"]
    },
    "test-user-2": {
      "uid": "test-user-2",
      "nickname": "TestUser2",
      "coins": 0,
      "diamonds": 0,
      "energy": 0
    }
  }
}
```

**Test Items:**
```json
{
  "items": {
    "item-001": {
      "id": "item-001",
      "name": "Test Ball",
      "type": "ball",
      "price": 100,
      "stats": { "speed": 10, "jump": 5 }
    }
  }
}
```

---

## Benefits of Automation

| Benefit | Impact |
|---------|--------|
| **Faster Regression Testing** | Reduce manual test time from 2 hours → 5 minutes |
| **Early Bug Detection** | Catch service failures before manual testing |
| **Confidence in Refactoring** | Safe to refactor services with test safety net |
| **CI/CD Integration** | Block broken code from merging to main |

---

## Risks & Mitigation

| Risk | Mitigation |
|------|------------|
| **Flaky Tests** | Use deterministic test data, avoid time-based tests |
| **Slow Tests** | Use Firebase Emulator (local, fast) instead of real Firebase |
| **Maintenance Overhead** | Keep tests simple (ping/pong pattern) |
| **False Positives** | Retry failed tests 2x before marking as failure |

---

## Timeline (Proposed)

| Phase | Duration | Deliverable |
|-------|----------|-------------|
| **Setup** | 1 week | Firebase Emulator + Unity Test Framework |
| **Phase 1** | 1 week | Ping/Pong tests for all 27 services |
| **Phase 2** | 1 week | Timeout + Duplicate tests |
| **Phase 3** | 2 weeks | Negative tests + CI/CD integration |

**Total:** ~5 weeks (with 1 developer)

---

## Next Steps (Backlog)

- [ ] **Decision:** Approve automation plan (Founder)
- [ ] **Backlog:** Add "Setup Firebase Emulator" task
- [ ] **Backlog:** Add "Create Ping Cloud Functions" task
- [ ] **Backlog:** Add "Write 27 Service Tests" task
- [ ] **Backlog:** Add "CI/CD Integration" task

---

## Related Documents

- [SMOKE.md](./SMOKE.md) - Manual smoke test checklist
- [CRITICAL.md](./CRITICAL.md) - Critical path manual tests
- [COMPONENTS.md](../architecture/COMPONENTS.md) - 27 services overview

---

**Status:** 📝 Planning Phase
**Owner:** TBD
**Next Review:** After production launch (when manual testing stabilizes)
