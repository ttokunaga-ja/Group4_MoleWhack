using UnityEngine;

/// <summary>
/// QR コード情報データクラス
/// UUID ごとに追跡情報を保持する（Singleton で管理）
/// </summary>
public class QRInfo
{
    public string uuid;                    // QR の一意識別子
    public Pose firstPose;                 // 初回検出時の World Pose
    public Pose lastPose;                  // 最終観測時の World Pose
    public bool isTracked;                 // 現在追跡中か
    public System.DateTime firstSeenAt;    // 初回検出時刻
    public float lastSeenTime;             // Time.time での最後の目撃時刻

    public QRInfo(string uuid, Pose firstPose)
    {
        this.uuid = uuid;
        this.firstPose = firstPose;
        this.lastPose = firstPose;
        this.isTracked = true;
        this.firstSeenAt = System.DateTime.UtcNow;
        this.lastSeenTime = Time.time;
    }

    public void UpdatePose(Pose newPose)
    {
        this.lastPose = newPose;
        this.lastSeenTime = Time.time;
    }
}
