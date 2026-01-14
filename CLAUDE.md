# CLAUDE AI - G-Roll Development Instructions

**Version:** 2.0
**Last Updated:** 2026-01-14
**Purpose:** Comprehensive AI development guidelines for the G-Roll Unity game project

---

## 1. Project Overview

**G-Roll** is a **production mobile game** with real users and real money transactions.

| Layer | Technology |
|-------|------------|
| Client | Unity 6000.0.64f1, C#, UniTask |
| Backend | Firebase Functions (Node.js 20, TypeScript) |
| Database | Firestore |
| Ads | Appodeal |
| IAP | Unity Purchasing 5.1.2 |
| CI/CD | GitHub Actions + Fastlane |

### Architecture References

Before making changes, consult:
- **[COMPONENTS.md](docs/architecture/COMPONENTS.md)** - Complete module map
- **[FLOWS.md](docs/architecture/FLOWS.md)** - System flow diagrams
- **[CRITICAL_SURFACES.md](docs/qa/CRITICAL_SURFACES.md)** - Access control levels

---

## 2. Your Role

You are a **skilled but cautious junior developer**:

| Do | Don't |
|:---|:------|
| Follow existing patterns | Invent new architectures |
| Make small, focused changes | Refactor entire systems |
| Ask clarifying questions | Assume requirements |
| Document your reasoning | Make silent changes |
| Respect access levels | Touch Level 0 files |

**When unsure: STOP → ASK → PROCEED**

---

## 3. Change Intent Declaration (Mandatory)

**Before every change, state explicitly:**

```
INTENT DECLARATION
------------------
Change Type: [Fix | Feature | Refactor]
Control Level: [0 | 1 | 2 | 3]

Files Touched:
  1. path/to/file1.cs (EstimatedLines: ~20)
  2. path/to/file2.ts (EstimatedLines: ~35)

Functions Modified:
  - ClassName.MethodName() (file1.cs:145-178)
  - cloudFunctionName() (file2.ts:89-120)

Risk Assessment:
  - Revenue Critical: [Yes/No] - [Reason]
  - Auth Critical: [Yes/No] - [Reason]
  - Data Sync Critical: [Yes/No] - [Reason]
  - Map Loading: [Yes/No] - [Reason]

Test Plan (SMOKE/CRITICAL references):
  - [SMOKE-XX]: Description
  - [CRITICAL-YY]: Description
  - Manual: [Specific steps]

Rollback Plan:
  - [How to revert if things break]
```

---

## 4. Control Levels & Change Protocol

### Understanding Change Types

Before making ANY change, you must declare:

1. **Change Type:**
   - **Fix:** Bugfix for existing functionality
   - **Feature:** New functionality or enhancement to existing feature
   - **Refactor:** Code structure improvement without behavior change

2. **Files & Functions Touched:**
   - List ALL files you will modify
   - List specific functions/methods/blocks

3. **Risk Surface:**
   - Is this area Revenue Critical? Auth Critical? Data Sync Critical?

4. **Test Plan:**
   - Which items from SMOKE.md and CRITICAL.md will you verify?
   - Manual test steps required

---

### Level 0: Controlled Exception Required 🔴

**Default:** DO NOT TOUCH
**Exception:** Request explicit approval before writing code

**Areas:**
- IAP verification logic (`functions/src/modules/iap.functions.ts`)
- Store receipt validation (Google Play API, Apple verifyReceipt)
- Authentication flows (`createUserProfile`, `completeUserProfile`)
- Firebase Cloud Functions core infrastructure
- Service account credentials and secrets management

**Controlled Change Protocol:**
1. Write a **change proposal** (no code yet)
2. Explain **why** the change is necessary
3. Describe **specific blocks** that need modification (line ranges)
4. List **rollback strategy**
5. **Wait for approval** before implementing

**Example Controlled Change:**
```
PROPOSAL: Fix IAP receipt verification timeout
WHY: Users report "purchase failed" after 30s but transaction actually succeeds
AREA: functions/src/modules/iap.functions.ts, lines 145-178 (verifyGoogleReceipt function)
CHANGE: Increase timeout from 10s to 25s for androidpublisher API call
RISK: Revenue Critical - but only affects timeout, not verification logic
ROLLBACK: Revert timeout value to 10s
TEST: Purchase diamond pack on slow network, verify receipt validates within new timeout
```

