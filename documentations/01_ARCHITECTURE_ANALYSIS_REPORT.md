# G-Roll Proje Mimari Analiz Raporu

**Tarih:** Ocak 2026
**Analiz Edilen Dosya Sayısı:** 210+ C# Script
**Sahneler:** Boot Scene, Game Scene, MapDesignerScene
**Teknolojiler:** Unity 2022+, Firebase (Auth, Firestore, Functions), DOTween

---

## EXECUTIVE SUMMARY

Bu rapor, G-Roll Unity mobil oyun projesinin kapsamlı bir mimari analizini içermektedir. Proje, tek oyunculu endless runner/chapter-based bir oyun olup Firebase backend ile entegre çalışmaktadır.

### Kritik Bulgular

| Seviye | Sayı | Açıklama |
|--------|------|----------|
| **KRITIK** | 8 | Acil müdahale gerektiren mimari sorunlar |
| **YÜKSEK** | 12 | Kısa vadede çözülmesi gereken sorunlar |
| **ORTA** | 15 | Orta vadede iyileştirme gerektiren alanlar |
| **DÜŞÜK** | 10+ | Uzun vadeli refactoring önerileri |

### Temel Sorunlar Özeti
1. **Singleton Anti-Pattern Overuse:** 15+ Singleton class
2. **God Classes:** PlayerMovement (1014 satır), GameplayManager (752 satır)
3. **Tight Coupling:** Manager'lar arası doğrudan bağımlılık
4. **Inconsistent Event System:** Mixed event patterns
5. **No Dependency Injection:** Manual wiring everywhere
6. **Firebase Hard-coded:** No abstraction layer
7. **Chaotic UI Architecture:** No navigation framework

---

## BÖLÜM 1: PROJE YAPISI ANALİZİ

### 1.1 Klasör Organizasyonu

```
Assets/_Game Assets/Scripts/
├── Controllers/          # Game flow controllers, interfaces
├── Data/                 # Data types (MapDataTypes)
├── Debug/                # SRDebugger integration
├── Editor/               # Unity Editor tools
├── Entities/             # Game objects (Collectables, Obstacles)
│   ├── Collectables/     # Coin, Boosters
│   └── Obstacles/        # Wall, Piston, Laser, etc.
├── Gameplay/             # ReviveController
├── Managers/             # 20+ Manager classes (PROBLEM)
├── MapDesignerTools/     # Level editor system
├── Networks/             # Firebase services (16 files)
├── Player/               # PlayerController, Movement, Animator
├── Tools/                # Debug tools
├── UI/                   # 60+ UI panels (MASSIVE)
├── Utilities/            # Duplicate folder!
└── Utility/              # Currency, EventHub, tools
    ├── Currency/         # Currency system
    ├── Editor/           # DevTools
    └── UI/               # UI utilities
```

### 1.2 Kritik Yapısal Sorunlar

#### SORUN #1: Duplicate Utility Folders
```
Scripts/Utilities/  ← Boş veya minimal
Scripts/Utility/    ← Ana utility kodları
```
**Impact:** Kod organizasyonu karışıklığı, yeni geliştiriciler için kafa karışıklığı

#### SORUN #2: Managers Klasörü Şişkinliği
20+ Manager class tek klasörde:
- AppManager, AppFlowManager, GameManager, GameplayManager
- UIManager, TouchManager, MapManager
- AdManager, IAPManager, ShopItemManager
- BootManager, DataManager, HapticManager
- NotificationManager, ReviewManager, ObjectPoolingManager
- NetworkConnectionMonitor, BackgroundMusicManager
- GameplayCameraManager, NotificationBadgeManager

**Öneri:** Domain-based folder structure (Auth/, Gameplay/, Economy/, etc.)

---

## BÖLÜM 2: SINGLETON VE GLOBAL STATE ANALİZİ

### 2.1 Mevcut Singleton'lar

| Class | Type | Scope | Risk |
|-------|------|-------|------|
| GameManager | Manual Instance | Scene | HIGH |
| GameplayManager | Manual Instance | Scene | HIGH |
| UIManager | MonoSingleton<T> | Scene | MEDIUM |
| AppManager | MonoSingleton<T> | DontDestroy | MEDIUM |
| AppFlowManager | Manual Instance | DontDestroy | HIGH |
| UserDatabaseManager | Manual Instance | DontDestroy | HIGH |
| UserInventoryManager | Manual Instance | Scene | HIGH |
| PlayerStatsRemoteService | Manual Instance | Scene | MEDIUM |
| LeaderboardManager | Unknown | Scene | MEDIUM |
| BackgroundMusicManager | Unknown | Scene | LOW |
| NetworkConnectionMonitor | Unknown | Scene | MEDIUM |
| GameplayCameraManager | Unknown | Scene | MEDIUM |
| NotificationBadgeManager | Unknown | Scene | LOW |
| UITopPanel | Manual Instance | Scene | LOW |
| UIBottomPanel | Manual Instance | Scene | LOW |

