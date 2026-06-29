#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MonsterWalker))]
public sealed class MonsterWalkerEditor : UnityEditor.Editor
{
    static readonly string[] Tabs = { "Patrol", "Chase", "Animation" };
    const string TabKey = "MonsterWalker_SelectedTab";

    SerializedProperty patrolPoints;
    SerializedProperty loop;
    SerializedProperty waitAtPoint;
    SerializedProperty patrolOnStart;

    SerializedProperty canChase;
    SerializedProperty player;
    SerializedProperty detectionRadius;
    SerializedProperty loseInterestRadius;
    SerializedProperty stopDistance;

    void OnEnable()
    {
        patrolPoints = serializedObject.FindProperty("patrolPoints");
        loop = serializedObject.FindProperty("loop");
        waitAtPoint = serializedObject.FindProperty("waitAtPoint");
        patrolOnStart = serializedObject.FindProperty("patrolOnStart");

        canChase = serializedObject.FindProperty("canChase");
        player = serializedObject.FindProperty("player");
        detectionRadius = serializedObject.FindProperty("detectionRadius");
        loseInterestRadius = serializedObject.FindProperty("loseInterestRadius");
        stopDistance = serializedObject.FindProperty("stopDistance");
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
            case 0: DrawPatrolTab(); break;
            case 1: DrawChaseTab(); break;
            case 2: MoverEditorUtil.DrawAnimation(serializedObject); break;
        }

        serializedObject.ApplyModifiedProperties();

        DrawRuntimeControls();
    }

    void DrawHeaderBox()
    {
        int count = patrolPoints != null ? patrolPoints.arraySize : 0;
        string status = Application.isPlaying
            ? (((MonsterWalker)target).IsActive ? "Active" : "Stopped")
            : "Edit Mode";

        MoverEditorUtil.HeaderBox("Monster Walker", $"Patrol Points: {count}    Status: {status}");
    }

    void DrawPatrolTab()
    {
        MoverEditorUtil.Section("Patrol Points", "Ordered points the monster walks between. Loop returns to the first point; otherwise it ping-pongs back and forth.");

        EditorGUILayout.PropertyField(patrolPoints, new GUIContent("Patrol Points"), true);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Patrol Point", GUILayout.Height(22f)))
                MoverEditorUtil.AddTransformPoint(patrolPoints, (MonsterWalker)target, "Patrol");

            using (new EditorGUI.DisabledScope(patrolPoints.arraySize == 0))
            {
                if (GUILayout.Button("Remove Last", GUILayout.Height(22f)))
                    patrolPoints.arraySize--;
            }
        }

        EditorGUILayout.Space(8f);
        MoverEditorUtil.Section("Patrol Settings", null);
        EditorGUILayout.PropertyField(loop);
        EditorGUILayout.PropertyField(waitAtPoint);
        EditorGUILayout.PropertyField(patrolOnStart);

        EditorGUILayout.Space(8f);
        MoverEditorUtil.DrawMovement(serializedObject);
    }

    void DrawChaseTab()
    {
        MoverEditorUtil.Section("Chase (optional)", "When enabled, the monster leaves its patrol to chase the player within the detection radius, and gives up once the player is beyond the lose-interest radius.");
        EditorGUILayout.PropertyField(canChase);

        using (new EditorGUI.DisabledScope(!canChase.boolValue))
        {
            EditorGUILayout.PropertyField(player);
            EditorGUILayout.PropertyField(detectionRadius);
            EditorGUILayout.PropertyField(loseInterestRadius);
            EditorGUILayout.PropertyField(stopDistance);
        }
    }

    void DrawRuntimeControls()
    {
        if (!Application.isPlaying)
            return;

        EditorGUILayout.Space(8f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Runtime Test", EditorStyles.boldLabel);
            var walker = (MonsterWalker)target;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Start Patrol", GUILayout.Height(24f)))
                    walker.StartPatrol();

                if (GUILayout.Button("Stop Patrol", GUILayout.Height(24f)))
                    walker.StopPatrol();
            }
        }
    }
}
#endif
