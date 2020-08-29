using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System;
#if UNITY_5_4_OR_NEWER
using UnityEngine.SceneManagement;
#endif
using System.IO;
using System.Threading;
#if !NET_LEGACY && !NET_2_0_SUBSET
using System.Threading.Tasks;
#endif
using System.Linq;

namespace BugseePlugin
{
    public enum BugseeFrameRate
    {
        Low = 1,
        Medium,
        High
    }

    public enum BugseeSeverityLevel
    {
        Low = 1,
        Medium,
        High,
        Critical,
        Blocker
    }

    public enum BugseeLogLevel
    {
        Invalid = 0,
        Error,
        Warning,
        Info,
        Debug,
        Verbose
    }

    public enum BugseeStyle
    {
        Default,
        Dark,
        BasedOnStatusBar
    }

    public enum BugseeLifecycleEventType
    {
        /**
         * Event is dispatched when Bugsee was successfully launched
         */
        Launched = 0,
        /**
         * Event is dispatched when Bugsee is started after being stopped
         */
        Started = 1,
        /**
         * Event is dispatched when Bugsee is stopped
         */
        Stopped = 2,
        /**
         * Event is dispatched when Bugsee recording is resumed after being paused
         */
        Resumed = 3,
        /**
         * Event is dispatched when Bugsee recording is paused
         */
        Paused = 4,
        /**
         * Event is dispatched when Bugsee is launched and pending crash report is
         * discovered. That usually means that app was relaunched after crash.
         */
        RelaunchedAfterCrash = 5,
        /**
         * Event is dispatched before the reporting UI is shown
         */
        BeforeReportShown = 6,
        /**
         * Event is dispatched when reporting UI is shown
         */
        AfterReportShown = 7,
        /**
         * Event is dispatched when report is about to be uploaded to the server
         */
        BeforeReportUploaded = 8,
        /**
         * Event is dispatched when report was successfully uploaded to the server
         */
        AfterReportUploaded = 9,
        /**
         * Event is dispatched before the Feedback controller is shown
         */
        BeforeFeedbackShown = 10,
        /**
         * Event is dispatched after the Feedback controller is shown
         */
        AfterFeedbackShown = 11
    }

    public struct BugseeReport
    {
        public string type;
        public BugseeSeverityLevel severityLevel;
    }

    public struct BugseeAttachment
    {
        public string name;
        public string filename;
        public byte[] data;
    }

    public class Bugsee : MonoBehaviour
    {
        [Serializable]
        private class BugseeExceptionFrame
        {
            public string trace = "";
        }

        [Serializable]
        private class BugseeException
        {
            public string name;
            public string reason;
            public BugseeExceptionFrame[] frames;
            public string signature;
            public string buildID;
        }

        private struct LogEvent
        {
            public static LogEvent Create(string message, string stackTrace, LogType logType)
            {
                var result = new LogEvent();
                result.Message = message;
                result.StackTrace = stackTrace;
                result.LogType = logType;
                return result;
            }

            public string Message { get; private set; }
            public string StackTrace { get; private set; }
            public LogType LogType { get; private set; }
        }

        public const string ShakeToReportKey = "ShakeToReport";
        public const string MaxRecordingTimeKey = "MaxRecordingTime";
        public const string ScreenshotToReportKey = "ScreenshotToReport";
        public const string CrashReportKey = "CrashReport";
        public const string MaxFrameRateKey = "MaxFrameRate";
        public const string EndpointKey = "endpoint";
        public const string KillDetectionKey = @"BugseeKillDetectionKey";
        public const string MonitorNetworkKey = @"MonitorNetwork";
        public const string VideoEnabledKey = @"VideoEnabled";
        public const string StyleKey = @"BugseeStyle";
        public const string ReportPrioritySelectorKey = @"BugseeReportPrioritySelector";
        public const string DefaultCrashPriorityKey = @"BugseeDefaultCrashPriority";
        public const string DefaultBugPriorityKey = @"BugseeDefaultBugPriority";
        public const string EnableMachExceptionsKey = @"BugseeEnableMachExceptions";
        public const string CaptureLogsKey = @"CaptureLogs";

        private const string SnapshotSizeKey = "SnapshotSize";

        public delegate List<BugseeAttachment> AttachmentForReport(BugseeReport report);
        public static event AttachmentForReport OnAttachmentForReport;

        public delegate void LifecycleEventDelegate(BugseeLifecycleEventType eventType);
        public static event LifecycleEventDelegate OnLifecycleEvent;

        public delegate void RecordingStarted(bool mediaProjectionEnabled);
        /// <summary>
        /// Currently, handlers for this event are invoked only on Android platform.
        /// </summary>
        public static event RecordingStarted OnRecordingStarted;

        // bool value is a "handled" indicator.
        private static readonly Queue<KeyValuePair<Exception, bool>> exceptionInfoQueue = new Queue<KeyValuePair<Exception, bool>>();
        private static readonly Queue<LogEvent> logEventsQueue = new Queue<LogEvent>();

#if NET_2_0_SUBSET
        private static readonly Regex exceptionCleanerRegex = new Regex("[\\d,.#\\[\\]{}()!@$%^&*:\\r\\n\\s]+", RegexOptions.IgnoreCase | RegexOptions.Singleline);
#else
        private static readonly Regex exceptionCleanerRegex = new Regex("[\\d,.#\\[\\]{}()!@$%^&*:\\r\\n\\s]+", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);
#endif

        private volatile static bool isBugseeEnabled = false;
        private static string buildGUID;
        private volatile static int mainThreadId;

        private void Awake()
        {
            gameObject.name = "bgs_gameObject";
            mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            OnAwake();
        }

        private static string Serialize(object obj, bool allowNullResult)
        {
            object normalizedValue = (!allowNullResult && obj == null) ? String.Empty : obj;
            return Serialize(normalizedValue);
        }

        // We have the copy of this method in Xamarin JsonSerializer class. There are unit tests for this class.
        private static string Serialize(object obj)
        {
            if (obj == null)
                return null;

            StringBuilder sb = new StringBuilder();

            var type = obj.GetType();
            if (type.IsPrimitive)
            {
                sb.Append(obj.ToString().ToLower());
            }
            else if (type == typeof(string))
            {
                sb.Append("\"" + obj.ToString() + "\"");
            }
            else if (type.IsEnum)
            {
                int val = (int)obj;
                sb.Append(val.ToString());
            }
            else if (type.IsArray)
            {
                var castedValue = obj as Array;
                var linearValues = castedValue.GetEnumerator();
                SerializeMultidimensionalArray(sb, castedValue, linearValues, 0);
            }
            else if (obj is IList)
            {
                int i = 0;
                sb.Append("[");
                foreach (object element in (IList)obj)
                {
                    if (i++ > 0)
                        sb.Append(", ");
                    sb.Append(Serialize(element));
                }
                sb.Append("]");
            }
            else if (obj is IDictionary)
            {
                int i = 0;
                sb.Append("{");
                var enumerator = ((IDictionary)obj).GetEnumerator();
                while (enumerator.MoveNext())
                {
                    if (i++ > 0)
                    {
                        sb.Append(", ");
                    }
                    sb.Append("\"" + enumerator.Key + "\": " + Serialize(enumerator.Value));
                }
                sb.Append("}");
            }
            else if (obj is Rect)
            {
                var rect = (Rect)obj;
                sb.Append("{");
                sb.Append("\"X\":" + Mathf.Floor(rect.x) +
                          ",\"Y\":" + Mathf.Floor(rect.y) +
                          ",\"Width\":" + Mathf.Floor(rect.width) +
                          ",\"Height\":" + Mathf.Floor(rect.height));
                sb.Append("}");
            }
            else
            {
                sb.Append("\"" + obj.ToString() + "\"");
            }

            return sb.ToString();
        }