### 2.2 Singleton Implementation Patterns

**Pattern 1: Manual Instance (Problematic)**
```csharp
// GameManager.cs - Lines 13-17
public static GameManager Instance;
void Awake()
{
    if (Instance == null) Instance = this;
    else { Destroy(gameObject); return; }
}
```
**Sorunlar:**
- Race condition riski (multiple Awake calls)
- No thread safety
- Instance null check her kullanımda gerekli
- Destroy sırası belirsiz

**Pattern 2: MonoSingleton<T> (Better but still problematic)**
```csharp
// MonoSingleton.cs - Plugins/Tools/Utility/
public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
{
    private static T _mInstance;
    public static T Instance { get => ... } // FindAnyObjectByType fallback
}
```
**Sorunlar:**
- FindAnyObjectByType performans maliyeti
- Temporary instance creation (unexpected behavior)
- Still global mutable state

### 2.3 Singleton Kullanım Dağılımı

67 dosyada `.Instance` referansı bulundu. Bu, tight coupling'in somut kanıtıdır.

**En Çok Referans Edilen:**
1. `GameplayManager.Instance` - 25+ location
2. `UIManager.Instance` - 40+ location
3. `UserDatabaseManager.Instance` - 30+ location
4. `GameManager.Instance` - 15+ location

---

## BÖLÜM 3: GAME FLOW VE STATE MANAGEMENT

### 3.1 Phase System

```csharp
// GamePhase.cs
public enum GamePhase
{
    Boot,      // Firebase initialization
    Meta,      // Main menu, shop, settings
    Gameplay   // Active game session
}
```

**Flow Diagram:**
```
[Boot Scene]
    │
    ▼
AppManager.Start()
    │
    ├── ReviewManager.Initialize()
    ├── HapticManager.Initialize()
    ├── NotificationManager.Initialize()
    ├── AdManager.Initialize()
    │
    ▼
BootManager.ContinueToGameScene()
    │
    ▼
[Game Scene]
    │
    ▼
AppFlowManager.RunBootSequenceAsync()
    │
    ├── Firebase.CheckAndFixDependenciesAsync()
    ├── AuthCheck
    │   ├── Already Logged → ProfileCheck
    │   └── Not Logged → ShowLoginUI()
    │
    ├── ProfileCheck
    │   ├── Complete → GameDataLoad
    │   └── Incomplete → ShowSetNameUI()
    │
    └── GameDataLoad
        ├── UserStatManager.RefreshAllAsync()
        ├── UserInventoryManager.InitializeAsync()
        └── GameManager.Initialize() → Meta Phase
```

### 3.2 Kritik Flow Sorunları

#### SORUN #3: Non-Deterministic Initialization Order
```csharp
// AppManager.cs:17-21
private void Start()
{
    ReviewManager.Initialize();
    HapticManager.Initialize();
    NotificationManager.Initialize();
    AdManager.Initialize();
    BootManager.ContinueToGameScene();
}
```
**Sorunlar:**
- Hard-coded sıra
- Failure handling yok
- Async initialization yok (blocking)
- Dependency graph yok

#### SORUN #4: Race Condition Risk
```csharp
// AppFlowManager.cs:63-65
if (UserDatabaseManager.Instance == null)
{
    LogError("UserDatabaseManager missing in scene!");
    return;
}
```
- Scene loading sırasında Instance henüz set edilmemiş olabilir
- Awake order belirsiz

### 3.3 Event Channel System

**ScriptableObject Event Channels:**
```csharp
// VoidEventChannelSO.cs
public class VoidEventChannelSO : ScriptableObject
{
    public event Action OnEvent;
    public void Raise() => OnEvent?.Invoke();
}

// PhaseEventChannelSO.cs
public class PhaseEventChannelSO : ScriptableObject
{
    public event Action<GamePhase> OnEvent;
    public void Raise(GamePhase phase) => OnEvent?.Invoke(phase);
}
```

