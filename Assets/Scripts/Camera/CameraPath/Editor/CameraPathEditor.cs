using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EchoesOfAether.Camera.Editor
{
    [CustomEditor(typeof(CameraPath))]
    public sealed class CameraPathEditor : UnityEditor.Editor
    {
        SerializedProperty _waypoints;

        void OnEnable()
        {
            _waypoints = serializedObject.FindProperty("waypoints");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();

            EditorGUILayout.Space(8f);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Waypoint Tools", EditorStyles.boldLabel);

                if (GUILayout.Button("Add Waypoint", GUILayout.Height(24f)))
                    AddWaypoint((CameraPath)target);
            }

            serializedObject.ApplyModifiedProperties();
        }

        void AddWaypoint(CameraPath path)
        {
            var sceneName = SanitizeSceneName(SceneManager.GetActiveScene().name);
            var waypointName = GetNextWaypointName(sceneName, path);

            var waypointObject = new GameObject(waypointName);
            Undo.RegisterCreatedObjectUndo(waypointObject, "Add Camera Path Waypoint");

            waypointObject.transform.SetParent(path.transform, false);
            waypointObject.transform.position = GetSpawnPosition(path);
            waypointObject.transform.rotation = Quaternion.identity;

            _waypoints.arraySize++;
            _waypoints.GetArrayElementAtIndex(_waypoints.arraySize - 1).objectReferenceValue =
                waypointObject.transform;

            serializedObject.ApplyModifiedProperties();
            path.RebuildCache();

            EditorUtility.SetDirty(path);
            if (!Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(path.gameObject.scene);

            Selection.activeGameObject = waypointObject;
        }

        static string SanitizeSceneName(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return "Scene";

            return sceneName.Replace(" ", "_");
        }

        static string GetNextWaypointName(string sceneName, CameraPath path)
        {
            var prefix = $"WP_{sceneName}_";
            var maxIndex = 0;

            foreach (var waypoint in path.Waypoints)
            {
                if (waypoint == null) continue;
                if (TryParseWaypointIndex(waypoint.name, prefix, out var index))
                    maxIndex = Mathf.Max(maxIndex, index);
            }

            foreach (Transform child in path.transform)
            {
                if (TryParseWaypointIndex(child.name, prefix, out var index))
                    maxIndex = Mathf.Max(maxIndex, index);
            }

            return $"{prefix}{maxIndex + 1:00}";
        }

        static bool TryParseWaypointIndex(string objectName, string prefix, out int index)
        {
            index = 0;
            if (!objectName.StartsWith(prefix))
                return false;

            var suffix = objectName.Substring(prefix.Length);
            return int.TryParse(suffix, out index);
        }

        static Vector3 GetSpawnPosition(CameraPath path)
        {
            Transform lastWaypoint = null;
            for (var i = path.Waypoints.Count - 1; i >= 0; i--)
            {
                if (path.Waypoints[i] != null)
                {
                    lastWaypoint = path.Waypoints[i];
                    break;
                }
            }

            if (lastWaypoint != null)
            {
                if (path.Waypoints.Count >= 2)
                {
                    Transform previousWaypoint = null;
                    for (var i = path.Waypoints.Count - 2; i >= 0; i--)
                    {
                        if (path.Waypoints[i] != null)
                        {
                            previousWaypoint = path.Waypoints[i];
                            break;
                        }
                    }

                    if (previousWaypoint != null)
                    {
                        var segment = lastWaypoint.position - previousWaypoint.position;
                        segment.y = 0f;
                        if (segment.sqrMagnitude > 0.0001f)
                            return lastWaypoint.position + segment.normalized * 5f;
                    }
                }

                return lastWaypoint.position + Vector3.forward * 5f;
            }

            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null && sceneView.camera != null)
                return sceneView.camera.transform.position;

            return path.transform.position;
        }
    }
}