        private static void SerializeMultidimensionalArray(StringBuilder stringBuilder, Array array, System.Collections.IEnumerator linearValues, int dimension)
        {
            stringBuilder.Append("[");
            for (int index = 0; index < array.GetLength(dimension); index++)
            {
                if (index > 0)
                {
                    stringBuilder.Append(", ");
                }
                if (dimension == array.Rank - 1)
                {
                    linearValues.MoveNext();
                    stringBuilder.Append(Serialize(linearValues.Current));
                }
                else
                {
                    SerializeMultidimensionalArray(stringBuilder, array, linearValues, dimension + 1);
                }
            }
            stringBuilder.Append("]");
        }

        private static string HashString(string value)
        {
            using (var hasher = new System.Security.Cryptography.SHA256Managed())
            {
                hasher.Initialize();

                var bytesToHash = System.Text.Encoding.UTF8.GetBytes(value);
                var hashBytes = hasher.ComputeHash(bytesToHash);

                hasher.Clear();

                var hashString = new StringBuilder();

                foreach (byte hashByte in hashBytes)
                {
                    hashString.Append(hashByte.ToString("x2"));
                }

                return hashString.ToString();
            }
        }

        private static string GetCleanExceptionName(string name)
        {
            if (!string.IsNullOrEmpty(name))
            {
                return exceptionCleanerRegex.Replace(name, "").ToLowerInvariant();
            }

            return "";
        }

        private static string CalculateSignature(string name, string[] frames)
        {
            var allFrames = string.Join("", frames);

            return HashString(name + allFrames + buildGUID);
        }

        private static string[] GetRawFramesTraces(String stackTrace)
        {
            if (String.IsNullOrEmpty(stackTrace))
                return new string[0];

            return stackTrace.TrimEnd('\r', '\n').Split('\n');
        }

        private static BugseeExceptionFrame[] GetBugseeFrames(string[] rawFrames)
        {
            BugseeExceptionFrame[] frames = new BugseeExceptionFrame[rawFrames.Length];

            for (int i = 0; i < rawFrames.Length; ++i)
            {
                frames[i] = new BugseeExceptionFrame() { trace = rawFrames[i] };
            }

            return frames;
        }

#if UNITY_5_4_OR_NEWER
        private static void OnSceneChanged(Scene fromScene, Scene toScene)
        {
            Dictionary<string, object> trace = new Dictionary<string, object>
            {
                { "name", toScene.name },
                { "index", toScene.buildIndex }
            };

            Trace("___scene", trace);
        }
#else
        private void OnLevelWasLoaded(int index)
        {
            Dictionary<string, object> trace = new Dictionary<string, object>
            {
                { "name", "" },
                { "index", index }
            };

            Trace("___scene", trace);
        }
#endif

        private static void UpdateBuildGUID()
        {
#if UNITY_5_4_OR_NEWER
            buildGUID = Application.buildGUID;
#else
            buildGUID = Application.version;
#endif
        }

        void Update()
        {
            lock (exceptionInfoQueue)
            {
                while (exceptionInfoQueue.Count > 0)
                {
                    var exceptionInfo = exceptionInfoQueue.Dequeue();
                    HandleExceptionOnCurrentThread(exceptionInfo.Key, exceptionInfo.Value);
                }
            }

            lock (logEventsQueue)
            {
                while (logEventsQueue.Count > 0)
                {
                    var logEvent = logEventsQueue.Dequeue();
                    HandleLogOnCurrentThread(logEvent);
                }
            }

            if (isBugseeEnabled)
                OnUpdate();
        }

        private static bool IsCurrentThreadMain()
        {
            return System.Threading.Thread.CurrentThread.ManagedThreadId == mainThreadId;
        }

        private static BugseeSeverityLevel UnityLogLevelToBugseeSeverityLevel(LogType logType)
        {
            switch (logType)
            {
                case LogType.Warning:
                    return BugseeSeverityLevel.Medium;
                case LogType.Log:
                    return BugseeSeverityLevel.Low;
                case LogType.Error:
                    return BugseeSeverityLevel.Critical;
                case LogType.Exception:
                    return BugseeSeverityLevel.Blocker;
                case LogType.Assert:
                    return BugseeSeverityLevel.High;
                default:
                    return BugseeSeverityLevel.Low;
            }
        }

        private static void StartOnUnityLevel(IDictionary<string, object> options)
        {
            if (options.ContainsKey(CrashReportKey))
            {
                // If CrashReport option became "false" after relaunch we need just to remove listeners. Anyway we need to remove them to prevent adding listeners twice.
                System.AppDomain.CurrentDomain.UnhandledException -= OnHandleUnresolvedException;
                Application.logMessageReceivedThreaded -= HandleLog;
#if !NET_LEGACY && !NET_2_0_SUBSET
                TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
#endif

                if (options[CrashReportKey] is bool)
                {
                    if ((bool)options[CrashReportKey])
                    {
                        System.AppDomain.CurrentDomain.UnhandledException += OnHandleUnresolvedException;

                        // Log exceptions, which are not handled in a client plugin, but are handled on Unity level (plugin crashes on all threads).
                        Application.logMessageReceivedThreaded += HandleLog;
                    }
                }
#if !NET_LEGACY && !NET_2_0_SUBSET
                TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
#endif
            }

            isBugseeEnabled = true;
        }

        private static void HandleException(Exception exception, bool handled)
        {
            if (exception == null)
                return;

            if (IsCurrentThreadMain())
            {
                HandleExceptionOnCurrentThread(exception, handled);
            }
            else
            {
                // An arbitrary thread can't access JNI, hence we add a passed exception to a queue. Then this exception will be handled on UI thread.
                lock (exceptionInfoQueue)
                {
                    exceptionInfoQueue.Enqueue(new KeyValuePair<Exception, bool>(exception, handled));
                }
            }
        }

        private static void HandleLog(string logString, string stackTrace, LogType type)
        {
            if (type == LogType.Exception)
            {
                if (IsCurrentThreadMain())
                {
                    HandleLogOnCurrentThread(LogEvent.Create(logString, stackTrace, type));
                }
                else
                {
                    // An arbitrary thread can't access JNI, hence we add a passed exception to a queue. Then this exception will be handled on UI thread.
                    lock (logEventsQueue)
                    {
                        logEventsQueue.Enqueue(LogEvent.Create(logString, stackTrace, type));
                    }
                }
            }
        }

#if !NET_LEGACY && !NET_2_0_SUBSET
        private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            if (e.Exception != null)
                HandleException(e.Exception, false);
        }
#endif

        public static void OnHandleUnresolvedException(object sender, System.UnhandledExceptionEventArgs args)
        {

            if (args == null || args.ExceptionObject == null)
                return;

            var exception = args.ExceptionObject as Exception;

            if (exception != null)
            {
                // Even if OnHandleUnresolvedException() is called (for unhandled exceptions in worker threads, when compiled with IL2CPP setting) only a client plugin is crashed,
                // app continues working. But we treat this exception as "unhandled", because client code crashed. When compiled with Mono setting this handler is never called, 
                // app is just crashed, now info is shown in debug logs.
                HandleException(exception, false);
            }
        }

