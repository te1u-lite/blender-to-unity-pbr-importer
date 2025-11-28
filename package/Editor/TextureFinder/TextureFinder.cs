using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace BlenderToUnityPBRImporter.Editor
{
    /// <summary>
    /// 指定フォルダ内の Texture2D を検索し、
    /// Albedo / Normal / Metallic / Roughness などの候補リストとして返す。
    /// 複数候補があっても良い（後でUIで選択できるため）
    /// </summary>
    public static class TextureFinder
    {
        public class TextureSearchResult
        {
            public List<Texture2D> Albedo = new();
            public List<Texture2D> Normal = new();
            public List<Texture2D> Metallic = new();
            public List<Texture2D> Roughness = new();
            public List<Texture2D> Unknown = new();  // どれにも当てはまらなかったもの
        }


        public static TextureSearchResult FindTextures(string folderFullPath)
        {
            var result = new TextureSearchResult();

            if (string.IsNullOrEmpty(folderFullPath) || !Directory.Exists(folderFullPath))
            {
                Debug.LogError($"[TextureFinder] フォルダが存在しません: {folderFullPath}");
                return result;
            }

            string folderAssetPath = ToAssetPath(folderFullPath);

            // Texture2D を検索
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderAssetPath });

            foreach (var guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (tex == null) continue;

                string lower = Path.GetFileName(assetPath).ToLower();

                if (IsAlbedo(lower))
                {
                    result.Albedo.Add(tex);
                }
                else if (IsNormal(lower))
                {
                    result.Normal.Add(tex);
                }
                else if (IsMetallic(lower))
                {
                    result.Metallic.Add(tex);
                }
                else if (IsRoughness(lower))
                {
                    result.Roughness.Add(tex);
                }
                else
                {
                    result.Unknown.Add(tex);
                }
            }

            return result;
        }

        private static bool IsAlbedo(string f)
        {
            return f.Contains("albedo") ||
                f.Contains("basecolor") ||
                f.Contains("_diff") ||
                f.Contains("diffuse") ||
                f.Contains("color");
        }

        private static bool IsNormal(string f)
        {
            return f.Contains("normal") ||
                f.Contains("nrm") ||
                f.Contains("_nor");
        }

        private static bool IsMetallic(string f)
        {
            return f.Contains("metal") ||
                f.Contains("metallic");
        }

        private static bool IsRoughness(string f)
        {
            return f.Contains("rough") ||
                f.Contains("roughness") ||
                f.Contains("_rgh");
        }

        // 絶対パスを Assets/ に変換
        private static string ToAssetPath(string fullPath)
        {
            fullPath = fullPath.Replace("\\", "/");
            var dataPath = Application.dataPath.Replace("\\", "/");

            if (fullPath.StartsWith(dataPath))
            {
                return "Assets" + fullPath.Substring(dataPath.Length);
            }

            Debug.LogWarning($"[TextureFinder] Assets パスに変換できません: {fullPath}");
            return fullPath;
        }
    }
}