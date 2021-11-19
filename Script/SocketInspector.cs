using UnityEditor;
using UnityEngine;

namespace MingXu.Socket
{
    [CustomEditor(typeof(SocketIOUnity))]
    [CanEditMultipleObjects]
    public class SocketInspector : Editor
    {
        private bool ShowLoggingOptions;
        private SerializedProperty DontShowLogs;
        private SerializedProperty DontShowWarnings;
        private SerializedProperty DontShowErrors;

        private bool ShowSocketOptions;
        private SerializedProperty Adress;
        private SerializedProperty state;

        private void OnEnable()
        {
            DontShowLogs = serializedObject.FindProperty("DontShowLogs");
            DontShowWarnings = serializedObject.FindProperty("DontShowWarnings");
            DontShowErrors = serializedObject.FindProperty("DontShowErrors");
            Adress = serializedObject.FindProperty("Adress");
            state = serializedObject.FindProperty("SocketState");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            ShowLoggingOptions = EditorGUILayout.BeginFoldoutHeaderGroup(ShowLoggingOptions, "Logging options");
            if (ShowLoggingOptions)
            {
                EditorGUILayout.HelpBox("Should logs/warnings/errors of the socket be logged?", UnityEditor.MessageType.Info);
                EditorGUILayout.PropertyField(DontShowLogs);
                EditorGUILayout.PropertyField(DontShowWarnings);
                EditorGUILayout.PropertyField(DontShowErrors);
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            ShowSocketOptions = EditorGUILayout.BeginFoldoutHeaderGroup(ShowSocketOptions, "Socket info");
            if (ShowSocketOptions)
            {
                if (Application.isPlaying)
                {
                    EditorGUILayout.LabelField("Adress: " + Adress.stringValue);
                    EditorGUILayout.LabelField("State: " + (state.enumValueIndex == (int)State.Closed ? "Closed" : (state.enumValueIndex == (int)State.Open ? "Open" : "Opening")));
                }
                else
                {
                    EditorGUILayout.HelpBox("Only available in Play Mode", UnityEditor.MessageType.Warning);
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            serializedObject.ApplyModifiedProperties();
        }
    }
}