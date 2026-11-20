using UnityEditor;
using UnityEngine;

namespace EcclesiaFighters.EditorTools
{
    /// <summary>
    /// Custom Inspector for ChurchStageController providing one-click Load / Save buttons
    /// for Stage 0 (Nave Central) and Stage 1 (Altar Sagrado) while aligning the church FBX.
    /// </summary>
    [CustomEditor(typeof(ChurchStageController))]
    public class ChurchStageControllerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var ctrl = (ChurchStageController)target;

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Visual Alignment Tools — 3D Church Stages", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "1. Click 'Load Stage 0' or 'Load Stage 1' to preview that arena.\n" +
                "2. Move/rotate ChurchStageRoot, SpawnA/B, or walls in the Scene view.\n" +
                "3. Click 'Save Current Layout to Stage 0 / 1' to store your adjustments.",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Load Stage 0\n(Nave Central)", GUILayout.Height(36)))
            {
                ctrl.LoadStage0();
                SceneView.RepaintAll();
            }
            if (GUILayout.Button("Save Current Layout\nto Stage 0", GUILayout.Height(36)))
            {
                ctrl.SaveCurrentLayoutToStage0();
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Load Stage 1\n(Altar Sagrado)", GUILayout.Height(36)))
            {
                ctrl.LoadStage1();
                SceneView.RepaintAll();
            }
            if (GUILayout.Button("Save Current Layout\nto Stage 1", GUILayout.Height(36)))
            {
                ctrl.SaveCurrentLayoutToStage1();
                SceneView.RepaintAll();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Ensure 3D Church FBX, Colliders & Lights", GUILayout.Height(26)))
            {
                ctrl.EnsureArenaObjects();
                ctrl.ApplyPreset(ctrl.activePresetIndex);
                SceneView.RepaintAll();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(6);

            DrawDefaultInspector();
        }
    }
}
