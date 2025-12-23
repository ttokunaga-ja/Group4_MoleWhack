using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// XR 向け（World Space Canvas 用）Setup UI。
/// IMGUI を使わず、Button/TMP_Text を通して操作する。
/// </summary>
public class SetupXRUIController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private QRPoseLocker poseLocker;
    [SerializeField] private QRTrustMonitor trustMonitor;
    [SerializeField] private GameFlowController flow;

    [Header("UI")]
    [SerializeField] private Button setupStartButton;
    [SerializeField] private Button gameStartButton;
    [SerializeField] private Button setupAgainButton;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI trustText;

    [Header("Lookup Paths (under OVRCameraRig/Canvas)")]
    [SerializeField] private string canvasRootPath = "OVRCameraRig/TrackingSpace/CenterEyeAnchor/Canvas";
    [SerializeField] private string setupStartButtonPath = "OVRCameraRig/TrackingSpace/CenterEyeAnchor/Canvas/Button_SetupStart (TMP)";
    [SerializeField] private string gameStartButtonPath = "OVRCameraRig/TrackingSpace/CenterEyeAnchor/Canvas/Button_GameStart (TMP)";
    [SerializeField] private string setupAgainButtonPath = "OVRCameraRig/TrackingSpace/CenterEyeAnchor/Canvas/Button_SetupAgain (TMP)";
    [SerializeField] private string statusTextPath = "OVRCameraRig/TrackingSpace/CenterEyeAnchor/Canvas/Text_Status (TMP)";
    [SerializeField] private string timerTextPath = "OVRCameraRig/TrackingSpace/CenterEyeAnchor/Canvas/Text_Timer (TMP)";
    [SerializeField] private string trustTextPath = "OVRCameraRig/TrackingSpace/CenterEyeAnchor/Canvas/Text_Trust (TMP)";

    private bool hasLocked;
    private bool hasFailed;

    private void Awake()
    {
        poseLocker = poseLocker ?? FindObjectOfType<QRPoseLocker>();
        trustMonitor = trustMonitor ?? FindObjectOfType<QRTrustMonitor>();
        flow = flow ?? GameFlowController.Instance ?? FindObjectOfType<GameFlowController>();

        // 明示パスで UI を取得（OVRCameraRig/Canvas 配下を前提）
        setupStartButton = setupStartButton ?? GetComponentAtPath<Button>(setupStartButtonPath);
        gameStartButton = gameStartButton ?? GetComponentAtPath<Button>(gameStartButtonPath);
        setupAgainButton = setupAgainButton ?? GetComponentAtPath<Button>(setupAgainButtonPath);
        statusText = statusText ?? GetComponentAtPath<TextMeshProUGUI>(statusTextPath);
        timerText = timerText ?? GetComponentAtPath<TextMeshProUGUI>(timerTextPath);
        trustText = trustText ?? GetComponentAtPath<TextMeshProUGUI>(trustTextPath);

        if (poseLocker != null)
        {
            poseLocker.AutoStartOnEnable = false; // XR はボタン操作前提
        }
    }

    private void OnEnable()
    {
        if (poseLocker == null) return;
        poseLocker.OnCollectingStarted += HandleCollectingStarted;
        poseLocker.OnPoseLocked += HandlePoseLocked;
        poseLocker.OnLockFailed += HandleLockFailed;
        if (trustMonitor != null)
        {
            trustMonitor.OnTrustChanged += HandleTrustChanged;
        }
        UpdateUIState();
    }

    private void OnDisable()
    {
        if (poseLocker == null) return;
        poseLocker.OnCollectingStarted -= HandleCollectingStarted;
        poseLocker.OnPoseLocked -= HandlePoseLocked;
        poseLocker.OnLockFailed -= HandleLockFailed;
        if (trustMonitor != null)
        {
            trustMonitor.OnTrustChanged -= HandleTrustChanged;
        }
    }

    private void Update()
    {
        if (poseLocker != null && poseLocker.State == QRPoseLocker.LockerState.Collecting && timerText != null)
        {
            float elapsed = poseLocker.GetElapsedSeconds();
            float remaining = Mathf.Max(0f, poseLocker.CollectionDuration - elapsed);
            timerText.text = $"残り: {remaining:F1}s";
        }
    }

    // ===== Button Hooks =====
    public void OnClickSetupStart()
    {
        poseLocker?.BeginCollect();
        trustMonitor?.BeginSetup();
    }

    public void OnClickGameStart()
    {
        if (poseLocker == null || poseLocker.State != QRPoseLocker.LockerState.Locked) return;
        if (trustMonitor != null && trustMonitor.CurrentTrust < trustMonitor.TrustLowThreshold)
        {
            SetStatus("信頼度が不足しています。再調整してください。");
            return;
        }

        trustMonitor?.BeginGameplay();
        flow?.GoToGameplay();
    }

    public void OnClickSetupAgain()
    {
        poseLocker?.Retry();
        trustMonitor?.BeginSetup();
    }

    // ===== Event Handlers =====
    private void HandleCollectingStarted()
    {
        hasLocked = false;
        hasFailed = false;
        SetStatus("スキャン中... (10秒)");
        SetTimerVisible(true);
        UpdateButtons();
    }

    private void HandlePoseLocked(string uuid, Pose pose)
    {
        hasLocked = true;
        hasFailed = false;
        SetStatus($"ロック完了: {poseLocker?.LockedPoseCount ?? 0}件");
        SetTimerVisible(false);
        UpdateButtons();
    }

    private void HandleLockFailed()
    {
        hasFailed = true;
        hasLocked = false;
        SetStatus("ロック失敗（サンプル不足など）");
        SetTimerVisible(false);
        UpdateButtons();
    }

    private void HandleTrustChanged(float value)
    {
        if (trustText != null)
        {
            trustText.text = $"信頼度: {value:F2}";
        }
    }

    // ===== UI Helpers =====
    private void UpdateUIState()
    {
        if (poseLocker == null) return;
        switch (poseLocker.State)
        {
            case QRPoseLocker.LockerState.Idle:
            case QRPoseLocker.LockerState.Retry:
            case QRPoseLocker.LockerState.Failed:
                SetStatus("準備完了");
                SetTimerVisible(false);
                break;
            case QRPoseLocker.LockerState.Collecting:
                SetStatus("スキャン中... (10秒)");
                SetTimerVisible(true);
                break;
            case QRPoseLocker.LockerState.Locked:
                SetStatus($"ロック完了: {poseLocker.LockedPoseCount}件");
                SetTimerVisible(false);
                break;
        }
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        bool canStart = poseLocker != null && (poseLocker.State == QRPoseLocker.LockerState.Idle || poseLocker.State == QRPoseLocker.LockerState.Failed || poseLocker.State == QRPoseLocker.LockerState.Retry);
        bool canGameStart = poseLocker != null && poseLocker.State == QRPoseLocker.LockerState.Locked && hasLocked;
        bool canRetry = hasFailed || hasLocked;

        if (setupStartButton != null) setupStartButton.interactable = canStart;
        if (gameStartButton != null) gameStartButton.interactable = canGameStart;
        if (setupAgainButton != null) setupAgainButton.interactable = canRetry;

        if (gameStartButton != null) gameStartButton.gameObject.SetActive(canGameStart);
        if (setupAgainButton != null) setupAgainButton.gameObject.SetActive(canRetry);
    }

    private void SetStatus(string text)
    {
        if (statusText != null) statusText.text = text;
    }

    private void SetTimerVisible(bool visible)
    {
        if (timerText != null) timerText.gameObject.SetActive(visible);
    }

    private T GetComponentAtPath<T>(string path) where T : Component
    {
        if (string.IsNullOrEmpty(path)) return null;
        GameObject go = GameObject.Find(path);
        if (go == null) return null;
        return go.GetComponent<T>();
    }
}
