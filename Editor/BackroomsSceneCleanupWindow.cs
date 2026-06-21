#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BackroomsSceneCleanupWindow : EditorWindow
{
    [Serializable]
    private class ComponentSnapshot
    {
        public string gameObjectPath;
        public string typeName;
        public bool enabled = true;
    }

    [Serializable]
    private class CleanupSnapshot
    {
        public string scenePath;
        public List<ComponentSnapshot> removed = new List<ComponentSnapshot>();
    }

    private const string SnapshotKey = "BackroomsSceneCleanupWindow_Snapshot";
    private Vector2 scroll;

    private static readonly string[] RemoveFromMainCamera =
    {
        "FlashlightBatteryUI",
        "BackroomsSprintFX",
        "BackroomsHorrorHUD",
        "BackroomsAutoHorrorHUD",
        "BackroomsHorrorHUDMinimalPolished",
        "BackroomsHorrorHUDMinimal1080p",
        "BatteryInventoryCanvasUI",
        "BatteryInventoryCanvasUISimpleCleanV2",
        "BatteryInventoryCanvasUIBackroomsHorror",
        "BatteryInventoryCanvasUIBackroomsHorrorV2",
        "BatteryInventoryCanvasUIBackroomsMinimal",
        "BatteryInventoryCanvasUIBackroomsMinimalFixed",
        "BatteryInventoryCanvasUIBackroomsMinimalFixedV2",
        "BatteryInventoryCanvasUIBackroomsMinimalPolished",
        "BatteryInventoryCanvasUIBackroomsCleanCentered"
    };

    [MenuItem("Tools/Backrooms/Scene Cleanup + Revert")]
    public static void ShowWindow()
    {
        GetWindow<BackroomsSceneCleanupWindow>("Backrooms Cleanup");
    }

    private void OnGUI()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Backrooms Scene Cleanup", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "This tool removes duplicate HUD, battery, and inventory UI scripts from the active scene and keeps only the core gameplay scripts. It also stores a snapshot so you can revert the removal later.",
            MessageType.Info);

        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("Current cleanup targets", EditorStyles.boldLabel);
        foreach (string name in RemoveFromMainCamera)
            EditorGUILayout.LabelField("- " + name);

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Recommended keepers based on your current scene: BackroomsPlayer, BackroomsPlayerVitals, BackroomsHeadBob, FlashlightBatterySystem, BatteryInventory, BatteryInventoryUseInput, BackroomsHum, BackroomsFogPulse, BackroomsSectionLightFlicker. This tool does not remove those. [file:20][file:1]",
            MessageType.None);

        GUI.enabled = FindMainCamera() != null;
        if (GUILayout.Button("Clean Scene"))
            CleanScene();
        GUI.enabled = HasSnapshot();
        if (GUILayout.Button("Revert Cleanup"))
            RevertCleanup();
        GUI.enabled = true;

        EditorGUILayout.Space();
        DrawStatus();
        EditorGUILayout.EndScrollView();
    }

    private void DrawStatus()
    {
        Camera cam = FindMainCamera();
        if (cam == null)
        {
            EditorGUILayout.HelpBox("No Main Camera found in the active scene.", MessageType.Warning);
            return;
        }

        var behaviours = cam.GetComponents<MonoBehaviour>()
            .Where(b => b != null)
            .Select(b => b.GetType().Name)
            .ToList();

        EditorGUILayout.LabelField("Main Camera scripts now", EditorStyles.boldLabel);
        foreach (var name in behaviours)
            EditorGUILayout.LabelField("- " + name);

        if (HasSnapshot())
            EditorGUILayout.HelpBox("A revert snapshot exists for this editor session/project prefs.", MessageType.Info);
    }

    private void CleanScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        Camera cam = FindMainCamera();
        if (cam == null)
        {
            EditorUtility.DisplayDialog("Backrooms Cleanup", "No Main Camera found.", "OK");
            return;
        }

        var snapshot = new CleanupSnapshot { scenePath = scene.path };
        int removedCount = 0;

        Undo.SetCurrentGroupName("Backrooms Scene Cleanup");
        int group = Undo.GetCurrentGroup();

        foreach (MonoBehaviour mb in cam.GetComponents<MonoBehaviour>())
        {
            if (mb == null) continue;
            string typeName = mb.GetType().Name;
            if (!RemoveFromMainCamera.Contains(typeName)) continue;

            snapshot.removed.Add(new ComponentSnapshot
            {
                gameObjectPath = GetGameObjectPath(mb.gameObject),
                typeName = mb.GetType().AssemblyQualifiedName,
                enabled = GetEnabledState(mb)
            });

            Undo.DestroyObjectImmediate(mb);
            removedCount++;
        }

        SaveSnapshot(snapshot);
        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog(
            "Backrooms Cleanup",
            "Cleanup complete. Removed " + removedCount + " duplicate/legacy UI scripts from Main Camera and stored a revert snapshot.",
            "OK");
    }

    private void RevertCleanup()
    {
        CleanupSnapshot snapshot = LoadSnapshot();
        if (snapshot == null || snapshot.removed == null || snapshot.removed.Count == 0)
        {
            EditorUtility.DisplayDialog("Backrooms Cleanup", "No cleanup snapshot found.", "OK");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        int restored = 0;

        Undo.SetCurrentGroupName("Backrooms Scene Cleanup Revert");
        int group = Undo.GetCurrentGroup();

        foreach (ComponentSnapshot item in snapshot.removed)
        {
            GameObject go = FindByPath(item.gameObjectPath);
            if (go == null) continue;

            Type type = Type.GetType(item.typeName);
            if (type == null || !typeof(Component).IsAssignableFrom(type)) continue;
            if (go.GetComponent(type) != null) continue;

            Component c = Undo.AddComponent(go, type);
            SetEnabledState(c, item.enabled);
            restored++;
        }

        Undo.CollapseUndoOperations(group);
        EditorSceneManager.MarkSceneDirty(scene);
        ClearSnapshot();

        EditorUtility.DisplayDialog(
            "Backrooms Cleanup",
            "Revert complete. Restored " + restored + " removed components.",
            "OK");
    }

    private static Camera FindMainCamera()
    {
        Camera cam = Camera.main;
        if (cam != null) return cam;
        return FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(c => c.CompareTag("MainCamera"))
            ?? FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
    }

    private static string GetGameObjectPath(GameObject obj)
    {
        if (obj == null) return string.Empty;
        string path = obj.name;
        Transform current = obj.transform.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }
        return path;
    }

    private static GameObject FindByPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        string[] parts = path.Split('/');
        GameObject root = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(r => r.name == parts[0]);
        if (root == null) return null;
        Transform current = root.transform;
        for (int i = 1; i < parts.Length; i++)
        {
            current = current.Find(parts[i]);
            if (current == null) return null;
        }
        return current.gameObject;
    }

    private static bool GetEnabledState(Component component)
    {
        PropertyInfo prop = component.GetType().GetProperty("enabled", BindingFlags.Public | BindingFlags.Instance);
        if (prop != null && prop.PropertyType == typeof(bool) && prop.CanRead)
            return (bool)prop.GetValue(component);
        return true;
    }

    private static void SetEnabledState(Component component, bool value)
    {
        if (component == null) return;
        PropertyInfo prop = component.GetType().GetProperty("enabled", BindingFlags.Public | BindingFlags.Instance);
        if (prop != null && prop.PropertyType == typeof(bool) && prop.CanWrite)
            prop.SetValue(component, value);
    }

    private static bool HasSnapshot()
    {
        return !string.IsNullOrEmpty(EditorPrefs.GetString(SnapshotKey, string.Empty));
    }

    private static void SaveSnapshot(CleanupSnapshot snapshot)
    {
        EditorPrefs.SetString(SnapshotKey, JsonUtility.ToJson(snapshot));
    }

    private static CleanupSnapshot LoadSnapshot()
    {
        string json = EditorPrefs.GetString(SnapshotKey, string.Empty);
        if (string.IsNullOrEmpty(json)) return null;
        return JsonUtility.FromJson<CleanupSnapshot>(json);
    }

    private static void ClearSnapshot()
    {
        EditorPrefs.DeleteKey(SnapshotKey);
    }
}
#endif
