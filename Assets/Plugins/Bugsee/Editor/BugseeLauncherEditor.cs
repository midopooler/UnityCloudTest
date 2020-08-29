using UnityEditor;
using UnityEngine;
using System.Timers;
using BugseePlugin;

[CustomEditor(typeof(BugseeLauncher))]
public class BugseeLauncherEditor : Editor {

	SerializedProperty AndroidTokenField;
    SerializedProperty IosTokenField;


	public virtual void OnEnable()
	{
        AndroidTokenField = serializedObject.FindProperty("AndroidAppToken");
        IosTokenField = serializedObject.FindProperty("IosAppToken");
	}

	public override void OnInspectorGUI()
	{
        serializedObject.Update();

        EditorGUILayout.BeginVertical("Box");
        GUILayout.Space (10);
        EditorGUILayout.PropertyField(IosTokenField, new GUIContent("iOS App Token"));
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical("Box");
		GUILayout.Space (10);
        EditorGUILayout.PropertyField(AndroidTokenField);
        
        if(IosTokenField.stringValue == "your-bugsee-token" && AndroidTokenField.stringValue == "your-bugsee-token"){
			EditorGUILayout.HelpBox("Please use your app token provided from http://app.bugsee.com", MessageType.Warning);
		}
		EditorGUILayout.EndVertical();

		EditorGUILayout.BeginVertical("Box");
        if (GUILayout.Button("Modify Bugsee behavior further with Launch options"))
        {
            Help.BrowseURL("https://docs.bugsee.com/sdk/unity/configuration/");
        }
        EditorGUILayout.EndVertical();

        serializedObject.ApplyModifiedProperties();
	}
}
