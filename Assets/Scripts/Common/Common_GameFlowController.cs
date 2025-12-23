using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Setup→Gameplay→Results のシーンフローを管理するシンプルトップレベルコントローラ。
/// シングルトンとして維持し、必要なシーン遷移 API を提供する。
/// </summary>
public class GameFlowController : MonoBehaviour
{
    public static GameFlowController Instance { get; private set; }

    [Header("Scene Names")]
    [SerializeField] private string setupSceneName = "Setup";
    [SerializeField] private string gameplaySceneName = "Gameplay";
    [SerializeField] private string resultsSceneName = "Results";
    [Header("Auto Attach (optional)")]
    [SerializeField] private bool ensureMoleWaveController = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    public void GoToSetup()
    {
        LoadScene(setupSceneName);
    }

    public void GoToGameplay()
    {
        LoadScene(gameplaySceneName);
    }

    public void GoToResults()
    {
        LoadScene(resultsSceneName);
    }

    private void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return;
        SceneManager.LoadScene(sceneName);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == gameplaySceneName)
        {
            // Gameplay に入ったらセッションを開始（カウントダウン→プレイ）
            var session = GameSessionManager.Instance;
            if (session != null && session.AutoStartOnGameplayScene)
            {
                session.BeginSession();
            }

            if (ensureMoleWaveController)
            {
                var mwc = FindObjectOfType<Gameplay_MoleWaveController>();
                if (mwc == null)
                {
                    gameObject.AddComponent<Gameplay_MoleWaveController>();
                    Debug.Log("[GameFlowController] Added Gameplay_MoleWaveController automatically");
                }
            }
        }
        else if (scene.name == setupSceneName)
        {
            // Setup に戻ったらセッションをリセット
            GameSessionManager.Instance?.ForceResetToIdle();
            ScoreManager.Instance?.ResetScore();
        }
    }
}
