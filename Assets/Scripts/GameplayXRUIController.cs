using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// XR 向け Gameplay UI（World Space Canvas 用）。
/// カウントダウン、残り時間、スコア表示と End ボタンを提供する。
/// </summary>
public class GameplayXRUIController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI stateText;
    [SerializeField] private TextMeshProUGUI timeText;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI countdownText;
    [SerializeField] private Button endButton;

    [Header("Lookup Paths (under OVRCameraRig/Canvas)")]
    [SerializeField] private string stateTextPath = "OVRCameraRig/TrackingSpace/CenterEyeAnchor/Canvas/Text_State (TMP)";
    [SerializeField] private string timeTextPath = "OVRCameraRig/TrackingSpace/CenterEyeAnchor/Canvas/Text_Time (TMP)";
    [SerializeField] private string scoreTextPath = "OVRCameraRig/TrackingSpace/CenterEyeAnchor/Canvas/Text_Score (TMP)";
    [SerializeField] private string countdownTextPath = "OVRCameraRig/TrackingSpace/CenterEyeAnchor/Canvas/Text_Countdown (TMP)";
    [SerializeField] private string endButtonPath = "OVRCameraRig/TrackingSpace/CenterEyeAnchor/Canvas/Button_End (TMP)";

    private GameSessionManager session;
    private ScoreManager score;

    private void Start()
    {
        session = GameSessionManager.Instance;
        score = ScoreManager.Instance;

        // 明示パスで取得（OVRCameraRig/Canvas 配下を前提）
        stateText = stateText ?? GetComponentAtPath<TextMeshProUGUI>(stateTextPath);
        timeText = timeText ?? GetComponentAtPath<TextMeshProUGUI>(timeTextPath);
        scoreText = scoreText ?? GetComponentAtPath<TextMeshProUGUI>(scoreTextPath);
        countdownText = countdownText ?? GetComponentAtPath<TextMeshProUGUI>(countdownTextPath);
        endButton = endButton ?? GetComponentAtPath<Button>(endButtonPath);

        if (endButton != null)
        {
            endButton.onClick.AddListener(HandleEndClicked);
        }
    }

    private void OnDestroy()
    {
        if (endButton != null)
        {
            endButton.onClick.RemoveListener(HandleEndClicked);
        }
    }

    private void Update()
    {
        if (session == null || score == null) return;

        if (stateText != null)
        {
            stateText.text = session.State.ToString();
        }

        if (session.State == GameSessionManager.SessionState.Countdown)
        {
            if (countdownText != null)
            {
                int c = Mathf.CeilToInt(session.RemainingCountdown);
                countdownText.text = c > 0 ? c.ToString() : "START!";
                countdownText.gameObject.SetActive(true);
            }
            if (timeText != null) timeText.text = "";
        }
        else
        {
            if (countdownText != null) countdownText.gameObject.SetActive(false);
            if (timeText != null)
            {
                int t = Mathf.CeilToInt(session.RemainingPlaySeconds);
                timeText.text = $"Time: {t}s";
            }
        }

        if (scoreText != null)
        {
            scoreText.text = $"Score: {score.CurrentScore}";
        }
    }

    private void HandleEndClicked()
    {
        session?.EndSession();
    }

    private T GetComponentAtPath<T>(string path) where T : Component
    {
        if (string.IsNullOrEmpty(path)) return null;
        GameObject go = GameObject.Find(path);
        if (go == null) return null;
        return go.GetComponent<T>();
    }
}
