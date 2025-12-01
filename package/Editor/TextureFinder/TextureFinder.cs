using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace BlenderToUnityPBRImporter.Editor
{
    /// <summary>
    /// 指定フォルダから Texture2D を検索し、
    /// Albedo / Normal / Metallic / Roughness などへ分類するユーティリティ。
    /// </summary>
    public static class TextureFinder
    {
        /// <summary>
        /// テクスチャ分類結果を保持するデータクラス。
        /// </summary>
        public class TextureSearchResult
        {
            public List<Texture2D> Albedo = new();
            public List<Texture2D> Normal = new();
            public List<Texture2D> Metallic = new();
            public List<Texture2D> Roughness = new();
            public List<Texture2D> Unknown = new();  // どれにも当てはまらなかったもの
        }

        /// <summary>
        /// 指定フォルダ内の Texture2D を検索し分類する。
        /// </summary>
        public static TextureSearchResult FindTextures(string folderFullPath)
        {
            var result = new TextureSearchResult();

            if (string.IsNullOrEmpty(folderFullPath) || !Directory.Exists(folderFullPath))
            {
                Debug.LogError($"[ERROR][TextureFinder] フォルダが存在しません: {folderFullPath}");
                return result;
            }

            string folderAssetPath = ToAssetPath(folderFullPath);

            // Texture2D のみを検索
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderAssetPath });

            foreach (var guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (tex == null) continue;

                string f = Path.GetFileName(assetPath).ToLower();
                if (IsAlbedo(f)) result.Albedo.Add(tex);
                else if (IsNormal(f)) result.Normal.Add(tex);
                else if (IsMetallic(f)) result.Metallic.Add(tex);
                else if (IsRoughness(f)) result.Roughness.Add(tex);
                else result.Unknown.Add(tex);
            }

            Debug.Log($"[INFO][TextureFinder] テクスチャ検索完了: Albedo={result.Albedo.Count}, Normal={result.Normal.Count}, Metallic={result.Metallic.Count}, Roughness={result.Roughness.Count}, Unknown={result.Unknown.Count}");

            return result;
        }

        static bool Match(string fileName, string[] keywords)
        {
            fileName = fileName.ToLower();
            foreach (var key in keywords)
                if (fileName.Contains(key.ToLower()))
                    return true;
            return false;
        }

        /// <summary>
        /// ファイル名から Albedo テクスチャかどうかを判定する。
        /// </summary>
        public static bool IsAlbedo(string f)
        {
            var settings = PbrImportSettings.GetOrCreateSettings();
            return Match(f, settings.albedoKeywords);
        }

        /// <summary>
        /// ファイル名から NormalMap かどうかを判定する。
        /// </summary>
        public static bool IsNormal(string f)
        {
            var settings = PbrImportSettings.GetOrCreateSettings();
            return Match(f, settings.normalKeywords);
        }

        /// <summary>
        /// ファイル名から Metallic テクスチャかどうかを判定する。
        /// </summary>
        public static bool IsMetallic(string f)
        {
            var settings = PbrImportSettings.GetOrCreateSettings();
            return Match(f, settings.metallicKeywords);
        }

        /// <summary>
        /// ファイル名から Roughness テクスチャかどうかを判定する。
        /// </summary>
        public static bool IsRoughness(string f)
        {
            var settings = PbrImportSettings.GetOrCreateSettings();
            return Match(f, settings.roughnessKeywords);
        }


        /// <summary>
        /// 絶対パスを Assets/ から始まるパスへ変換する。
        /// </summary>
        public static string ToAssetPath(string fullPath)
        {
            fullPath = fullPath.Replace("\\", "/");
            string data = Application.dataPath.Replace("\\", "/");
            return fullPath.StartsWith(data) ? "Assets" + fullPath[data.Length..] : fullPath;
        }
    }
}