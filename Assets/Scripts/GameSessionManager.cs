using UnityEngine;
using System;

/// <summary>
/// ゲーム進行の中核（カウントダウン→制限時間→終了）を管理するシングルトン。
/// ポイントや時間の設定を Inspector から変更できるようにして柔軟性を持たせる。
/// </summary>
public class GameSessionManager : MonoBehaviour
{
    public static GameSessionManager Instance { get; private set; }

    public enum SessionState
    {
        Idle,
        Countdown,
        Playing,
        Ended
    }

    [Header("Game Settings")]
    [SerializeField] private float countdownSeconds = 3f;
    [SerializeField] private float playDurationSeconds = 30f;
    [SerializeField] private int pointsPerHit = 10;
    [SerializeField] private bool autoStartOnGameplayScene = true;
    [SerializeField] private bool autoGoToResultsOnEnd = true;

    public SessionState State { get; private set; } = SessionState.Idle;
    public float RemainingPlaySeconds { get; private set; }
    public float RemainingCountdown { get; private set; }

    public event Action<float> OnCountdownTick;
    public event Action OnCountdownFinished;
    public event Action<float> OnPlayTick;
    public event Action OnSessionEnded;

    private ScoreManager score;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        score = ScoreManager.Instance ?? CreateScoreManager();
    }

    private ScoreManager CreateScoreManager()
    {
        var go = new GameObject("ScoreManager (Auto)");
        var mgr = go.AddComponent<ScoreManager>();
        DontDestroyOnLoad(go);
        return mgr;
    }

    private void Update()
    {
        if (State == SessionState.Countdown)
        {
            RemainingCountdown -= Time.deltaTime;
            OnCountdownTick?.Invoke(Mathf.Max(0f, RemainingCountdown));
            if (RemainingCountdown <= 0f)
            {
                StartGameplay();
            }
        }
        else if (State == SessionState.Playing)
        {
            RemainingPlaySeconds -= Time.deltaTime;
            OnPlayTick?.Invoke(Mathf.Max(0f, RemainingPlaySeconds));
            if (RemainingPlaySeconds <= 0f)
            {
                EndSession();
            }
        }
    }

    public void BeginSession()
    {
        // セットアップから Gameplay に入った直後に呼ぶ
        score = ScoreManager.Instance ?? CreateScoreManager();
        score.ResetScore();

        RemainingCountdown = Mathf.Max(0f, countdownSeconds);
        RemainingPlaySeconds = Mathf.Max(0f, playDurationSeconds);
        State = countdownSeconds > 0f ? SessionState.Countdown : SessionState.Playing;

        if (State == SessionState.Playing)
        {
            OnCountdownFinished?.Invoke();
        }
    }

    private void StartGameplay()
    {
        RemainingCountdown = 0f;
        OnCountdownFinished?.Invoke();
        RemainingPlaySeconds = Mathf.Max(0f, playDurationSeconds);
        State = SessionState.Playing;
    }

    public void EndSession()
    {
        if (State == SessionState.Ended) return;
        State = SessionState.Ended;
        OnSessionEnded?.Invoke();
        if (autoGoToResultsOnEnd)
        {
            GameFlowController.Instance?.GoToResults();
        }
    }

    public void RegisterHit(string uuid)
    {
        if (State != SessionState.Playing) return;
        score = ScoreManager.Instance ?? CreateScoreManager();
        score.AddScore(pointsPerHit);
    }

    public void ForceResetToIdle()
    {
        State = SessionState.Idle;
        RemainingCountdown = 0f;
        RemainingPlaySeconds = 0f;
    }

    public int GetCurrentScore()
    {
        score = ScoreManager.Instance ?? CreateScoreManager();
        return score.CurrentScore;
    }

    public int PointsPerHit => pointsPerHit;
    public float CountdownSeconds => countdownSeconds;
    public float PlayDurationSeconds => playDurationSeconds;
    public bool AutoStartOnGameplayScene => autoStartOnGameplayScene;
    public bool AutoGoToResultsOnEnd => autoGoToResultsOnEnd;
}
