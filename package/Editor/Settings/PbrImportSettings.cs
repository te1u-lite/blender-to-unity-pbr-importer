using UnityEngine;

namespace BlenderToUnityPBRImporter.Editor
{
    public enum ShaderPipeline
    {
        Standard,
        URP,
        HDRP
    }

    /// <summary>
    /// 全自動インポートの設定を保存する ScriptableObject
    /// </summary>
    public class PbrImportSettings : ScriptableObject
    {
        public bool autoImportEnabled = true;

        public ShaderPipeline shaderPipeline = ShaderPipeline.Standard;

        public bool generateMetallicRoughness = true;

        // 命名規則
        public string[] albedoKeywords = new[] { "albedo", "basecolor", "diff", "color" };
        public string[] normalKeywords = new[] { "normal", "nrm", "_nor" };
        public string[] metallicKeywords = new[] { "metal", "metallic" };
        public string[] roughnessKeywords = new[] { "rough", "roughness", "_rgh" };

        // 優先度
        public int albedoPriority = 0;
        public int normalPriority = 0;
        public int metallicPriority = 0;
        public int roughnessPriority = 0;

        public static PbrImportSettings GetOrCreateSettings()
        {
            const string path = "Assets/Editor/PbrImporterSettings.asset";
            var settings = UnityEditor.AssetDatabase.LoadAssetAtPath<PbrImportSettings>(path);

            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PbrImportSettings>();
                UnityEditor.AssetDatabase.CreateAsset(settings, path);
                UnityEditor.AssetDatabase.SaveAssets();
            }

            return settings;
        }
    }
}