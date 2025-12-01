using System;
using System.Collections.Generic;
using UnityEditor;

namespace BlenderToUnityPBRImporter.Editor
{
    [InitializeOnLoad]
    public static class DeferredFbxProcessor
    {
        private static readonly Queue<Action> queue = new();
        private static bool isScheduled = false;

        static DeferredFbxProcessor()
        {
            EditorApplication.update += ProcessQueue;
        }

        public static void Enqueue(Action action)
        {
            queue.Enqueue(action);

            // 1回だけ schedule
            if (!isScheduled)
            {
                isScheduled = true;
                EditorApplication.delayCall += () => { isScheduled = false; };
            }
        }

        private static void ProcessQueue()
        {
            // Unity が Import 中なら何もしない
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            while (queue.Count > 0)
            {
                var action = queue.Dequeue();
                try { action?.Invoke(); }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                }
            }
        }
    }
}