**Kullanım (Manual Subscribe/Unsubscribe):**
```csharp
// GameManager.cs:23-33
private void OnEnable()
{
    if (requestStartGameplay != null) requestStartGameplay.OnEvent += OnRequestStartGameplay;
    if (requestReturnToMeta != null) requestReturnToMeta.OnEvent += OnRequestReturnToMeta;
}

private void OnDisable()
{
    if (requestStartGameplay != null) requestStartGameplay.OnEvent -= OnRequestStartGameplay;
    if (requestReturnToMeta != null) requestReturnToMeta.OnEvent -= OnRequestReturnToMeta;
}
```

#### SORUN #5: Memory Leak Risk
- 95+ dosyada manual subscribe/unsubscribe pattern
- Unsubscribe unutulursa memory leak
- Inspector'dan atanmazsa NullReferenceException

---

## BÖLÜM 4: GOD CLASS ANALİZİ

### 4.1 PlayerMovement.cs - 1014 Satır

**Single Responsibility Violation:**
Bu class şu sorumlulukları tek başına taşıyor:
1. Movement (Move, ChangeDirection)
2. Rotation (RotateTowardsMovement)
3. Jump System (Jump, JumpCustom)
4. Teleport System (RequestTeleport, TeleportRoutine)
5. Wall Hit Feedback (WallHitFeedback, WallHitBounceSequence)
6. Coasting (StartCoasting, UpdateCoasting, StopCompletely)
7. Intro Animation (PlayIntroSequence)
8. Input Handling (HandleSwipe, HandleDoubleTap)
9. State Management (_isFrozen, _isCoasting, teleportInProgress)
10. External Stats Application (SetExternalAccelerationPer60Sec, SetPlayerSize)

**State Variables (Karmaşıklık Göstergesi):**
```csharp
private bool _isFrozen = false;
private bool _isCoasting = false;
private bool _isIntroPlaying = false;
private bool teleportInProgress = false;
private bool IsJumping { get; private set; }
private bool _firstMoveNotified = false;
private SwipeDirection? _queuedPortalExitDirection = null;
```

**Önerilen Bölünme:**
```
PlayerMovement/
├── PlayerMovementController.cs  (Main orchestration)
├── PlayerJumpBehavior.cs        (Jump mechanics)
├── PlayerTeleportBehavior.cs    (Teleport mechanics)
├── PlayerWallHitHandler.cs      (Wall collision feedback)
├── PlayerCoastingBehavior.cs    (Coasting mechanics)
├── PlayerInputHandler.cs        (Swipe/Tap handling)
└── PlayerIntroBehavior.cs       (Intro sequence)
```

### 4.2 GameplayManager.cs - 752 Satır

**Sorumluluklar:**
1. Session lifecycle (BeginSession, EndSession)
2. Player spawning coordination
3. Map loading coordination
4. Score/Currency tracking
5. Revive system
6. UI coordination
7. Server submission
8. Stats caching

**Circular Dependencies:**
```
GameplayManager → UIManager.Instance
GameplayManager → UserDatabaseManager.Instance
GameplayManager → logicApplier (injected)
UIManager → GameplayManager.Instance (for revive)
```

### 4.3 UserDatabaseManager.cs - 1624 Satır

**Sorumluluklar:**
1. Firebase Auth (Login, Register, Social Login)
2. Firestore CRUD (LoadUserData, SaveUserData)
3. Profile Completion
4. Referral System
5. Leaderboard Queries
6. Energy System
7. Ad Claims
8. Chapter Map Fetching
9. Session Submission
10. Main Thread Dispatching

**SORUN #6: Too Many Responsibilities**
Bu class, en az 5 ayrı service'e bölünmeli:
- AuthService
- UserProfileService
- LeaderboardService
- ReferralService
- SessionService

---

## BÖLÜM 5: NETWORK LAYER ANALİZİ

### 5.1 Firebase Integration Architecture

```
Networks/
├── UserDatabaseManager.cs      # Auth + Firestore (GOD CLASS)
├── SessionRemoteService.cs     # Session request/submit (static)
├── PlayerStatsRemoteService.cs # Player equipment stats
├── InventoryRemoteService.cs   # Inventory operations
├── IAPRemoteService.cs         # In-app purchases
├── LeaderboardService.cs       # Leaderboard queries
├── AchievementService.cs       # Achievement tracking
├── ElitePassService.cs         # Season pass
├── ReferralRemoteService.cs    # Referral system
├── UserEnergyService.cs        # Energy system
├── AutopilotService.cs         # Autopilot feature
├── MapManager.cs               # Map loading
└── NetworkConnectionMonitor.cs # Connection status
```

### 5.2 Firebase Direct Usage (No Abstraction)

```csharp
// SessionRemoteService.cs:11
private static FirebaseFunctions Fn =>
    FirebaseFunctions.GetInstance(FirebaseApp.DefaultInstance, "us-central1");
```

