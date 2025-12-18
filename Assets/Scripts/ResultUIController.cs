using UnityEngine;

/// <summary>
/// Results シーンで Setup に戻るためのシンプルな UI。
/// </summary>
public class ResultUIController : MonoBehaviour
{
    private ScoreManager score;

    private void Start()
    {
        score = ScoreManager.Instance;
    }

    private void OnGUI()
    {
        const float panelWidth = 260f;
        const float panelHeight = 180f;
        Rect panelRect = new Rect((Screen.width - panelWidth) * 0.5f, (Screen.height - panelHeight) * 0.5f, panelWidth, panelHeight);
        GUILayout.BeginArea(panelRect, GUI.skin.box);

        int finalScore = score != null ? score.CurrentScore : 0;
        GUILayout.Label($"Final Score: {finalScore}", new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter });
        GUILayout.Space(16f);

        if (GUILayout.Button("Play Again (Same Settings)", GUILayout.Height(32f)))
        {
            GameFlowController.Instance?.GoToGameplay();
        }

        if (GUILayout.Button("Back to Setup", GUILayout.Height(32f)))
        {
            GameFlowController.Instance?.GoToSetup();
        }

        if (GUILayout.Button("Exit Game", GUILayout.Height(32f)))
        {
            Application.Quit();
        }

        GUILayout.EndArea();
    }
}
