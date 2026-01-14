# Lead Developer Review Checklist

> **Purpose**: Items to verify with the lead developer
> **Context**: These items were identified during AI pipeline setup
> **Action**: Review with lead dev and update relevant documents

---

## Pipeline Documents Created

The following documents were created and need lead developer review:

| Document | Location | Status |
|----------|----------|--------|
| CLAUDE.md | `/CLAUDE.md` | ⏳ Needs Review |
| COMPONENTS.md | `/docs/architecture/COMPONENTS.md` | ⏳ Needs Review |
| FLOWS.md | `/docs/architecture/FLOWS.md` | ⏳ Needs Review |
| CRITICAL_SURFACES.md | `/docs/qa/CRITICAL_SURFACES.md` | ⏳ Needs Review |
| SMOKE.md | `/docs/qa/SMOKE.md` | ⏳ Needs Review |
| CRITICAL.md | `/docs/qa/CRITICAL.md` | ⏳ Needs Review |
| PR Template | `/.github/pull_request_template.md` | ⏳ Needs Review |
| CODEOWNERS | `/.github/CODEOWNERS` | ⏳ Needs Review |

---

## Questions for Lead Developer

### 1. Access Level Verification

**Question**: Are the following files correctly classified?

#### Level 0 (Hard Lock) - Confirm these should NEVER be touched by AI:
- [ ] `IAPManager.cs` - Correct?
- [ ] `IAPRemoteService.cs` - Correct?
- [ ] `iap.functions.ts` - Correct?
- [ ] `FirebaseLoginHandler.cs` - Correct?
- [ ] `firebase.ts` - Correct?
- [ ] `build.yml` - Correct?
- [ ] `Fastfile` - Correct?

**Any files to add to Level 0?**
```
_______________________________________________
```

#### Level 1 (Guarded) - Confirm line limits:
- [ ] `AdManager.cs` (50 lines max) - Correct?
- [ ] `UserDatabaseManager.cs` (30 lines max) - Correct?
- [ ] `CurrencyManager.cs` (30 lines max) - Correct?

**Any files to add/remove from Level 1?**
```
_______________________________________________
```

---

### 2. Build & CI/CD

**Question**: What is the current build process?

- Build trigger pattern: `build:` prefix? Other?
  ```
  _______________________________________________
  ```

- Test command (if any):
  ```
  _______________________________________________
  ```

- Any additional CI/CD files not covered?
  ```
  _______________________________________________
  ```

---

### 3. Testing Infrastructure

**Question**: What testing exists currently?

- [ ] Unity Test Framework setup?
- [ ] Backend tests (Jest/Mocha)?
- [ ] Integration tests?
- [ ] Test commands to run:
  ```
  _______________________________________________
  ```

---

### 4. CODEOWNERS Configuration

**Question**: GitHub username for CODEOWNERS file?

Current placeholder: `@lead-developer`

Replace with:
```
_______________________________________________
```

Any additional owners to add?
```
_______________________________________________
```

---

### 5. Flow Diagram Accuracy

**Question**: Review FLOWS.md for accuracy.

Key flows to verify:
- [ ] App Startup Flow - Accurate?
- [ ] IAP Purchase Flow - Accurate?
- [ ] Session Flow - Accurate?
- [ ] Energy System Flow - Accurate?

Corrections needed:
```
_______________________________________________
_______________________________________________
```

---

### 6. Missing Components

**Question**: Any major components missing from COMPONENTS.md?

Review the architecture document and note any:
- Missing managers
- Missing services
- Missing integrations
- Incorrect descriptions

```
_______________________________________________
_______________________________________________
```

---

### 7. QA Checklists

**Question**: Review SMOKE.md and CRITICAL.md.

- [ ] SMOKE.md covers essential quick checks?
- [ ] CRITICAL.md covers all revenue-critical tests?
- [ ] Any tests to add?

```
_______________________________________________
```

---

### 8. Technical Debt Priorities

**Question**: Review TECHNICAL_DEBT.md priorities.

Confirm severity levels:
- [ ] H1 (UserDatabaseManager size) - High priority?
- [ ] H2 (No unit tests) - High priority?
- [ ] H3 (No backend tests) - High priority?

Any other critical debt to add?
```
_______________________________________________
```

---

## Action Items After Review

After lead developer review, update:

1. [ ] CRITICAL_SURFACES.md - If access levels change
2. [ ] CLAUDE.md - If rules change
3. [ ] CODEOWNERS - With real GitHub usernames
4. [ ] FLOWS.md - If flows are incorrect
5. [ ] COMPONENTS.md - If components are missing

---

## Sign-Off

| Role | Name | Date | Signature |
|------|------|------|-----------|
| Lead Developer | | | |
| CEO/Product | | | |

---

*Created: 2025-01-12*