**Sorunlar:**
1. Firebase SDK doğrudan kullanılıyor
2. Mock/test edilemiyor
3. Backend değişikliği tüm kodu etkiler
4. Region hard-coded

### 5.3 Async/Await Inconsistency

**Pattern 1: Proper async/await**
```csharp
// SessionRemoteService.cs:77
public static async Task<RequestSessionResponse> RequestSessionAsync(GameMode mode)
{
    try
    {
        var res = await call.CallAsync(...);
        return new RequestSessionResponse { ... };
    }
    catch (FunctionsException fex) { throw; }
}
```

**Pattern 2: Fire and Forget (Dangerous)**
```csharp
// UserDatabaseManager.cs:306
var syncFunc = FirebaseFunctions.DefaultInstance.GetHttpsCallable("syncUserEmail");
_ = syncFunc.CallAsync(); // Fire and forget to not block UI
```

**Pattern 3: Mixed await/result check**
```csharp
// GameManager.cs:119-136
var task = SessionRemoteService.RequestSessionAsync(mode);
try { await task; }
catch (Exception ex) { Debug.LogWarning(...); }

if (task.IsFaulted || task.IsCanceled) // Checking AFTER await
{
    ShowInsufficientEnergyPanel();
    return false;
}
```
Bu pattern yanlış: await sonrası IsFaulted/IsCanceled kontrol etmek gereksiz çünkü exception zaten catch edildi.

### 5.4 Main Thread Safety

```csharp
// UserDatabaseManager.cs:38-40
private readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();
private void EnqueueMain(Action a) { if (a != null) _mainThreadQueue.Enqueue(a); }
private void Update() { while (_mainThreadQueue.TryDequeue(out var a)) a?.Invoke(); }
```

**Sorunlar:**
- Update() polling (her frame çalışıyor)
- UniTask kullanılmıyor
- Coroutine bazlı alternatif yok

**Öneri:** UniTask veya MainThreadDispatcher pattern

---

## BÖLÜM 6: UI SYSTEM ANALİZİ

### 6.1 UI Panel Sayısı

60+ UI Panel bulundu:
- UIMainMenu, UIHomePanel
- UIGamePlay (HUD)
- UILevelEnd
- UILoginPanel, UISetNamePanel
- UIShopPanel, UIIAPShopPanel
- UIProfileAndSettingsPanel
- UILeaderboardDisplay, UIRankingPanel
- UITaskPanel, UIAchievementsPanel
- UIEnergyDisplay, UICurrencyDisplay
- UIElitePassPanel
- UISessionGate
- ... ve daha fazlası

### 6.2 UIManager Architecture

```csharp
// UIManager.cs
public class UIManager : MonoSingleton<UIManager>
{
    public UIMainMenu mainMenu;
    public UIGamePlay gamePlay;
    public UILevelEnd levelEnd;
    public UIOverlay overlay;
    public ProfileAndSettingsPanel profileAndSettingsPanel;
    public UINewHighScorePanel newHighScorePanel;
    public UIGameplayLoading gameplayLoading;
    public InsufficientEnergyPanel insufficientEnergyPanel;
    public UIIAPShopPanel iapShopPanel;

    // Phase-based visibility
    private void OnPhaseChanged(GamePhase phase) { ... }
}
```

### 6.3 Kritik UI Sorunları

#### SORUN #7: No Navigation Framework
- Back button handling yok
- Navigation stack yok
- Deep linking yok
- Panel lifecycle inconsistent

#### SORUN #8: UIFadePanel Dynamic Addition
```csharp
// UIManager.cs:68-69
var fp = comp.GetComponent<UIFadePanel>();
if (fp == null) fp = comp.gameObject.AddComponent<UIFadePanel>();
```
Runtime'da component ekleniyor - dirty pattern

#### SORUN #9: Duplicate Fade Helper
`Fade()` helper method 4 kez UIManager içinde tekrar tanımlanmış (Lines 66, 120, 146, 165).

### 6.4 Önerilen UI Architecture

```
UI/
├── Core/
│   ├── UINavigationController.cs
│   ├── UIScreenBase.cs
│   └── UIPopupBase.cs
├── Screens/
│   ├── HomeScreen/
│   ├── ShopScreen/
│   └── GameplayScreen/
├── Popups/
│   ├── SettingsPopup/
│   └── PurchasePopup/
└── Components/
    ├── CurrencyDisplay/
    └── EnergyBar/
```

---

## BÖLÜM 7: EVENT SYSTEM ANALİZİ

### 7.1 Mevcut Event Patterns

