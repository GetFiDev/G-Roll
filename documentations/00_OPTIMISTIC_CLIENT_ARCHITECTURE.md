# G-Roll Optimistic Client Architecture

**Versiyon:** 1.0
**Tarih:** Ocak 2026
**Kapsam:** Tüm client-side sistemler için optimistic update, rollback ve fallback mekanizmaları

---

## EXECUTIVE SUMMARY

Bu döküman, G-Roll Unity projesinin tüm client-server etkileşimlerinde kullanılacak **Optimistic Client Architecture** felsefesini tanımlar. Amaç, kullanıcı deneyimini network gecikmelerinden bağımsız hale getirmek, aynı zamanda veri tutarlılığını garantilemektir.

---

## BÖLÜM 1: MENTAL MODEL

### 1.1 Temel Felsefe: "Assume Success, Prepare for Failure"

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         OPTIMISTIC UPDATE FLOW                          │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│   [User Action]                                                         │
│        │                                                                │
│        ▼                                                                │
│   ┌─────────────────┐                                                   │
│   │  1. SNAPSHOT    │  ◄── Mevcut state'i kaydet (rollback için)       │
│   │     STATE       │                                                   │
│   └────────┬────────┘                                                   │
│            │                                                            │
│            ▼                                                            │
│   ┌─────────────────┐                                                   │
│   │  2. OPTIMISTIC  │  ◄── UI'ı hemen güncelle (sanki başarılı)        │
│   │     UPDATE      │                                                   │
│   └────────┬────────┘                                                   │
│            │                                                            │
│            ▼                                                            │
│   ┌─────────────────┐                                                   │
│   │  3. SERVER      │  ◄── Arka planda server'a istek at               │
│   │     REQUEST     │                                                   │
│   └────────┬────────┘                                                   │
│            │                                                            │
│       ┌────┴────┐                                                       │
│       ▼         ▼                                                       │
│   [SUCCESS]  [FAILURE]                                                  │
│       │         │                                                       │
│       ▼         ▼                                                       │
│   ┌────────┐ ┌──────────┐                                               │
│   │CONFIRM │ │ ROLLBACK │  ◄── Başarısızlıkta snapshot'a dön           │
│   │ STATE  │ │  STATE   │                                               │
│   └────────┘ └────┬─────┘                                               │
│                   │                                                     │
│                   ▼                                                     │
│              ┌──────────┐                                               │
│              │ FALLBACK │  ◄── Kullanıcıya bilgi ver, alternatif sun   │
│              │   UI     │                                               │
│              └──────────┘                                               │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 1.2 Neden Optimistic?

**Geleneksel (Pessimistic) Yaklaşım:**
```
User taps "Equip Item" → Loading spinner → Wait 500ms-2000ms → UI Update
```
- Kullanıcı bekler
- Her işlem "yavaş" hissettirir
- Network latency direkt UX'e yansır

**Optimistic Yaklaşım:**
```
User taps "Equip Item" → Instant UI Update → Background sync → (Confirm or Rollback)
```
- Kullanıcı anında feedback alır
- Uygulama "hızlı" ve "responsive" hisseder
- Network latency kullanıcıdan gizlenir

### 1.3 Temel Prensipler

| Prensip | Açıklama |
|---------|----------|
| **Instant Feedback** | Kullanıcı her işlemde anında görsel feedback alır |
| **Graceful Degradation** | Server failure durumunda uygulama çökmez |
| **State Consistency** | Optimistic state ile server state sonunda eşleşir |
| **Transparent Recovery** | Rollback kullanıcıya minimum rahatsızlıkla yapılır |
| **Predictable Behavior** | Aynı işlem her zaman aynı şekilde davranır |

---

## BÖLÜM 2: CORE COMPONENTS

### 2.1 Optimistic Operation Lifecycle

Her optimistic işlem şu yaşam döngüsünü takip eder:

