using System;
using System.Collections.Generic;
using UnityEngine;

namespace BugseePlugin
{
    public enum BugseeAndroidVideoMode
    {
        /// <summary>
        /// Video is not recorded. Created issues will contain console logs, events and traces, but will not contain video.
        /// </summary>
        None,
        /// <summary>
        /// User is not asked to allow video recording, but frame rate is lower comparing to <see cref="V2"/> mode and some special views like status bar,
        /// soft keyboard and views, which contain Surface (MapView, VideoView, GlSurfaceView, etc.)
        /// are not recorded.
        /// </summary>
        V1,
        /// <summary>
        /// MediaProjection API is used, while recording video. All types of views are recorded, but user is asked to allow video recording. <para /> 
        /// Note: MediaProjection is known to conflict with Unity rendering (<see cref="https://answers.unity.com/questions/1259305/android-unity-screen-recording-and-mediaprojection.html"/>), 
        /// which can lead to a hung UnityPlayerActivity. It's not recommended to use this mode in production build.
        /// </summary>
        V2,
        /// <summary>
        /// User is not asked to allow video recording, but frame rate is lower comparing to <see cref="V2"/>
        /// mode and system views like status bar and soft keyboard are not recorded. <para /> 
        /// Note: this mode is experimental and works only on Android API level 24 and higher. On lower API
        /// levels video mode is automatically switched to <see cref="V1"/>.
        /// </summary>
        V3,
        /// <summary>
        /// New frame is formed when Bugsee.Snapshot() method is called. It gives an ability to decrease computational overheads, which comes at the expense of lower frame rate.
        /// </summary>
        V4
    }

    public enum SnapshotSize
    {
        /// <summary>
        /// Max frame side is 320.
        /// </summary>
        Small,
        /// <summary>
        /// Max frame side is 480.
        /// </summary>
        Middle
    }

    /// <summary>
    /// Contains options that help you configure the behavior of Bugsee.
    /// </summary>
    public class AndroidLaunchOptions : BugseeLaunchOptions
    {
        public AndroidLaunchOptions()
        {
            // Set defaults
            this.SetDefaults();
        }

        private void SetDefaults()
        {
            MaxRecordingTime = 60;
            ShakeToTrigger = true;
            NotificationBarTrigger = true;
            CrashReport = true;
            UseSdCard = true;
            VideoEnabled = true;
            VideoScale = 1.0f;
            CaptureLogs = true;
            ExtendedVideoMode = false;
            VideoMode = BugseeAndroidVideoMode.None;
            MonitorNetwork = true;
            ServiceMode = false;
            LogLevel = BugseeLogLevel.Verbose;
            FrameRate = BugseeFrameRate.High;
            MaxDataSize = 50;
            ReportPrioritySelector = false;
            DefaultCrashPriority = BugseeSeverityLevel.Blocker;
            DefaultBugPriority = BugseeSeverityLevel.High;
            WifiOnlyUpload = false;
            CaptureDeviceAndNetworkNames = true;
            SnapshotSize = SnapshotSize.Small;
#if UNITY_ANDROID && !UNITY_EDITOR && BUGSEE_VIDEO_V4
            SnapshotCopyFactor = SnapshotHandler.DEFAULT_COPY_FACTOR;
#endif
            // No concrete default value for ScreenshotEnabled, because it depends on VideoEnabled option value.
        }

        /// <summary>
        /// Reverts any changes and resets options to their defaults
        /// </summary>
        public override void Reset()
        {
            base.Reset();

            this.SetDefaults();
        }

        /// <summary>
        /// Returns new instance with all options set to default values
        /// </summary>
        /// <returns>The default options.</returns>
        public static BugseeLaunchOptions GetDefaultOptions()
        {
            return new AndroidLaunchOptions();
        }

        /// <summary>
        /// Maximum recording duration (in seconds)
        /// </summary>
        /// <value>The max recording time.</value>
        public int MaxRecordingTime
        {
            get { return (int)this["MaxRecordingTime"]; }
            set { this["MaxRecordingTime"] = value; }
        }

