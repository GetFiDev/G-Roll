# G-Roll Technical Debt Registry

> **Purpose**: Track known technical issues and improvement opportunities
> **Status**: Living document, update as debt is added/resolved
> **Updated**: 2025-01-12

---

## Severity Levels

| Level | Description | Action |
|-------|-------------|--------|
| Critical | Affects revenue/data | Fix immediately |
| High | Affects stability/UX | Fix this sprint |
| Medium | Maintainability issue | Plan to fix |
| Low | Nice to have | When time permits |

---

## Critical Debt

### None currently identified

*This is good! Keep monitoring.*

---

## High Severity Debt

### H1: UserDatabaseManager.cs is Monolithic (60KB)

**Location**: `Assets/_Game Assets/Scripts/Networks/UserDatabaseManager.cs`

**Problem**: Single file handling too many responsibilities:
- User profile
- Inventory
- Stats
- Energy
- Multiple data syncs

**Risk**: Hard to maintain, easy to introduce bugs, difficult to test.

**Suggested Fix**:
- Extract into domain-specific managers
- UserProfileManager, UserInventoryManager, UserStatsManager
- Use composition pattern

**Effort**: Large (needs careful refactoring)
**Access Level**: Level 1 - Requires extra caution
**Status**: 🔴 Open

---

### H2: No Unit Tests for Network Services

**Location**: `Assets/_Game Assets/Scripts/Networks/`

**Problem**: 27 network services with no automated tests.

**Risk**: Regressions go unnoticed until production.

**Suggested Fix**:
- Add Unity Test Framework tests
- Mock Firestore calls
- Test success and failure paths

**Effort**: Medium (ongoing)
**Access Level**: Level 2 - Safe
**Status**: 🔴 Open

---

### H3: No Backend Function Tests

**Location**: `functions/src/modules/`

**Problem**: 14 function modules with no automated tests.

**Risk**: Backend changes can break client without warning.

**Suggested Fix**:
- Add Jest/Mocha tests
- Use Firebase emulator for integration tests
- Cover critical paths (IAP, session, energy)

**Effort**: Medium (ongoing)
**Access Level**: Level 2 for non-critical, Level 1 for critical
**Status**: 🔴 Open

---

## Medium Severity Debt

### M1: Inconsistent Async Patterns

**Location**: Various scripts

**Problem**: Mix of Coroutines and UniTask.

**Risk**: Confusion, harder to maintain.

**Suggested Fix**:
- Standardize on UniTask
- Migrate Coroutines gradually

**Effort**: Medium
**Status**: 🟡 Acknowledged

---

### M2: Hardcoded Strings in UI

**Location**: `Assets/_Game Assets/Scripts/UI/*.cs`

**Problem**: User-facing strings hardcoded instead of localized.

**Risk**: Makes localization difficult.

**Suggested Fix**:
- Create localization system
- Move strings to resource files

**Effort**: Medium
**Status**: 🟡 Acknowledged

---

### M3: Missing XML Documentation

**Location**: All C# files

**Problem**: Public APIs lack XML documentation.

**Risk**: Hard for new developers (and AI) to understand contracts.

**Suggested Fix**:
- Add XML docs to public methods
- Focus on Network services first

**Effort**: Low (ongoing)
**Status**: 🟡 Acknowledged

---

### M4: Firebase SDK Vulnerabilities

**Location**: Dependencies

**Problem**: Dependabot shows 9 vulnerabilities (6 high, 3 moderate).

**Risk**: Security vulnerabilities in dependencies.

**Suggested Fix**:
- Review and update dependencies
- Check compatibility before updating

**Effort**: Medium
**Status**: 🟡 Acknowledged

---

## Low Severity Debt

### L1: Console Log Cleanup

**Problem**: Debug.Log statements in production code.

**Suggested Fix**: Audit and remove or convert to conditional logging.

**Status**: 🟢 Low priority

---

### L2: Magic Numbers in Gameplay

**Location**: `Assets/_Game Assets/Scripts/Entities/*.cs`

**Problem**: Speed, timing, and other values hardcoded.

**Suggested Fix**: Move to ScriptableObjects or constants file.

**Status**: 🟢 Low priority

---

### L3: Unused Code Cleanup

**Problem**: Some files may contain dead code.

**Suggested Fix**: Static analysis to identify unused methods.

**Status**: 🟢 Low priority

---

## Resolved Debt

| ID | Description | Resolved Date | PR |
|----|-------------|---------------|-----|
| - | - | - | - |

---

## Adding New Debt

When adding new debt:
1. Assign severity level
2. Identify location and files
3. Describe problem and risk
4. Suggest fix approach
5. Note access level of affected files

---

*Last updated: 2025-01-12*
