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
        public bool ApplyToMaterial(Material material, TextureAssignmentData data, TextureAssignmentWindow.MRMode mode)
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

            switch (mode)
            {
                case TextureAssignmentWindow.MRMode.Auto:
                    ApplyAutoMode(material, data);
                    break;

                case TextureAssignmentWindow.MRMode.MetallicSmoothness:
                    if (data.Metallic != null)
                    {
                        material.SetTexture("_MetallicGlossMap", data.Metallic);
                        material.EnableKeyword("_METALLICGLOSSMAP");
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
            ApplyToMaterial(material, data, TextureAssignmentWindow.MRMode.MetallicAndRoughness);

            Debug.Log("[INFO][TextureAssigner] テクスチャ自動割り当て処理が完了しました。");
        }

        private void ApplyAutoMode(Material material, TextureAssignmentData data)
        {
            // 1. metallicsmoothness を名前で探索
            Texture2D metallicSmooth = FindMetallicSmoothnessTexture(data.AllTextures);

            if (metallicSmooth != null)
            {
                ApplyAsMetallicSmoothness(material, metallicSmooth);
                Debug.Log("[AutoMode] metallicsmoothness テクスチャを直接使用");
                return;
            }

            // 2. metallic + roughness あり → metallicsmoothness 生成
            if (data.Metallic != null && data.Roughness != null)
            {
                GenerateAndApplyMR(material, data);
                Debug.Log("[AutoMode] metallic + roughness から metallicsmoothness を生成");
                return;
            }

            // 3. metallic + smoothness あり → metallicsmoothness 生成
            if (data.Metallic != null && data.Smoothness != null)
            {
                ApplyMetallicAndSmoothness(material, data);
                Debug.Log("[AutoMode] metallic + smoothness から metallicsmoothness を生成");
                return;
            }

            // 4. metallic のみ + (smoothness/roughness無し)
            // → metallic だけでも metallicsmoothness を生成して α=1 のマップを作る
            if (data.Metallic != null)
            {
                Texture2D generated = TextureConverter.CreateMetallicRoughnessMap(
                    AssetDatabase.GetAssetPath(data.Metallic),
                    null
                );
                material.SetTexture("_MetallicGlossMap", generated);
                material.EnableKeyword("_METALLICGLOSSMAP");
                Debug.Log("[AutoMode] metallic のみから metallicsmoothness を生成 (α=1)");
                return;
            }

            // 5. roughness または smoothness のみ
            // → metallic=0 の metallicsmoothness を生成
            if (data.Roughness != null)
            {
                Texture2D generated = TextureConverter.CreateMetallicRoughnessMap(null,
                    AssetDatabase.GetAssetPath(data.Roughness));
                material.SetTexture("_MetallicGlossMap", generated);
                material.EnableKeyword("_METALLICGLOSSMAP");
                Debug.Log("[AutoMode] roughness のみから metallicsmoothness を生成");
                return;
            }

            if (data.Smoothness != null)
            {
                ApplyMetallicAndSmoothness(material, data);
                Debug.Log("[AutoMode] smoothness のみから metallicsmoothness を生成");
                return;
            }

            Debug.LogWarning("[AutoMode] Metallic関連テクスチャが見つかりませんでした。");
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


        private void ApplyAsMetallicSmoothness(Material material, Texture2D tex)
        {
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
                return;

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

            Texture2D mrTex = TextureConverter.CreateMetallicRoughnessMap(metallicPath, roughPath);

            if (mrTex != null)
            {
                material.SetTexture("_MetallicGlossMap", mrTex);
                material.EnableKeyword("_METALLICGLOSSMAP");
            }
        }
    }
}
