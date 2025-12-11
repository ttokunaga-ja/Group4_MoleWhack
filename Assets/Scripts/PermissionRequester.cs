using System.Collections;
using UnityEngine;
using UnityEngine.Android;

public class PermissionRequester : MonoBehaviour
{
    void Start()
    {
        StartCoroutine(RequestPermissions());
    }

    IEnumerator RequestPermissions()
    {
        // 1. Wait for system initialization
        yield return new WaitForSeconds(3.0f);

        // 2. Define permissions
        string scenePermission = "com.oculus.permission.USE_SCENE";
        
        // 3. Request Scene Permission if not granted
        if (!Permission.HasUserAuthorizedPermission(scenePermission))
        {
            var callbacks = new PermissionCallbacks();
            callbacks.PermissionDenied += (p) => Debug.LogWarning($"Denied: {p}");
            callbacks.PermissionGranted += (p) => Debug.Log($"Granted: {p}");
            Permission.RequestUserPermission(scenePermission, callbacks);
        }
    }
}