**Pattern 1: ScriptableObject Event Channels**
```csharp
[SerializeField] private VoidEventChannelSO requestStartGameplay;
requestStartGameplay.OnEvent += Handler;
```

**Pattern 2: C# Events (Static)**
```csharp
// GameplayManager.cs:11
public static event Action<string> OnCollectibleNotification;
```
Risk: Memory leak, subscriber'lar unsubscribe unutabilir

**Pattern 3: C# Events (Instance)**
```csharp
// UserDatabaseManager.cs:25-30
public event Action<string> OnLog;
public event Action OnRegisterSucceeded;
public event Action<string> OnRegisterFailed;
```

**Pattern 4: EventHub (Unused)**
```csharp
// EventHub.cs - Generic event hub EXISTS but NOT USED
public class EventHub<TKey, TPayload>
{
    private readonly Dictionary<TKey, Action<TPayload>> _eventTable = new();
}
```

### 7.2 Event System Karmaşıklığı

| Pattern | Kullanım Sayısı | Risk |
|---------|-----------------|------|
| SO Event Channels | ~10 | MEDIUM (Inspector dependency) |
| Static Events | ~15 | HIGH (Memory leak) |
| Instance Events | ~30 | MEDIUM (Lifecycle issues) |
| EventHub | 0 | N/A (Unused) |
| Direct Calls | ~100+ | HIGH (Tight coupling) |

### 7.3 Önerilen Unified Event System

```csharp
// MessageBus.cs
public class MessageBus : IMessageBus
{
    public void Publish<T>(T message) where T : IMessage;
    public IDisposable Subscribe<T>(Action<T> handler) where T : IMessage;
}

// Usage
public class GameStartedMessage : IMessage { public string SessionId; }

// Publisher
_messageBus.Publish(new GameStartedMessage { SessionId = "..." });

// Subscriber (auto-dispose with IDisposable)
_subscription = _messageBus.Subscribe<GameStartedMessage>(OnGameStarted);
```

---

## BÖLÜM 8: PLAYER SYSTEM ANALİZİ

### 8.1 Player Component Structure

```
Player GameObject
├── PlayerController.cs      # Main orchestrator
├── PlayerMovement.cs        # Movement logic (1014 lines!)
├── PlayerAnimator.cs        # Animation control
├── PlayerStatHandler.cs     # Equipment stats (JSON-based)
├── PlayerCollision.cs       # Collision handling
└── PlayerMagnet (child)     # Coin magnet
```

### 8.2 Player State Machine (Implicit)

```
                    ┌─────────┐
                    │  IDLE   │
                    └────┬────┘
                         │ FirstMove
                         ▼
┌──────────┐       ┌─────────┐       ┌──────────┐
│ TELEPORT │◄─────►│ RUNNING │◄─────►│ JUMPING  │
└──────────┘       └────┬────┘       └──────────┘
                        │
        ┌───────────────┼───────────────┐
        ▼               ▼               ▼
   ┌─────────┐    ┌─────────┐    ┌──────────┐
   │ FROZEN  │    │COASTING │    │ WALL_HIT │
   └─────────┘    └─────────┘    └──────────┘
```

**Sorun:** Bu state machine implicit (boolean flags ile yönetiliyor). Explicit FSM yok.

### 8.3 Önerilen State Machine

```csharp
public interface IPlayerState
{
    void Enter(PlayerContext context);
    void Update(PlayerContext context);
    void Exit(PlayerContext context);
    IPlayerState HandleInput(PlayerContext context, PlayerInput input);
}

public class PlayerStateMachine
{
    private IPlayerState _currentState;

    public void ChangeState(IPlayerState newState)
    {
        _currentState?.Exit(_context);
        _currentState = newState;
        _currentState.Enter(_context);
    }
}
```

---

## BÖLÜM 9: DEPENDENCY INJECTION İHTİYACI

### 9.1 Mevcut Durum: Manual Wiring

```csharp
// GameplayManager.cs:69-70
playerSpawner = playerSpawnerBehaviour as IPlayerSpawner;
mapLoader = mapLoaderBehaviour as IMapLoader;
```

**Sorunlar:**
- Runtime cast failure riski
- Null check her yerde
- Test edilemez
- Circular dependency detection yok

### 9.2 Önerilen: VContainer/Zenject

