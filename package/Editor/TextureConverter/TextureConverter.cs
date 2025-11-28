using System.IO;
using UnityEngine;
using UnityEditor;

namespace BlenderToUnityPBRImporter.Editor
{
    public static class TextureConverter
    {
        /// <summary>
        /// Roughness テクスチャを Smoothness テクスチャに変換 (1 - roughness) して新規 PNG を生成する
        /// </summary>
        /// <param name="roughTexPath">Roughness テクスチャの絶対 or Assets パス</param>
        /// <returns>生成された Smoothness Texture2D（失敗時は null）</returns>
        public static Texture2D CreateSmoothnessTexture(string roughTexPath)
        {
            string assetPath = ToAssetPath(roughTexPath);
            var roughTex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);

            if (roughTex == null)
            {
                Debug.LogError($"[TextureConverter] テクスチャを読み込めませんでした: {assetPath}");
                return null;
            }

            // 1. Readable でなければ設定して再インポート
            if (!roughTex.isReadable)
            {
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                importer.isReadable = true;
                importer.SaveAndReimport();

                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = false;
                importer.sRGBTexture = false;

                // Reimport 後にもう一度ロードし直す
                roughTex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);

                if (!roughTex.isReadable)
                {
                    Debug.LogError("[Converter] Readable 化に失敗しました");
                    return null;
                }
            }

            // ピクセル反転
            Texture2D readableTex = GetReadableCopy(roughTex);
            if (readableTex == null)
            {
                Debug.LogError("[TextureConverter] テクスチャコピーに失敗しました");
                return null;
            }

            Color[] pixels = readableTex.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                float r = pixels[i].r;   // Roughness テクスチャの red
                float s = 1f - r;        // Smoothness = 1 - Roughness

                pixels[i] = new Color(0f, 0f, 0f, s);
            }

            Texture2D smoothTex = new Texture2D(readableTex.width, readableTex.height, TextureFormat.RGBA32, false);
            smoothTex.SetPixels(pixels);
            smoothTex.Apply();

            // 保存先パス指定
            string newPath = GenerateSmoothnessPath(assetPath);

            // PNG 保存
            File.WriteAllBytes(newPath, smoothTex.EncodeToPNG());
            AssetDatabase.ImportAsset(newPath);

            // Smoothness テクスチャ取得
            var imported = AssetDatabase.LoadAssetAtPath<Texture2D>(newPath);
            Debug.Log($"[TextureConverter] Smoothness テクスチャ生成: {newPath}");
            return imported;
        }

        public static Texture2D CreateMetallicRoughnessMap(string metallicPath, string roughnessPath)
        {
            Texture2D metallicTex = null;
            Texture2D roughTex = null;

            if (!string.IsNullOrEmpty(metallicPath))
                metallicTex = LoadReadableTexture(metallicPath);

            if (!string.IsNullOrEmpty(roughnessPath))
                roughTex = LoadReadableTexture(roughnessPath);

            if (metallicTex == null && roughTex == null)
            {
                Debug.LogWarning("[TextureConverter] Metallic も Roughness も存在しないため生成をスキップします。");
                return null;
            }

            int width = metallicTex != null ? metallicTex.width : roughTex.width;
            int height = metallicTex != null ? metallicTex.height : roughTex.height;

            var output = new Texture2D(width, height, TextureFormat.RGBA32, false);

            Color[] metallicPixels = metallicTex != null ? metallicTex.GetPixels() : null;
            Color[] roughPixels = roughTex != null ? roughTex.GetPixels() : null;

            Color[] result = new Color[width * height];

            for (int i=0; i<result.Length; i++)
            {
                float m = metallicPixels != null ? metallicPixels[i].r : 0f;
                float r = roughPixels != null ? roughPixels[i].r : 1f;// roughness がなければ 1 とみなす
                float s = 1f - r; // Smoothness

                result[i] = new Color(m, m, m, s);
            }

            output.SetPixels(result);
            output.Apply();

            // 保存
            string dir = Path.GetDirectoryName(ToAssetPath(metallicPath ?? roughnessPath));
            string file = "metallicroughness.png";
            string savePath = Path.Combine(dir, file).Replace('\\', '/');

            File.WriteAllBytes(savePath, output.EncodeToPNG());
            AssetDatabase.ImportAsset(savePath);

            // Importer 設定
            var importer = AssetImporter.GetAtPath(savePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = false;
                importer.SaveAndReimport();
            }

            Debug.Log("[TextureConverter] MetallicRoughnessMap を生成しました: " + savePath);

            return AssetDatabase.LoadAssetAtPath<Texture2D>(savePath);
        }

        private static Texture2D LoadReadableTexture(string path)
        {
            string assetPath = ToAssetPath(path);

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex == null)
                return null;

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if(!tex.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();

                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            }

            return tex;
        }

        // -----------------------------------------------------
        //  Helpers
        // -----------------------------------------------------
        private static bool EnsureReadable(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                return false;

            if (!importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            return true;
        }

        private static Texture2D GetReadableCopy(Texture2D src)
        {
            RenderTexture rt = RenderTexture.GetTemporary(
                src.width, src.height, 0,
                RenderTextureFormat.Default, RenderTextureReadWrite.Linear);

            Graphics.Blit(src, rt);

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;

            Texture2D tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            tex.Apply();

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            return tex;
        }

        private static string GenerateSmoothnessPath(string roughAssetPath)
        {
            string dir = Path.GetDirectoryName(roughAssetPath);
            string file = Path.GetFileNameWithoutExtension(roughAssetPath);

            string newFile = file + "_smooth.png";
            string newPath = Path.Combine(dir, newFile).Replace('\\', '/');

            return newPath;
        }

        private static string ToAssetPath(string path)
        {
            if (path.StartsWith("Assets/"))
                return path;

            string full = path.Replace("\\", "/");
            int idx = full.IndexOf("Assets/");
            if (idx >= 0)
                return full.Substring(idx);

            Debug.LogWarning($"[TextureConverter] Assetsパスへの変換が必要です: {path}");
            return path;
        }
    }
}