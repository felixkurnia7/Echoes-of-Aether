#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[CustomEditor(typeof(NPCWalker))]
public sealed class NPCWalkerEditor : UnityEditor.Editor
{
    static readonly string[] Tabs = { "Path", "Proximity", "Animation", "Events" };
    const string TabKey = "NPCWalker_SelectedTab";

    SerializedProperty waypoints;
    SerializedProperty moveSpeed;
    SerializedProperty turnSpeed;
    SerializedProperty arriveThreshold;
    SerializedProperty keepCurrentHeight;
    SerializedProperty walkOnStart;

    SerializedProperty player;
    SerializedProperty playerNearbyRadius;

    SerializedProperty animator;
    SerializedProperty speedParam;
    SerializedProperty isMovingParam;
    SerializedProperty moveYParam;
    SerializedProperty moveXParam;
    SerializedProperty moveBoolParam;
    SerializedProperty idleBoolParam;

    SerializedProperty onFinished;
    SerializedProperty resumeIfStoryFlag;
    SerializedProperty resumeFromWaypointIndex;

    void OnEnable()
    {
        waypoints = serializedObject.FindProperty("waypoints");
        moveSpeed = serializedObject.FindProperty("moveSpeed");
        turnSpeed = serializedObject.FindProperty("turnSpeed");
        arriveThreshold = serializedObject.FindProperty("arriveThreshold");
        keepCurrentHeight = serializedObject.FindProperty("keepCurrentHeight");
        walkOnStart = serializedObject.FindProperty("walkOnStart");

        player = serializedObject.FindProperty("player");
        playerNearbyRadius = serializedObject.FindProperty("playerNearbyRadius");

        animator = serializedObject.FindProperty("animator");
        speedParam = serializedObject.FindProperty("speedParam");
        isMovingParam = serializedObject.FindProperty("isMovingParam");
        moveYParam = serializedObject.FindProperty("moveYParam");
        moveXParam = serializedObject.FindProperty("moveXParam");
        moveBoolParam = serializedObject.FindProperty("moveBoolParam");
        idleBoolParam = serializedObject.FindProperty("idleBoolParam");

        onFinished = serializedObject.FindProperty("onFinished");
        resumeIfStoryFlag = serializedObject.FindProperty("resumeIfStoryFlag");
        resumeFromWaypointIndex = serializedObject.FindProperty("resumeFromWaypointIndex");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawHeaderBox();

        int tab = SessionState.GetInt(TabKey, 0);
        tab = GUILayout.Toolbar(tab, Tabs, GUILayout.Height(26f));
        SessionState.SetInt(TabKey, tab);

        EditorGUILayout.Space(6f);

        switch (tab)
        {
            case 0: DrawPathTab(); break;
            case 1: DrawProximityTab(); break;
            case 2: DrawAnimationTab(); break;
            case 3: DrawEventsTab(); break;
        }

        serializedObject.ApplyModifiedProperties();

        DrawRuntimeControls();
    }

    void DrawHeaderBox()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("NPC Walker", EditorStyles.boldLabel);

            int count = waypoints != null ? waypoints.arraySize : 0;
            string status = Application.isPlaying
                ? (((NPCWalker)target).IsWalking ? "Walking" : "Idle")
                : "Edit Mode";

