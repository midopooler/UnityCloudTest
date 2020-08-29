using UnityEngine;

using UnityEditor;
using UnityEditor.Callbacks;
#if UNITY_EDITOR_OSX && (UNITY_5 || UNITY_5_3_OR_NEWER)
using UnityEditor.iOS.Xcode;
#endif
using System.IO;

public class BugseeXcodeMod : MonoBehaviour {
 
    [PostProcessBuild]
    public static void OnPostprocessBuild(BuildTarget buildTarget, string path)
    {
#if ( UNITY_IPHONE || UNITY_IOS ) && UNITY_EDITOR_OSX && (UNITY_5 || UNITY_5_3_OR_NEWER)
		if (buildTarget != BuildTarget.iOS) return;
        
        string projPath = PBXProject.GetPBXProjectPath(path);
        PBXProject proj = new PBXProject();
 
        proj.ReadFromString(File.ReadAllText(projPath));

        #if UNITY_2019_3_OR_NEWER
        var target = proj.GetUnityFrameworkTargetGuid();
        #else
        var target = proj.TargetGuidByName(PBXProject.GetUnityTargetName());
        #endif

        proj.AddFrameworkToProject(target, "Security.framework", false);
        proj.AddFrameworkToProject(target, "CoreImage.framework", false);
 
     	// proj.AddBuildProperty(target, "OTHER_LDFLAGS", "-ObjC"); 
     	File.WriteAllText(projPath, proj.WriteToString());
#endif
	}
    
}
