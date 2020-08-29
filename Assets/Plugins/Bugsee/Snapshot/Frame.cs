#if UNITY_ANDROID && !UNITY_EDITOR && BUGSEE_VIDEO_V4

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace BugseePlugin
{
    public class Frame
    {
        public enum State
        {
            Ready,
            CopyingFromGpuToCpu, 
            Transfer
        }

        public Texture2D texture;
        public long timestampMs;
        public RenderTexture buffer;
        public volatile State state;

        private readonly int partCount;
        private int currentPart;
        private int partHeight;
        private Rect readRect;

        public Frame(int texturePartCount)
        {
            partCount = texturePartCount;
        }

        public bool FillNextTexturePart()
        {
            // Method is called by mistake.
            if (currentPart == partCount)
                return true;

            RenderTexture.active = buffer;
            try
            {
                // Last part should be read.
                if (currentPart == partCount - 1)
                {
                    texture.ReadPixels(new Rect(0, currentPart * partHeight, buffer.width, buffer.height), 0, currentPart * partHeight);
                }
                else // The part is not the last one.
                {
                    texture.ReadPixels(new Rect(0, currentPart * partHeight, buffer.width, (currentPart + 1) * partHeight), 0, currentPart * partHeight);
                }
            }
            finally
            {
                RenderTexture.active = null;
            }

            currentPart++;
            bool result = (currentPart == partCount);
            if (result)
            {
                state = State.Transfer;
            }
            return result;
        }

        public Texture2D GetTexture()
        {
            return texture;
        }

        internal static long GetTimeSinceStartupMs()
        {
            return (long)Math.Round(Time.realtimeSinceStartup * 1000);
        }

        internal void Capture(RenderTexture source, Material material)
        {
            timestampMs = GetTimeSinceStartupMs();
            if (material != null)
            {
                Graphics.Blit(source, buffer, material);
            }
            else
            {
                Graphics.Blit(source, buffer);
            }
            currentPart = 0;
            partHeight = buffer.height / partCount;
            if (buffer.height % partCount != 0)
            {
                partHeight++;
            }
        }
    }
}

#endif