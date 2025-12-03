using UnityEngine;

namespace BlenderToUnityPBRImporter.Editor
{
    /// <summary>
    /// マテリアル生成処理の基底クラス。
    /// 具体的なシェーダー選択や追加処理は派生クラスで実装する。
    /// </summary>
    public abstract class MaterialBuilder
    {
        /// <summary>
        /// 指定パスに新規マテリアルを生成する。
        /// 派生クラスが構築するシェーダーを使用する。
        /// </summary>
        /// <param name="materialPath">保存先フォルダ（Assets 内パス）</param>
        /// <param name="baseName">マテリアル名のベース</param>
        public abstract Material CreateMaterial(string materialPath, string baseName);

        /// <summary>
        /// 利用するシェーダーを返す。
        /// 派生クラス側で具体的なシェーダーを指定する。
        /// </summary>
        protected abstract Shader GetShader();

        /// <summary>
        /// マテリアル名を生成するヘルパー。
        /// 派生クラスで override することで命名規則を変更可能。
        /// </summary>
        /// <param name="baseName">ベース名</param>
        public virtual string BuildMaterialName(string baseName)
            => string.IsNullOrEmpty(baseName) ? "Unnamed_mat" : $"{baseName}_mat";
    }
}