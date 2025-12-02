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

        private int albedoIndex = 0;
        private int normalIndex = 0;
        private int metallicIndex = 0;
        private int roughnessIndex = 0;
        private int smoothnessIndex = 0;

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
            albedoIndex = assignmentData.Albedo ? assignmentData.AllTextures.IndexOf(assignmentData.Albedo) + 1 : 0;
            normalIndex = assignmentData.Normal ? assignmentData.AllTextures.IndexOf(assignmentData.Normal) + 1 : 0;
            metallicIndex = assignmentData.Metallic ? assignmentData.AllTextures.IndexOf(assignmentData.Metallic) + 1 : 0;
            roughnessIndex = assignmentData.Roughness ? assignmentData.AllTextures.IndexOf(assignmentData.Roughness) + 1 : 0;


            Debug.Log("[INFO][TextureAssignmentWindow] テクスチャ検索が完了しました。");
        }

        /// <summary>
        /// テクスチャ選択 UI の描画。
        /// </summary>
        private void DrawTextureSelectionUI()
        {
            var texList = new string[] { "None" }.Concat(
                assignmentData.AllTextures.Select(t => t ? t.name : "(null)")
                ).ToArray();

            var names = assignmentData.AllTextures.Select(t => t ? t.name : "(null)").ToArray();

            EditorGUILayout.LabelField("手動割り当て", EditorStyles.boldLabel);

            mrMode = (MRMode)EditorGUILayout.EnumPopup("Metallic/Roughness Mode", mrMode);

            if (mrMode == MRMode.Auto)
            {
                EditorGUILayout.HelpBox("AutoMode: 自動判定で MetallicSmoothness を適用します。", MessageType.Info);
                // Albedo / Normal だけ表示
                albedoIndex = EditorGUILayout.Popup("Albedo", albedoIndex, texList);
                assignmentData.Albedo = GetSelectedWithNone(albedoIndex);

                normalIndex = EditorGUILayout.Popup("Normal", normalIndex, texList);
                assignmentData.Normal = GetSelectedWithNone(normalIndex);

                return;
            }

            GUILayout.Space(6);

            // ● Albedo 共通
            albedoIndex = EditorGUILayout.Popup("Albedo", albedoIndex, texList);
            assignmentData.Albedo = GetSelectedWithNone(albedoIndex);

            // ● Normal 共通
            normalIndex = EditorGUILayout.Popup("Normal", normalIndex, texList);
            assignmentData.Normal = GetSelectedWithNone(normalIndex);


            GUILayout.Space(6);

            // ▼ モード別の切り替え
            switch (mrMode)
            {
                case MRMode.MetallicSmoothness:
                    EditorGUILayout.LabelField("MetallicSmoothness モード", EditorStyles.boldLabel);

                    metallicIndex = EditorGUILayout.Popup("Metallic", metallicIndex, texList);
                    assignmentData.Metallic = GetSelectedWithNone(metallicIndex);

                    break;

                case MRMode.MetallicAndSmoothness:
                    EditorGUILayout.LabelField("Metallic + Smoothness モード", EditorStyles.boldLabel);

                    metallicIndex = EditorGUILayout.Popup("Metallic", metallicIndex, texList);
                    assignmentData.Metallic = GetSelectedWithNone(metallicIndex);

                    smoothnessIndex = EditorGUILayout.Popup("Smoothness", smoothnessIndex, texList);
                    assignmentData.Smoothness = GetSelectedWithNone(smoothnessIndex);
                    break;

                case MRMode.MetallicAndRoughness:
                    EditorGUILayout.LabelField("Metallic + Roughness モード", EditorStyles.boldLabel);

                    metallicIndex = EditorGUILayout.Popup("Metallic", metallicIndex, texList);
                    assignmentData.Metallic = GetSelectedWithNone(metallicIndex);

                    roughnessIndex = EditorGUILayout.Popup("Roughness", roughnessIndex, texList);
                    assignmentData.Roughness = GetSelectedWithNone(roughnessIndex);

                    break;
            }
        }

        Texture2D GetSelectedWithNone(int index)
        {
            if (index <= 0) return null;
            int texIndex = index - 1;

            return (texIndex >= 0 && texIndex < assignmentData.AllTextures.Count)
                ? assignmentData.AllTextures[texIndex]
                : null;
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
            assigner.ApplyToMaterial(mat, assignmentData, searchResult, mrMode);

            RemapMaterialToFbx(fbxObject, mat);

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

        private void RemapMaterialToFbx(GameObject fbx, Material externalMaterial)
        {
            string fbxPath = AssetDatabase.GetAssetPath(fbx);

            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("[ERROR] ModelImporter が取得できません: " + fbxPath);
                return;
            }

            // FBX 内部マテリアルを探索
            var internalAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);

            foreach (var internalObj in internalAssets)
            {
                if (internalObj is not Material internalMat)
                    continue;

                // 内部マテリアル名で Remap
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), internalMat.name);

                importer.AddRemap(id, externalMaterial);
            }

            AssetDatabase.WriteImportSettingsIfDirty(fbxPath);
            AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceUpdate);

            Debug.Log("[INFO] FBX のマテリアルを新規マテリアルへ Remap しました。");
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