        /// <summary>
        /// Enables/diables shake gesture detection to trigger report
        /// </summary>
        /// <value><c>true</c> to enable; otherwise, <c>false</c>.</value>
        public bool ShakeToTrigger
        {
            get { return (bool)this["ShakeToTrigger"]; }
            set { this["ShakeToTrigger"] = value; }
        }

        /// <summary>
        /// Trigger report from notification bar
        /// </summary>
        /// <value><c>true</c> to enable; otherwise, <c>false</c>.</value>
        public bool NotificationBarTrigger
        {
            get { return (bool)this["NotificationBarTrigger"]; }
            set { this["NotificationBarTrigger"] = value; }
        }

        /// <summary>
        /// Catch and report application crashes
        /// </summary>
        /// <value><c>true</c> to enable; otherwise, <c>false</c>.</value>
        public bool CrashReport
        {
            get { return (bool)this["CrashReport"]; }
            set { this["CrashReport"] = value; }
        }

        /// <summary>
        /// By default Bugsee saves its data to SD card (if ServiceMode option is not enabled).
        /// When set to false, internal app storage will be used. If your app does not require
        /// external storage, you might want to disable it in production builds. 
        /// </summary>
        /// <value><c>true</c> to enable; otherwise, <c>false</c>.</value>
        public bool UseSdCard
        {
            get { return (bool)this["UseSdCard"]; }
            set { this["UseSdCard"] = value; }
        }

        /// <summary>
        /// Record video. If false, created issues will contain console logs, events and traces,
        /// but will not contain video.
        /// </summary>
        /// <value><c>true</c> to enable; otherwise, <c>false</c>.</value>
        public bool VideoEnabled
        {
            get { return (bool)this["VideoEnabled"]; }
            set { this["VideoEnabled"] = value; }
        }

        /// <summary>
        /// Additional down scaling applied to recorded video, (e.g., 0.5 would reduce both width and height by half).
        /// </summary>
        /// <value>1.0 by default, less than 1.0 to down scale recorded video.</value>
        public float VideoScale
        {
            get { return (float)this["VideoScale"]; }
            set { this["VideoScale"] = value; }
        }

        /// <summary>
        /// If true, MediaProjection API is used, while recording video. In this case all types of
        /// views are recorded, but user is asked to allow video recording. If false (experimental),
        /// view drawing cache is used to capture the screen. In this case user is not asked to
        /// allow video recording, but frame rate is lower and some special views like status bar,
        /// soft keyboard and views, which contain Surface (MapView, VideoView, GlSurfaceView, etc.)
        /// are not recorded. Option has no effect, if VideoEnabled option is set to false.
        /// </summary>
        /// <value><c>true</c> to enable; otherwise, <c>false</c>.</value>
        //[Obsolete("")]
        public bool ExtendedVideoMode
        {
            get { return (bool)this["ExtendedVideoMode"]; }
            set
            {
                this["ExtendedVideoMode"] = value;
                if (value) {
                    VideoMode = BugseeAndroidVideoMode.V2;
                }
            }
        }

        /// <summary>
        /// Mode, used while recording video.
        /// <value><code>VideoMode.MediaProjection</code> by default.</value>
        /// </summary>
        public BugseeAndroidVideoMode VideoMode
        {
            get { return (BugseeAndroidVideoMode)(int)this["VideoMode"]; }
            set { this["VideoMode"] = (int)value; }
        }

        /// <summary>
        /// Monitor network events
        /// </summary>
        /// <value><c>true</c> enable; otherwise, <c>false</c>.</value>
        public bool MonitorNetwork
        {
            get { return (bool)this["MonitorNetwork"]; }
            set { this["MonitorNetwork"] = value; }
        }

        /// <summary>
        /// Should be used, when Bugsee is launched from service. If true, video is not recorded and
        /// recording is not stopped, when app goes to background; ShakeToTrigger and
        /// NotificationBarTrigger options are set to false automatically.
        /// </summary>
        /// <value><c>true</c> to enable; otherwise, <c>false</c>.</value>
        public bool ServiceMode
        {
            get { return (bool)this["ServiceMode"]; }
            set { this["ServiceMode"] = value; }
        }

