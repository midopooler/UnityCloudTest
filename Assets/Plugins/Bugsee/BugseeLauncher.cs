using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using BugseePlugin;
using System;

// Learn more about launch options from our documentation
// iOS     - https://docs.bugsee.com/sdk/ios/configuration/
// Android - https://docs.bugsee.com/sdk/android/configuration/

namespace BugseePlugin
{
    public delegate BugseeLaunchOptions GetOptionsHandler();
    public delegate String GetAppTokenHandler();

    public partial class BugseeLauncher : MonoBehaviour
    {
        public static GetOptionsHandler AndroidOptionsHandler { get; set; }
        public static GetOptionsHandler IosOptionsHandler { get; set; }

        public static GetAppTokenHandler AndroidAppTokenHandler { get; set; }
        public static GetAppTokenHandler IosAppTokenHandler { get; set; }

        public Material mat;
        public string AndroidAppToken = "your-bugsee-token";
        public string IosAppToken = "your-bugsee-token";

        // Use this for initialization
        void Awake ()
        {
            InitializeHandlers();

            gameObject.AddComponent<Bugsee>();

            if (Application.isMobilePlatform)
            {
                if (Application.platform == RuntimePlatform.IPhonePlayer)
                {
                    var appToken = IosAppTokenHandler.Invoke();
                    if (appToken == "your-bugsee-token")
                    {
                        Debug.Log("Please set your bugsee token, you can find it on app.bugsee.com");
                    }
                    Bugsee.Launch(appToken, IosOptionsHandler.Invoke());
                }
                else
                {
                    var appToken = AndroidAppTokenHandler.Invoke();
                    if (appToken == "your-bugsee-token")
                    {
                        Debug.Log("Please set your bugsee token, you can find it on app.bugsee.com");
                    }
                    Bugsee.Launch(appToken, AndroidOptionsHandler.Invoke());
                }
            }
            DontDestroyOnLoad(this);
        }

        private void InitializeHandlers()
        {
            if (AndroidOptionsHandler == null)
            {
                AndroidOptionsHandler = GetNullOptions;
            }
            if (IosOptionsHandler == null)
            {
                IosOptionsHandler = GetNullOptions;
            }
            if (AndroidAppTokenHandler == null)
            {
                AndroidAppTokenHandler = GetAndroidAppToken;
            }
            if (IosAppTokenHandler == null)
            {
                IosAppTokenHandler = GetIosAppToken;
            }
        }

        private BugseeLaunchOptions GetNullOptions()
        {
            return null;
        }

        private String GetAndroidAppToken()
        {
            return AndroidAppToken;
        }

        public String GetIosAppToken()
        {
            return IosAppToken;
        }
    }
}