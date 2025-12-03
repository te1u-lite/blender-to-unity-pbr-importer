using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

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
            Debug.Log("[Deferred] Enqueue called");

            queue.Enqueue(action);

            if (!isScheduled)
            {
                Debug.Log("[Deferred] Schedule first call");
                isScheduled = true;
                EditorApplication.delayCall += () =>
                {
                    Debug.Log("[Deferred] delayCall executed");
                    isScheduled = false;
                };
            }
        }

        private static void ProcessQueue()
        {
            // Unity が Import 中なら何もしない
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            while (queue.Count > 0)
            {
                Debug.Log($"[Deferred] ProcessQueue dequeue: {queue.Count}");
                var action = queue.Dequeue();
                action?.Invoke();
            }
        }
    }
}