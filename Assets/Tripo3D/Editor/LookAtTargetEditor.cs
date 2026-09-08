using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Tripo3D.Editor
{
    [CustomEditor(typeof(LookAtTarget))]
    sealed class LookAtTargetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var look = (LookAtTarget)target;
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            if (!EditorGUI.EndChangeCheck())
                return;

            Undo.RecordObject(look.transform, "LookAtTarget orbit");
            look.ApplyOrbit();
            look.ApplyLook();
            EditorUtility.SetDirty(look.transform);
            var scene = look.gameObject.scene;
            if (scene.IsValid())
                EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
