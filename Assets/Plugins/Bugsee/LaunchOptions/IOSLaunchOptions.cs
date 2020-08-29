using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace BugseePlugin
{
    /// <summary>
    /// Contains options that help you configure the behavior of Bugsee.
    /// </summary>
    public class IOSLaunchOptions : BugseeLaunchOptions
    {
        public IOSLaunchOptions()
        {
            this.SetDefaults();
        }

        private void SetDefaults()
        {
            MaxRecordingTime = 60;
            ShakeToReport = false;
            ScreenshotToReport = true;
            CrashReport = true;
            KillDetection = false;
            VideoEnabled = true;
            VideoScale = 0.0f;
            CaptureLogs = true;
            MonitorNetwork = true;
            ReportPrioritySelector = false;
            FrameRate = BugseeFrameRate.High;
            DefaultCrashPriority = BugseeSeverityLevel.Blocker;
            DefaultBugPriority = BugseeSeverityLevel.Low;
            Style = BugseeStyle.Default;
            WifiOnlyUpload = false;
            MaxDataSize = 50;
            CaptureDeviceAndNetworkNames = true;
        }

        public override void Reset()
        {
            base.Reset();
            this.SetDefaults();
        }

        public static BugseeLaunchOptions GetDefaultOptions()
        {
            return new IOSLaunchOptions();
        }

        /// <summary>
        /// Additional down scaling applied to recorded video, (e.g., 0.5 would reduce both width and height by half).
        /// </summary>
        /// <value>Default: 0 - means not used</value>
        public float VideoScale
        {
            get { return (float)this["VideoScale"]; }
            set { this["VideoScale"] = value; }
        }

		/// <summary>
		/// Use this option to change frame rate to Low, Medium or High
		/// </summary>
		/// <value>Framerate value.</value>
		public BugseeFrameRate FrameRate
		{
            get { return (BugseeFrameRate)this["FrameRate"]; }
			set { this["FrameRate"] = value; }
		}

        /// <summary>
        /// Maximum recording duration
        /// </summary>
        /// <value>The max recording time.</value>
        public int MaxRecordingTime
        {
            get { return (int)this["MaxRecordingTime"]; }
            set { this["MaxRecordingTime"] = value; }
        }

        /// <summary>
        /// Bugsee will avoid using more disk space than specified. <br/>
        /// Option has value of int type and should be specified in Mb.Value should not be smaller than 10.
        /// </summary>
        /// <value>The max data size. Default: 50</value>
        public int MaxDataSize
        {
            get { return (int)this["MaxDataSize"]; }
            set { this["MaxDataSize"] = value; }
        }

        /// <summary>
        /// Shake gesture to trigger report
        /// </summary>
        /// <value><c>true</c> to enable; otherwise, <c>false</c>.</value>
        public bool ShakeToReport
        {
            get { return (bool)this["ShakeToReport"]; }
            set { this["ShakeToReport"] = value; }
        }

        /// <summary>
        /// Screenshot key to trigger report
        /// </summary>
        /// <value><c>true</c> to enable; otherwise, <c>false</c>.</value>
        public bool ScreenshotToReport
        {
            get { return (bool)this["ScreenshotToReport"]; }
            set { this["ScreenshotToReport"] = value; }
        }

        /// <summary>
        /// Catch and report application crashes
        /// </summary>
        /// <value><c>true</c> if crash report; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// iOS allows only one crash detector to be active at a time, if you insist
        /// on using an alternative solution for handling crashes, you might want to
        /// use this option and disable Bugsee from taking over.
        /// </remarks>
        public bool CrashReport
        {
            get { return (bool)this["CrashReport"]; }
            set { this["CrashReport"] = value; }
        }

        /// <summary>
        /// Detect abnormal termination (experimental)
        /// </summary>
        /// <value><c>true</c> to enable; otherwise, <c>false</c>.</value>
        public bool KillDetection
        {
            get { return (bool)this["BugseeKillDetectionKey"]; }
            set { this["BugseeKillDetectionKey"] = value; }
        }

        /// <summary>
        /// Enable video recording
        /// </summary>
        /// <value><c>true</c> to enable; otherwise, <c>false</c>.</value>
        public bool VideoEnabled
        {
            get { return (bool)this["VideoEnabled"]; }
            set { this["VideoEnabled"] = value; }
        }

        /// <summary>
        /// Automatically capture all console logs
        /// </summary>
        /// <value><c>true</c> ito enable; otherwise, <c>false</c>.</value>
        public bool CaptureLogs
        {
            get { return (bool)this["CaptureLogs"]; }
            set { this["CaptureLogs"] = value; }
        }

        /// <summary>
        /// Capture network traffic
        /// </summary>
        /// <value><c>true</c> to enable; otherwise, <c>false</c>.</value>
        public bool MonitorNetwork
        {
            get { return (bool)this["MonitorNetwork"]; }
            set { this["MonitorNetwork"] = value; }
        }

        /// <summary>
        /// Bugsee upload allowed only by wifi
        /// </summary>
        /// <value><c>true</c> to enable; otherwise, <c>false</c>.</value>
         public bool WifiOnlyUpload
        {
            get { return (bool)this["WifiOnlyUpload"]; }
            set { this["WifiOnlyUpload"] = value; }
        }

        /// <summary>
        /// Screenshot that appears in report
        /// Default: When videoEnabled it's true, but if videoEnabled == false it's false
        /// </summary>
        /// <value><c>true</c> to enable; otherwise, <c>false</c>.</value>
        public bool ScreenshotEnabled
        {
            get { return (bool)this["ScreenshotEnabled"]; }
            set { this["ScreenshotEnabled"] = value; }
        }


        /// <summary>
        /// Allow user to modify priority when reporting manually
        /// </summary>
        /// <value><c>true</c> to enable; otherwise, <c>false</c>.</value>
        public bool ReportPrioritySelector
        {
            get { return (bool)this["BugseeReportPrioritySelector"]; }
            set { this["BugseeReportPrioritySelector"] = value; }
        }

        /// <summary>
        /// Default priority for crashes
        /// </summary>
        /// <value>The default crash priority.</value>
        public BugseeSeverityLevel DefaultCrashPriority
        {
            get { return (BugseeSeverityLevel)this["BugseeDefaultCrashPriority"]; }
            set { this["BugseeDefaultCrashPriority"] = value; }
        }

        /// <summary>
        /// Default priority for bugs
        /// </summary>
        /// <value>The default bug priority.</value>
        public BugseeSeverityLevel DefaultBugPriority
        {
            get { return (BugseeSeverityLevel)this["BugseeDefaultBugPriority"]; }
            set { this["BugseeDefaultBugPriority"] = value; }
        }

        /// <summary>
        /// Defines the style which is used in Bugsee reporting UI
        /// </summary>
        /// <value>The style.</value>
        public BugseeStyle Style
        {
            get
            {
                var styleObj = this["BugseeStyle"];

                if (styleObj != null)
                {
                    return (BugseeStyle)Enum.Parse(typeof(BugseeStyle), styleObj.ToString());
                }

                return BugseeStyle.Default;
            }
            set
            {
                this["BugseeStyle"] = Enum.GetName(typeof(BugseeStyle), value);
            }
        }

        public bool CaptureDeviceAndNetworkNames
        {
            get { return (bool)this["CaptureDeviceAndNetworkNames"]; }
            set { this["CaptureDeviceAndNetworkNames"] = value; }
        }
    }
}
