using UnityEditor;
using UnityEngine;

namespace Fungus.EditorUtils
{
    [CustomEditor(typeof(IfObjectiveCommand))]
    public class IfObjectiveCommandEditor : CommandEditor
    {
        SerializedProperty targetProp;
        SerializedProperty subObjectivesProp;
        SerializedProperty matchProp;
        SerializedProperty objectiveProp;
        SerializedProperty checkProp;
        SerializedProperty expectedProp;

        public override void OnEnable()
        {
            base.OnEnable();
            targetProp = serializedObject.FindProperty("target");
            subObjectivesProp = serializedObject.FindProperty("subObjectives");
            matchProp = serializedObject.FindProperty("match");
            objectiveProp = serializedObject.FindProperty("objective");
            checkProp = serializedObject.FindProperty("check");
            expectedProp = serializedObject.FindProperty("expected");
        }

        public override void DrawCommandGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(targetProp);

            // 0 = SubObjective, 1 = Objective (see IfObjectiveCommand.Target).
            bool isSubObjective = targetProp.enumValueIndex == 0;

            if (isSubObjective)
            {
                EditorGUILayout.PropertyField(subObjectivesProp, new GUIContent("Sub Objectives"), true);
                if (subObjectivesProp.arraySize > 1)
                    EditorGUILayout.PropertyField(matchProp);
            }
            else
            {
                EditorGUILayout.PropertyField(objectiveProp);
                EditorGUILayout.PropertyField(checkProp);
            }

            EditorGUILayout.PropertyField(expectedProp);

            serializedObject.ApplyModifiedProperties();
        }
    }
}
