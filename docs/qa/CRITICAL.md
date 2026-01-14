# Critical Path Testing - G-Roll

**Version:** 2.0
**Last Updated:** 2026-01-14

---

## ⚠️ IMPORTANT: This is a MANUAL TEST CHECKLIST

**This document contains MANUAL test procedures for revenue-critical and game-breaking paths.**

### Who Runs These Tests?
- **Developer:** Before merging to main branch
- **QA (if exists):** Before releasing to TestFlight/Google Play
- **Founder:** Before EVERY production release (final gate)

### When to Run?
- **CRITICAL Tests:** Before production release ONLY
- **Prerequisite:** SMOKE tests must ALL pass first
- **Frequency:** 30-45 minutes per full run
- **Blocking:** Production release CANNOT proceed if any test fails

### What if Tests Fail?
- **STOP** the release process immediately
- **DO NOT** ship to production
- **FIX** the issue (treat as P0 bug)
- **RE-RUN** all CRITICAL tests
- **GET APPROVAL** from founder before retrying release

---

## 1. In-App Purchase (CRITICAL)

### 1.1 Consumable (Diamonds)

| # | Test | Expected | Pass |
|---|------|----------|------|
| 1.1.1 | Open IAP shop | Prices from store visible | [ ] |
| 1.1.2 | Buy smallest pack | Store payment sheet shows | [ ] |
| 1.1.3 | Complete purchase | Server verifies receipt | [ ] |
| 1.1.4 | Diamonds credited | Balance increases | [ ] |
| 1.1.5 | No pending warning | Transaction complete | [ ] |

### 1.2 Non-Consumable (Remove Ads)

| # | Test | Expected | Pass |
|---|------|----------|------|
| 1.2.1 | Purchase Remove Ads | Transaction completes | [ ] |
| 1.2.2 | Ads disabled | No interstitials | [ ] |
| 1.2.3 | Persists after restart | Still no ads | [ ] |
| 1.2.4 | Restore purchases | Works correctly | [ ] |

### 1.3 Subscription (Elite Pass)

| # | Test | Expected | Pass |
|---|------|----------|------|
| 1.3.1 | Purchase Elite Pass | Transaction completes | [ ] |
| 1.3.2 | Benefits active | 2x coins, faster energy | [ ] |
| 1.3.3 | Persists after restart | Still active | [ ] |

### 1.4 Edge Cases

| # | Test | Expected | Pass |
|---|------|----------|------|
| 1.4.1 | Cancel mid-purchase | No charge, no reward | [ ] |
| 1.4.2 | Network loss during verify | Recovers on retry | [ ] |
| 1.4.3 | Kill app during purchase | Recovers on restart | [ ] |

---

## 2. Ad Rewards

| # | Test | Expected | Pass |
|---|------|----------|------|
| 2.1 | Watch rewarded ad | Plays completely | [ ] |
| 2.2 | Reward granted | Coins/energy added | [ ] |
| 2.3 | Server validates | Not just client-side | [ ] |
| 2.4 | Daily limit works | Stops after max views | [ ] |
| 2.5 | Interstitial frequency | Shows at correct rate | [ ] |

---

## 3. User Data Integrity

### 3.1 Persistence

| # | Test | Expected | Pass |
|---|------|----------|------|
| 3.1.1 | Complete session | Score saved | [ ] |
| 3.1.2 | Earn coins | Persists after restart | [ ] |
| 3.1.3 | Purchase item | In inventory after restart | [ ] |
| 3.1.4 | Unlock achievement | Persists | [ ] |

### 3.2 Recovery

| # | Test | Expected | Pass |
|---|------|----------|------|
| 3.2.1 | Uninstall/reinstall | Data recovers on login | [ ] |
| 3.2.2 | Login new device | Same data synced | [ ] |

---

## 4. Energy System

| # | Test | Expected | Pass |
|---|------|----------|------|
| 4.1 | Consumed on play | Correct amount | [ ] |
| 4.2 | Regenerates over time | +1 per interval | [ ] |
| 4.3 | Respects cap | Doesn't exceed max | [ ] |
| 4.4 | Ad refill works | Grants energy | [ ] |

---

## 5. Session Anti-Cheat

| # | Test | Expected | Pass |
|---|------|----------|------|
| 5.1 | Session on server | Doc created | [ ] |
| 5.2 | Result validated | Server calculates reward | [ ] |
| 5.3 | Duplicate blocked | Only first submit | [ ] |

---

## 6. Leaderboard & Social

| # | Test | Expected | Pass |
|---|------|----------|------|
| 6.1 | Score submitted | On leaderboard | [ ] |
| 6.2 | Rank correct | Position accurate | [ ] |
| 6.3 | Daily streak | Rewards granted | [ ] |

---

## 7. Platform Specific

### iOS

| # | Test | Expected | Pass |
|---|------|----------|------|
| 7.1 | ATT prompt | Shows correctly | [ ] |
| 7.2 | Restore purchases | All restored | [ ] |
| 7.3 | Background resume | Works correctly | [ ] |

### Android

| # | Test | Expected | Pass |
|---|------|----------|------|
| 7.4 | Back button | Correct per screen | [ ] |
| 7.5 | App switch | Resumes correctly | [ ] |

---

## Release Criteria

### MUST PASS (Blocking)
- [ ] All Section 1 (IAP)
- [ ] Section 2.1-2.3 (Ad rewards granted)
- [ ] Section 3 (Data integrity)
- [ ] Section 4 (Energy)
- [ ] Section 5 (Anti-cheat)

### Should Pass (Non-blocking)
- [ ] Section 6 (Leaderboard)
- [ ] Section 7 (Platform-specific)

---

## Sign-Off

| Role | Name | Date |
|------|------|------|
| QA | | |
| Dev Lead | | |
| Product | | |

| Field | Value |
|-------|-------|
| Build | v_____ |
| Platform | [ ] iOS [ ] Android |
| Result | [ ] RELEASE [ ] BLOCK |
| Blocking Issues | |