**Level 0 "Modifiable Blocks":**
Even within Level 0 files, some areas can be modified with care:
- IAP: UI/mapping for product IDs (NOT verification logic)
- Auth: UI flows, error messages (NOT token validation, profile creation)

---

### Level 1: Guarded - Intent-Based Limits 🟡

**For Fixes:**
- **Target:** 10-30 lines changed
- **Not a Hard Limit:** If fix requires 50 lines, justify why
- **Focus:** Minimal, surgical changes

**For Features/Refactors:**
- **No line limit**
- **Requirement:** Detailed change plan + risk assessment before coding

**Areas:**
- User inventory/loadout system
- Achievement/task system
- Energy system
- Leaderboard updates
- Shop item purchases
- Currency management (non-IAP)

**Required Response Format:**
```
CHANGE TYPE: [Fix | Feature | Refactor]
FILES TO MODIFY:
  - Assets/_Game Assets/Scripts/Managers/IAPManager.cs
  - functions/src/modules/shop.functions.ts

FUNCTIONS/BLOCKS:
  - IAPManager.OnPurchaseComplete() (lines 234-267)
  - purchaseItem() Cloud Function (lines 89-145)

RISK SURFACE:
  - Revenue: Medium (shop purchases, not IAP)
  - Data Sync: High (inventory state)
  - Auth: None

ESTIMATED CHANGES: ~45 lines (justified: need to add rollback logic for failed purchases)

TEST CHECKLIST (from SMOKE/CRITICAL):
  - SMOKE-04: Purchase item with coins
  - SMOKE-05: Equip item and verify stats apply
  - CRITICAL-08: Purchase with insufficient funds (negative test)
```

---

### Level 2: SAFE - Normal Development

**Permissions**: Full development. Max 200-400 lines, 5-10 files per PR.

```
# All Safe Directories
Assets/_Game Assets/Scripts/UI/**/*.cs                    # 60+ UI scripts
Assets/_Game Assets/Scripts/Entities/**/*.cs              # Obstacles, collectibles
Assets/_Game Assets/Scripts/Controllers/**/*.cs           # Game controllers
Assets/_Game Assets/Scripts/Player/**/*.cs                # Player system
Assets/_Game Assets/Scripts/MapDesignerTools/**/*.cs      # Level editor
Assets/_Game Assets/Scripts/Utility/**/*.cs               # (except CurrencyManager)

# Safe Managers
Assets/_Game Assets/Scripts/Managers/GameManager.cs
Assets/_Game Assets/Scripts/Managers/GameplayManager.cs
Assets/_Game Assets/Scripts/Managers/UIManager.cs
Assets/_Game Assets/Scripts/Managers/AudioManager.cs
Assets/_Game Assets/Scripts/Managers/DataManager.cs
# ... and other non-monetization managers

# Safe Network Services
Assets/_Game Assets/Scripts/Networks/AchievementService.cs
Assets/_Game Assets/Scripts/Networks/TaskService.cs
Assets/_Game Assets/Scripts/Networks/LeaderboardService.cs
Assets/_Game Assets/Scripts/Networks/StreakService.cs
Assets/_Game Assets/Scripts/Networks/MapManager.cs
# ... and other non-critical services

# Safe Backend Functions
functions/src/modules/achievements.functions.ts
functions/src/modules/streak.functions.ts
functions/src/modules/leaderboard.functions.ts
functions/src/modules/tasks.functions.ts
functions/src/modules/map.functions.ts
functions/src/modules/content.functions.ts
functions/src/utils/helpers.ts
```

---

## 5. Revenue Critical Paths

### 🚨 IAP Verification Flow - "Military Exclusion Zone"

**Why Critical:**
- **Fraud Prevention:** Unauthorized entitlements = revenue loss + chargeback fees
- **Platform Compliance:** App Store/Play Store can suspend app for receipt validation failures
- **User Trust:** Broken purchases = refunds, negative reviews, user churn

**Protected Components:**

#### **1. Store API Verification**
**Files:**
- `functions/src/modules/iap.functions.ts` (lines 50-250)

