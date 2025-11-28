using System.IO;
using UnityEngine;
using UnityEditor;

namespace BlenderToUnityPBRImporter.Editor
{
    public class TextureAssigner
    {
        public void AssignTextures(Material material, string textureSearchFolder)
        {
            if (material == null)
            {
                Debug.LogError("[TextureAssigner] material が null");
                return;
            }

            if (!Directory.Exists(textureSearchFolder))
            {
                Debug.LogError($"[TextureAssigner] テクスチャフォルダが存在しません: {textureSearchFolder}");
                return;
            }

            string folderAssetPath = textureSearchFolder.Replace(Application.dataPath, "Assets");

            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderAssetPath });

            Texture2D albedo = null;
            Texture2D normal = null;
            Texture2D metallic = null;
            Texture2D roughness = null;

            string metallicPath = null;
            string roughnessPath = null;

            foreach (var guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                string lower = Path.GetFileName(assetPath).ToLower();

                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (tex == null) continue;

                // Albedo
                if (IsAlbedo(lower))
                {
                    albedo = tex;
                    material.SetTexture("_MainTex", tex);
                    continue;
                }

                // Normal
                if (IsNormal(lower))
                {
                    SetTextureAsNormalMap(assetPath);
                    normal = tex;
                    material.SetTexture("_BumpMap", tex);
                    continue;
                }

                // Metallic
                if (IsMetallic(lower))
                {
                    metallic = tex;
                    metallicPath = assetPath;
                    continue;
                }

                // Roughness
                if (IsRoughness(lower))
                {
                    roughness = tex;
                    roughnessPath = assetPath;
                    continue;
                }
            }

            // ==========================================
            // MetallicRoughnessMap の生成（追加部分）
            // ==========================================
            Texture2D metallicRoughnessMap = null;

            if (metallicPath != null || roughnessPath != null)
            {
                metallicRoughnessMap = TextureConverter.CreateMetallicRoughnessMap(metallicPath, roughnessPath);
            }

            // ==========================================
            // Material への割り当て
            // ==========================================
            if (metallicRoughnessMap != null)
            {
                material.SetTexture("_MetallicGlossMap", metallicRoughnessMap);
                material.EnableKeyword("_METALLICGLOSSMAP");
            }
            else if (metallic != null)
            {
                // fallback metallic only
                material.SetTexture("_MetallicGlossMap", metallic);
                material.EnableKeyword("_METALLICGLOSSMAP");
            }

            Debug.Log("[TextureAssigner] テクスチャ割り当て完了");
        }


        // -------------------------------------------------------------
        // 判定ロジック（元コードと同じ）
        // -------------------------------------------------------------
        private bool IsAlbedo(string f)
        {
            return f.Contains("color") ||
                   f.Contains("albedo") ||
                   f.Contains("basecolor") ||
                   f.Contains("_diff") ||
                   f.Contains("diffuse");
        }

        private bool IsNormal(string f)
        {
            return f.Contains("normal") || f.Contains("nrm") || f.Contains("nor");
        }

        private bool IsMetallic(string f)
        {
            return f.Contains("metal") || f.Contains("metallic");
        }

        private bool IsRoughness(string f)
        {
            return f.Contains("rough") ||
                   f.Contains("roughness") ||
                   f.Contains("_rgh");
        }

        private void SetTextureAsNormalMap(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }
        }
    }
}