```csharp
public enum OptimisticOperationState
{
    Idle,           // Başlangıç durumu
    Pending,        // Optimistic update uygulandı, server yanıtı bekleniyor
    Confirmed,      // Server onayladı
    RolledBack,     // Server reddetti, rollback yapıldı
    Failed          // Kritik hata, fallback gerekli
}
```

### 2.2 State Snapshot Interface

```csharp
/// <summary>
/// Herhangi bir sistemin state'ini snapshot olarak alıp restore edebilmek için interface.
/// </summary>
public interface ISnapshotable<TSnapshot>
{
    /// <summary>
    /// Mevcut state'in değiştirilemez bir kopyasını döndürür.
    /// </summary>
    TSnapshot CreateSnapshot();

    /// <summary>
    /// Verilen snapshot'a geri döner (rollback).
    /// </summary>
    void RestoreSnapshot(TSnapshot snapshot);
}
```

### 2.3 Optimistic Operation Handler

```csharp
/// <summary>
/// Generic optimistic operation handler.
/// TState: Snapshot tipi
/// TResult: Server response tipi
/// </summary>
public class OptimisticOperation<TState, TResult>
{
    public OptimisticOperationState State { get; private set; }
    public TState Snapshot { get; private set; }
    public TResult ServerResult { get; private set; }
    public Exception Error { get; private set; }

    public event Action OnConfirmed;
    public event Action<TState> OnRolledBack;
    public event Action<Exception> OnFailed;
}
```

---

## BÖLÜM 3: DESIGN PATTERNS

### 3.1 Optimistic Service Pattern

Her service aşağıdaki pattern'ı takip eder:

```csharp
public interface IOptimisticService<TState>
{
    /// <summary>
    /// Optimistic işlem başlatır.
    /// </summary>
    /// <param name="optimisticAction">UI'ı güncelleyen action</param>
    /// <param name="serverOperation">Server'a gönderilecek async işlem</param>
    /// <param name="rollbackAction">Hata durumunda çağrılacak rollback</param>
    UniTask<OperationResult> ExecuteOptimisticAsync(
        Action optimisticAction,
        Func<UniTask<ServerResponse>> serverOperation,
        Action rollbackAction
    );
}
```

### 3.2 Rollback Strategy Pattern

```csharp
public interface IRollbackStrategy
{
    /// <summary>
    /// Soft rollback: Kullanıcıya bilgi vermeden sessizce geri al.
    /// Örnek: Coin sayısı düzeltme (küçük farklar)
    /// </summary>
    void SoftRollback();

    /// <summary>
    /// Hard rollback: Kullanıcıya bilgi vererek geri al.
    /// Örnek: Satın alma iptal, achievement geri alma
    /// </summary>
    void HardRollback(string reason);

    /// <summary>
    /// Deferred rollback: Daha sonra sync edilecek şekilde işaretle.
    /// Örnek: Offline modda yapılan işlemler
    /// </summary>
    void DeferRollback(PendingOperation operation);
}
```

### 3.3 Fallback Strategy Pattern

```csharp
public interface IFallbackStrategy
{
    /// <summary>
    /// Retry: Belirli bir süre sonra tekrar dene.
    /// </summary>
    UniTask<bool> RetryAsync(int maxAttempts, TimeSpan delay);

    /// <summary>
    /// Cache Fallback: Local cache'den devam et.
    /// </summary>
    void UseCachedData();

    /// <summary>
    /// Graceful Degradation: Özelliği devre dışı bırak.
    /// </summary>
    void DisableFeature(string featureName);

    /// <summary>
    /// User Notification: Kullanıcıya durumu bildir.
    /// </summary>
    void NotifyUser(FallbackNotification notification);
}
```

---

## BÖLÜM 4: CONCRETE EXAMPLES

### 4.1 Örnek 1: Item Equip (Kıyafet Giyme)

**Senaryo:** Kullanıcı envanterinden bir kıyafet seçip giyiyor.