        private static void StopOnUnityLevel()
        {
            if (isBugseeEnabled)
            {
                AppDomain.CurrentDomain.UnhandledException -= OnHandleUnresolvedException;
#if !NET_LEGACY && !NET_2_0_SUBSET
                TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
#endif
                Application.logMessageReceivedThreaded -= HandleLog;
                isBugseeEnabled = false;
            }
        }

#if UNITY_IPHONE && !UNITY_EDITOR
        #region iOS methods
        [DllImport("__Internal")]
        private static extern void _bugsee_launch(string appToken, string options);

        [DllImport("__Internal")]
        private static extern void _bugsee_relaunch(string options);

        [DllImport("__Internal")]
        private static extern void _bugsee_stop();

        [DllImport("__Internal")]
        private static extern void _bugsee_show_report(string summary, string description, int severity);

        [DllImport("__Internal")]
        private static extern void _bugsee_pause();

        [DllImport("__Internal")]
        private static extern void _bugsee_resume();

        [DllImport("__Internal")]
        private static extern void _bugsee_registerEvent(string name, string parameters);

        [DllImport("__Internal")]
        private static extern void _bugsee_traceKey(string name, string wrapperDict);

        [DllImport("__Internal")]
        private static extern void _bugsee_upload(string summary, string description, int severity);

        [DllImport("__Internal")]
        private static extern void _bugsee_log(string message, int level);

        [DllImport("__Internal")]
        private static extern void _bugsee_logException(string name, string reason, string frames, bool handled);

        [DllImport("__Internal")]
        private static extern void _bugsee_feedback();

        [DllImport("__Internal")]
        private static extern void _bugsee_set_def_feedback_greeting(string message);

        [DllImport("__Internal")]
        private static extern void _bugsee_assert(bool condition, string description);

        [DllImport("__Internal")]
        private static extern void _bugsee_testExceptionCrash();

        [DllImport("__Internal")]
        private static extern void _bugsee_testSignalCrash();

        [DllImport("__Internal")]
        private static extern bool _bugsee_set_attribute(string value);

        [DllImport("__Internal")]
        private static extern string _bugsee_get_attribute(string key);

        [DllImport("__Internal")]
        private static extern bool _bugsee_clear_attribute(string key);

        [DllImport("__Internal")]
        private static extern void _bugsee_set_email(string email);

        [DllImport("__Internal")]
        private static extern string _bugsee_get_email();

        [DllImport("__Internal")]
        private static extern void _bugsee_clear_email();

        [DllImport("__Internal")]
        private static extern void _bugsee_setAttachmentsForReport(string attachments);

        [DllImport("__Internal")]
        private static extern bool _bugsee_addSecureRect(string rect);

        [DllImport("__Internal")]
        private static extern bool _bugsee_removeSecureRect(string rect);

        [DllImport("__Internal")]
        private static extern void _bugsee_removeAllSecureRects();

        [DllImport("__Internal")]
        private static extern string _bugsee_getAllSecureRects();

        [DllImport("__Internal")]
        private static extern string _bugsee_get_device_id();

        private static void HandleExceptionOnCurrentThread(Exception exception, bool handled)
        {
            if (exception == null)
                return;

            var rawFrames = GetRawFramesTraces(exception.StackTrace);

            var bgsEx = new BugseeException();
            bgsEx.name = exception.GetType().FullName;
            bgsEx.reason = exception.Message;
            bgsEx.signature = CalculateSignature(bgsEx.name, rawFrames);
            bgsEx.frames = GetBugseeFrames(rawFrames);
            bgsEx.buildID = buildGUID;

            _bugsee_logException("UnityManagedException", JsonUtility.ToJson(bgsEx), Serialize(rawFrames), handled);
        }

        private static void HandleLogOnCurrentThread(LogEvent logEvent)
        {
            var name = logEvent.Message.Split(':').First();
            var rawFrames = GetRawFramesTraces(logEvent.StackTrace);

            var bgsEx = new BugseeException();
            bgsEx.name = name;
            bgsEx.reason = logEvent.Message;
            bgsEx.signature = CalculateSignature(name, rawFrames);
            bgsEx.frames = GetBugseeFrames(rawFrames);
            bgsEx.buildID = buildGUID;

            _bugsee_logException("UnityManagedException", JsonUtility.ToJson(bgsEx), Serialize(rawFrames), false);
        }

        public static string GetDeviceID()
        {
            return _bugsee_get_device_id();
        }

        public static void LogException(Exception exception)
        {
            HandleException(exception, true);
        }

        private static bool _handlersRegistered = false;
        private static void RegisterHandlers()
        {
            if (!_handlersRegistered)
            {
                _handlersRegistered = true;
#if UNITY_5_4_OR_NEWER
                SceneManager.activeSceneChanged += OnSceneChanged;
#endif
            }
        }

        private static void StopHandlers()
        {
            if (_handlersRegistered)
            {
                _handlersRegistered = false;
#if UNITY_5_4_OR_NEWER
                SceneManager.activeSceneChanged -= OnSceneChanged;
#endif
            }
        }

        public static void Launch(string appToken, BugseeLaunchOptions options = null)
        {
            var launchOptions = options == null ? IOSLaunchOptions.GetDefaultOptions() : options;
            var serializedOptions = launchOptions.SerializeOptions();
            Launch(appToken, serializedOptions);
        }
        
        public static void Launch(string appToken, IDictionary<string, object> options)
        {
            if (isBugseeEnabled)
            {
                Debug.Log("Bugsee already launched, check your code, you might be calling it twice!");
                return;
            }

            UpdateBuildGUID();

            string jsonStr = null;
            if (options != null)
            {
                jsonStr = Serialize(options);
            }

            if (Application.isMobilePlatform)
            {
                _bugsee_launch(appToken, jsonStr);
            }

            StartOnUnityLevel(options);
            RegisterHandlers();
        }

        public static void Relaunch(BugseeLaunchOptions options = null)
        {
            if (!isBugseeEnabled)
            {
                Debug.Log("Bugsee: Please Bugsee.Launch() first.");
                return;
            }

            var launchOptions = options == null ? IOSLaunchOptions.GetDefaultOptions() : options;
            var serializedOptions = launchOptions.SerializeOptions();
            string jsonStr = null;
            if (serializedOptions != null){
                jsonStr = Serialize(serializedOptions);
            }

            if (Application.isMobilePlatform)
            {
                _bugsee_relaunch(jsonStr);
            }

            StartOnUnityLevel(serializedOptions);
            RegisterHandlers();
        }

        public static void Stop()
        {
            if (!isBugseeEnabled)
            {
                Debug.Log("Bugsee not launched");
                return;
            }

            if (Application.isMobilePlatform)
            {
                _bugsee_stop();
            }

            StopHandlers();
            isBugseeEnabled = false;
        }

        public static void ShowReport(string summary = "", string description = "", BugseeSeverityLevel severity = BugseeSeverityLevel.High)
        {
            _bugsee_show_report(summary, description, (int)severity);
        }

        public static void Pause()
        {
            _bugsee_pause();
        }

