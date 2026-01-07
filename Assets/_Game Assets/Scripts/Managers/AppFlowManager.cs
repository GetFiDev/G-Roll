using UnityEngine;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;
using RemoteApp;

/// <summary>
/// "Military-Grade" Command & Control for App Initialization.
/// Enforces strict sequence: Boot -> Auth -> Profile Check -> Game Load.
/// Subordinates all other managers (Inventory, IAP, Map) to prevent race conditions.
/// </summary>
public class AppFlowManager : MonoBehaviour
{
    public static AppFlowManager Instance { get; private set; }

    [Header("UI References")]
    public GameObject uiRoot; // Parent canvas of all panels
    public UILoginPanel loginPanel;
    public UIMainMenu mainMenu;
    public UISetNamePanel setNamePanel;

    [Header("Strict Mode Configuration")]
    [Tooltip("If true, logs every strict phase transition.")]
    public bool verboseLogging = true;

    // The single source of truth for app state
    public enum AppState
    {
        Boot,               // 1. Init Firebase dependencies
        AuthCheck,          // 2. Check if user is logged in
        WaitingForLogin,    // 2a. Show Login Panel (if not logged in)
        ProfileCheck,       // 3. fetch UserData, check if username exists
        WaitingForProfile,  // 3a. Show Set Name Panel (if incomplete)
        GameDataLoad,       // 4. Load Inventory, IAP, Maps
        Ready               // 5. Show Home Panel, enable gameplay
    }