```csharp
// GameInstaller.cs
public class GameInstaller : MonoInstaller
{
    public override void InstallBindings()
    {
        // Managers
        Container.Bind<IGameManager>().To<GameManager>().AsSingle();
        Container.Bind<ISessionService>().To<SessionService>().AsSingle();

        // Services
        Container.Bind<IAuthService>().To<FirebaseAuthService>().AsSingle();
        Container.Bind<ILeaderboardService>().To<FirebaseLeaderboardService>().AsSingle();

        // UI
        Container.Bind<INavigationService>().To<UINavigationService>().AsSingle();
    }
}

// GameplayManager.cs
public class GameplayManager : IGameplayManager
{
    [Inject] private readonly IPlayerSpawner _playerSpawner;
    [Inject] private readonly IMapLoader _mapLoader;
    [Inject] private readonly ISessionService _sessionService;
}
```

---

## BÖLÜM 10: CODE QUALITY ISSUES

### 10.1 Naming Convention Inconsistency

```csharp
// Mixed styles
public class UIMainMenu { }      // UI prefix
public class ProfileAndSettingsPanel { } // No prefix
public class UIGamePlay { }      // UI prefix
public class InsufficientEnergyPanel { } // Inconsistent

// Method naming
SetPhase() vs OnPhaseChanged()
RequestSessionAndStartAsync() vs BeginSessionRequested()
AddCoins() vs OnCollectibleNotification()
```

### 10.2 Silent Exception Handling

```csharp
// PlayerMovement.cs:793
catch (Exception) { /* sessizce geç */ }

// UserDatabaseManager.cs:1584
catch { /* Ignore parse errors, return empty */ }
```

### 10.3 Magic Numbers

```csharp
// PlayerMovement.cs
float groundY = 0.25f;  // Standard player height
float coastDuration = 1.5f;
float stopDuration = 0.5f;

// GameplayManager.cs
await Task.Delay(300);  // Why 300ms?
if (elapsed < 2.0f)     // Why 2 seconds?
```

### 10.4 Commented Out Code

```csharp
// GameManager.cs:37-39
// DISABLED: Now controlled by AppFlowManager.InitializeGameManager()
// SetPhase(GamePhase.Meta);
// StartCoroutine(FetchAndDebugItems());
```

---

## BÖLÜM 11: SECURITY CONCERNS

### 11.1 Client-Side Validation

```csharp
// GameManager.cs:99-116
if (mode == GameMode.Chapter)
{
    var energySnapshot = await UserEnergyService.FetchSnapshotAsync();
    if (energySnapshot.current < 1)
    {
        ShowInsufficientEnergyPanel();
        return false;
    }
}
```
Energy check client-side yapılıyor. Manipüle edilebilir. Server-side validation zorunlu.

### 11.2 Currency Manipulation Risk

```csharp
// UserDatabaseManager.cs:669-677
var patch = new Dictionary<string, object>
{
    { "mail", data.mail },
    { "username", data.username },
    { "currency", data.currency },  // Client writes currency!
    // ...
};
```
Firestore Security Rules ile kontrol edilmeli.

---

## BÖLÜM 12: PERFORMANCE CONCERNS

### 12.1 FindObjectByType Kullanımı

```csharp
// MonoSingleton.cs:18
_mInstance = FindAnyObjectByType(typeof(T)) as T;

// PlayerMovement.cs:170
var speedElement = FindFirstObjectByType<UISpeedElement>(FindObjectsInactive.Include);
```
Her frame çağrılmıyor ama yine de O(n) complexity.

### 12.2 Update Polling

```csharp
// UserDatabaseManager.cs:40
private void Update() { while (_mainThreadQueue.TryDequeue(out var a)) a?.Invoke(); }
```
Her frame çalışan polling pattern.

### 12.3 Object Pooling Eksikliği

`ObjectPoolingManager` var ama kullanımı sınırlı görünüyor. Özellikle:
- Coin'ler
- Particle effects
- UI elements

---

## BÖLÜM 13: REFACTORING ROADMAP

### Phase 1: Acil Müdahale (1-2 Hafta)

1. **Singleton Reduction**
   - GameManager, GameplayManager için interface tanımla
   - Static Instance yerine property injection hazırlığı

2. **God Class Split**
   - PlayerMovement → 5 ayrı component
   - UserDatabaseManager → 4 ayrı service

3. **Event System Unification**
   - EventHub'ı aktif et veya yeni MessageBus yaz
   - Static event'leri kaldır

### Phase 2: Yapısal İyileştirme (2-4 Hafta)

4. **DI Container Integration**
   - VContainer veya Zenject ekle
   - Interface-based dependency injection

5. **UI Navigation Framework**
   - UIScreenBase, UIPopupBase base classes
   - Navigation stack implementation

6. **Firebase Abstraction Layer**
   - IAuthService, IFirestoreService interfaces
   - Mock implementation for tests

### Phase 3: Polish (4-6 Hafta)