```csharp
public class InventoryService : IInventoryService, ISnapshotable<InventorySnapshot>
{
    private InventoryState _currentState;

    public async UniTask<EquipResult> EquipItemOptimisticAsync(string itemId)
    {
        // 1. SNAPSHOT: Mevcut durumu kaydet
        var snapshot = CreateSnapshot();
        var previousEquippedItem = _currentState.EquippedItems[itemId];

        // 2. OPTIMISTIC UPDATE: UI'ı hemen güncelle
        _currentState.EquipItem(itemId);
        OnInventoryChanged?.Invoke(_currentState); // UI güncellenir

        // 3. SERVER REQUEST: Arka planda server'a gönder
        try
        {
            var response = await _remoteService.EquipItemAsync(itemId);

            // 4a. SUCCESS: Server onayladı
            if (response.Success)
            {
                // Server'dan gelen veriyle state'i confirm et
                _currentState.SyncWithServer(response.ServerState);
                return EquipResult.Success();
            }
            else
            {
                // 4b. ROLLBACK: Server reddetti
                RestoreSnapshot(snapshot);
                OnInventoryChanged?.Invoke(_currentState); // UI rollback

                // FALLBACK: Kullanıcıya bilgi ver
                return EquipResult.Failed(response.ErrorMessage);
            }
        }
        catch (NetworkException ex)
        {
            // 4c. NETWORK ERROR: Rollback + Fallback
            RestoreSnapshot(snapshot);
            OnInventoryChanged?.Invoke(_currentState);

            // Retry seçeneği sun
            return EquipResult.NetworkError(ex, canRetry: true);
        }
    }

    public InventorySnapshot CreateSnapshot()
    {
        return new InventorySnapshot
        {
            EquippedItems = new Dictionary<string, string>(_currentState.EquippedItems),
            Timestamp = DateTime.UtcNow
        };
    }

    public void RestoreSnapshot(InventorySnapshot snapshot)
    {
        _currentState.EquippedItems = new Dictionary<string, string>(snapshot.EquippedItems);
    }
}
```

**UI Tarafı:**

```csharp
public class EquipItemButton : MonoBehaviour
{
    [Inject] private IInventoryService _inventoryService;
    [Inject] private IFeedbackService _feedbackService;

    private string _itemId;

    public async void OnEquipClicked()
    {
        // Butonu disable et (double-tap önleme)
        SetInteractable(false);

        // Optimistic işlemi başlat
        var result = await _inventoryService.EquipItemOptimisticAsync(_itemId);

        // Sonuca göre feedback
        switch (result.Status)
        {
            case OperationStatus.Success:
                _feedbackService.PlaySuccessHaptic();
                break;

            case OperationStatus.RolledBack:
                _feedbackService.ShowToast("Item could not be equipped: " + result.Message);
                _feedbackService.PlayErrorHaptic();
                break;

            case OperationStatus.NetworkError:
                if (result.CanRetry)
                {
                    _feedbackService.ShowRetryDialog("Connection failed. Retry?",
                        onRetry: () => OnEquipClicked());
                }
                break;
        }

        SetInteractable(true);
    }
}
```

### 4.2 Örnek 2: Achievement Claim

**Senaryo:** Kullanıcı bir achievement'ı claim edip ödülünü alıyor.

```csharp
public class AchievementService : IAchievementService
{
    public async UniTask<ClaimResult> ClaimAchievementOptimisticAsync(string achievementId)
    {
        var achievement = _achievements[achievementId];

        // Guard: Zaten claim edilmiş mi?
        if (achievement.IsClaimed)
        {
            return ClaimResult.AlreadyClaimed();
        }

        // 1. SNAPSHOT
        var currencySnapshot = _currencyService.CreateSnapshot();
        var achievementSnapshot = new AchievementSnapshot(achievement);

        // 2. OPTIMISTIC UPDATE
        // Achievement'ı claimed olarak işaretle
        achievement.SetClaimed(true);
        OnAchievementUpdated?.Invoke(achievement);

        // Ödülü kullanıcıya ver (optimistic)
        _currencyService.AddCurrency(achievement.Reward);

        // 3. SERVER REQUEST
        try
        {
            var response = await _remoteService.ClaimAchievementAsync(achievementId);

            if (response.Success)
            {
                // Server'dan gelen gerçek değerlerle sync et
                // (Server farklı bonus vermiş olabilir)
                _currencyService.SyncWithServer(response.NewCurrencyBalance);
                return ClaimResult.Success(response.ActualReward);
            }
            else
            {
                // 4. ROLLBACK: Her iki state'i de geri al
                achievement.SetClaimed(false);
                OnAchievementUpdated?.Invoke(achievement);

                _currencyService.RestoreSnapshot(currencySnapshot);

                return ClaimResult.Failed(response.Error);
            }
        }
        catch (Exception ex)
        {
            // ROLLBACK
            achievement.SetClaimed(false);
            OnAchievementUpdated?.Invoke(achievement);
            _currencyService.RestoreSnapshot(currencySnapshot);

            return ClaimResult.NetworkError(ex);
        }
    }
}
```