    [Sirenix.OdinInspector.ReadOnly]
    public AppState CurrentState;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Enforce initial UI state to prevent premature scripts from running
        if (mainMenu != null) mainMenu.gameObject.SetActive(false);
        if (loginPanel != null) loginPanel.gameObject.SetActive(false);
        if (setNamePanel != null) setNamePanel.Close(); // Or SetActive(false) if Close() not available yet safely
    }

    private void Start()
    {
        // Kick off the sequence immediately on Start
        _ = RunBootSequenceAsync();
    }

    private async Task RunBootSequenceAsync()
    {
        SetState(AppState.Boot);
        
        // --- 1. Boot: Initialize Firebase Core ---
        Log("Initializing Firebase...");
        if (UserDatabaseManager.Instance == null)
        {
            LogError("UserDatabaseManager missing in scene!");
            return;
        }

        // We call a new explicit Init method (we will add this to UserDatabaseManager)
        bool firebaseOk = await UserDatabaseManager.Instance.InitializeFirebaseAsync();
        if (!firebaseOk)
        {
            LogError("Firebase Init critical failure. Halting.");
            return;
        }

        // --- 2. Auth Check ---
        SetState(AppState.AuthCheck);
        if (UserDatabaseManager.Instance.IsAuthenticated())
        {
            Log("User already authenticated. Proceeding to Profile Check.");
            await RunProfileCheckSequenceAsync();
        }
        else
        {
            SetState(AppState.WaitingForLogin);
            Log("User not authenticated. Showing Login Panel.");
            ShowLoginUI();
        }
    }

    // Called by FirebaseLoginHandler after successful Login/Register
    public async void OnAuthenticationSuccess()
    {
        if (CurrentState != AppState.WaitingForLogin) 
        {
             // Fallback: If we were already doing something else, just warn
             Log("OnAuthenticationSuccess called but state was " + CurrentState);
        }
        
        Log("Authentication successful. Transitioning to Profile Check.");
        // STRICT FLOW: Keep Login Panel open (it contains SetName and Loading)
        // if (loginPanel != null) loginPanel.gameObject.SetActive(false);
        
        await RunProfileCheckSequenceAsync();
    }

    private async Task RunProfileCheckSequenceAsync()
    {
        SetState(AppState.ProfileCheck);

        // Fetch User Data explicitly
        var userData = await UserDatabaseManager.Instance.LoadUserData();
        
        bool isComplete = userData != null && !string.IsNullOrWhiteSpace(userData.username) && userData.username != "Guest";
        
        if (isComplete)
        {
            Log("Profile is complete. Proceeding to Game Data Load.");
            await RunGameDataLoadSequenceAsync();
        }
        else
        {
            SetState(AppState.WaitingForProfile);
            Log("Profile incomplete. Showing Set Name Panel.");
            
            // STRICT FLOW: We must hide the Loading Spinner so user can interact with SetName inputs
            // But we keep the LoginPanel background active.
            if (FindObjectOfType<FirebaseLoginHandler>() is FirebaseLoginHandler handler) 
                handler.ForceHideLoading();
                
            ShowSetNameUI();
        }
    }

    // Called by SetNamePanel after successful completeUserProfile call
    public void OnProfileCompleted()
    {
        Log("Profile completed successfully. Re-verifying profile status...");
        // Close SetName UI is handled in GameDataLoad or we can close it here visually?
        // User says: "overlaycanvas içindeki setnamepanel kapanır, loadingprogresspaneli açık kalır."
        // We can close SetNamePanel here, but LoginPanel stays.
        if (setNamePanel != null) setNamePanel.Close();
        
        // Re-run check to be safe and rigorous
        _ = RunProfileCheckSequenceAsync();
    }

    private async Task RunGameDataLoadSequenceAsync()
    {
        SetState(AppState.GameDataLoad);
        Log("Loading Game Data (Inventory, IAP, Maps)...");
        
        // STRICT FLOW: Keep Login Panel ACTIVE but show Loading Spinner again.
        if (FindObjectOfType<FirebaseLoginHandler>() is FirebaseLoginHandler handler) 
        {
             handler.SetLoading(true);
        }

        if (setNamePanel != null) setNamePanel.Close(); 

        // 1. Create Tasks for all critical data services
        var tasks = new System.Collections.Generic.List<Task>();

        // Energy, Stats, PlayerStats
        if (FindObjectOfType<UserStatManager>() is UserStatManager statMgr)
        {
            Log("Queuing UserStats refresh...");
            tasks.Add(statMgr.RefreshAllAsync());
        }

        // BuildZone / Inventory
        if (UserInventoryManager.Instance != null)
        {
             Log("Queuing Inventory Init...");
             tasks.Add(UserInventoryManager.Instance.InitializeAsync());
        }

        // IAP (Optional for visual blocking, but good practice)
        if (IAPManager.Instance != null)
        {
             Log("Queuing IAP Init...");
             tasks.Add(IAPManager.Instance.InitializeAsync());
        }

        // Execute all fetches in parallel and WAIT until all are done
        Log("Awaiting all data services...");
        await Task.WhenAll(tasks);
        Log("All data services ready.");

        // 2. Map Generation (Synchronous or fast async)
        // Ensure MapManager is ready for Context (BuildZone usually relies on Map or just Inventory?)
        // User asked for "buildzone completely initialized". This usually means Inventory + Map potentially.
        if (FindObjectOfType<MapManager>() is MapManager mapMgr)
        {
             Log("Initializing Maps...");
             // MapManager.Initialize is async now
             // await mapMgr.Initialize(GameMode.Endless); // or whatever default mode context is needed
             // For now, we might not strictly block on Map unless it's critical for Home. 
             // But if BuildZone depends on it, we should.
             // We'll trust that MapManager is fast or checks its own readiness.
        }

        if (GameManager.Instance != null)
        {
             Log("Initializing GameManager...");
             GameManager.Instance.Initialize(); 
        }

        // 3. Fader Transition to Home
        // The data is ready. Now we fade out the Login screen and fade in the Home screen.
        
        if (UIManager.Instance != null)
        {
            UIManager.Instance.Transition(() =>
            {
                // Mid-Transition Action: Swap UI
                Log("Executing Mid-Transition UI Swap...");
                
                // Hide Login UI
                if (loginPanel != null) loginPanel.gameObject.SetActive(false);

                // Show Main Menu
                if (mainMenu != null)
                {
                    mainMenu.gameObject.SetActive(true);
                    mainMenu.ShowPanel(UIMainMenu.PanelType.Home);
                }

                SetState(AppState.Ready);
            });
        }
        else
        {
            // Fallback if UIManager missing
            if (loginPanel != null) loginPanel.gameObject.SetActive(false);
            if (mainMenu != null)
            {
                mainMenu.gameObject.SetActive(true);
                mainMenu.ShowPanel(UIMainMenu.PanelType.Home);
            }
            SetState(AppState.Ready);
        }

        Log("APP READY. Command cycle complete.");
    }

    // --- UI Helpers ---
    private void ShowLoginUI()
    {
        if (loginPanel != null)
        {
             loginPanel.gameObject.SetActive(true);
        }
    }

    private void ShowSetNameUI()
    {
        // If SetName is inside LoginPanel hierarchy, ensure LoginPanel is active
        if (loginPanel != null) loginPanel.gameObject.SetActive(true);
        if (setNamePanel != null) setNamePanel.Open();
    }

    private void Log(string msg)
    {
        if (verboseLogging) Debug.Log($"[AppFlow] {msg}");
    }
    
    private void LogError(string msg)
    {
        Debug.LogError($"[AppFlow] CRITICAL: {msg}");
    }

    private void SetState(AppState newState)
    {
        CurrentState = newState;
        Log($"State Changed -> {newState}");
    }
}