        public static void Resume()
        {
            _bugsee_resume();
        }

        public static void Event(string name, IDictionary<string, object> parameters = null)
        {
            string jsonStr = null;
            if (parameters != null)
            {
                jsonStr = Serialize(parameters);
            }

            _bugsee_registerEvent(name, jsonStr);
        }

        public static void Trace(string name, object value)
        {
            string jsonStr = Serialize(value);
            _bugsee_traceKey(name, jsonStr);
        }

        public static void Upload(string summary, string description = "", BugseeSeverityLevel severity = BugseeSeverityLevel.High)
        {
            _bugsee_upload(summary, description, (int)severity);
        }

        public static void Log(string message, BugseeLogLevel level = BugseeLogLevel.Debug)
        {
            _bugsee_log(message, (int)level);
        }

        public static void ShowFeedbackUI()
        {
            _bugsee_feedback();
        }

        public static void TestExceptionCrash()
        {
            _bugsee_testExceptionCrash();
        }

        public static void TestSignalCrash() {
            _bugsee_testSignalCrash();
        }

        public static void SetFeedbackGreetingMessage(string message)
        {
            _bugsee_set_def_feedback_greeting(message);
        }

        public static void Assert(bool condition, string description = "")
        {
            if (!condition)
                _bugsee_assert(false, description);
        }

        public static bool SetAttribute(string key, object val)
        {
            string serializedVal = Serialize(val);
            string result = "{" + "\"key\":\"" + key + "\",\"value\":" + serializedVal + "}";
            return _bugsee_set_attribute(result);
        }

        public static object GetAttribute(string key)
        {
            var result = _bugsee_get_attribute(key);

            return result;
        }

        public static bool ClearAttribute(string key)
        {
            return _bugsee_clear_attribute(key);
        }

        public static void SetEmail(string email) {
            _bugsee_set_email(email);
        }

        public static string GetEmail() { 
            return _bugsee_get_email(); 
        }

        public static void ClearEmail() {
            _bugsee_clear_email();
        }

        public static bool AddSecureRect(Rect rect)
        {
            return _bugsee_addSecureRect(Serialize(rect));
        }

        public static bool RemoveSecureRect(Rect rect)
        {
            return _bugsee_removeSecureRect(Serialize(rect));
        }

        public static void RemoveAllSecureRects()
        {
            _bugsee_removeAllSecureRects();
        }

        public static Rect[] GetAllSecureRects()
        {
            string rects = _bugsee_getAllSecureRects();
            
            if (rects == null) return null;

            rects = rects.Substring(1, rects.Length - 2);
            var rectComponentsStr = rects.Split(',');

            if (rectComponentsStr == null || rectComponentsStr.Length < 1) return null;

            var rectComponents = Array.ConvertAll(rectComponentsStr, Int32.Parse);

            var result = new Rect[rectComponents.Length/4];
            for (int i = 0; i < rectComponents.Length; i += 4)
            {
                    result[i] = new Rect(rectComponents[i],
                                        rectComponents[i + 1],
                                        rectComponents[i + 2],
                                        rectComponents[i + 3]);
            }

            return result;
        }

        private static void OnUpdate()
        {
        }

        private static void OnAwake()
        {
        }

        public static void Snapshot()
        {
        }

        #endregion
#elif UNITY_ANDROID && !UNITY_EDITOR
        #region Android methods
        public static AndroidJavaClass bugseeAdapterClass = new AndroidJavaClass("com.bugsee.library.BugseeUnityAdapter");
        private static bool isRenderErrorLogged = false;
        private volatile static BugseeAndroidVideoMode currentVideoMode;
        private volatile static AndroidJavaClass bugseeOpenGlFrameProducerClass;
        public static AndroidJavaClass unityActivityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        public static AndroidJavaObject activityObj = unityActivityClass.GetStatic<AndroidJavaObject>("currentActivity");


        // Variables used to process attachments on a worker thread.
        private static readonly AutoResetEvent attachmentsEvent = new AutoResetEvent(false);
        private static String reportInfoRaw;
        private static AttachmentForReport currentAttachmentDelegate;
        private static AndroidJavaClass attachmentClass;
        private static readonly System.Object attachmentsSyncObject = new System.Object();

#if UNITY_ANDROID && !UNITY_EDITOR && BUGSEE_VIDEO_V4
        private volatile static SnapshotHandler snapshotHandler;
#endif
        private const String VALUE_NAME = "value";

        private static bool sceneChangedRegistered;

        static Bugsee()
        {
            InitializeOpenGlFrameProducerClass();
            // Initialize thread for attachments processing.
            Thread attachmentsThread = new Thread(ProcessAttachmentsAsync);
            attachmentsThread.Name = "processAttachmentsThread";
            attachmentsThread.Start();
        }

        public static void OnPostRender()
        {
            if (!isBugseeEnabled)
                return;

            // Multithreaded rendering is not currently supported in UnityOpenGl video mode.
            if (SystemInfo.graphicsMultiThreaded)
                return;

            //            if (currentVideoMode != BugseeAndroidVideoMode.UnityOpenGl)
            //            {
            // Give Android Bugsee SDK a chance to obtain GPU info.
            bugseeAdapterClass.CallStatic("onNewFrame");
            return;
            //            }

            // Code for BugseeAndroidVideoMode.UnityOpenGl
            /*if (bugseeOpenGlFrameProducerClass == null && !isRenderErrorLogged)
            {
                Debug.LogWarning("Bugsee: failed to find Bugsee native library");
                isRenderErrorLogged = true;
            }

            if (bugseeOpenGlFrameProducerClass == null || isRenderErrorLogged)
                return;

            try
            {
                bugseeOpenGlFrameProducerClass.CallStatic("onNewFrame");
            } catch (Exception ex)
            {
                if (!isRenderErrorLogged)
                {
                    Debug.LogException(ex);
                    isRenderErrorLogged = true;
                }
            }*/
        }

        public static void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
#if UNITY_ANDROID && !UNITY_EDITOR && BUGSEE_VIDEO_V4
            if (currentVideoMode == BugseeAndroidVideoMode.V4)
                snapshotHandler.OnRenderImage(source);
#endif