7. **State Machine Implementation**
   - Player state machine
   - Game phase state machine

8. **Testing Infrastructure**
   - Unit test framework
   - Integration test setup
   - Mock services

9. **Documentation**
   - API documentation
   - Architecture diagrams
   - Onboarding guide

---

## BÖLÜM 14: DOĞRU MİMARİ ÖNERİSİ

### 14.1 Recommended Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                        PRESENTATION LAYER                        │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐              │
│  │   Screens   │  │   Popups    │  │ Components  │              │
│  │ (MVVM/MVP)  │  │   (MVVM)    │  │    (MVC)    │              │
│  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘              │
│         │                │                │                      │
│  ┌──────▼────────────────▼────────────────▼──────┐              │
│  │              Navigation Service                │              │
│  └─────────────────────┬─────────────────────────┘              │
└────────────────────────┼────────────────────────────────────────┘
                         │
┌────────────────────────┼────────────────────────────────────────┐
│                        │    APPLICATION LAYER                    │
│  ┌─────────────────────▼─────────────────────┐                  │
│  │              Use Cases / Interactors       │                  │
│  │  ┌─────────┐ ┌─────────┐ ┌─────────────┐  │                  │
│  │  │ Session │ │  Shop   │ │ Achievement │  │                  │
│  │  │  Flow   │ │Purchase │ │   Tracker   │  │                  │
│  │  └────┬────┘ └────┬────┘ └──────┬──────┘  │                  │
│  └───────┼───────────┼─────────────┼─────────┘                  │
│          │           │             │                             │
│  ┌───────▼───────────▼─────────────▼───────┐                    │
│  │           Domain Services                │                    │
│  │  ┌───────┐ ┌────────┐ ┌────────────┐    │                    │
│  │  │ Auth  │ │Economy │ │ Leaderboard│    │                    │
│  │  │Service│ │Service │ │  Service   │    │                    │
│  │  └───┬───┘ └───┬────┘ └─────┬──────┘    │                    │
│  └──────┼─────────┼────────────┼───────────┘                    │
└─────────┼─────────┼────────────┼────────────────────────────────┘
          │         │            │
┌─────────┼─────────┼────────────┼────────────────────────────────┐
│         │         │            │    INFRASTRUCTURE LAYER         │
│  ┌──────▼─────────▼────────────▼──────┐                         │
│  │         Firebase Gateway            │                         │
│  │  ┌─────────┐ ┌──────────┐ ┌─────┐  │                         │
│  │  │  Auth   │ │Firestore │ │Funcs│  │                         │
│  │  └─────────┘ └──────────┘ └─────┘  │                         │
│  └────────────────────────────────────┘                         │
│                                                                  │
│  ┌────────────────┐  ┌────────────────┐                         │
│  │ Local Storage  │  │  Analytics     │                         │
│  └────────────────┘  └────────────────┘                         │
└─────────────────────────────────────────────────────────────────┘
```

### 14.2 Recommended Folder Structure

```
Assets/
├── _Core/
│   ├── Bootstrap/
│   │   ├── GameBootstrapper.cs
│   │   └── SceneLoader.cs
│   ├── DI/
│   │   ├── GameInstaller.cs
│   │   └── SceneInstaller.cs
│   ├── Events/
│   │   ├── MessageBus.cs
│   │   └── Messages/
│   └── Utils/
│       ├── ObjectPool.cs
│       └── Extensions/
│
├── Domain/
│   ├── Auth/
│   │   ├── IAuthService.cs
│   │   ├── AuthService.cs
│   │   └── Models/
│   ├── Economy/
│   │   ├── ICurrencyService.cs
│   │   ├── IInventoryService.cs
│   │   └── Models/
│   ├── Gameplay/
│   │   ├── ISessionService.cs
│   │   ├── IMapService.cs
│   │   └── Models/
│   └── Social/
│       ├── ILeaderboardService.cs
│       └── IReferralService.cs
│
├── Infrastructure/
│   ├── Firebase/
│   │   ├── FirebaseAuthService.cs
│   │   ├── FirestoreService.cs
│   │   └── FunctionsService.cs
│   ├── LocalStorage/
│   │   └── PlayerPrefsStorage.cs
│   └── Analytics/
│       └── AnalyticsService.cs
│
├── Presentation/
│   ├── Screens/
│   │   ├── Home/
│   │   │   ├── HomeScreen.cs
│   │   │   ├── HomeScreenViewModel.cs
│   │   │   └── HomeScreen.prefab
│   │   ├── Shop/
│   │   ├── Gameplay/
│   │   └── Settings/
│   ├── Popups/
│   ├── Components/
│   └── Navigation/
│       ├── INavigationService.cs
│       └── NavigationService.cs
│
├── Gameplay/
│   ├── Player/
│   │   ├── StateMachine/
│   │   │   ├── PlayerStateMachine.cs
│   │   │   └── States/
│   │   ├── Components/
│   │   │   ├── PlayerMovement.cs
│   │   │   └── PlayerAnimator.cs
│   │   └── PlayerFacade.cs
│   ├── Map/
│   │   ├── MapGenerator.cs
│   │   └── Entities/
│   └── Camera/
│       └── GameplayCamera.cs
│
└── Scenes/
    ├── Boot.unity
    ├── Game.unity
    └── MapDesigner.unity
