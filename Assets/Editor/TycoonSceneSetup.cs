#if UNITY_EDITOR
using Tycoon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class TycoonSceneSetup
{
    const string ScenePath = "Assets/Scenes/TycoonScene.unity";

    [MenuItem("Tools/Tycoon/Create Tycoon Scene")]
    public static void CreateScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var gameGO = new GameObject("TycoonGame");
        gameGO.AddComponent<GameManager>();

        EditorSceneManager.SaveScene(scene, ScenePath);
        Selection.activeGameObject = gameGO;
        Debug.Log($"Tycoon scene created at {ScenePath}. Press Play to test it.");
    }
}
#endif
