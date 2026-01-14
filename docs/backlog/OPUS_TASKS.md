# Opus 4.5 Task Backlog

> **Purpose**: Pre-approved tasks for AI-assisted development
> **Status**: Tasks here are vetted and safe for Opus to work on
> **Updated**: 2025-01-12

---

## How to Use This Document

1. **Pick a task** from the appropriate priority level
2. **Follow CLAUDE.md** guidelines for implementation
3. **Create a PR** following the template
4. **Mark task as done** when merged

---

## Priority Levels

| Priority | Description | Typical Turnaround |
|----------|-------------|-------------------|
| P0 | Critical/Blocking | Same day |
| P1 | High priority | 1-2 days |
| P2 | Medium priority | This week |
| P3 | Low priority/Nice-to-have | When time permits |

---

## P1: High Priority Tasks

### 1.1 Add Unit Tests for Core Services (Level 2)

**Description**: Add unit tests for non-critical network services.

**Files to Create**:
```
Assets/Tests/Editor/
├── AchievementServiceTests.cs
├── TaskServiceTests.cs
├── LeaderboardServiceTests.cs
└── StreakServiceTests.cs
```

**Acceptance Criteria**:
- [ ] Tests use Unity Test Framework
- [ ] Each service has at least 3-5 test cases
- [ ] Tests cover success and failure paths
- [ ] Tests can run without network (mocked)

**Estimated Effort**: Medium

---

### 1.2 Improve Error Messages in UI (Level 2)

**Description**: Replace generic error messages with user-friendly ones.

**Files to Modify**:
```
Assets/_Game Assets/Scripts/UI/UIShopPanel.cs
Assets/_Game Assets/Scripts/UI/UILoginPanel.cs
Assets/_Game Assets/Scripts/UI/UISessionGate.cs
```

**Acceptance Criteria**:
- [ ] Network errors show "Check your connection"
- [ ] Server errors show "Something went wrong. Try again."
- [ ] No technical jargon in user-facing messages

**Estimated Effort**: Small

---

### 1.3 Add Loading States to UI Panels (Level 2)

**Description**: Show loading indicators during async operations.

**Files to Modify**:
```
Assets/_Game Assets/Scripts/UI/UIShopPanel.cs
Assets/_Game Assets/Scripts/UI/UILeaderboardDisplay.cs
Assets/_Game Assets/Scripts/UI/UIAchievementsPanelController.cs
```

**Acceptance Criteria**:
- [ ] Loading spinner visible during fetch
- [ ] UI disabled during loading (prevent double-tap)
- [ ] Graceful handling if load fails

**Estimated Effort**: Small

---

## P2: Medium Priority Tasks

### 2.1 Refactor Player Movement (Level 2)

**Description**: Extract movement logic for better testability.

**Files to Modify**:
```
Assets/_Game Assets/Scripts/Player/PlayerMovement.cs
Assets/_Game Assets/Scripts/Player/PlayerController.cs
```

**Acceptance Criteria**:
- [ ] Movement logic in separate testable class
- [ ] No behavior changes
- [ ] Existing gameplay unaffected

**Estimated Effort**: Medium

---

### 2.2 Add Analytics Events (Level 2)

**Description**: Track key user actions for analytics.

**Events to Add**:
- Session start/end
- Level complete
- Shop open
- IAP initiated (not completed - that's Level 0)
- Ad watched

**Acceptance Criteria**:
- [ ] Events fire at correct moments
- [ ] Event names follow convention
- [ ] No PII in event data

**Estimated Effort**: Medium

---

### 2.3 Optimize Object Pooling (Level 2)

**Description**: Review and optimize object pool usage.

**Files to Review**:
```
Assets/_Game Assets/Scripts/Managers/ObjectPoolingManager.cs
Assets/_Game Assets/Scripts/Entities/*.cs
```

**Acceptance Criteria**:
- [ ] All frequently spawned objects use pooling
- [ ] Pool sizes tuned based on usage
- [ ] No memory leaks from pool

**Estimated Effort**: Medium

---

### 2.4 Document Map Designer Tools (Level 2)

**Description**: Add documentation for internal map editor.

**Files to Create**:
```
docs/tools/MAP_DESIGNER.md
```

**Acceptance Criteria**:
- [ ] How to use each tool
- [ ] Keyboard shortcuts
- [ ] Export/import process

**Estimated Effort**: Small

---

## P3: Low Priority Tasks

### 3.1 UI Polish - Animations (Level 2)

**Description**: Add subtle animations to UI transitions.

**Files to Modify**:
```
Assets/_Game Assets/Scripts/UI/UIFadePanel.cs
Assets/_Game Assets/Scripts/UI/UICurrencyDisplay.cs
```

**Acceptance Criteria**:
- [ ] Smooth fade transitions
- [ ] Currency change animation
- [ ] No performance impact

**Estimated Effort**: Small

---

### 3.2 Add Code Comments to Complex Methods (Level 2)

**Description**: Document non-obvious code sections.

**Priority Files**:
```
Assets/_Game Assets/Scripts/Controllers/Map.cs
Assets/_Game Assets/Scripts/Utility/ProbabilityTable.cs
```

**Acceptance Criteria**:
- [ ] Comments explain WHY not WHAT
- [ ] Complex algorithms documented
- [ ] No redundant obvious comments

**Estimated Effort**: Small

---

### 3.3 Audit Console Logs (Level 2)

**Description**: Remove debug logs, standardize error logging.

**Acceptance Criteria**:
- [ ] No Debug.Log in release code
- [ ] Debug.LogError for actual errors
- [ ] Consistent log format: `[ClassName] message`

**Estimated Effort**: Small

---

## Completed Tasks

| Task | Completed | PR |
|------|-----------|-----|
| - | - | - |

---

## Notes for Opus

When picking a task:
1. Check that files are Level 2 (Safe)
2. Follow the mandatory response format from CLAUDE.md
3. Create focused PRs (one task = one PR)
4. Run mental smoke test before submitting

---

*Last updated: 2025-01-12*
