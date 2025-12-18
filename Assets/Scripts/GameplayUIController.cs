using UnityEngine;

/// <summary>
/// Gameplay シーン用 UI。
/// - カウントダウン表示
/// - 制限時間とスコア表示
/// - 手動でリザルトへ遷移する End ボタン
/// </summary>
public class GameplayUIController : MonoBehaviour
{
    private GameSessionManager session;
    private ScoreManager score;

    private void Start()
    {
        session = GameSessionManager.Instance;
        score = ScoreManager.Instance;
    }

    private void OnGUI()
    {
        const float padding = 16f;
        const float panelWidth = 200f;
        const float lineHeight = 22f;

        // 左上にカウントダウン/残り時間/スコア
        GUILayout.BeginArea(new Rect(padding, padding, panelWidth, 100f), GUI.skin.box);
        if (session != null)
        {
            GUILayout.Label($"State: {session.State}");
            if (session.State == GameSessionManager.SessionState.Countdown)
            {
                GUILayout.Label($"Countdown: {Mathf.CeilToInt(session.RemainingCountdown)}");
            }
            else if (session.State == GameSessionManager.SessionState.Playing)
            {
                GUILayout.Label($"Time: {Mathf.CeilToInt(session.RemainingPlaySeconds)}s");
            }
        }

        int currentScore = score != null ? score.CurrentScore : 0;
        GUILayout.Label($"Score: {currentScore}");
        GUILayout.EndArea();

        // 右上に End ボタン
        float width = 100f;
        float height = 30f;
        Rect rect = new Rect(Screen.width - width - padding, padding, width, height);
        if (GUI.Button(rect, "End"))
        {
            GameSessionManager.Instance?.EndSession();
        }

        // カウントダウン大きめ表示
        if (session != null && session.State == GameSessionManager.SessionState.Countdown)
        {
            int countdown = Mathf.CeilToInt(session.RemainingCountdown);
            string text = countdown > 0 ? countdown.ToString() : "START!";
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 48,
                alignment = TextAnchor.MiddleCenter
            };
            Rect centerRect = new Rect(0, (Screen.height - 60f) * 0.5f, Screen.width, 60f);
            GUI.Label(centerRect, text, style);
        }
    }
}
