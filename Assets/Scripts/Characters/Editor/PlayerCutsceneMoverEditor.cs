#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerCutsceneMover))]
public sealed class PlayerCutsceneMoverEditor : UnityEditor.Editor
{
    static readonly string[] Tabs = { "Path", "Control", "Animation" };
    const string TabKey = "PlayerCutsceneMover_SelectedTab";

    SerializedProperty path;
    SerializedProperty controlToDisable;
    SerializedProperty resumeIfStoryFlag;
    SerializedProperty resumeFromPointIndex;

    void OnEnable()
    {
        path = serializedObject.FindProperty("path");
        controlToDisable = serializedObject.FindProperty("controlToDisable");
        resumeIfStoryFlag = serializedObject.FindProperty("resumeIfStoryFlag");
        resumeFromPointIndex = serializedObject.FindProperty("resumeFromPointIndex");
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
            case 1: DrawControlTab(); break;
            case 2: MoverEditorUtil.DrawAnimation(serializedObject); break;
        }

        serializedObject.ApplyModifiedProperties();

        DrawRuntimeControls();
    }

    void DrawHeaderBox()
    {
        int count = path != null ? path.arraySize : 0;
        string status = Application.isPlaying
            ? (((PlayerCutsceneMover)target).IsPlaying ? "Playing" : "Idle")
            : "Edit Mode";

        MoverEditorUtil.HeaderBox("Player Cutscene Mover", $"Path Points: {count}    Status: {status}");
    }

    void DrawPathTab()
    {
        MoverEditorUtil.Section("Cutscene Path", "Ordered points the character walks through during the cutscene. Control is suspended while it plays and restored afterwards. Each point can stop and wait for a sub-objective before continuing.");

        EditorGUILayout.PropertyField(path, new GUIContent("Path"), true);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Add Path Point", GUILayout.Height(22f)))
                MoverEditorUtil.AddTransformPoint(path, (PlayerCutsceneMover)target, "Path", "point");

            using (new EditorGUI.DisabledScope(path.arraySize == 0))
            {
                if (GUILayout.Button("Remove Last", GUILayout.Height(22f)))
                    path.arraySize--;
            }
        }

        EditorGUILayout.Space(8f);
        MoverEditorUtil.DrawMovement(serializedObject);
    }

    void DrawControlTab()
    {
        MoverEditorUtil.Section("Control To Suspend While Moving", "Behaviours disabled while the cutscene move runs and re-enabled afterwards. For the Player, add PlayerController, PlayerInputHandler, and CharacterController.");
        EditorGUILayout.PropertyField(controlToDisable, new GUIContent("Control To Disable"), true);

        EditorGUILayout.Space(8f);
        MoverEditorUtil.Section("Resume After Battle", "When returning to this scene with the story flag already set (e.g. after a battle), the player teleports to the point it had reached and continues from the NEXT point instead of replaying the whole path. Add a point after it so the player can keep walking.");
        EditorGUILayout.PropertyField(resumeIfStoryFlag, new GUIContent("Resume If Story Flag"));
        EditorGUILayout.PropertyField(resumeFromPointIndex, new GUIContent("Resume From Point Index"));

        MoverEditorUtil.ResumeIndexHint(resumeIfStoryFlag, resumeFromPointIndex, path, "point");
    }

    void DrawRuntimeControls()
    {
        if (!Application.isPlaying)
            return;

        EditorGUILayout.Space(8f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Runtime Test", EditorStyles.boldLabel);
            var mover = (PlayerCutsceneMover)target;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Play", GUILayout.Height(24f)))
                    mover.Play();

                if (GUILayout.Button("Stop", GUILayout.Height(24f)))
                    mover.Stop();
            }
        }
    }
}
#endif
