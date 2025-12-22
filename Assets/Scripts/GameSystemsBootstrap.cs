using UnityEngine;

/// <summary>
/// スコアとセッション管理の必須コンポーネントを自動生成するブートストラップ。
/// シーン配置漏れによる Null を防ぐ。
/// </summary>
public static class GameSystemsBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureSystems()
    {
        if (ScoreManager.Instance == null)
        {
            var scoreGo = new GameObject("ScoreManager (Auto)");
            scoreGo.AddComponent<ScoreManager>();
            Object.DontDestroyOnLoad(scoreGo);
        }

        if (GameSessionManager.Instance == null)
        {
            var sessionGo = new GameObject("GameSessionManager (Auto)");
            sessionGo.AddComponent<GameSessionManager>();
            Object.DontDestroyOnLoad(sessionGo);
        }
    }
}
