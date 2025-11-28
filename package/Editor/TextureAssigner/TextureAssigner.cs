using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace BlenderToUnityPBRImporter.Editor
{
    public class TextureAssigner
    {
        public class TextureAssignmentData
        {
            public List<Texture2D> AllTextures = new();  // ← ここが重要：手動選択用
            public List<Texture2D> AlbedoCandidates = new();
            public List<Texture2D> NormalCandidates = new();
            public List<Texture2D> MetallicCandidates = new();
            public List<Texture2D> RoughnessCandidates = new();

            // UI で最終的に選ばれたもの
            public Texture2D Albedo;
            public Texture2D Normal;
            public Texture2D Metallic;
            public Texture2D Roughness;
        }

        /// <summary>
        /// TextureFinder の結果を基に「仮割り当て」 (候補が1つなら自動決定)
        /// Material にはまだ適用しない (UI確認後に適用する)
        /// </summary>
        public TextureAssignmentData PrepareAssignment(TextureFinder.TextureSearchResult search)
        {
            var data = new TextureAssignmentData();

            // 全テクスチャを統合 (ここが重要:手動選択用)
            data.AllTextures.AddRange(search.Albedo);
            data.AllTextures.AddRange(search.Normal);
            data.AllTextures.AddRange(search.Metallic);
            data.AllTextures.AddRange(search.Roughness);
            data.AllTextures.AddRange(search.Unknown);

            // 候補リスト
            data.AlbedoCandidates = search.Albedo;
            data.NormalCandidates = search.Normal;
            data.MetallicCandidates = search.Metallic;
            data.RoughnessCandidates = search.Roughness;

            // 候補が 1 つだけなら自動選択
            if(search.Albedo.Count == 1) data.Albedo = search.Albedo[0];
            if(search.Normal.Count == 1) data.Normal = search.Normal[0];
            if(search.Metallic.Count == 1) data.Metallic = search.Metallic[0];
            if(search.Roughness.Count == 1) data.Roughness = search.Roughness[0];

            return data;
        }

        /// <summary>
        /// UI で確定した TextureAssignmentData から Material に設定する
        /// </summary>
        public void ApplyToMaterial(Material material, TextureAssignmentData data)
        {
            if (data.Albedo != null)
            {
                material.SetTexture("_MainTex", data.Albedo);
            }

            if (data.Normal != null)
            {
                material.SetTexture("_BumpMap", data.Normal);
            }
        }

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
