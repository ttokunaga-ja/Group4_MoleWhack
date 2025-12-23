using UnityEngine;
using System;

/// <summary>
/// スコア管理の単純なシングルトン。
/// AddScore/Reset で操作し、OnScoreChanged で UI が購読できる。
/// </summary>
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    public event Action<int> OnScoreChanged;

    public int CurrentScore { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        CurrentScore = 0;
    }

    public void ResetScore()
    {
        CurrentScore = 0;
        OnScoreChanged?.Invoke(CurrentScore);
    }

    public void AddScore(int delta)
    {
        CurrentScore += delta;
        OnScoreChanged?.Invoke(CurrentScore);
    }
}
