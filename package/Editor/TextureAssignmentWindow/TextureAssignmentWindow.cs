using UnityEditor;
using UnityEngine;
using System.Linq;
using System.IO;

namespace BlenderToUnityPBRImporter.Editor
{

    /// <summary>
    /// FBX に含まれるテクスチャを検索し、
    /// ユーザーが手動で割り当て確認できる専用 EditorWindow。
    /// </summary>
    public class TextureAssignmentWindow : EditorWindow
    {
        private GameObject fbxObject;
        private TextureFinder.TextureSearchResult searchResult;
        private TextureAssigner.TextureAssignmentData assignmentData;

        public enum MRMode
        {
            Auto,
            MetallicSmoothness,
            MetallicAndSmoothness,
            MetallicAndRoughness
        }
        public MRMode mrMode = MRMode.MetallicAndRoughness;

        private int albedoIndex = -1;
        private int normalIndex = -1;
        private int metallicIndex = -1;
        private int roughnessIndex = -1;

        [MenuItem("tools/Texture Assignment")]
        public static void ShowWindow() => GetWindow<TextureAssignmentWindow>("Texture Assignment");


        private void OnGUI()
        {
            fbxObject = (GameObject)EditorGUILayout.ObjectField(
                "FBX Object",
                fbxObject,
                typeof(GameObject),
                false
            );

            if (GUILayout.Button("テクスチャ検索"))
                DoSearch();

            GUILayout.Space(10);

            if (assignmentData != null)
            {
                DrawTextureSelectionUI();
                GUILayout.Space(10);

                if (GUILayout.Button("選択結果をマテリアルに適用"))
                    ApplyAssignments();
            }
        }

        /// <summary>
        /// FBX の FBM フォルダを検索し、テクスチャ候補を取得する。
        /// </summary>
        private void DoSearch()
        {
            if (!fbxObject)
            {
                Debug.LogError("[ERROR][TextureAssignmentWindow] FBX Object が設定されていません。");
                return;
            }

            string fbmFolder = GetFBMFolder(fbxObject);
            if (fbmFolder == null)
            {
                Debug.LogError("[ERROR][TextureAssignmentWindow] FBM フォルダが見つかりません。");
                return;
            }

            searchResult = TextureFinder.FindTextures(fbmFolder);
            assignmentData = new TextureAssigner().PrepareAssignment(searchResult);

            // UI 初期選択値
            albedoIndex = assignmentData.AllTextures.IndexOf(assignmentData.Albedo);
            normalIndex = assignmentData.AllTextures.IndexOf(assignmentData.Normal);
            metallicIndex = assignmentData.AllTextures.IndexOf(assignmentData.Metallic);
            roughnessIndex = assignmentData.AllTextures.IndexOf(assignmentData.Roughness);

            Debug.Log("[INFO][TextureAssignmentWindow] テクスチャ検索が完了しました。");
        }