**Önemli Not:** Achievement claim gibi kritik işlemlerde, rollback kullanıcıya açıkça gösterilmeli:

```csharp
// UI'da rollback gösterimi
if (result.WasRolledBack)
{
    // Para animasyonu ters çalışsın
    _currencyDisplay.AnimateChange(
        from: result.OptimisticAmount,
        to: result.RolledBackAmount,
        type: AnimationType.Decrease
    );

    // Toast göster
    _toastService.ShowError("Ödül alınamadı. Lütfen tekrar deneyin.");
}
```

### 4.3 Örnek 3: Task Progress Update

**Senaryo:** Oyuncu bir görevi ilerletir (örn: 10 coin topla).

```csharp
public class TaskProgressService : ITaskProgressService
{
    // Task progress için batching kullanalım
    private readonly Dictionary<string, int> _pendingProgressUpdates = new();
    private readonly float _batchInterval = 2.0f; // 2 saniyede bir server'a gönder

    public void AddProgressOptimistic(string taskId, int amount)
    {
        var task = _tasks[taskId];

        // 1. Snapshot yok çünkü batching yapıyoruz
        // Bunun yerine pending updates tutuyoruz

        // 2. OPTIMISTIC UPDATE
        task.CurrentProgress += amount;

        // Task tamamlandı mı kontrol et
        if (task.CurrentProgress >= task.TargetProgress && !task.IsCompleted)
        {
            task.MarkAsCompleted(); // Optimistic completion
        }

        OnTaskProgressUpdated?.Invoke(task);

        // 3. BATCH için biriktir
        if (!_pendingProgressUpdates.ContainsKey(taskId))
        {
            _pendingProgressUpdates[taskId] = 0;
        }
        _pendingProgressUpdates[taskId] += amount;

        // Batch timer'ı başlat (eğer başlamamışsa)
        StartBatchTimerIfNeeded();
    }

    private async void FlushProgressBatch()
    {
        if (_pendingProgressUpdates.Count == 0) return;

        // Batch'i kopyala ve temizle
        var batch = new Dictionary<string, int>(_pendingProgressUpdates);
        _pendingProgressUpdates.Clear();

        try
        {
            // Tek bir request ile tüm progress'leri gönder
            var response = await _remoteService.BatchUpdateProgressAsync(batch);

            if (!response.Success)
            {
                // ROLLBACK: Tüm batch için geri al
                foreach (var kvp in batch)
                {
                    var task = _tasks[kvp.Key];
                    task.CurrentProgress -= kvp.Value;

                    // Completion durumunu da geri al
                    if (task.CurrentProgress < task.TargetProgress)
                    {
                        task.MarkAsIncomplete();
                    }

                    OnTaskProgressUpdated?.Invoke(task);
                }
            }
            else
            {
                // Server'dan gelen doğru değerlerle sync et
                foreach (var serverTask in response.Tasks)
                {
                    _tasks[serverTask.Id].SyncWithServer(serverTask);
                    OnTaskProgressUpdated?.Invoke(_tasks[serverTask.Id]);
                }
            }
        }
        catch (Exception ex)
        {
            // Network hatası - retry mekanizması
            _pendingProgressUpdates.Merge(batch); // Batch'i geri ekle
            ScheduleRetry();
        }
    }
}
```