        /// <summary>
        /// Minimal log level of Logcat messages, which will be attached to reports. Option has no effect 
        /// if CaptureLogs option is false. Option has value of BugseeLogLevel type.
        /// </summary>
        /// <value>The log level.</value>
        public BugseeLogLevel LogLevel
        {
            get { return (BugseeLogLevel)(int)this["LogLevel"]; }
            set { this["LogLevel"] = (int)value; }
        }

        /// <summary>
        /// Specifies how often frames are captured. <br/>
        /// </summary>
        /// <value>The frame rate.</value>
        public BugseeFrameRate FrameRate
        {
            get { return (BugseeFrameRate)(int)this["FrameRate"]; }
            set { this["FrameRate"] = (int)value; }
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
            get { return (BugseeSeverityLevel)(int)this["BugseeDefaultCrashPriority"]; }
            set { this["BugseeDefaultCrashPriority"] = (int)value; }
        }

        /// <summary>
        /// Default priority for bugs
        /// </summary>
        /// <value>The default bug priority.</value>
        public BugseeSeverityLevel DefaultBugPriority
        {
            get { return (BugseeSeverityLevel)(int)this["BugseeDefaultBugPriority"]; }
            set { this["BugseeDefaultBugPriority"] = (int)value; }
        }
        
         /// <summary>
        /// Automatically capture Logcat logs
        /// </summary>
        /// <value><c>true</c> to enable; otherwise, <c>false</c>.</value>
        public bool CaptureLogs
        {
            get { return (bool)this["CaptureLogs"]; }
            set { this["CaptureLogs"] = value; }
        }
        
        /// <summary>
        /// Attach screenshot to a report.
        /// </summary>
        /// <value><c>true</c> to enable; otherwise, <c>false</c>.</value>
        public bool ScreenshotEnabled
        {
            get
            {
                var result = this["ScreenshotEnabled"];
                return (result == null) ? VideoEnabled : (bool)result;
            }
            set { this["ScreenshotEnabled"] = value; }
        }

        /// <summary>
        /// Upload reports only when a device is connected to a wifi network.
        /// </summary>
        /// <value><c>true</c> to enable; otherwise, <c>false</c>.</value>
        public bool WifiOnlyUpload
        {
            get { return (bool)this["WifiOnlyUpload"]; }
            set { this["WifiOnlyUpload"] = value; }
        }
        
        /// <summary>
        /// Bugsee will avoid using more disk space than specified (in MB). If total Bugsee data size exceeds
        /// specified value, oldest recordings (even not sent) will be removed. Value should not be smaller
        /// than 10.
        /// </summary>
        /// <value>The size of the max data.</value>
        public int MaxDataSize
        {
            get;
            set;
        }

        public bool CaptureDeviceAndNetworkNames
        {
            get { return (bool)this["CaptureDeviceAndNetworkNames"]; }
            set { this["CaptureDeviceAndNetworkNames"] = value; }
        }

        /// <summary>
        /// Taken snapshot is divided into parts and copied from GPU part by part on each rendered frame. The more this factor is, the less computational overhead becomes, but potential
        /// snapshot frequency becomes less too. The value must be in interval [1; 100]. Option has no effect for VideoModes other than VideoMode.V4.
        /// </summary>
        /// <value>Snapshot copy factor.</value>
        public int SnapshotCopyFactor
        {
            get { return (int)this["SnapshotCopyFactor"]; }
            set { this["SnapshotCopyFactor"] = value; }
        }

        /// <summary>
        /// Size of image, taken as a snapshot.
        /// </summary>
        /// <value>Size of snapshot.</value>
        public SnapshotSize SnapshotSize
        {
            get { return (SnapshotSize)(int)this["SnapshotSize"]; }
            set { this["SnapshotSize"] = (int)value; }
        }
    }
}
