# G-Roll Smoke Test Checklist

> **Purpose**: Quick 5-10 minute verification after every build
> **When**: After each build, before any further testing
> **Duration**: 5-10 minutes

---

## 1. App Launch & Auth

| # | Test | Expected | Pass |
|---|------|----------|------|
| 1.1 | App launches | Splash screen, no crash | [ ] |
| 1.2 | Firebase initializes | No error logs | [ ] |
| 1.3 | Login completes | Main menu loads | [ ] |
| 1.4 | User data loads | Currency/energy visible | [ ] |

---

## 2. Main Menu

| # | Test | Expected | Pass |
|---|------|----------|------|
| 2.1 | UI elements visible | All buttons, displays work | [ ] |
| 2.2 | Currency display | Shows coins/diamonds | [ ] |
| 2.3 | Energy display | Shows current energy | [ ] |
| 2.4 | Shop opens | Products visible | [ ] |

---

## 3. Core Gameplay

| # | Test | Expected | Pass |
|---|------|----------|------|
| 3.1 | Start session | Map loads, player spawns | [ ] |
| 3.2 | Player control | Responds to input | [ ] |
| 3.3 | Collect coin | Counter increases | [ ] |
| 3.4 | Hit obstacle | Death/damage works | [ ] |
| 3.5 | Session ends | Level end screen shows | [ ] |
| 3.6 | Rewards granted | Balance updated | [ ] |
| 3.7 | Return to menu | Navigation works | [ ] |

---

## 4. Quick Checks

| # | Test | Expected | Pass |
|---|------|----------|------|
| 4.1 | No crashes | Stable throughout | [ ] |
| 4.2 | No red console errors | Clean logs | [ ] |
| 4.3 | IAP products load | Prices visible in shop | [ ] |
| 4.4 | Ad button visible | When applicable | [ ] |

---

## Result

| Field | Value |
|-------|-------|
| Date | |
| Build | v_____ |
| Platform | [ ] iOS [ ] Android |
| Result | [ ] PASS [ ] FAIL |
| Notes | |

**If FAIL: Stop testing, report to developer immediately.**