---

## BÖLÜM 5: ERROR HANDLING TAXONOMY

### 5.1 Error Kategorileri

```csharp
public enum OptimisticErrorCategory
{
    /// <summary>
    /// Geçici network hatası. Retry ile çözülebilir.
    /// Örnek: Timeout, connection lost
    /// </summary>
    Transient,

    /// <summary>
    /// İş kuralı hatası. Rollback gerekli.
    /// Örnek: Yetersiz bakiye, item zaten equipped
    /// </summary>
    BusinessRule,

    /// <summary>
    /// State uyuşmazlığı. Full resync gerekli.
    /// Örnek: Client state ile server state uyuşmuyor
    /// </summary>
    StateConflict,

    /// <summary>
    /// Kritik sistem hatası. Feature disable edilmeli.
    /// Örnek: Server 500, invalid response format
    /// </summary>
    Critical
}
```

### 5.2 Error Response Matrix

| Error Type | Rollback | User Notification | Retry | Action |
|------------|----------|-------------------|-------|--------|
| Transient | Evet | Minimal | Otomatik 3x | Queue operation |
| BusinessRule | Evet | Açıklayıcı toast | Hayır | Show error state |
| StateConflict | Evet + Full Resync | "Syncing..." | Hayır | Force refresh |
| Critical | Evet | Error dialog | Manuel | Disable feature |

### 5.3 Recovery Flow

```
[Error Detected]
      │
      ▼
┌─────────────────┐
│ Categorize Error│
└────────┬────────┘
         │
    ┌────┴────┬────────────┬──────────────┐
    ▼         ▼            ▼              ▼
[Transient] [BusinessRule] [StateConflict] [Critical]
    │         │             │              │
    ▼         ▼             ▼              ▼
┌────────┐ ┌──────────┐ ┌─────────────┐ ┌──────────┐
│Auto    │ │Show Error│ │Force Resync │ │Disable   │
│Retry   │ │Toast     │ │All Data     │ │Feature   │
│(3x)    │ │Rollback  │ │Show Loading │ │Show Error│
└───┬────┘ └──────────┘ └─────────────┘ └──────────┘
    │
    ▼
[Still Failing?]
    │
    ├── No → [Success]
    │
    └── Yes → [Queue for Later / Notify User]
```

---

## BÖLÜM 6: STATE SYNCHRONIZATION

### 6.1 Sync Strategies

```csharp
public enum SyncStrategy
{
    /// <summary>
    /// Her işlemde anında sync. En güvenli, en yavaş.
    /// Kullanım: Kritik işlemler (para transferi, satın alma)
    /// </summary>
    Immediate,

    /// <summary>
    /// Belirli aralıklarla batch sync. Dengeli.
    /// Kullanım: Progress updates, statistics
    /// </summary>
    Batched,

    /// <summary>
    /// Sadece belirli event'lerde sync.
    /// Kullanım: Session start/end, phase changes
    /// </summary>
    EventDriven,

    /// <summary>
    /// Offline destekli lazy sync.
    /// Kullanım: Settings, preferences
    /// </summary>
    Lazy
}
```

### 6.2 Conflict Resolution

```csharp
public interface IConflictResolver<TState>
{
    /// <summary>
    /// Client wins: Optimistic değeri koru.
    /// </summary>
    TState ResolveClientWins(TState clientState, TState serverState);

    /// <summary>
    /// Server wins: Server değerini al.
    /// </summary>
    TState ResolveServerWins(TState clientState, TState serverState);

    /// <summary>
    /// Merge: İki state'i akıllıca birleştir.
    /// </summary>
    TState ResolveMerge(TState clientState, TState serverState);

    /// <summary>
    /// Last write wins: Timestamp'e göre karar ver.
    /// </summary>
    TState ResolveLastWriteWins(TState clientState, TState serverState);
}
```

### 6.3 G-Roll için Sync Tablosu

