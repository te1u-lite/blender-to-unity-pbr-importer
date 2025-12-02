using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using JetBrains.Annotations;
using Codice.Client.Common;

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
            public List<Texture2D> SmoothnessCandidates = new();

            public Texture2D Albedo;
            public Texture2D Normal;
            public Texture2D Metallic;
            public Texture2D Roughness;
            public Texture2D Smoothness;
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

            if (search.Albedo.Count >= 1)
                data.Albedo = search.Albedo[0];

            if (search.Normal.Count >= 1)
                data.Normal = search.Normal[0];

            if (search.Metallic.Count >= 1)
                data.Metallic = search.Metallic[0];

            if (search.Roughness.Count >= 1)
                data.Roughness = search.Roughness[0];

            return data;
        }
        /// <summary>
        /// TextureAssignmentData の内容を Material に反映する。
        /// Albedo/Normal は直接セットし、Metallic/Roughness は MR マップ生成に利用する。
        /// </summary>
        public bool ApplyToMaterial(Material material, TextureAssignmentData data, TextureFinder.TextureSearchResult search, TextureAssignmentWindow.MRMode mode)
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
                string path = AssetDatabase.GetAssetPath(data.Albedo);
                EnsureNotNormalMap(path);

                material.SetTexture("_MainTex", data.Albedo);
                changed = true;
            }
            else if (data.Albedo == null)
            {
                material.SetTexture("_MainTex", null);
            }

            // Normal
            if (data.Normal && material.GetTexture("_BumpMap") != data.Normal)
            {
                var normalPath = AssetDatabase.GetAssetPath(data.Normal);
                SetTextureAsNormalMap(normalPath);
                material.SetTexture("_BumpMap", data.Normal);
                changed = true;
            }
            else if (data.Normal == null)
            {
                material.SetTexture("_BumpMap", null);
            }

            switch (mode)
            {
                case TextureAssignmentWindow.MRMode.Auto:
                    ApplyAutoMode(material, data, search);
                    break;

                case TextureAssignmentWindow.MRMode.MetallicSmoothness:
                    if (data.Metallic != null)
                    {
                        if (material.GetTexture("_MetallicGlossMap") != data.Metallic)
                            changed = true;

                        string path = AssetDatabase.GetAssetPath(data.Metallic);
                        EnsureNotNormalMap(path);

                        material.SetTexture("_MetallicGlossMap", data.Metallic);
                        material.EnableKeyword("_METALLICGLOSSMAP");
                    }
                    else
                    {
                        if (material.GetTexture("_MetallicGlossMap") != null)
                            changed = true;

                        material.SetTexture("_MetallicGlossMap", null);
                        material.DisableKeyword("_METALLICGLOSSMAP");
                    }
                    break;

                case TextureAssignmentWindow.MRMode.MetallicAndSmoothness:
                    ApplyMetallicAndSmoothness(material, data);
                    break;

                case TextureAssignmentWindow.MRMode.MetallicAndRoughness:
                    GenerateAndApplyMR(material, data);   // 既存関数を利用
                    break;
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
            ApplyToMaterial(material, data, search, TextureAssignmentWindow.MRMode.MetallicAndRoughness);

            Debug.Log("[INFO][TextureAssigner] テクスチャ自動割り当て処理が完了しました。");
        }

        private void ApplyAutoMode(Material material, TextureAssignmentData data, TextureFinder.TextureSearchResult search)
        {
            // 1. metallicsmoothness
            var ms = FindMetallicSmoothnessTexture(data.AllTextures);
            if (ms != null)
            {
                ApplyAsMetallicSmoothness(material, ms);
                return;
            }

            // 2. metallic + roughness
            if (search.Metallic.Count > 0 && search.Roughness.Count > 0)
            {
                data.Metallic = search.Metallic[0];
                data.Roughness = search.Roughness[0];
                GenerateAndApplyMR(material, data);
                return;
            }

            // 3. metallic + smoothness (SmoothnessCandidates を使う)
            if (search.Metallic.Count > 0 && data.SmoothnessCandidates.Count > 0)
            {
                data.Metallic = search.Metallic[0];
                data.Smoothness = data.SmoothnessCandidates[0];
                ApplyMetallicAndSmoothness(material, data);
                return;
            }

            // 4. metallic only
            if (search.Metallic.Count > 0)
            {
                data.Metallic = search.Metallic[0];

                string path = AssetDatabase.GetAssetPath(data.Metallic);
                EnsureNotNormalMap(path);

                var tex = TextureConverter.CreateMetallicRoughnessMap(
                    AssetDatabase.GetAssetPath(data.Metallic), null);

                material.SetTexture("_MetallicGlossMap", tex);
                return;
            }

            // 5. roughness only
            if (search.Roughness.Count > 0)
            {
                data.Roughness = search.Roughness[0];

                string path = AssetDatabase.GetAssetPath(data.Roughness);
                EnsureNotNormalMap(path);

                var tex = TextureConverter.CreateMetallicRoughnessMap(
                    null, AssetDatabase.GetAssetPath(data.Roughness));

                material.SetTexture("_MetallicGlossMap", tex);
                return;
            }

            // 6. none
            material.SetTexture("_MetallicGlossMap", null);
        }

        private Texture2D FindMetallicSmoothnessTexture(List<Texture2D> all)
        {
            foreach (var t in all)
            {
                if (t == null) continue;
                string name = t.name.ToLower();

                // 既存 MetallicGlossMap 判定を強化
                bool hasMetal = name.Contains("metal") || name.Contains("met");
                bool hasSmooth = name.Contains("smooth") || name.Contains("gloss") || name.Contains("_sg") || name.Contains("sm");

                if (hasMetal && hasSmooth)
                    return t;
            }
            return null;
        }


        private void EnsureNotNormalMap(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null && importer.textureType == TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true; // 必要に応じて
                importer.SaveAndReimport();

                Debug.Log($"[INFO][TextureAssigner] NormalMap → Default に再インポート: {assetPath}");
            }
        }


        private void ApplyAsMetallicSmoothness(Material material, Texture2D tex)
        {
            string path = AssetDatabase.GetAssetPath(tex);
            EnsureNotNormalMap(path);

            material.SetTexture("_MetallicGlossMap", tex);
            material.EnableKeyword("_METALLICGLOSSMAP");
        }

        private void ApplyMetallicAndSmoothness(Material material, TextureAssignmentData data)
        {
            string metallicPath = data.Metallic ? AssetDatabase.GetAssetPath(data.Metallic) : null;
            string smoothPath = data.Smoothness ? AssetDatabase.GetAssetPath(data.Smoothness) : null;

            Texture2D metallic = metallicPath != null ? TextureConverter.LoadReadable(metallicPath) : null;
            Texture2D smooth = smoothPath != null ? TextureConverter.LoadReadable(smoothPath) : null;

            if (metallic == null && smooth == null)
            {
                material.SetTexture("_MetallicGlossMap", null);
                material.DisableKeyword("_METALLICGLOSSMAP");
                return;
            }
            int w = metallic ? metallic.width : smooth.width;
            int h = metallic ? metallic.height : smooth.height;

            Color[] result = new Color[w * h];
            Color[] mPix = metallic ? metallic.GetPixels() : null;
            Color[] sPix = smooth ? smooth.GetPixels() : null;

            for (int i = 0; i < result.Length; i++)
            {
                float m = mPix != null ? mPix[i].r : 0;
                float s = sPix != null ? sPix[i].r : 1;
                result[i] = new Color(m, m, m, s); // α = smoothness
            }

            var output = new Texture2D(w, h, TextureFormat.RGBA32, false);
            output.SetPixels(result);
            output.Apply();

            // 保存先（metallic と同じフォルダに）
            string dir = Path.GetDirectoryName(metallicPath ?? smoothPath);
            string savePath = Path.Combine(dir, "metallicsmoothness.png").Replace("\\", "/");
            File.WriteAllBytes(savePath, output.EncodeToPNG());
            AssetDatabase.ImportAsset(savePath);

            Texture2D finalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(savePath);

            string path = AssetDatabase.GetAssetPath(finalTex);
            EnsureNotNormalMap(path);

            material.SetTexture("_MetallicGlossMap", finalTex);
            material.EnableKeyword("_METALLICGLOSSMAP");
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

            if (metallicPath != null) EnsureNotNormalMap(metallicPath);
            if (roughPath != null) EnsureNotNormalMap(roughPath);

            Texture2D mrTex = TextureConverter.CreateMetallicRoughnessMap(metallicPath, roughPath);

            if (mrTex != null)
            {
                string path = AssetDatabase.GetAssetPath(mrTex);
                EnsureNotNormalMap(path);

                material.SetTexture("_MetallicGlossMap", mrTex);
                material.EnableKeyword("_METALLICGLOSSMAP");
            }
            else
            {
                material.SetTexture("_MetallicGlossMap", null);
                material.DisableKeyword("_METALLICGLOSSMAP");
            }
        }
    }
}
