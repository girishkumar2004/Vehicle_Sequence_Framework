using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class CreateSplitObject
{
    static CreateSplitObject()
    {
        EditorApplication.delayCall += Execute;
    }

    private static void Execute()
    {
        var existing = GameObject.Find("split");
        if (existing == null)
        {
            var go = new GameObject("split");
            Undo.RegisterCreatedObjectUndo(go, "Create split GameObject");
            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[CreateSplitObject] Created GameObject 'split' and saved active scene!");
        }
    }
}
