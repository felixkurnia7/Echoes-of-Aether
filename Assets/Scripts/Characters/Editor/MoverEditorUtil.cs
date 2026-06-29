#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Shared drawing helpers for the custom inspectors of every <see cref="CharacterMover"/>
/// (NPCWalker, MonsterWalker, PlayerCutsceneMover) so they all look and behave the same.
/// </summary>
internal static class MoverEditorUtil
{
    public static void Section(string title, string help)
    {
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        if (!string.IsNullOrEmpty(help))
            EditorGUILayout.HelpBox(help, MessageType.None);
    }

    public static void HeaderBox(string title, string status)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(status);
        }

        EditorGUILayout.Space(4f);
    }

    /// <summary>Draws the shared Movement fields declared on CharacterMover.</summary>
    public static void DrawMovement(SerializedObject so)
    {
        Section("Movement", null);
        EditorGUILayout.PropertyField(so.FindProperty("moveSpeed"));
        EditorGUILayout.PropertyField(so.FindProperty("turnSpeed"));
        EditorGUILayout.PropertyField(so.FindProperty("arriveThreshold"));
        EditorGUILayout.PropertyField(so.FindProperty("keepCurrentHeight"));
    }

    /// <summary>Draws the shared Animation fields declared on CharacterMover.</summary>
    public static void DrawAnimation(SerializedObject so)
    {
        Section("Animation (optional)",
            "Only parameters that actually exist on the controller are set. Use Speed/IsMoving/MoveX/MoveY for blend-tree rigs, or Move/Idle Bool Param for AnyState-bool rigs (e.g. the Bear's 'WalkForward'/'Idle').");
        EditorGUILayout.PropertyField(so.FindProperty("animator"));
        EditorGUILayout.PropertyField(so.FindProperty("speedParam"));
        EditorGUILayout.PropertyField(so.FindProperty("isMovingParam"));
        EditorGUILayout.PropertyField(so.FindProperty("moveYParam"));
        EditorGUILayout.PropertyField(so.FindProperty("moveXParam"));
        EditorGUILayout.PropertyField(so.FindProperty("moveBoolParam"));
        EditorGUILayout.PropertyField(so.FindProperty("idleBoolParam"));
    }

    /// <summary>
    /// Appends a fresh point GameObject to a list serialized property, placing it
    /// just ahead of the previous point (or the owner), and selects it. For a
    /// List&lt;Transform&gt; leave <paramref name="relativePropName"/> null; for a list of
    /// structs holding a Transform, pass the field name (e.g. "point").
    /// </summary>
    public static void AddTransformPoint(SerializedProperty list, Component owner, string baseName, string relativePropName = null)
    {
        int sequence = list.arraySize + 1;
        string pointName = $"{baseName}_{owner.name}_{sequence:00}";

        var go = new GameObject(pointName);
        Undo.RegisterCreatedObjectUndo(go, "Create Point");

        Scene targetScene = owner.gameObject.scene;
        if (targetScene.IsValid() && go.scene != targetScene)
            SceneManager.MoveGameObjectToScene(go, targetScene);

        go.transform.position = NextPointPosition(list, owner, relativePropName);

        list.arraySize++;
        SerializedProperty element = list.GetArrayElementAtIndex(list.arraySize - 1);
        SerializedProperty slot = relativePropName == null ? element : element.FindPropertyRelative(relativePropName);
        slot.objectReferenceValue = go.transform;
        list.serializedObject.ApplyModifiedProperties();

        Selection.activeGameObject = go;
    }

    /// <summary>
    /// Shows a contextual help/warning box for a resume-after-battle setup: validates the
    /// index against the point list and explains what will happen at runtime.
    /// </summary>
    public static void ResumeIndexHint(SerializedProperty resumeFlag, SerializedProperty resumeIndex, SerializedProperty list, string pointWord)
    {
        if (resumeFlag == null || string.IsNullOrEmpty(resumeFlag.stringValue))
            return;

        int count = list != null ? list.arraySize : 0;
        int index = resumeIndex != null ? resumeIndex.intValue : 0;

        if (count == 0)
        {
            EditorGUILayout.HelpBox($"No {pointWord}s defined yet.", MessageType.Warning);
            return;
        }

        if (index < 0 || index >= count)
        {
            EditorGUILayout.HelpBox($"Index {index} is out of range (0..{count - 1}).", MessageType.Error);
            return;
        }

        int next = index + 1;
        if (next < count)
            EditorGUILayout.HelpBox($"On resume: teleport to {pointWord} [{index}], then walk to {pointWord} [{next}].", MessageType.Info);
        else
            EditorGUILayout.HelpBox($"On resume: teleport to {pointWord} [{index}] (the last one). Add a {pointWord} after it so the character can continue walking.", MessageType.Warning);
    }

    static Vector3 NextPointPosition(SerializedProperty list, Component owner, string relativePropName)
    {
        Vector3 basePosition = owner.transform.position;

        if (list.arraySize > 0)
        {
            SerializedProperty last = list.GetArrayElementAtIndex(list.arraySize - 1);
            SerializedProperty slot = relativePropName == null ? last : last.FindPropertyRelative(relativePropName);
            if (slot.objectReferenceValue is Transform lastTransform && lastTransform != null)
                basePosition = lastTransform.position;
        }

        return basePosition + owner.transform.forward * 2f;
    }
}
#endif
