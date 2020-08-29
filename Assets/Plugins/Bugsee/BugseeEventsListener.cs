using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace BugseePlugin
{
    class BugseeEventsListener : MonoBehaviour
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        public void OnPostRender()
        {
            // We can't catch this event in Bugsee plugin directly, because in this case AndroidJavaClass instances are not initialized correctly.
            Bugsee.OnPostRender();
        }

        public void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            Bugsee.OnRenderImage(source, destination);
        }
#endif
    }
}
