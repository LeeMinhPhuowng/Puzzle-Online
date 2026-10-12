using System;
using UnityEngine;

namespace PuzzleOnline.Utility
{
    public readonly struct ResponsiveGUI : IDisposable
    {
        private static float Scale => Mathf.Max(0.01f, Mathf.Min(Screen.width / 1920f, Screen.height / 1080f));
        public static float LogicalWidth => Screen.width / Scale;
        public static float LogicalHeight => Screen.height / Scale;
        private readonly Matrix4x4 previous;
        public float Width { get; }
        public float Height { get; }

        // Pass the layout explicitly: new ResponsiveGUI() zero-initializes a struct.
        public ResponsiveGUI(bool fixedLayout)
        {
            previous = GUI.matrix;
            float scale = Mathf.Max(0.01f, Mathf.Min(Screen.width / 1920f, Screen.height / 1080f));
            Width = Screen.width / scale;
            Height = Screen.height / scale;
            Vector3 offset = fixedLayout
                ? new Vector3((Screen.width - 1920f * scale) * 0.5f, (Screen.height - 1080f * scale) * 0.5f)
                : Vector3.zero;
            GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, new Vector3(scale, scale, 1f));
        }

        public void Dispose() => GUI.matrix = previous;
    }
}
