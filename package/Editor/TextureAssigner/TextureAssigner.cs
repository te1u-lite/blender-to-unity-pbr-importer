using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using JetBrains.Annotations;

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
            var settings = PbrImportSettings.GetOrCreateSettings();

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
            if (search.Albedo.Count == 1)
                data.Albedo = search.Albedo[0];
            else if (search.Albedo.Count > 1)
                data.Albedo = SelectByPriority(search.Albedo, settings.albedoPriority);

            if (search.Normal.Count == 1)
                data.Normal = search.Normal[0];
            else if (search.Normal.Count > 1)
                data.Normal = SelectByPriority(search.Normal, settings.normalPriority);

            if (search.Metallic.Count == 1)
                data.Metallic = search.Metallic[0];
            else if (search.Metallic.Count > 1)
                data.Metallic = SelectByPriority(search.Metallic, settings.metallicPriority);

            if (search.Roughness.Count == 1)
                data.Roughness = search.Roughness[0];
            else if (search.Roughness.Count > 1)
                data.Roughness = SelectByPriority(search.Roughness, settings.roughnessPriority);

            return data;
        }

        private Texture2D SelectByPriority(List<Texture2D> list, int Priority)
        {
            if (Priority < 0 || Priority >= list.Count)
                return list[0];
            return list[Priority];
        }

        /// <summary>
        /// TextureAssignmentData の内容を Material に反映する。
        /// Albedo/Normal は直接セットし、Metallic/Roughness は MR マップ生成に利用する。
        /// </summary>
        public bool ApplyToMaterial(Material material, TextureAssignmentData data)
        {
            bool changed = false;
            var settings = PbrImportSettings.GetOrCreateSettings();

            if (material == null)
            {
                Debug.LogError("[ERROR][TextureAssigner] material が null のため割り当てを中断します。");
                return false;
            }

            // Albedo
            if (data.Albedo && material.GetTexture("_MainTex") != data.Albedo)
            {
                material.SetTexture("_MainTex", data.Albedo);
                changed = true;
            }

            // Normal
            if (data.Normal && material.GetTexture("_BumpMap") != data.Normal)
            {
                var normalPath = AssetDatabase.GetAssetPath(data.Normal);
                SetTextureAsNormalMap(normalPath);
                material.SetTexture("_BumpMap", data.Normal);
                changed = true;
            }

            // Metallic / Roughness
            if (settings.generateMetallicRoughness)
            {
                Texture2D before = material.GetTexture("_MetallicGlossMap") as Texture2D;

                GenerateAndApplyMR(material, data);

                Texture2D after = material.GetTexture("_MetallicGlossMap") as Texture2D;

                if (before != after)
                    changed = true;
            }
            else
            {
                if (data.Metallic && material.GetTexture("_MetallicGlossMap") != data.Metallic)
                {
                    material.SetTexture("_MetallicGlossMap", data.Metallic);
                    material.EnableKeyword("_METALLICGLOSSMAP");
                    changed = true;
                }
            }

            if (changed)
                Debug.Log("[INFO][TextureAssigner] Material に変更がありました → Dirty");

            return changed;
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
            ApplyToMaterial(material, data);

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

        private void GenerateAndApplyMR(Material material, TextureAssignmentData data)
        {

            string metallicPath = data.Metallic ? AssetDatabase.GetAssetPath(data.Metallic) : null;
            string roughPath = data.Roughness ? AssetDatabase.GetAssetPath(data.Roughness) : null;

            Texture2D mrTex = TextureConverter.CreateMetallicRoughnessMap(metallicPath, roughPath);

            if (mrTex != null)
            {
                material.SetTexture("_MetallicGlossMap", mrTex);
                material.EnableKeyword("_METALLICGLOSSMAP");
            }
        }
    }
}