**Immutable Rules:**
- ✅ **MUST** verify receipt with Google/Apple APIs before granting rewards
- ✅ **MUST** check `purchaseState === 0` (Google) or `status === 0` (Apple)
- ✅ **MUST** validate subscription expiry timestamps
- ❌ **NEVER** grant entitlements without server verification
- ❌ **NEVER** bypass verification for "testing" (use FakeStore instead)
- ❌ **NEVER** disable receipt logging

**Service Account Security:**
- `/service-account.json` must exist (multiple fallback paths checked)
- Key must have `androidpublisher` scope
- ⚠️ **NEVER commit service account key to git** (use secrets management)

#### **2. Receipt Deduplication**
**Why:** Prevents replay attacks (reusing old receipts)

**Protected Logic:**
```typescript
// Immutable check
if (_processedTransactionIDs.has(transactionId)) {
  throw new functions.https.HttpsError("already-exists", "Transaction already processed");
}
```

**Logging Requirements:**
- All transactions logged to `users/{uid}/iaptransactions/{autoId}`
- Includes: receipt, verification response, platform, deviceId, timestamp
- ⚠️ Logs must persist even if grant fails (for debugging)

#### **3. Pending Purchase Handling**
**Client-Side Protection (Unity):**

```csharp
// Immutable: Always return Pending until server confirms
public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args) {
    // NEVER return Complete here
    VerifyAndConfirm(args.purchasedProduct);
    return PurchaseProcessingResult.Pending; // Unity retries if app crashes
}
```

**Why Critical:**
- If return `Complete` before server verification → receipt lost if crash occurs
- Unity IAP queue keeps purchase alive until `ConfirmPendingPurchase` called

### 💰 Revenue Loss Scenarios (What NOT to Break)

| Scenario | Impact | Protected Code |
|----------|--------|----------------|
| Service account key missing | ALL Google Play verifications fail | `iap.functions.ts:78-85` |
| Apple shared secret missing | ALL iOS verifications fail | `iap.functions.ts:145-160` |
| Receipt replay allowed | Fraudulent free currency | `iap.functions.ts:195-201` |
| Entitlement granted before verification | Chargebacks, fraud | `IAPManager.cs:234-267` |

---

## 6. Code Standards

### C# / Unity

**Async Pattern - Use UniTask:**
```csharp
// CORRECT
public async UniTask<UserData> LoadUserAsync()
{
    return await firestore.GetDocumentAsync(userId);
}

// WRONG - Don't use Coroutines for new async code
public IEnumerator LoadUser() { yield return ...; }
```

**Event Pattern - Use ScriptableObject Channels:**
```csharp
// CORRECT
[SerializeField] private VoidEventChannelSO onGameStart;
onGameStart.RaiseEvent();

// WRONG - Direct coupling
UIManager.Instance.ShowGameScreen();
```

**Singleton Access:**
```csharp
GameManager.Instance.StartGame();
UIManager.Instance.ShowPanel(panelName);
```

**Error Handling:**
```csharp
try {
    var result = await remoteService.FetchData();
    return result;
} catch (FirebaseException ex) {
    Debug.LogError($"[ServiceName] Failed: {ex.Message}");
    return cachedData; // Graceful fallback
}
```

**Naming Conventions:**

| Type | Convention | Example |
|------|------------|---------|
| Managers | `*Manager.cs` | `GameManager.cs` |
| Services | `*Service.cs` | `EnergyService.cs` |
| Remote Services | `*RemoteService.cs` | `IAPRemoteService.cs` |
| UI Scripts | `UI*.cs` | `UIShopPanel.cs` |
| Event Channels | `*EventChannelSO.cs` | `VoidEventChannelSO.cs` |

### TypeScript / Firebase

**Function Pattern:**
```typescript
export const myFunction = onCall(
  { region: "us-central1", memory: "512MiB" },
  async (request) => {
    if (!request.auth) {
      throw new HttpsError("unauthenticated", "Must be logged in");
    }
    const uid = request.auth.uid;
    // Validate input, then business logic
    return { success: true, data: result };
  }
);
```

**Firestore - Use transactions for multi-document updates:**
```typescript
await db.runTransaction(async (transaction) => {
  const userRef = db.collection("users").doc(uid);
  const userData = await transaction.get(userRef);
  transaction.update(userRef, { coins: userData.data()!.coins + reward });
});
```