```

### 14.3 Interface Örnekleri

```csharp
// Domain/Auth/IAuthService.cs
public interface IAuthService
{
    bool IsAuthenticated { get; }
    string CurrentUserId { get; }
    UniTask<AuthResult> LoginWithGoogleAsync();
    UniTask<AuthResult> LoginWithAppleAsync();
    UniTask LogoutAsync();
    event Action OnAuthStateChanged;
}

// Domain/Gameplay/ISessionService.cs
public interface ISessionService
{
    UniTask<SessionResponse> RequestSessionAsync(GameMode mode);
    UniTask<SubmitResponse> SubmitSessionAsync(SessionResult result);
    event Action<SessionState> OnSessionStateChanged;
}

// Presentation/Navigation/INavigationService.cs
public interface INavigationService
{
    UniTask PushScreenAsync<T>() where T : UIScreen;
    UniTask PopScreenAsync();
    UniTask ShowPopupAsync<T>() where T : UIPopup;
    void ClearStack();
}
```

---

## BÖLÜM 15: CONCLUSION

### Özet

G-Roll projesi, fonksiyonel bir oyun olmasına rağmen ciddi mimari sorunlar içermektedir:

1. **Singleton Addiction:** 15+ singleton class testability ve maintainability'yi öldürüyor
2. **God Classes:** 1000+ satırlık class'lar SOLID prensiplerini ihlal ediyor
3. **Tight Coupling:** `.Instance` referansları her yerde
4. **Inconsistent Patterns:** Event system, async patterns, naming conventions
5. **No DI:** Manual wiring everywhere
6. **Chaotic UI:** Navigation framework eksik

### Öncelik Sırası

| Öncelik | Aksiyon | Etki | Effort |
|---------|---------|------|--------|
| 1 | PlayerMovement split | Code maintainability | MEDIUM |
| 2 | DI container integration | Testability | HIGH |
| 3 | Event system unification | Debugging | MEDIUM |
| 4 | Firebase abstraction | Testability | MEDIUM |
| 5 | UI navigation framework | UX consistency | HIGH |

### Son Söz

Bu proje "çalışıyor" durumda ancak teknik borç birikmiş. Her yeni özellik eklemek giderek zorlaşacak. Refactoring yatırımı şimdi yapılmazsa, ileride daha maliyetli olacak.

---

## APPENDIX A: File-by-File Issue List

| Dosya | Satır | Sorun |
|-------|-------|-------|
| PlayerMovement.cs | 1014 | God class, needs split |
| GameplayManager.cs | 752 | Too many responsibilities |
| UserDatabaseManager.cs | 1624 | God class, needs split |
| UIManager.cs | 308 | Duplicate Fade() method |
| GameManager.cs | 177 | Mixed async patterns |
| AppManager.cs | 37 | No error handling in Start() |
| AppFlowManager.cs | 281 | Race condition risk |
| SessionRemoteService.cs | 174 | Firebase hard-coded |

## APPENDIX B: Singleton Usage Matrix

| Singleton | Used In (Count) |
|-----------|-----------------|
| UIManager.Instance | 40+ files |
| UserDatabaseManager.Instance | 30+ files |
| GameplayManager.Instance | 25+ files |
| GameManager.Instance | 15+ files |
| AppFlowManager.Instance | 10+ files |

## APPENDIX C: Recommended Libraries

| Kütüphane | Amaç | Link |
|-----------|------|------|
| VContainer | DI Container | github.com/hadashiA/VContainer |
| UniTask | Async/Await | github.com/Cysharp/UniTask |
| R3 | Reactive Extensions | github.com/Cysharp/R3 |
| MessagePipe | Message Bus | github.com/Cysharp/MessagePipe |

---

*Bu rapor, projenin mevcut durumunun objektif bir analizidir. Tüm öneriler, Unity ve C# best practices'e dayanmaktadır.*