            // We have to call this method here, otherwise screen will stay black.
            Graphics.Blit(source, destination);
        }

        // Called via message from java.
        void BugseeOnRecordingStarted(String mediaProjectionEnabled)
        {
            if (OnRecordingStarted != null)
            {
                String enabledNormalized = (mediaProjectionEnabled == null) ? null : mediaProjectionEnabled.ToLowerInvariant();
                bool enabled = "true".Equals(enabledNormalized);
                OnRecordingStarted.Invoke(enabled);
            }

#if UNITY_ANDROID && !UNITY_EDITOR && BUGSEE_VIDEO_V4
            if (currentVideoMode == BugseeAndroidVideoMode.V4)
                snapshotHandler.PlanSnapshot();
#endif
        }

        // Called via message from java.
        void BugseePrepareAttachments(String reportInfoRaw)
        {
            lock (attachmentsSyncObject)
            {
                currentAttachmentDelegate = OnAttachmentForReport;
                Bugsee.reportInfoRaw = reportInfoRaw;
                if (attachmentClass == null)
                {
                    // Worker thread crashes on this operation, even if was attached to JNI earlier, hence execute it aforehand.
                    attachmentClass = new AndroidJavaClass("com.bugsee.library.attachment.CustomAttachment");
                }
            }
            attachmentsEvent.Set();
        }

        // Called on a worker thread.
        private static void ProcessAttachmentsAsync()
        {
            AndroidJNI.AttachCurrentThread();
            while (true)
            {
                attachmentsEvent.WaitOne();
                PrepareAttachments();
            }
        }

        private static void PrepareAttachments()
        {
            AndroidJavaObject resultAttachments = null;
            AttachmentForReport attachmentDelegateLocal;
            String reportInfoRawLocal;
            AndroidJavaClass attachmentClassLocal;
            lock (attachmentsSyncObject)
            {
                attachmentDelegateLocal = currentAttachmentDelegate;
                reportInfoRawLocal = reportInfoRaw;
                attachmentClassLocal = attachmentClass;
            }
            if (attachmentDelegateLocal != null)
            {
                var values = reportInfoRawLocal.Split('|'); // '|' symbol can't be a part of file path.
                if (values.Length < 3)
                    return;

                BugseeReport report;
                string parentFolderPath = values[0];
                report.type = values[1];
                report.severityLevel = (BugseeSeverityLevel)Int32.Parse(values[2]);
                var attachments = attachmentDelegateLocal(report);
                if (attachments.Count > 0)
                {
                    string dirPath = Path.Combine(parentFolderPath, "bugsee_tmp");
                    if (!Directory.Exists(dirPath))
                    {
                        Directory.CreateDirectory(dirPath);
                    }
                    int i = 0;
                    resultAttachments = new AndroidJavaObject("java.util.ArrayList");
                    foreach (var attachment in attachments)
                    {
                        if (attachment.data.Length < 1) continue;

                        var aName = attachment.name;
                        var aFilename = attachment.filename;

                        if (aName == null) aName = i.ToString();
                        if (aFilename == null) aFilename = aName;

                        var toFile = dirPath + "/attachment_" + i.ToString();
                        var isWritten = writeDataToFile(attachment.data, toFile);
                        if (isWritten)
                        {
                            var currentAttachment = attachmentClassLocal.CallStatic<AndroidJavaObject>("fromDataFilePath", toFile);
                            currentAttachment.Call("setFileName", aFilename);
                            currentAttachment.Call("setName", aName);
                            resultAttachments.Call<bool>("add", currentAttachment);
                        }
                        i++;
                    }
                }
            }
            // Call setAttachments() even with null argument to inform java side that attachments processing is finished.
            bugseeAdapterClass.CallStatic("setAttachments", resultAttachments);
        }

        /// <summary>
        /// Form new frame for video. Method has effect only when BugseeAndroidVideoMode.V4 is specified as VideoMode within launch options.
        /// </summary>
        public static void Snapshot()
        {
#if UNITY_ANDROID && !UNITY_EDITOR && BUGSEE_VIDEO_V4
            if (currentVideoMode != BugseeAndroidVideoMode.V4)
            {
                Debug.LogWarning("Bugsee.Snapshot() method haves effect only when BugseeAndroidVideoMode.V4 is specified as VideoMode within launch options.");
            }
            else
            {
                snapshotHandler.PlanSnapshot();
            }
#else
            Debug.LogWarning("Bugsee.Snapshot() is no-op as BugseeAndroidVideoMode.V4 is not allowed and enabled with preprocessor setting BUGSEE_VIDEO_V4.");
#endif
        }

        public static void LogException(Exception exception)
        {
            HandleException(exception, true);
        }

        public static string GetDeviceID()
        {
            return bugseeAdapterClass.CallStatic<String>("getDeviceId");
        }

        private static void OnUpdate()
        {
#if UNITY_ANDROID && !UNITY_EDITOR && BUGSEE_VIDEO_V4
            if (currentVideoMode == BugseeAndroidVideoMode.V4)
                snapshotHandler.OnUpdate();
#endif
        }

        private static void OnAwake()
        {
#if UNITY_ANDROID && !UNITY_EDITOR && BUGSEE_VIDEO_V4
            if (currentVideoMode == BugseeAndroidVideoMode.V4)
                snapshotHandler.OnAwake();
#endif
        }

        private static bool writeDataToFile(byte[] data, string outFilePath)
        {
            BinaryWriter writer = null;
            try
            {
                FileStream fileStream = new FileStream(outFilePath, FileMode.Create);
                writer = new BinaryWriter(fileStream);
                writer.Write(data);
                return true;
            }
            catch (Exception e)
            {
                Debug.Log("Attachment write exception - " + e);
                return false;
            }
            finally
            {
                writer.Close();
            }
        }

        private static void HandleExceptionOnCurrentThread(Exception exception, bool handled)
        {
#if UNITY_ANDROID && !UNITY_EDITOR && BUGSEE_VIDEO_V4
            if (currentVideoMode == BugseeAndroidVideoMode.V4)
            {
                snapshotHandler.GetSnapshotFromCameras();
            }
#endif

            AndroidJavaObject exceptionInfo = GetExceptionInfo(exception);
            bugseeAdapterClass.CallStatic("logException", exceptionInfo, handled);
        }

        private static void HandleLogOnCurrentThread(LogEvent logEvent)
        {
#if UNITY_ANDROID && !UNITY_EDITOR && BUGSEE_VIDEO_V4
            if (currentVideoMode == BugseeAndroidVideoMode.V4)
                snapshotHandler.GetSnapshotFromCameras();
#endif

            AndroidJavaObject exceptionInfo = GetLogInfo(logEvent.Message, logEvent.StackTrace, logEvent.LogType);
            bugseeAdapterClass.CallStatic("logException", exceptionInfo, false);
        }

        private static AndroidJavaObject GetExceptionInfo(Exception exception)
        {
            var rawFrames = GetRawFramesTraces(exception.StackTrace);

            string exName = exception.GetType().FullName;
            string reason = exception.Message;
            var exceptionInfo = GetExceptionInfo(exName, reason, rawFrames);
            if (exception.InnerException != null)
            {
                exceptionInfo.Set<AndroidJavaObject>("cause", GetExceptionInfo(exception.InnerException));
            }
            return exceptionInfo;
        }

        private static AndroidJavaObject GetLogInfo(string message, string condition, LogType type)
        {
            string name = string.Empty;

            if (type == LogType.Exception)
                name = message.Split(':').First();
            else
                name = message.Split('\n').First();

            var rawFrames = GetRawFramesTraces(condition);

            return GetExceptionInfo(name, message, rawFrames);
        }

        private static AndroidJavaObject GetExceptionInfo(string logString, string stackTrace)
        {
            const string delimiter = ": ";
            var rawFrames = GetRawFramesTraces(stackTrace);
            int delimiterIndex = logString.IndexOf(delimiter);

            string exName = (delimiterIndex > 0) ? logString.Substring(0, delimiterIndex) : "";
            string reason = (delimiterIndex > 0) ? logString.Substring(delimiterIndex + delimiter.Length) : logString;
            return GetExceptionInfo(exName, reason, rawFrames);
        }

        private static AndroidJavaObject GetExceptionInfo(string exName, string reason, string[] rawFrames)
        {
            var bgsEx = new BugseeException();
            bgsEx.name = exName;
            bgsEx.reason = reason;
            bgsEx.signature = CalculateSignature(bgsEx.name, rawFrames);
            bgsEx.frames = GetBugseeFrames(rawFrames);

            // Note: we can't set null to AndroidJavaObject's field. Otherwise we will get NullReferenceException.
            AndroidJavaObject exceptionInfo = new AndroidJavaObject("com/bugsee/library/data/CrashInfo$ExceptionInfo");

            exceptionInfo.Set<String>("name", "UnityManagedException");
            exceptionInfo.Set<String>("reason", JsonUtility.ToJson(bgsEx));

            var frames = GetFrames(rawFrames);
            if (frames != null)
            {
                exceptionInfo.Set<AndroidJavaObject>("frames", frames);
            }

            exceptionInfo.Set<String>("additionalSignature", bgsEx.signature);
            return exceptionInfo;
        }

        private static AndroidJavaObject GetFrames(String[] stackFrames)
        {
            AndroidJavaObject lastFrame = null;
            var frames = new AndroidJavaObject("java.util.ArrayList");
            for (int i = 0; i < stackFrames.Length; i++)
            {
                if (stackFrames[i] == null)
                    continue;

                AndroidJavaObject frameInfo = new AndroidJavaObject("com/bugsee/library/data/CrashInfo$FrameInfo");
                frameInfo.Set<String>("trace", stackFrames[i]);
                frameInfo.Set<Int32>("count", 1);
                if ((lastFrame != null) && String.Equals(lastFrame.Get<String>("trace"), (frameInfo.Get<String>("trace"))))
                {
                    // Is this a recursion? Lets not accumulate everything but
                    // increase a counter on the last frame
                    lastFrame.Set<Int32>("count", lastFrame.Get<Int32>("count") + 1);
                }
                else
                {
                    // Add new frame to the list
                    frames.Call<bool>("add", frameInfo);
                    lastFrame = frameInfo;
                }
            }
            return frames;
        }

        public static void AddSecureRect(Rect pixelRect)
        {
            AndroidJavaObject javaRect = new AndroidJavaObject("android.graphics.Rect");
            javaRect.Call("set", pixelRect.xMin, pixelRect.yMin, pixelRect.xMax, pixelRect.yMax);
            bugseeAdapterClass.CallStatic("addSecureRectangle", javaRect);
        }

        public static void RemoveSecureRect(Rect pixelRect)
        {
            AndroidJavaObject javaRect = new AndroidJavaObject("android.graphics.Rect");
            javaRect.Call("set", pixelRect.xMin, pixelRect.yMin, pixelRect.xMax, pixelRect.yMax);
            bugseeAdapterClass.CallStatic("removeSecureRectangle", javaRect);
        }

        public static void RemoveAllSecureRects()
        {
            bugseeAdapterClass.CallStatic("removeAllSecureRectangles");
        }

        public static Rect[] GetAllSecureRects()
        {
            AndroidJavaObject javaArrayList = bugseeAdapterClass.CallStatic<AndroidJavaObject>("getAllSecureRectangles");
            int javaArrayListSize = javaArrayList.Call<int>("size");
            Rect[] resultArray = new Rect[javaArrayListSize];
            for (int i = 0; i < javaArrayListSize; i++)
            {
                var current = javaArrayList.Call<AndroidJavaObject>("get", i);
                int top = current.Get<int>("top");
                int left = current.Get<int>("left");
                int bottom = current.Get<int>("bottom");
                int right = current.Get<int>("right");
                resultArray[i] = new Rect(left, top, right - left, bottom - top);
            }
            return resultArray;
        }

        /// <summary>
        /// Initialize Bugsee SDK with custom parameters.
        /// </summary>
        /// <param name="appToken">Application token</param>
        /// <param name="options">Custom parameters</param>
        public static void Launch(string appToken, BugseeLaunchOptions options = null)
        {
            var launchOptions = options == null ? AndroidLaunchOptions.GetDefaultOptions() : options;
            AndroidLaunchOptions castedOptions = launchOptions as AndroidLaunchOptions;
            if (castedOptions != null)
            {
                // Disable GL screen capture if corresponding library was not loaded. Multithreaded rendering is not currently supported in UnityOpenGl video mode.
                /*if (bugseeOpenGlFrameProducerClass == null || SystemInfo.graphicsMultiThreaded)
                {
                    if (castedOptions.VideoMode == BugseeAndroidVideoMode.UnityOpenGl)
                    {
                        castedOptions.VideoMode = BugseeAndroidVideoMode.None;
                    }
                }*/
                currentVideoMode = castedOptions.VideoMode;

#if UNITY_ANDROID && !UNITY_EDITOR && BUGSEE_VIDEO_V4
                if (currentVideoMode == BugseeAndroidVideoMode.V4)
                {

                    if (snapshotHandler == null)
                        snapshotHandler = new SnapshotHandler(bugseeAdapterClass);

                    snapshotHandler.Initialize(castedOptions.SnapshotSize, castedOptions.SnapshotCopyFactor);

                }
#endif
            }
            var serializedOptions = launchOptions.SerializeOptions();
            Launch(appToken, serializedOptions);
        }

        public static void Launch(string appToken, IDictionary<string, object> options)
        {
            UpdateBuildGUID();
            AddWrapperInfo(options);
            bugseeAdapterClass.CallStatic("launch", activityObj, appToken, Serialize(options));
            StartOnUnityLevel(options);
            RegisterSceneChanged();
        }

        private static void InitializeOpenGlFrameProducerClass()
        {
            String videoFrameProducerClassName = "com.bugsee.opengl.VideoFrameProducer";
            try
            {
                AndroidJavaClass javaClass = new AndroidJavaClass("java.lang.Class");
                // Will throw exception, which can be caught, if VideoFrameProducer is not loaded. Otherwise, AndroidJavaClass can produce uncatchable error in this case.
                javaClass.CallStatic<AndroidJavaClass>("forName", videoFrameProducerClassName);
                bugseeOpenGlFrameProducerClass = new AndroidJavaClass(videoFrameProducerClassName);
            }
            catch (Exception ex)
            {
                // Ignore.   
            }
        }

        /// <summary>
        /// Restart Bugsee SDK with new options.
        /// </summary>
        /// <param name="options">Custom options</param>
        public static void Relaunch(BugseeLaunchOptions options = null)
        {
            if (!isBugseeEnabled)
            {
                Debug.Log("Bugsee: Please Bugsee.Launch() first.");
                return;
            }

            var launchOptions = options == null ? AndroidLaunchOptions.GetDefaultOptions() : options;
            AndroidLaunchOptions castedOptions = launchOptions as AndroidLaunchOptions;
            if (castedOptions != null)
            {
                currentVideoMode = castedOptions.VideoMode;
            }
            var dictionaryOptions = launchOptions.SerializeOptions();
            AddWrapperInfo(dictionaryOptions);
            bugseeAdapterClass.CallStatic("relaunch", Serialize(dictionaryOptions));
            StartOnUnityLevel(dictionaryOptions);
            RegisterSceneChanged();
        }

        /// <summary>
        /// Stop bugsee.
        /// </summary>
        public static void Stop()
        {
            if (!isBugseeEnabled)
            {
                Debug.Log("Bugsee not launched.");
                return;
            }

            bugseeAdapterClass.CallStatic("stop");
            StopOnUnityLevel();
            // We don't stop SceneChangedListener in order to be able to detect last scene name on next Launch().
        }

        private static void AddWrapperInfo(IDictionary<string, object> options)
        {
            var wrapperInfo = new Dictionary<string, object>();
            wrapperInfo.Add("type", "UNITY");
            wrapperInfo.Add("version", BugseePluginVersion.BUGSEE_PLUGIN_VERSION);
            wrapperInfo.Add("build", BugseePluginVersion.BUGSEE_PLUGIN_BUILD);
            wrapperInfo.Add("unity_version", Application.unityVersion);
            options.Add("wrapper_info", wrapperInfo);
        }

        public static void ShowReport(string summary = "", string description = "", BugseeSeverityLevel severity = BugseeSeverityLevel.High)
        {
            bugseeAdapterClass.CallStatic("showReportDialog", summary, description, (int)severity);
        }

        public static void Pause()
        {
            bugseeAdapterClass.CallStatic("pause");
        }

        public static void Resume()
        {
            bugseeAdapterClass.CallStatic("resume");
        }

        public static void Event(string name, IDictionary<string, object> parameters = null)
        {
            bugseeAdapterClass.CallStatic("event", name, Serialize(parameters));
        }

        public static void Trace(string name, object value)
        {
            string serializedVal = Serialize(value, false);
            string result = "{\"" + VALUE_NAME + "\":" + serializedVal + "}";
            bugseeAdapterClass.CallStatic("trace", name, result);
        }

        public static void Upload(string summary, string description = "", BugseeSeverityLevel severity = BugseeSeverityLevel.High)
        {
            bugseeAdapterClass.CallStatic("upload", summary, description, (int)severity);
        }

        public static void Log(string message, BugseeLogLevel level = BugseeLogLevel.Debug)
        {
            bugseeAdapterClass.CallStatic("log", message, (int)level);
        }

        public static void ShowFeedbackUI()
        {
            bugseeAdapterClass.CallStatic("showFeedbackActivity");
        }

        public static void SetFeedbackGreetingMessage(string message)
        {
            bugseeAdapterClass.CallStatic("setDefaultFeedbackGreeting", message);
        }

        public static void Assert(bool condition, string description = "")
        {
            // Not implemented in native Android Bugsee library.
        }

        private static void RegisterSceneChanged()
        {
            if (!sceneChangedRegistered)
            {
#if UNITY_5_4_OR_NEWER
                SceneManager.activeSceneChanged += OnSceneChanged;
#endif
                sceneChangedRegistered = true;
            }
        }

        private static void UnregisterSceneChanged()
        {
            if (sceneChangedRegistered)
            {
#if UNITY_5_4_OR_NEWER
                SceneManager.activeSceneChanged -= OnSceneChanged;
#endif
                sceneChangedRegistered = false;
            }
        }

        public static bool SetAttribute(string key, object val)
        {
            if (key == null || key.Length == 0)
                return false;

            string serializedVal = Serialize(val, false);
            string result = "{\"" + VALUE_NAME + "\":" + serializedVal + "}";
            bugseeAdapterClass.CallStatic("setAttribute", key, result);
            return true;
        }

        public static object GetAttribute(string key)
        {
            string strValue = bugseeAdapterClass.CallStatic<string>("getAttribute", key);
            if (strValue == null)
                return null;

            return DeserializeValue(strValue);
        }

        public static bool ClearAttribute(string key)
        {
            if (key == null || key.Length == 0)
                return false;

            bugseeAdapterClass.CallStatic("clearAttribute", key);
            return true;
        }

        public static void SetEmail(string email)
        {
            bugseeAdapterClass.CallStatic("setEmail", email);
        }

        public static string GetEmail()
        {
            return bugseeAdapterClass.CallStatic<string>("getEmail");
        }

        public static void ClearEmail()
        {
            bugseeAdapterClass.CallStatic("setEmail", null);
        }

        public static void TestExceptionCrash()
        {
            try
            {
                bugseeAdapterClass.CallStatic("throwException");
            }
            catch (Exception ex)
            {
                LogException(ex);
            }
        }

        public static void TestSignalCrash() { }

        private static object DeserializeValue(string jsonString)
        {
            int prefixLength = "{\"value\":".Length;
            int postfixLength = 1;
            if (jsonString == null || jsonString.Length <= prefixLength + postfixLength)
                return jsonString; // Too short to be in valid format.

            string valueString = jsonString.Substring(prefixLength, jsonString.Length - prefixLength - postfixLength);
            // Don't try to parse complex data structures - they were already converted to string in SetAttribute() method (at least on Android).
            if (valueString.StartsWith("{") || valueString.StartsWith("["))
                return valueString;

            if (valueString.Length > 2 && valueString.StartsWith("\"") && valueString.EndsWith("\""))
                return valueString.Substring(1, valueString.Length - 2);

            bool boolValue;
            if (Boolean.TryParse(valueString, out boolValue))
                return boolValue;

            int intValue;
            if (Int32.TryParse(valueString, out intValue))
                return intValue;

            long longValue;
            if (Int64.TryParse(valueString, NumberStyles.Integer, CultureInfo.InvariantCulture, out longValue))
                return longValue;

            var cultures = new List<CultureInfo>() { CultureInfo.InvariantCulture, CultureInfo.CurrentCulture, CultureInfo.CurrentUICulture };
            float floatValue;
            foreach (var culture in cultures)
            {
                if (Single.TryParse(valueString, NumberStyles.Number, culture, out floatValue))
                    return floatValue;
            }

            double doubleValue;
            foreach (var culture in cultures)
            {
                if (Double.TryParse(valueString, NumberStyles.Number, culture, out doubleValue))
                    return doubleValue;
            }

            // Unknown format.
            return valueString;
        }

        #endregion