| System | Sync Strategy | Conflict Resolution | Priority |
|--------|---------------|---------------------|----------|
| Currency | Immediate | Server Wins | Critical |
| Inventory Equip | Immediate | Server Wins | High |
| Task Progress | Batched (2s) | Merge (add) | Medium |
| Achievement Claim | Immediate | Server Wins | High |
| Settings/Prefs | Lazy | Last Write Wins | Low |
| Leaderboard Score | Event-Driven | Server Wins | Medium |
| Energy | Immediate | Server Wins | Critical |

---

## BÖLÜM 7: UI FEEDBACK FELSEFESİ

### 7.1 Temel Prensip: "No Pending State"

Optimistic UI'ın **tüm amacı** kullanıcıya loading/pending/waiting göstermemektir. UI her zaman "normal" görünür - ya başarılı state'te ya da rollback sonrası eski state'te.

```
┌─────────────────────────────────────────────────────────────────┐
│                    OPTIMISTIC UI - DOĞRU MODEL                   │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│                        [NORMAL STATE]                            │
│                        ┌───────────┐                             │
│                        │           │                             │
│            ┌──────────►│   IDLE    │◄──────────┐                │
│            │           │           │           │                 │
│            │           └─────┬─────┘           │                 │
│            │                 │                 │                 │
│            │          User Action              │                 │
│            │                 │                 │                 │
│            │                 ▼                 │                 │
│            │           [INSTANT]               │                 │
│            │           UI Update               │                 │
│            │           (No loading)            │                 │
│            │                 │                 │                 │
│       ┌────┴────┐       ┌────┴────┐            │                 │
│       ▼         ▼       ▼         ▼            │                 │
│   [SUCCESS]  [FAILURE]                         │                 │
│   (Silent)   (Rollback)                        │                 │
│       │         │                              │                 │
│       │         ▼                              │                 │
│       │    ┌─────────┐                         │                 │
│       │    │ROLLBACK │  Shake + Toast          │                 │
│       │    │ANIMATION│──────────────────────────┘                │
│       │    └─────────┘                                           │
│       │                                                          │
│       └────────────────────(Stay in new state)                   │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

### 7.2 UI State'leri

Optimistic sistemde sadece **2 UI state** vardır:

| State | Açıklama | Visual |
|-------|----------|--------|
| **IDLE** | Normal state. Başarılı işlemler burada kalır. | Hiçbir özel görsel yok |
| **ROLLBACK** | Hata durumunda geri alım. | Kısa shake + error toast |

**DIKKAT:** "Pending" veya "Loading" state **YOKTUR**. Bu optimistic'in tüm amacıdır.

### 7.3 Success Durumu

Başarılı işlemlerde **hiçbir feedback gerekmez**:

```
Kullanıcı "Equip" butonuna basar
    │
    ▼
UI anında güncellenir (item equipped görünür)
    │
    ▼
Arka planda server onaylar
    │
    ▼
