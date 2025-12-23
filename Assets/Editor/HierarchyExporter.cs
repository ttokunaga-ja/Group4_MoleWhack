using UnityEngine;
using UnityEditor;
using System.IO;

public class HierarchyExporter
{
    [MenuItem("Tools/Export Hierarchy To File")]
    public static void ExportHierarchy()
    {
        string path = Path.Combine(Application.dataPath, "../HierarchyOutput.txt");
        using (StreamWriter writer = new StreamWriter(path))
        {
            foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                WriteGameObject(go, writer, "");
            }
        }
        Debug.Log("Hierarchy exported to " + path);
    }

    static void WriteGameObject(GameObject go, StreamWriter writer, string indent)
    {
        writer.WriteLine(indent + go.name);
        foreach (Transform child in go.transform)
        {
            WriteGameObject(child.gameObject, writer, indent + "  ");
        }
    }
}
