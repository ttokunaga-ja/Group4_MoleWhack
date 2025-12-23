using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Temporary helper to run Setup flow inside Gameplay scene only.
/// - Starts QRPoseLocker/QRTrustMonitor collection
/// - Waits for pose lock + trust OK, then starts gameplay session
/// - Intended for fallback when scene transitions fail; remove after normal flow is restored.
/// </summary>
public class Gameplay_SingleSceneSetupBootstrap : MonoBehaviour
{
    [Header("References (optional)")]
    [SerializeField] private QRPoseLocker poseLocker;
    [SerializeField] private QRTrustMonitor trustMonitor;
    [SerializeField] private QRObjectPositioner positioner;
    [SerializeField] private GameSessionManager sessionManager;

    [Header("Settings")]
    [SerializeField] private string setupSceneName = "Setup";
    [SerializeField] private string gameplaySceneName = "Gameplay";
    [SerializeField] private bool onlyWhenSetupSceneNotLoaded = true;
    [SerializeField] private bool autoRetryOnFail = true;
    [SerializeField] private float retryDelaySeconds = 2f;
    [SerializeField] private bool keepSessionAlive = true; // disable auto-end for single-scene fallback

    private bool gameplayStarted = false;

    private void Awake()
    {
        poseLocker = poseLocker ?? FindObjectOfType<QRPoseLocker>();
        trustMonitor = trustMonitor ?? FindObjectOfType<QRTrustMonitor>();
        positioner = positioner ?? FindObjectOfType<QRObjectPositioner>();
        sessionManager = sessionManager ?? GameSessionManager.Instance ?? FindObjectOfType<GameSessionManager>();

        if (sessionManager != null && keepSessionAlive)
        {
            var disableAutoEndField = typeof(GameSessionManager).GetField("disableAutoEnd", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (disableAutoEndField != null)
            {
                disableAutoEndField.SetValue(sessionManager, true);
                Debug.Log("[SingleSceneSetupBootstrap] disableAutoEnd=true for single-scene mode");
            }
        }

        if (onlyWhenSetupSceneNotLoaded)
        {
            bool setupLoaded = SceneManager.GetSceneByName(setupSceneName).isLoaded;
            bool isGameplay = SceneManager.GetActiveScene().name == gameplaySceneName;
            if (!isGameplay || setupLoaded)
            {
                Debug.Log("[SingleSceneSetupBootstrap] Disabled (setup scene loaded or active scene not gameplay)");
                enabled = false;
                return;
            }
        }

        if (poseLocker != null)
        {
            poseLocker.AutoStartOnEnable = false;
        }
        if (positioner != null)
        {
            // ensure locked pose only to avoid drift during gameplay
            var positionerUseLocked = positioner.GetType().GetField("useLockedPoseOnly", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (positionerUseLocked != null)
            {
                positionerUseLocked.SetValue(positioner, true);
            }
        }
    }

    private void OnEnable()
    {
        if (poseLocker != null)
        {
            poseLocker.OnPoseLocked += HandlePoseLocked;
            poseLocker.OnLockFailed += HandleLockFailed;
            poseLocker.BeginCollect();
            Debug.Log("[SingleSceneSetupBootstrap] PoseLocker BeginCollect");
        }
        if (trustMonitor != null)
        {
            trustMonitor.OnTrustChanged += HandleTrustChanged;
            trustMonitor.BeginSetup();
            Debug.Log("[SingleSceneSetupBootstrap] TrustMonitor BeginSetup");
        }
    }

    private void OnDisable()
    {
        if (poseLocker != null)
        {
            poseLocker.OnPoseLocked -= HandlePoseLocked;
            poseLocker.OnLockFailed -= HandleLockFailed;
        }
        if (trustMonitor != null)
        {
            trustMonitor.OnTrustChanged -= HandleTrustChanged;
        }
    }

    private void HandlePoseLocked(string uuid, Pose pose)
    {
        TryStartGameplay();
    }

    private void HandleLockFailed()
    {
        if (autoRetryOnFail && poseLocker != null)
        {
            Invoke(nameof(RestartCollect), retryDelaySeconds);
            Debug.Log("[SingleSceneSetupBootstrap] Lock failed -> retry scheduled");
        }
    }

    private void HandleTrustChanged(float value)
    {
        TryStartGameplay();
    }

    private void RestartCollect()
    {
        if (trustMonitor != null) trustMonitor.BeginSetup();
        poseLocker?.Retry();
        Debug.Log("[SingleSceneSetupBootstrap] Restart collect");
    }

    private void TryStartGameplay()
    {
        if (gameplayStarted) return;
        bool locked = poseLocker == null || poseLocker.State == QRPoseLocker.LockerState.Locked;
        bool trustOK = trustMonitor == null || trustMonitor.CurrentTrust >= trustMonitor.TrustLowThreshold;

        if (locked && trustOK)
        {
            trustMonitor?.BeginGameplay();
            sessionManager = sessionManager ?? GameSessionManager.Instance ?? FindObjectOfType<GameSessionManager>();
            sessionManager?.BeginSession();
            gameplayStarted = true;
        }
    }
}