        /// <summary>
        /// テクスチャ選択 UI の描画。
        /// </summary>
        private void DrawTextureSelectionUI()
        {
            var names = assignmentData.AllTextures.Select(t => t ? t.name : "(null)").ToArray();

            EditorGUILayout.LabelField("手動割り当て", EditorStyles.boldLabel);

            mrMode = (MRMode)EditorGUILayout.EnumPopup("Metallic/Roughness Mode", mrMode);

            if (mrMode == MRMode.Auto)
            {
                EditorGUILayout.HelpBox("AutoMode: 自動判定で MetallicSmoothness を適用します。", MessageType.Info);
                // Albedo / Normal だけ表示
                albedoIndex = EditorGUILayout.Popup("Albedo", albedoIndex, names);
                assignmentData.Albedo = GetSelected(albedoIndex);

                normalIndex = EditorGUILayout.Popup("Normal", normalIndex, names);
                assignmentData.Normal = GetSelected(normalIndex);
                return;
            }

            GUILayout.Space(6);

            // ● Albedo 共通
            albedoIndex = EditorGUILayout.Popup("Albedo", albedoIndex, names);
            assignmentData.Albedo = GetSelected(albedoIndex);

            // ● Normal 共通
            normalIndex = EditorGUILayout.Popup("Normal", normalIndex, names);
            assignmentData.Normal = GetSelected(normalIndex);

            GUILayout.Space(6);

            // ▼ モード別の切り替え
            switch (mrMode)
            {
                case MRMode.MetallicSmoothness:
                    EditorGUILayout.LabelField("MetallicSmoothness モード", EditorStyles.boldLabel);

                    metallicIndex = EditorGUILayout.Popup("MetallicSmoothness", metallicIndex, names);
                    assignmentData.Metallic = GetSelected(metallicIndex); // Metallic に統一して格納
                    break;

                case MRMode.MetallicAndSmoothness:
                    EditorGUILayout.LabelField("Metallic + Smoothness モード", EditorStyles.boldLabel);

                    metallicIndex = EditorGUILayout.Popup("Metallic", metallicIndex, names);
                    assignmentData.Metallic = GetSelected(metallicIndex);

                    // Smoothness 用の index が必要なので追加
                    int smoothIndex = assignmentData.AllTextures.IndexOf(assignmentData.Smoothness);
                    smoothIndex = EditorGUILayout.Popup("Smoothness", smoothIndex, names);
                    assignmentData.Smoothness = GetSelected(smoothIndex);

                    break;

                case MRMode.MetallicAndRoughness:
                    EditorGUILayout.LabelField("Metallic + Roughness モード", EditorStyles.boldLabel);

                    metallicIndex = EditorGUILayout.Popup("Metallic", metallicIndex, names);
                    assignmentData.Metallic = GetSelected(metallicIndex);

                    roughnessIndex = EditorGUILayout.Popup("Roughness", roughnessIndex, names);
                    assignmentData.Roughness = GetSelected(roughnessIndex);

                    break;
            }
        }

        /// <summary>
        /// ドロップダウンで選択されたテクスチャを返す。
        /// </summary>
        private Texture2D GetSelected(int index)
            => (index >= 0 && index < assignmentData.AllTextures.Count) ? assignmentData.AllTextures[index] : null;

        /// <summary>
        /// Material を生成し、選択したテクスチャを適用する。
        /// </summary>
        private void ApplyAssignments()
        {
            var mat = CreateMaterialForFBX(fbxObject);
            if (!mat)
            {
                Debug.LogError("[ERROR][TextureAssignmentWindow] Material の生成に失敗しました。");
                return;
            }
            var assigner = new TextureAssigner();
            assigner.ApplyToMaterial(mat, assignmentData, mrMode);

            Debug.Log("[INFO][TextureAssignmentWindow] Material へテクスチャ割り当てが完了しました。");
        }

        /// <summary>
        /// FBX と同階層に Materials フォルダを作成し、マテリアルを生成する。
        /// </summary>
        private Material CreateMaterialForFBX(GameObject fbx)
        {
            string fbxPath = AssetDatabase.GetAssetPath(fbx);
            string dir = Path.GetDirectoryName(fbxPath);
            string name = Path.GetFileNameWithoutExtension(fbxPath);

            // マテリアル保存フォルダ
            string matFolder = dir + "/Materials";
            if (!AssetDatabase.IsValidFolder(matFolder))
            {
                AssetDatabase.CreateFolder(dir, "Materials");
                Debug.Log($"[INFO][TextureAssignmentWindow] Materials フォルダを作成しました: {matFolder}");
            }

            return new MaterialBuilderStandard().CreateMaterial(matFolder, name);
        }


        /// <summary>
        /// FBX に対応する FBM フォルダのフルパスを返す。
        /// </summary>
        private string GetFBMFolder(GameObject fbx)
        {
            string fbxPath = AssetDatabase.GetAssetPath(fbx);
            string dir = Path.GetDirectoryName(fbxPath);
            string name = Path.GetFileNameWithoutExtension(fbxPath);
            string fbm = $"{dir}/{name}.fbm";

            return Directory.Exists(fbm) ? fbm : null; ;
        }
    }
}