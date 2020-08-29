#if UNITY_ANDROID && !UNITY_EDITOR && BUGSEE_VIDEO_V4

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace BugseePlugin
{
    class SnapshotHandler
    {
        private const bool isVerbose = false;
        public const int DEFAULT_COPY_FACTOR = 1;

        // If 640 is added to the list of supported sizes, changes in DeviceInfoProvider.getNormalizedSize() would be necessary.
        private const int MAX_FRAME_SIDE_SMALL = 320;
        private const int MAX_FRAME_SIDE_MIDDLE = 480;

        private readonly Frame[] frames = new Frame[2];
        private readonly AndroidJavaClass bugseeAdapterClass;

        private volatile bool planSnapshot;
        private int currentFrameIndex = 0;
        private int copyFactor;
        private int maxFrameSide;
        private volatile Material flipFrameMaterial;

        // Variables to make snapshot immediately on exception.
        private readonly Camera[] cameras = new Camera[20];
        private RenderTexture fullSizeTexture;

        internal SnapshotHandler(AndroidJavaClass adapterClass)
        {
            bugseeAdapterClass = adapterClass;
        }

        // Called on UI thread.
        internal void OnAwake()
        {
            //if (flipFrameMaterial == null)
            //{
            //    flipFrameMaterial = new Material(Shader.Find("Hidden/SwapRB")); //Flip
            //    Debug.Log("flipFrameMaterial: " + flipFrameMaterial);
            //}  
        }

        // Called on UI thread.
        internal void OnUpdate()
        {
            Frame frameToProcess;
            lock (frames)
            {
                frameToProcess = frames
                    .Where(frame => frame.state != Frame.State.Ready)
                    .OrderBy(frame => frame.timestampMs)
                    .FirstOrDefault();
            }
            if (frameToProcess != null)
            {
                if (frameToProcess.state == Frame.State.Transfer)
                {

                    TransferToJava(frameToProcess, false);

                    frameToProcess.state = Frame.State.Ready;
                }
                else // Copy next part of texture from GPU.
                {

                }
            }
        }

        internal void TransferToJava(Frame frame, bool doAllOnUiThread)
        {
            NativeArray<byte> data = frame.texture.GetRawTextureData<byte>();
            IntPtr ptr;

            // Get pointer to native (C++) pixel array.
            unsafe
            {
                ptr = (IntPtr)NativeArrayUnsafeUtility.GetUnsafePtr(data); // void* -> IntPtr explicit conversion.
            }
            // Send native pixel array pointer to java side.
            bugseeAdapterClass.CallStatic("onNewFrame", ptr.ToInt64(), frame.texture.width, frame.texture.height, frame.timestampMs, doAllOnUiThread);
        }

        // Called on rendering thread.
        internal void OnRenderImage(RenderTexture source)
        {
            if (planSnapshot)
            {

                lock (frames)
                {
                    if (frames[currentFrameIndex].state != Frame.State.Ready)
                    {
                        // Too frequent snapshot requests. Buffer is full, hence reject the request.
                        planSnapshot = false;
                        return;
                    }
                    frames[currentFrameIndex].state = Frame.State.CopyingFromGpuToCpu;
                }
                frames[currentFrameIndex].Capture(source, flipFrameMaterial);

                planSnapshot = false;
                currentFrameIndex = (currentFrameIndex + 1) % frames.Length;
            }
        }

        // Can be called on different threads.
        internal void PlanSnapshot()
        {

            planSnapshot = true;
        }

        // Must be called before Bugsee.launch().
        internal void Initialize(SnapshotSize snapshotSize, int copyFactor)
        {
            maxFrameSide = (snapshotSize == SnapshotSize.Small) ? MAX_FRAME_SIDE_SMALL : MAX_FRAME_SIDE_MIDDLE;
            this.copyFactor = ToInterval(copyFactor, 1, 100);
            // Notify java side about frame size beforehand.
            bugseeAdapterClass.CallStatic("onNewFrame", 0L, maxFrameSide, maxFrameSide, 0L, false);
            // Notify java side about initial timestamp.
            bugseeAdapterClass.CallStatic("setInitialTimestampMs", (long)Math.Round(Time.realtimeSinceStartup * 1000));
            // Choose frame width and height.
            int width, height;
            int maxScreenDimension = Math.Max(Screen.width, Screen.height);
            if (maxScreenDimension == Screen.height)
            {
                height = Math.Min(Screen.height, maxFrameSide);
                width = Mathf.RoundToInt((height / (float)Screen.height) * Screen.width);
            }
            else
            {
                width = Math.Min(Screen.width, maxFrameSide);
                height = Mathf.RoundToInt(width / (float)Screen.width * Screen.height);
            }
            // Create texture and frames buffer.
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGB24, false, false);
            for (int x = 0; x < frames.Length; x++)
            {
                // Each frame has its own buffer, but all of them share the same texture, because frames' texture is filled in turn.
                frames[x] = new Frame(copyFactor)
                {
                    texture = texture,
                    timestampMs = -1,
                    buffer = new RenderTexture(width, height, 1, RenderTextureFormat.Default, RenderTextureReadWrite.Default),
                    state = Frame.State.Ready
                };
            }
        }

        // Called on UI thread.
        internal void GetSnapshotFromCameras()
        {
            // Find frame to process.
            Frame frameToProcess;
            lock (frames)
            {
                frameToProcess = frames
                    .Where(frame => frame.state == Frame.State.Ready)
                    .FirstOrDefault();

                if (frameToProcess == null)
                    return;

                frameToProcess.state = Frame.State.CopyingFromGpuToCpu;
            }

            try
            {
                if (fullSizeTexture == null)
                {
                    fullSizeTexture = new RenderTexture(Screen.width, Screen.height, 1, RenderTextureFormat.Default, RenderTextureReadWrite.Default);
                }
                int cameraCount = Camera.GetAllCameras(cameras);
                int drawnCameraCount = 0;
                frameToProcess.timestampMs = Frame.GetTimeSinceStartupMs();
                for (int i = 0; i < cameraCount; i++)
                {
                    if (cameras[i] == null)
                        continue;

                    // Render camera to a full size texture.
                    if (cameras[i].targetTexture == null)
                    {
                        cameras[i].targetTexture = fullSizeTexture;
                        cameras[i].Render();
                        cameras[i].targetTexture = null;
                        drawnCameraCount++;
                    }
                }
                if (drawnCameraCount > 0)
                {
                    ProcessFullSizeTexture(fullSizeTexture, frameToProcess);
                }
            }
            finally
            {
                frameToProcess.state = Frame.State.Ready;
            }
        }

        private void ProcessFullSizeTexture(RenderTexture fullSizeTexture, Frame tempFrame)
        {
            // Copy full size texture to a smaller one with scaling.
            Graphics.Blit(fullSizeTexture, tempFrame.buffer);
            // Copy texture from GPU to CPU.
            RenderTexture.active = tempFrame.buffer;
            try
            {
                tempFrame.texture.ReadPixels(new Rect(0, 0, tempFrame.buffer.width, tempFrame.buffer.height), 0, 0);
            }
            finally
            {
                RenderTexture.active = null;
            }
            TransferToJava(tempFrame, true);
        }

        private static int ToInterval(int value, int min, int max)
        {
            return Math.Max(min, Math.Min(max, value));
        }
    }
}

#endif