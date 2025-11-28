using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace BlenderToUnityPBRImporter.Editor
{
    /// <summary>
    /// テクスチャ検索結果から仮割り当てデータを作成し、
    /// Material への適用処理を提供するユーティリティクラス。
    /// </summary>
    public class TextureAssigner
    {
        /// <summary>
        /// テクスチャ候補および選択結果をまとめたデータクラス。
        /// </summary>
        public class TextureAssignmentData
        {
            public List<Texture2D> AllTextures = new();
            public List<Texture2D> AlbedoCandidates = new();
            public List<Texture2D> NormalCandidates = new();
            public List<Texture2D> MetallicCandidates = new();
            public List<Texture2D> RoughnessCandidates = new();

            public Texture2D Albedo;
            public Texture2D Normal;
            public Texture2D Metallic;
            public Texture2D Roughness;
        }

        /// <summary>
        /// TextureFinder の結果を基に候補リストを整理し、
        /// 候補が1つの場合は自動選択する。
        /// Material にはまだ適用しない。
        /// </summary>
        public TextureAssignmentData PrepareAssignment(TextureFinder.TextureSearchResult search)
        {
            var data = new TextureAssignmentData()
            {
                AlbedoCandidates = search.Albedo,
                NormalCandidates = search.Normal,
                MetallicCandidates = search.Metallic,
                RoughnessCandidates = search.Roughness
            };

            // 全テクスチャを統合 (ここが重要:手動選択用)
            data.AllTextures.AddRange(search.Albedo);
            data.AllTextures.AddRange(search.Normal);
            data.AllTextures.AddRange(search.Metallic);
            data.AllTextures.AddRange(search.Roughness);
            data.AllTextures.AddRange(search.Unknown);

            // 候補が 1 つだけなら自動選択
            if (search.Albedo.Count == 1) data.Albedo = search.Albedo[0];
            if (search.Normal.Count == 1) data.Normal = search.Normal[0];
            if (search.Metallic.Count == 1) data.Metallic = search.Metallic[0];
            if (search.Roughness.Count == 1) data.Roughness = search.Roughness[0];

            return data;
        }

        /// <summary>
        /// TextureAssignmentData の内容を Material に反映する。
        /// Albedo/Normal は直接セットし、Metallic/Roughness は MR マップ生成に利用する。
        /// </summary>
        public void ApplyToMaterial(Material material, TextureAssignmentData data, bool generateMR = true)
        {
            if (material == null)
            {
                Debug.LogError("[ERROR][TextureAssigner] material が null のため割り当てを中断します。");
                return;
            }

            // 1. Albedo / Normal をセット
            if (data.Albedo)
                material.SetTexture("_MainTex", data.Albedo);

            if (data.Normal)
            {
                var normalPath = AssetDatabase.GetAssetPath(data.Normal);
                SetTextureAsNormalMap(normalPath);
                material.SetTexture("_BumpMap", data.Normal);
            }

            // 2. Metallic / Roughness をセット or MR を生成
            if (!generateMR) return;

            string metallicPath = data.Metallic ? AssetDatabase.GetAssetPath(data.Metallic) : null;
            string roughPath = data.Roughness ? AssetDatabase.GetAssetPath(data.Roughness) : null;

            Texture2D mrTex = TextureConverter.CreateMetallicRoughnessMap(metallicPath, roughPath);

            if (mrTex != null)
            {
                material.SetTexture("_MetallicGlossMap", mrTex);
                material.EnableKeyword("_METALLICGLOSSMAP");
            }
            else if (data.Metallic != null)
            {
                // fallback: Metallic 単体をそのまま使う
                material.SetTexture("_MetallicGlossMap", data.Metallic);
                material.EnableKeyword("_METALLICGLOSSMAP");
            }

            Debug.Log("[INFO][TextureAssigner] Material へのテクスチャ割り当てが完了しました。");
        }

        /// <summary>
        /// 指定フォルダ内のテクスチャを自動検索して Material に割り当てる簡略メソッド。
        /// </summary>
        public void AssignTextures(Material material, string textureSearchFolder)
        {
            if (material == null)
            {
                Debug.LogError("[ERROR][TextureAssigner] material が null のため処理を中断します。");
                return;
            }

            if (!Directory.Exists(textureSearchFolder))
            {
                Debug.LogError($"[ERROR][TextureAssigner] テクスチャフォルダが存在しません: {textureSearchFolder}");
                return;
            }

            string folderAssetPath = TextureFinder.ToAssetPath(textureSearchFolder);
            if (!folderAssetPath.StartsWith("Assets/"))
            {
                Debug.LogError($"[ERROR][TextureAssigner] テクスチャフォルダが Assets 以下ではありません: {folderAssetPath}");
                return;
            }

            // TextureFinder を使って候補リストを作成
            var search = TextureFinder.FindTextures(textureSearchFolder);
            var data = PrepareAssignment(search);

            // 自動割り当て
            ApplyToMaterial(material, data, generateMR: true);

            Debug.Log("[INFO][TextureAssigner] テクスチャ自動割り当て処理が完了しました。");
        }

        /// <summary>
        /// 指定テクスチャを NormalMap として再インポートする。
        /// </summary>
        private void SetTextureAsNormalMap(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.sRGBTexture = false;
                importer.SaveAndReimport();

                Debug.Log($"[INFO][TextureAssigner] NormalMap として再インポート: {assetPath}");
            }
        }
    }
}
