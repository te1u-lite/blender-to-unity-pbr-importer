using System.IO;
using UnityEngine;
using UnityEditor;

namespace BlenderToUnityPBRImporter.Editor
{
    /// <summary>
    /// Metallic / Roughness / Smoothness などの PBR テクスチャ生成を行うユーティリティクラス。
    /// 主に Standard シェーダー向けの Metal-Roughness 変換に使用する。
    /// </summary>
    public static class TextureConverter
    {
        /// <summary>
        /// Roughness テクスチャから Smoothness (1 - R) を生成し、PNG として保存する。
        /// </summary>
        /// <param name="roughTexPath">Roughness テクスチャのパス（絶対 or Assets/）</param>
        /// <returns>生成された Texture2D（失敗時は null）</returns>
        public static Texture2D CreateSmoothnessTexture(string roughTexPath)
        {
            string assetPath = ToAssetPath(roughTexPath);
            var roughTex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);

            if (roughTex == null)
            {
                Debug.LogError($"[ERROR][TextureConverter] テクスチャを読み込めませんでした: {assetPath}");
                return null;
            }

            var pixels = roughTex.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                float s = 1f - pixels[i].r;
                pixels[i] = new Color(0f, 0f, 0f, s);
            }

            var output = new Texture2D(roughTex.width, roughTex.height, TextureFormat.RGBA32, false);
            output.SetPixels(pixels);
            output.Apply();

            string savePath = GenerateSmoothnessPath(assetPath);
            File.WriteAllBytes(savePath, output.EncodeToPNG());
            AssetDatabase.ImportAsset(savePath);

            Debug.Log($"[INFO][TextureConverter] Smoothness Texture を生成しました: {savePath}");
            return AssetDatabase.LoadAssetAtPath<Texture2D>(savePath);
        }

        /// <summary>
        /// Metallic (R) と Roughness (R) → Standard シェーダーで使用できる
        /// MetallicGlossMap (RGB:Metallic, A:Smoothness) を生成する。
        /// </summary>
        /// <param name="metallicPath">Metallic テクスチャパス</param>
        /// <param name="roughnessPath">Roughness テクスチャパス</param>
        public static Texture2D CreateMetallicRoughnessMap(string metallicPath, string roughnessPath)
        {
            var metallic = !string.IsNullOrEmpty(metallicPath) ? LoadReadable(ToAssetPath(metallicPath)) : null;
            var rough = !string.IsNullOrEmpty(roughnessPath) ? LoadReadable(ToAssetPath(roughnessPath)) : null;

            if (metallic == null && rough == null)
            {
                Debug.LogWarning("[WARN][TextureConverter] Metallic / Roughness のどちらも入力されていません。");
                return null;
            }

            int w = metallic ? metallic.width : rough.width;
            int h = metallic ? metallic.height : rough.height;

            var result = new Color[w * h];
            var mPix = metallic ? metallic.GetPixels() : null;
            var rPix = rough ? rough.GetPixels() : null;

            for (int i = 0; i < result.Length; i++)
            {
                float m = mPix != null ? mPix[i].r : 0f;
                float r = rPix != null ? rPix[i].r : 1f;
                result[i] = new Color(m, m, m, 1f - r);
            }

            var output = new Texture2D(w, h, TextureFormat.RGBA32, false);
            output.SetPixels(result);
            output.Apply();

            // 保存
            string dir = Path.GetDirectoryName(ToAssetPath(metallicPath ?? roughnessPath));
            string savePath = Path.Combine(dir, "metallicsmoothness.png").Replace('\\', '/');
            File.WriteAllBytes(savePath, output.EncodeToPNG());
            AssetDatabase.ImportAsset(savePath);

            Debug.Log($"[INFO][TextureConverter] MetallicRoughnessMap を生成しました: {savePath}");
            return AssetDatabase.LoadAssetAtPath<Texture2D>(savePath);
        }

        /// <summary>
        /// 読み込み可能 (Readable) な Texture を返す。
        /// 必要であれば一時的に isReadable = true にして再インポートする。
        /// </summary>
        private static Texture2D LoadReadable(string assetPath)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex == null)
            {
                Debug.LogWarning($"[WARN][TextureConverter] 読み込み対象テクスチャが見つかりません: {assetPath}");
                return null;
            }


            var imp = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            if (!tex.isReadable)
            {
                imp.isReadable = true;
                imp.SaveAndReimport();

                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                Debug.Log($"[INFO][TextureConverter] 読み込み可能に変更しました: {assetPath}");
            }
            return tex;
        }

        /// <summary>
        /// Roughness の保存先パス（*_smooth.png）を生成する。
        /// </summary>
        private static string GenerateSmoothnessPath(string roughAssetPath)
        {
            string dir = Path.GetDirectoryName(roughAssetPath);
            string file = Path.GetFileNameWithoutExtension(roughAssetPath);
            return Path.Combine(dir, file + "_smooth.png").Replace('\\', '/');
        }

        /// <summary>
        /// フルパスを Assets/ から始まる Asset パスに変換する。
        /// </summary>
        private static string ToAssetPath(string path)
        {
            if (path.StartsWith("Assets/"))
                return path;

            string full = path.Replace("\\", "/");
            int idx = full.IndexOf("Assets/");

            return idx >= 0 ? full.Substring(idx) : path;
        }
    }
}