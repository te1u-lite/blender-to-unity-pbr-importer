using System.Data;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace BlenderToUnityPBRImporter.Editor
{
    public class PbrImportSettingsProvider : SettingsProvider
    {
        private SerializedObject so;
        private PbrImportSettings settings;

        public PbrImportSettingsProvider(string path, SettingsScope scope)
        : base(path, scope)
        {

        }

        public static bool IsSettingsAvailable()
        {
            return PbrImportSettings.GetOrCreateSettings() != null;
        }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            settings = PbrImportSettings.GetOrCreateSettings();
            so = new SerializedObject(settings);
        }

        public override void OnGUI(string searchContext)
        {
            so.Update();
            GUILayout.Label("FBX PBR Import Settings", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(so.FindProperty("autoImportEnabled"),
            new GUIContent("Auto Import Enabled"));

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Shader Pipeline", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("shaderPipeline"));

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Texture Naming Rules", EditorStyles.boldLabel);
            DrawKeywordList("Albedo", "albedoKeywords");
            DrawKeywordList("Normal", "normalKeywords");
            DrawKeywordList("Metallic", "metallicKeywords");
            DrawKeywordList("Roughness", "roughnessKeywords");

            so.ApplyModifiedProperties();
        }

        private void DrawKeywordList(string label, string propertyName)
        {
            var prop = so.FindProperty(propertyName);
            EditorGUILayout.PropertyField(prop, new GUIContent(label), true);
        }

        [SettingsProvider]
        public static SettingsProvider CreateProvider()
        {
            if (!IsSettingsAvailable()) return null;
            var provider = new PbrImportSettingsProvider(
                "Project/PBR FBX Importer",
                SettingsScope.Project);

            return provider;
        }

    }
}