#else // Other not supported platrofms, just empty methods
        #region Other platforms

        /// <summary>
        /// Initialize Bugsee SDK with custom parameters.
        /// </summary>
        /// <param name="appToken">Application token</param>
        /// <param name="options">Custom parameters</param>
        public static void Launch(string appToken, BugseeLaunchOptions options = null)
        {
        }

        public static string GetDeviceID()
        {
            return null;
        }

        /// <summary>
        /// Initialize Bugsee SDK with custom parameters.
        /// </summary>
        /// <param name="appToken">Application token</param>
        /// <param name="options">Dictionary of options</param>
        public static void Launch(string appToken, IDictionary<string, object> options)
        {
            if (!isBugseeEnabled) // remove warnign from console
            {
                isBugseeEnabled = true;
            }
        }

        /// <summary>
        /// Restart Bugsee SDK with new options.
        /// </summary>
        /// <param name="options">Custom options</param>
        public static void Relaunch(BugseeLaunchOptions options = null)
        {
            if (!isBugseeEnabled)
            {
                Debug.Log("Bugsee: Please Bugsee.Launch() first.");
                return;
            }

            isBugseeEnabled = true;
        }


        /// <summary>
        /// Stop bugsee.
        /// </summary>
        public static void Stop()
        {
            if (!isBugseeEnabled)
            {
                Debug.Log("Bugsee not launched.");
                return;
            }

            isBugseeEnabled = false;
        }

        /// <summary>
        /// Show Bugsee report dialog.
        /// </summary>
        /// <param name="summary">Brief summary of the issue to prefill</param>
        /// <param name="description">Description to prefill</param>
        /// <param name="severity">Severity to prefill</param>
        public static void ShowReport(string summary = "", string description = "", BugseeSeverityLevel severity = BugseeSeverityLevel.High)
        {
        }

        /// <summary>
        /// Pause bugsee video and loggers
        /// </summary>
        public static void Pause()
        {
        }
        /// <summary>
        /// Resume bugsee video and loggers
        /// </summary>
        public static void Resume()
        {
        }
        /// <summary>
        /// Register custom event with additional parameters.
        /// </summary>
        /// <param name="name">Unique name of the event</param>
        /// <param name="parameters">Dictionary of parameters to attach to event</param>
        public static void Event(string name, IDictionary<string, object> parameters = null)
        {
        }
        /// <summary>
        /// Custom trace with additional parameters.
        /// </summary>
        /// <param name="name">Unique name of the trace</param>
        /// <param name="value">Value of the variable</param>
        public static void Trace(string name, object value)
        {
        }
        /// <summary>
        /// Upload report manually.
        /// </summary>
        /// <param name="summary">Brief summary of the issue</param>
        /// <param name="description">Description of the issue</param>
        /// <param name="severity">Severity (1..5)</param>
        public static void Upload(string summary, string description = "", BugseeSeverityLevel severity = BugseeSeverityLevel.High)
        {
        }

        /// <summary>
        /// Upload report with exception.
        /// </summary>
        /// <param name="exception">This exception (it's callstack and name) will be shown on the web</param>
        public static void LogException(Exception exception)
        {
        }
        /// <summary>
        /// Send log message to the bugsee logs tab on the web
        /// </summary>
        /// <param name="message">Log message, will show on Logs tab on the web</param>
        /// <param name="level">Level of the log message, fill the message with the different collors on the web</param>
        public static void Log(string message, BugseeLogLevel level = BugseeLogLevel.Debug)
        {
        }

        /// <summary>
        /// Show feedback UI over the screen to comunicate with user
        /// </summary>
        public static void ShowFeedbackUI()
        {
        }

        /// <summary>
        /// Set first message for Feedback UI
        /// </summary>
        public static void SetFeedbackGreetingMessage(string message)
        {
        }

        /// <summary>
        /// Validate assertion and upload if condition is false.
        /// </summary>
        /// <param name="condition">Condition to validate</param>
        /// <param name="description">Description of the issue</param>
        public static void Assert(bool condition, string description = "")
        {
        }

        private static void RegisterExceptionHandlers()
        {
        }

        /// <summary>
        /// Emulate native exception crash
        /// </summary>
        public static void TestExceptionCrash() { }
        /// <summary>
        /// Emulate native signal crash
        /// </summary>
        public static void TestSignalCrash() { }
        /// <summary>
        /// Set user attribute
        /// </summary>
        /// <param name="key">Name of attribute</param>
        /// <param name="val">Attribute value</param>
        public static bool SetAttribute(string key, object val) { return true; }
        /// <summary>
        /// Get user attribute
        /// </summary>
        /// <param name="key">Name of attribute</param>
        public static object GetAttribute(string key) { return null; }
        /// <summary>
        /// Clear attribute with given key
        /// </summary>
        /// <param name="key">Name of attribute</param>
        public static bool ClearAttribute(string key) { return true; }

        /// <summary>
        /// Set users email
        /// </summary>
        /// <param name="email">email</param>
        public static void SetEmail(string email) { }
        public static string GetEmail() { return null; }
        public static void ClearEmail() { }

        /// <summary>
        /// Hides part of the screen under the Rect, maximum is 10 rects
        /// For iOS: all components of rect calculated in points from the top left corner.
        /// </summary>
        /// <param name="rect">Hidden Rect</param>
        public static bool AddSecureRect(Rect rect) { return false; }

        /// <summary>
        /// Remove secure rect, if it exist
        /// </summary>
        /// <param name="rect">Hidden Rect</param>
        public static bool RemoveSecureRect(Rect rect) { return false; }

        /// <summary>
        /// Remove all secure rects, can be added by Bugsee.AddSecureRect(Rect)
        /// </summary>
        public static void RemoveAllSecureRects() { }

        /// <summary>
        /// Get all Rects in array, null if no Rects
        /// </summary>
        public static Rect[] GetAllSecureRects() { return null; }

        private static void HandleExceptionOnCurrentThread(Exception exception, bool handled) { }

        private static void HandleLogOnCurrentThread(LogEvent logEvent) { }

        private static void OnUpdate() { }

        private static void OnAwake() { }

        public static void Snapshot() { }

        #endregion
