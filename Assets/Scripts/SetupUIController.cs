using UnityEngine;

/// <summary>
/// セットアップシーン用の旧IMGUIコントローラー（MR/VRでは不使用）。
/// MR/VR は SetupXRUIController を利用してください。
/// </summary>
public class SetupUIController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private QRPoseLocker poseLocker;
    [SerializeField] private QRTrustMonitor trustMonitor;
    [SerializeField] private GameFlowController flow;

    private void Awake()
    {
        if (poseLocker == null)
            poseLocker = FindObjectOfType<QRPoseLocker>();
        if (trustMonitor == null)
            trustMonitor = FindObjectOfType<QRTrustMonitor>();
        if (flow == null)
            flow = GameFlowController.Instance ?? FindObjectOfType<GameFlowController>();

        if (poseLocker != null)
        {
            // 自動開始を抑止
            poseLocker.AutoStartOnEnable = false;
        }
    }

    private void OnEnable()
    {
        if (poseLocker == null) return;
        poseLocker.OnCollectingStarted += HandleCollectingStarted;
        poseLocker.OnPoseLocked += HandlePoseLocked;
        poseLocker.OnLockFailed += HandleLockFailed;
    }

    private void OnDisable()
    {
        if (poseLocker == null) return;
        poseLocker.OnCollectingStarted -= HandleCollectingStarted;
        poseLocker.OnPoseLocked -= HandlePoseLocked;
        poseLocker.OnLockFailed -= HandleLockFailed;
    }

    private void HandleCollectingStarted()
    {
    }

    private void HandlePoseLocked(string uuid, Pose pose)
    {
    }

    private void HandleLockFailed()
    {
    }
}