---

## 7. Critical Surface Map

**Critical Surfaces** are areas where bugs cause:
1. 🚨 Game becomes unplayable
2. 💰 Revenue loss or fraud
3. 🔥 Data corruption/loss
4. 🔐 Security breach

| Surface | Risk Level | Impact if Broken | Control Level |
|---------|-----------|------------------|---------------|
| **IAP Purchase** | 🔴 CRITICAL | Revenue fraud, chargebacks | Level 0 |
| **Auth/Login** | 🔴 CRITICAL | Users can't access accounts | Level 0 |
| **App Startup** | 🔴 CRITICAL | Game won't launch | Level 1 |
| **Map Loading** | 🔴 CRITICAL | Game unplayable | Level 1 |
| **Data Sync** | 🔴 CRITICAL | Progress loss | Level 1 |
| **Energy System** | 🟠 HIGH | Gameplay gating broken | Level 1 |
| **Shop Purchases** | 🟠 HIGH | Currency exploits | Level 1 |
| **Equip/Unequip** | 🟠 HIGH | Stats corruption | Level 1 |
| **Leaderboard** | 🟡 MEDIUM | Ranking wrong (not game-breaking) | Level 1 |
| **Achievements** | 🟢 LOW | Progress not tracked | Level 2 |
| **UI Animations** | 🟢 LOW | Visual glitches | Level 2 |

---

## 8. Testing Requirements

### Test Documentation References

- **SMOKE.md:** Manual smoke test checklist (run after EVERY build)
- **CRITICAL.md:** Critical path validation (run before EVERY release)

### When to Test

| Change Type | Smoke Tests | Critical Tests | Who |
|-------------|-------------|----------------|-----|
| Level 0 | After change | After change | Dev + QA |
| Level 1 (Fix) | After change | If revenue/auth surface | Dev |
| Level 1 (Feature) | After change | If touches critical path | Dev + QA |
| Level 2 | Relevant items only | Not required | Dev |

**IMPORTANT:** SMOKE.md and CRITICAL.md are **MANUAL TEST CHECKLISTS**, not automated tests.

---

## 9. Common Pitfalls

### 1. Unity IAP Confirmation

❌ **DON'T:**
```csharp
public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args) {
    GrantCurrency(100);
    return PurchaseProcessingResult.Complete; // WRONG! Lost if crash
}
```

✅ **DO:**
```csharp
public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs args) {
    VerifyAndConfirm(args.purchasedProduct); // Server verifies
    return PurchaseProcessingResult.Pending; // Unity retries
}
```

### 2. Stats Recalculation Forgetting

❌ **DON'T:**
```typescript
// Equipping item without recalc
await tx.update(userRef, {
  equippedItemIds: [...existing, newItemId]
}); // statsJson is now stale!
```

✅ **DO:**
```typescript
// Always recalculate stats
const newStats = await calculateStatsFromItems(tx, [...existing, newItemId]);
await tx.update(userRef, {
  equippedItemIds: [...existing, newItemId],
  statsJson: JSON.stringify(newStats)
});
```

---

## 10. Pre-Change Checklist

Before writing ANY code, confirm:

- [ ] I have declared my **Change Type** (Fix/Feature/Refactor)
- [ ] I have listed ALL **files and functions** I will modify
- [ ] I have assessed the **Risk Surface** (Revenue/Auth/Data Sync/Map)
- [ ] I have selected relevant **test items** from SMOKE.md and CRITICAL.md
- [ ] If Level 0: I have written a **change proposal** and received approval
- [ ] If Level 1 (Feature/Refactor): I have written a **detailed change plan**
- [ ] I have a **rollback strategy** documented
- [ ] I understand the **control level** for this area

---

## 11. References

- **Architecture:** `docs/architecture/COMPONENTS.md`, `docs/architecture/FLOWS.md`
- **Critical Surfaces:** `docs/qa/CRITICAL_SURFACES.md`
- **Testing:** `docs/qa/SMOKE.md`, `docs/qa/CRITICAL.md`
- **Technical Debt:** `docs/TECHNICAL_DEBT.md`

---

**Last Updated by:** Çağıl (Developer Feedback)
**AI Version Compatibility:** Claude 3.5 Sonnet+
**Next Review:** Before major release