#endif


        private static string GetPathBasedOnOS()
        {
            if (Application.isEditor)
                return "file://" + Application.persistentDataPath + "/";
            //        else if (Application.isWebPlayer)
            //            return System.IO.Path.GetDirectoryName(Application.absoluteURL).Replace("\\", "/") + "/";
            else if (Application.isMobilePlatform || Application.isConsolePlatform)
                return Application.persistentDataPath;
            else // For standalone player.
                return "file://" + Application.persistentDataPath + "/";
        }

        static public string CreateDirectory()
        {
            // Choose the output path according to the build target.
            string outputPath = Path.Combine(GetPathBasedOnOS(), "bugsee_tmp");
            if (!Directory.Exists(outputPath))
                Directory.CreateDirectory(outputPath);

            return outputPath;
        }

        public void LifecycleEvent(string eventType)
        {
            if (eventType == null) return;
            if (OnLifecycleEvent != null)
            {
                var eventTypeResult = (BugseeLifecycleEventType)Enum.Parse(typeof(BugseeLifecycleEventType), eventType);
                OnLifecycleEvent(eventTypeResult);
            }
        }

        public void AttachmentsForReport(string reportInfoRaw)
        {
            string result = null;
            if (OnAttachmentForReport != null)
            {
                BugseeReport report;
                var values = reportInfoRaw.Split(',');
                report.type = values[0];
                report.severityLevel = (BugseeSeverityLevel)Int32.Parse(values[1]);
                var attachments = OnAttachmentForReport(report);

                if (attachments.Count > 0)
                {
                    var dirPath = CreateDirectory();

                    int i = 0;
                    result = "[";
                    foreach (var attachment in attachments)
                    {
                        if (attachment.data.Length < 1) continue;

                        var aName = attachment.name;
                        var aFilename = attachment.filename;

                        if (aName == null) aName = i.ToString();
                        if (aFilename == null) aFilename = name;

                        var toFile = dirPath + "/attachment_" + i.ToString();

                        var isWrited = true;
                        try
                        {
                            FileStream fileStream = new FileStream(toFile, FileMode.Create);
                            BinaryWriter writer = new BinaryWriter(fileStream);
                            writer.Write(attachment.data);
                            writer.Close();
                        }
                        catch (Exception e)
                        {
                            isWrited = false;
                            Debug.Log("Attachment write exception - " + e);
                        }

                        if (isWrited)
                        {
                            if (i++ > 0) result += ", ";
                            var escapedPath = Uri.EscapeUriString(toFile);
                            result += "{\"name\":\"" + aName + "\",\"filename\":\"" + aFilename + "\",\"path\":\"" + escapedPath + "\"}";
                        }

                    }

                    result += "]";
                }
            }

            // Completion result must be sended anyway, even if it's null
#if UNITY_IPHONE && !UNITY_EDITOR
        _bugsee_setAttachmentsForReport(result);
#endif
        }

    }
}