Hiçbir şey olmaz (zaten doğru state'teyiz)
```

Kullanıcı açısından: Butona bastım → Oldu. Bu kadar.

### 7.4 Failure Durumu (Rollback)

Sadece **hata durumunda** feedback veririz:

```
Kullanıcı "Equip" butonuna basar
    │
    ▼
UI anında güncellenir (item equipped görünür)
    │
    ▼
Arka planda server reddeder
    │
    ▼
ROLLBACK:
1. UI eski state'e döner (item unequipped)
2. Kısa shake animasyonu (dikkat çekmek için)
3. Toast mesajı: "İşlem başarısız. Tekrar deneyin."
```

### 7.5 Rollback Animasyonları

| Durum | Animasyon | Amaç |
|-------|-----------|------|
| Currency rollback | Sayı geriye doğru animate | Fark edilebilirlik |
| Item rollback | Element kısa shake | Dikkat çekme |
| List item rollback | Row kırmızı flash + shake | Hangi item etkilendi |
| Achievement rollback | Progress bar geriye | Görsel tutarlılık |

### 7.6 Notification Seviyeleri (Sadece Hatalar İçin)

```csharp
public enum RollbackNotificationLevel
{
    /// <summary>
    /// Sessiz rollback. Kritik olmayan işlemler için.
    /// Sadece UI state geri döner, toast yok.
    /// Örnek: Minor sync differences
    /// </summary>
    Silent,

    /// <summary>
    /// Minimal toast. Kullanıcı işlemi tekrar deneyebilir.
    /// Örnek: Item equip failed, progress update failed
    /// </summary>
    Toast,

    /// <summary>
    /// Belirgin hata. Kullanıcının dikkat etmesi gerekiyor.
    /// Örnek: Purchase failed, reward claim failed
    /// </summary>
    Prominent,

    /// <summary>
    /// Kritik hata. Blokleyici dialog.
    /// Örnek: Session invalid, auth expired
    /// </summary>
    Critical
}
```

### 7.7 Anti-Pattern: Loading Indicators

**YAPMA:**
```csharp
// YANLIŞ: Loading gösterme
async void OnEquipClicked()
{
    loadingSpinner.SetActive(true);  // ❌ YANLIŞ
    await _inventoryService.EquipItemAsync(itemId);
    loadingSpinner.SetActive(false);
}
```

**YAP:**
```csharp
// DOĞRU: Anında güncelle, hata durumunda rollback
async void OnEquipClicked()
{
    // UI zaten MessageBus üzerinden anında güncellenecek
    var result = await _inventoryService.EquipItemOptimisticAsync(itemId);

    // Sadece hata durumunda feedback
    if (result.IsFailure)
    {
        _feedbackService.ShowToast(result.Message);
    }
}
```

### 7.8 Button State Handling

Butonlar için tek istisna: **double-tap prevention**

```csharp
// Kabul edilebilir: Butonu kısa süre disable et (visual feedback değil, tekrar basma önleme)
async void OnEquipClicked()
{
    button.interactable = false;  // Double-tap önleme

    var result = await _inventoryService.EquipItemOptimisticAsync(itemId);

    button.interactable = true;

    if (result.IsFailure)
    {
        _feedbackService.ShowToast(result.Message);
    }
}
```

Bu "loading" değildir - sadece aynı işlemi iki kez tetiklemeyi önlemektir.

---

## BÖLÜM 8: IMPLEMENTATION CHECKLIST

### 8.1 Her Yeni Feature İçin

- [ ] Hangi state'ler snapshot alınmalı?
- [ ] Optimistic update hangi component'leri etkiler?
- [ ] Rollback durumunda UI nasıl güncellenir?
- [ ] Network error durumunda retry stratejisi ne?
- [ ] Conflict resolution kuralları ne?
- [ ] Kullanıcıya hangi feedback'ler verilir?

### 8.2 Code Review Checklist

- [ ] `CreateSnapshot()` tüm gerekli state'leri kapsıyor mu?
- [ ] `RestoreSnapshot()` tam restore yapıyor mu?
- [ ] Exception handling tüm edge case'leri kapsıyor mu?
- [ ] Event'ler doğru sırada fire ediliyor mu?
- [ ] Memory leak riski var mı (event unsubscribe)?
- [ ] Race condition riski var mı?

---

## BÖLÜM 9: ANTI-PATTERNS

### 9.1 Kaçınılması Gereken Patterns

**1. Fire and Forget**
```csharp
// KÖTÜ: Sonucu beklemeden bırakma
public void EquipItem(string itemId)
{
    _currentState.EquipItem(itemId);
    _ = _remoteService.EquipItemAsync(itemId); // Fire and forget!
}

// İYİ: Her zaman sonucu handle et
public async UniTask EquipItemAsync(string itemId)
{
    var snapshot = CreateSnapshot();
    _currentState.EquipItem(itemId);

    try
    {
        await _remoteService.EquipItemAsync(itemId);
    }
    catch
    {
        RestoreSnapshot(snapshot);
        throw;
    }
}
```

**2. Partial Rollback**
```csharp
// KÖTÜ: Sadece bir kısmını rollback etme
public void Rollback()
{
    _inventory.RestoreSnapshot(_inventorySnapshot);
    // Currency'yi unuttuk!
}

// İYİ: Tüm ilgili state'leri rollback et
public void Rollback()
{
    _inventory.RestoreSnapshot(_inventorySnapshot);
    _currency.RestoreSnapshot(_currencySnapshot);
    _achievements.RestoreSnapshot(_achievementSnapshot);
}
```

**3. Silent Failure**
```csharp
// KÖTÜ: Hatayı yutma
catch (Exception ex)
{
    Debug.LogError(ex); // Sadece log'a yaz, kullanıcı bilmez
}

// İYİ: Her zaman uygun feedback ver
catch (Exception ex)
{
    _logger.LogError(ex);
    RestoreSnapshot(snapshot);
    _feedbackService.ShowError("Operation failed. Please try again.");
}
```

**4. Over-Optimism**
```csharp
// KÖTÜ: Her şeyi optimistic yapma
public void DeleteAccount()
{
    ShowAccountDeletedUI(); // Hesabı silmeden UI'ı güncelleme!
    _ = _remoteService.DeleteAccountAsync();
}

// İYİ: Kritik işlemlerde pessimistic ol
public async UniTask DeleteAccountAsync()
{
    ShowLoadingUI();
    var result = await _remoteService.DeleteAccountAsync();

    if (result.Success)
    {
        ShowAccountDeletedUI();
    }
    else
    {
        ShowError(result.Error);
    }
}
```

---

## BÖLÜM 10: TESTING STRATEGY

### 10.1 Unit Test Scenarios

```csharp
[Test]
public async Task EquipItem_ServerSuccess_StateConfirmed()
{
    // Arrange
    var service = new InventoryService(mockRemote);
    mockRemote.Setup(x => x.EquipItemAsync(It.IsAny<string>()))
              .ReturnsAsync(SuccessResponse);

    // Act
    var result = await service.EquipItemOptimisticAsync("item_001");

    // Assert
    Assert.AreEqual(OperationStatus.Success, result.Status);
    Assert.IsTrue(service.IsEquipped("item_001"));
}

[Test]
public async Task EquipItem_ServerFailure_StateRolledBack()
{
    // Arrange
    var service = new InventoryService(mockRemote);
    var initialState = service.CreateSnapshot();
    mockRemote.Setup(x => x.EquipItemAsync(It.IsAny<string>()))
              .ReturnsAsync(FailureResponse);

    // Act
    var result = await service.EquipItemOptimisticAsync("item_001");

    // Assert
    Assert.AreEqual(OperationStatus.RolledBack, result.Status);
    Assert.AreEqual(initialState.EquippedItems, service.GetEquippedItems());
}

[Test]
public async Task EquipItem_NetworkError_StateRolledBackAndCanRetry()
{
    // Arrange
    mockRemote.Setup(x => x.EquipItemAsync(It.IsAny<string>()))
              .ThrowsAsync(new NetworkException());

    // Act
    var result = await service.EquipItemOptimisticAsync("item_001");

    // Assert
    Assert.AreEqual(OperationStatus.NetworkError, result.Status);
    Assert.IsTrue(result.CanRetry);
}
```

### 10.2 Integration Test Scenarios

- Network latency simulation (100ms, 500ms, 2000ms)
- Intermittent failure simulation (50% failure rate)
- Concurrent operation handling
- Offline → Online transition
- App backgrounding during pending operation

---

## SONUÇ

Bu architecture, G-Roll'un tüm client-server etkileşimlerinde tutarlı, güvenilir ve kullanıcı dostu bir deneyim sağlamayı hedefler. Her yeni feature bu dökümanı referans alarak implement edilmeli ve code review'larda bu prensiplere uygunluk kontrol edilmelidir.

**Golden Rule:** "Kullanıcı hiçbir zaman network'ü beklememeli, ama state'i hiçbir zaman yanlış olmamalı."

---

*Bu döküman, G-Roll Optimistic Client Architecture v1.0'dır. Güncellemeler için versiyon numarasını artırın.*
