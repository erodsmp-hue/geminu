#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

public static class BackroomsHierarchyContextExporter
{
    [MenuItem("Tools/Backrooms/Export Hierarchy Context Report")]
    public static void ExportReport()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog("Hierarchy Export", "No active loaded scene found.", "OK");
            return;
        }

        StringBuilder sb = new StringBuilder(1024 * 64);

        sb.AppendLine("=== BACKROOMS HIERARCHY CONTEXT REPORT ===");
        sb.AppendLine("Scene: " + scene.name);
        sb.AppendLine("Path: " + scene.path);
        sb.AppendLine("Generated: " + System.DateTime.Now);
        sb.AppendLine();

        AppendSceneSummary(sb, scene);
        AppendRootHierarchy(sb, scene);
        AppendImportantObjects(sb);
        AppendLightReport(sb);
        AppendCameraReport(sb);
        AppendCanvasReport(sb);
        AppendAudioReport(sb);
        AppendVolumeReport(sb);
        AppendNamedTransformChecks(sb);

        string sceneName = string.IsNullOrWhiteSpace(scene.name) ? "UnnamedScene" : scene.name;
        string defaultName = $"BackroomsHierarchyReport_{sceneName}.txt";
        string path = EditorUtility.SaveFilePanel(
            "Save Hierarchy Context Report",
            Application.dataPath,
            defaultName,
            "txt"
        );

        if (string.IsNullOrWhiteSpace(path))
            return;

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Hierarchy Export Complete",
            "Saved report to:\n" + path,
            "OK"
        );

        Debug.Log(sb.ToString());
    }

    private static void AppendSceneSummary(StringBuilder sb, Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        int totalObjects = 0;
        int totalComponents = 0;

        foreach (GameObject root in roots)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            totalObjects += all.Length;

            foreach (Transform t in all)
                totalComponents += t.GetComponents<Component>().Length;
        }

        sb.AppendLine("=== SCENE SUMMARY ===");
        sb.AppendLine("Root GameObjects: " + roots.Length);
        sb.AppendLine("Total GameObjects: " + totalObjects);
        sb.AppendLine("Total Components: " + totalComponents);
        sb.AppendLine();
    }

    private static void AppendRootHierarchy(StringBuilder sb, Scene scene)
    {
        sb.AppendLine("=== FULL HIERARCHY ===");
        foreach (GameObject root in scene.GetRootGameObjects())
            AppendTransformRecursive(sb, root.transform, 0);
        sb.AppendLine();
    }

    private static void AppendTransformRecursive(StringBuilder sb, Transform t, int depth)
    {
        string indent = new string(' ', depth * 2);
        GameObject go = t.gameObject;

        sb.Append(indent)
          .Append("- ")
          .Append(go.name)
          .Append("  [")
          .Append(go.activeInHierarchy ? "Active" : "Inactive")
          .Append("]")
          .Append(" Tag=").Append(go.tag)
          .Append(" Layer=").Append(LayerMask.LayerToName(go.layer))
          .AppendLine();

        Component[] comps = go.GetComponents<Component>();
        foreach (Component c in comps)
        {
            string cname = c == null ? "MISSING_SCRIPT" : c.GetType().Name;
            sb.Append(indent).Append("    • ").Append(cname).AppendLine();
        }

        for (int i = 0; i < t.childCount; i++)
            AppendTransformRecursive(sb, t.GetChild(i), depth + 1);
    }

    private static void AppendImportantObjects(StringBuilder sb)
    {
        sb.AppendLine("=== IMPORTANT OBJECT SEARCH ===");
        AppendObjectsOfType<Light>(sb, "Lights");
        AppendObjectsOfType<Camera>(sb, "Cameras");
        AppendObjectsOfType<AudioSource>(sb, "AudioSources");
        AppendObjectsOfType<Canvas>(sb, "Canvas");
        AppendObjectsOfType<CharacterController>(sb, "CharacterControllers");
        sb.AppendLine();
    }

    private static void AppendObjectsOfType<T>(StringBuilder sb, string label) where T : Component
    {
        T[] found = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        sb.AppendLine(label + ": " + found.Length);
        foreach (T item in found)
            sb.AppendLine(" - " + GetFullPath(item.transform) + " [" + item.GetType().Name + "]");
    }

    private static void AppendLightReport(StringBuilder sb)
    {
        sb.AppendLine("=== LIGHT REPORT ===");
        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Light l in lights)
        {
            sb.AppendLine(GetFullPath(l.transform));
            sb.AppendLine("  Type=" + l.type +
                          " Enabled=" + l.enabled +
                          " Active=" + l.gameObject.activeInHierarchy +
                          " Intensity=" + l.intensity +
                          " Range=" + l.range +
                          " Color=" + l.color +
                          " Shadows=" + l.shadows);

            Component flicker = l.GetComponent("BackroomsSectionLightFlicker");
            if (flicker != null)
                sb.AppendLine("  Has BackroomsSectionLightFlicker");

            sb.AppendLine();
        }
    }

    private static void AppendCameraReport(StringBuilder sb)
    {
        sb.AppendLine("=== CAMERA REPORT ===");
        Camera[] cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Camera c in cams)
        {
            sb.AppendLine(GetFullPath(c.transform));
            sb.AppendLine("  Enabled=" + c.enabled +
                          " FOV=" + c.fieldOfView +
                          " Near=" + c.nearClipPlane +
                          " Far=" + c.farClipPlane +
                          " HDR=" + c.allowHDR);

            foreach (Component comp in c.GetComponents<Component>())
            {
                if (comp == null)
                    sb.AppendLine("  Component=MISSING_SCRIPT");
                else
                    sb.AppendLine("  Component=" + comp.GetType().Name);
            }

            sb.AppendLine();
        }
    }

    private static void AppendCanvasReport(StringBuilder sb)
    {
        sb.AppendLine("=== UI / CANVAS REPORT ===");
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas c in canvases)
        {
            sb.AppendLine(GetFullPath(c.transform));
            sb.AppendLine("  RenderMode=" + c.renderMode +
                          " Enabled=" + c.enabled +
                          " SortingOrder=" + c.sortingOrder);

            CanvasGroup[] groups = c.GetComponentsInChildren<CanvasGroup>(true);
            sb.AppendLine("  CanvasGroups=" + groups.Length);

            sb.AppendLine();
        }
    }

    private static void AppendAudioReport(StringBuilder sb)
    {
        sb.AppendLine("=== AUDIO REPORT ===");
        AudioSource[] sources = Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (AudioSource a in sources)
        {
            sb.AppendLine(GetFullPath(a.transform));
            sb.AppendLine("  Enabled=" + a.enabled +
                          " Clip=" + (a.clip ? a.clip.name : "None") +
                          " Loop=" + a.loop +
                          " PlayOnAwake=" + a.playOnAwake +
                          " SpatialBlend=" + a.spatialBlend +
                          " Volume=" + a.volume +
                          " Pitch=" + a.pitch);
            sb.AppendLine();
        }
    }

    private static void AppendVolumeReport(StringBuilder sb)
    {
        sb.AppendLine("=== VOLUME / POST FX REPORT ===");
        Volume[] volumes = Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        sb.AppendLine("Volumes found: " + volumes.Length);

        foreach (Volume v in volumes)
        {
            sb.AppendLine(GetFullPath(v.transform));
            sb.AppendLine("  Enabled=" + v.enabled +
                          " Global=" + v.isGlobal +
                          " Priority=" + v.priority +
                          " Weight=" + v.weight +
                          " Profile=" + (v.profile ? v.profile.name : "None"));
            sb.AppendLine();
        }

        sb.AppendLine("Fog Enabled=" + RenderSettings.fog);
        sb.AppendLine("Fog Color=" + RenderSettings.fogColor);
        sb.AppendLine("Fog Density=" + RenderSettings.fogDensity);
        sb.AppendLine();
    }

    private static void AppendNamedTransformChecks(StringBuilder sb)
    {
        sb.AppendLine("=== EXPECTED NAME CHECKS ===");
        string[] expectedNames =
        {
            "CameraMotionPivot",
            "CameraEffectsPivot",
            "HeldFlashlightPivot"
        };

        foreach (string expected in expectedNames)
        {
            Transform found = FindTransformByName(expected);
            sb.AppendLine(expected + ": " + (found ? GetFullPath(found) : "NOT FOUND"));
        }

        sb.AppendLine();
    }

    private static Transform FindTransformByName(string targetName)
    {
        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform t in all)
        {
            if (t.name == targetName)
                return t;
        }
        return null;
    }

    private static string GetFullPath(Transform t)
    {
        if (t == null) return "(null)";
        StringBuilder sb = new StringBuilder(t.name);
        while (t.parent != null)
        {
            t = t.parent;
            sb.Insert(0, t.name + "/");
        }
        return sb.ToString();
    }
}
#endif