            EditorGUILayout.LabelField($"Waypoints: {count}    Status: {status}");
        }

        EditorGUILayout.Space(4f);
    }

    void DrawPathTab()
    {
        Section("Waypoints", "Ordered points the NPC walks through. Each waypoint can run an action and wait for the player, a sub-objective, or a story flag before continuing.");

        EditorGUILayout.PropertyField(waypoints, new GUIContent("Waypoints"), true);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Waypoint", GUILayout.Height(22f)))
                AddWaypointWithObject();

            using (new EditorGUI.DisabledScope(waypoints.arraySize == 0))
            {
                if (GUILayout.Button("Remove Last", GUILayout.Height(22f)))
                    waypoints.arraySize--;
            }
        }

        EditorGUILayout.Space(8f);
        Section("Movement", null);
        EditorGUILayout.PropertyField(moveSpeed);
        EditorGUILayout.PropertyField(turnSpeed);
        EditorGUILayout.PropertyField(arriveThreshold);
        EditorGUILayout.PropertyField(keepCurrentHeight);
        EditorGUILayout.PropertyField(walkOnStart);
    }

    void DrawProximityTab()
    {
        Section("Player Proximity", "Used by each waypoint's 'Wait For Player Nearby' gate. The player is auto-found at runtime if left empty.");
        EditorGUILayout.PropertyField(player);
        EditorGUILayout.PropertyField(playerNearbyRadius);
    }

    void DrawAnimationTab()
    {
        Section("Animation (optional)", "Animator parameters driven while walking. Leave a parameter name empty to skip it. Only parameters that actually exist on the controller are set. Use Speed/IsMoving/MoveX/MoveY for blend-tree rigs, or Move/Idle Bool Param for AnyState-bool rigs (e.g. the Bear's 'WalkForward'/'Idle').");
        EditorGUILayout.PropertyField(animator);
        EditorGUILayout.PropertyField(speedParam);
        EditorGUILayout.PropertyField(isMovingParam);
        EditorGUILayout.PropertyField(moveYParam);
        EditorGUILayout.PropertyField(moveXParam);
        EditorGUILayout.PropertyField(moveBoolParam);
        EditorGUILayout.PropertyField(idleBoolParam);
    }

    void DrawEventsTab()
    {
        Section("On Finished", "Invoked once after the NPC reaches the final waypoint.");
        EditorGUILayout.PropertyField(onFinished);

        EditorGUILayout.Space(8f);
        Section("Resume After Battle", "If the story flag is already true when the scene loads (e.g. after winning a battle), the NPC teleports onto 'Resume From Waypoint Index' (the point it had reached before the battle) and then walks to the NEXT waypoint. That waypoint's own action/block is NOT re-run, so the battle won't trigger again. Add at least one waypoint after it for the NPC to continue to.");
        EditorGUILayout.PropertyField(resumeIfStoryFlag, new GUIContent("Resume If Story Flag"));
        EditorGUILayout.PropertyField(resumeFromWaypointIndex, new GUIContent("Resume From Waypoint Index"));

        MoverEditorUtil.ResumeIndexHint(resumeIfStoryFlag, resumeFromWaypointIndex, waypoints, "waypoint");
    }

    void DrawRuntimeControls()
    {
        if (!Application.isPlaying)
            return;

        EditorGUILayout.Space(8f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Runtime Test", EditorStyles.boldLabel);
            var walker = (NPCWalker)target;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Start Walk", GUILayout.Height(24f)))
                    walker.StartWalk();

                if (GUILayout.Button("Stop Walk", GUILayout.Height(24f)))
                    walker.StopWalk();
            }
        }
    }

    void AddWaypointWithObject()
    {
        var walker = (NPCWalker)target;

        int sequence = waypoints.arraySize + 1;
        string waypointName = $"WP_{walker.name}_{sequence:00}";

        var go = new GameObject(waypointName);
        Undo.RegisterCreatedObjectUndo(go, "Create Waypoint");

        Scene targetScene = walker.gameObject.scene;
        if (targetScene.IsValid() && go.scene != targetScene)
            SceneManager.MoveGameObjectToScene(go, targetScene);

        go.transform.position = GetNextWaypointPosition(walker);

        waypoints.arraySize++;
        SerializedProperty element = waypoints.GetArrayElementAtIndex(waypoints.arraySize - 1);
        element.FindPropertyRelative("point").objectReferenceValue = go.transform;

        serializedObject.ApplyModifiedProperties();
        Selection.activeGameObject = go;
    }

    Vector3 GetNextWaypointPosition(NPCWalker walker)
    {
        Vector3 basePosition = walker.transform.position;

        if (waypoints.arraySize > 0)
        {
            SerializedProperty lastPoint = waypoints
                .GetArrayElementAtIndex(waypoints.arraySize - 1)
                .FindPropertyRelative("point");

            if (lastPoint.objectReferenceValue is Transform lastTransform && lastTransform != null)
                basePosition = lastTransform.position;
        }

        return basePosition + walker.transform.forward * 2f;
    }

    static void Section(string title, string help)
    {
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        if (!string.IsNullOrEmpty(help))
            EditorGUILayout.HelpBox(help, MessageType.None);
    }
}
#endif
