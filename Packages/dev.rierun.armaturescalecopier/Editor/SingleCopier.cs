using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace ShimotukiRieru.ArmatureScaleCopier
{
    /// <summary>
    /// Armatureオブジェクトの子にあるコンポーネントとTransform情報をコピー・ペーストするエディターツール
    /// </summary>
    public class SingleCopier : EditorWindow
    {
        private GameObject sourceArmature;
        private GameObject[] targetArmatures = new GameObject[0];
        private ArmatureData copiedData;
        private Vector2 scrollPosition;
        private Vector2 copiedDataScrollPosition;
        private bool isOverrideExistingComponents = true; // 既存コンポーネントの値を上書きするか
        private bool copyTransforms = true;
        private bool copyTransformsScale = true;
        private bool copyTransformsPosition = false;
        private bool copyTransformsRotation = false;
        private bool showTargetList = true;
        private bool copyTransformsFoldout = true;
        private List<ComponentInfo> componentList = new List<ComponentInfo>(); // コピー元に存在するコンポーネントタイプのリスト
        private List<string> copyComponentList = new List<string>(); // コピー対象のコンポーネントのネームスペースのリスト
        private List<bool> isFoldoutList = new List<bool>(); // 各カテゴリーの折りたたみ状態

        [MenuItem("Tools/ArmatureScaleCopier/Single Copier")]
        public static void ShowWindow()
        {
            GetWindow<SingleCopier>("Single Copier");
        }

        private void OnEnable()
        {
            FindComponentsInSourceArmature();
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            // ソースArmature選択
            GUILayout.Label("コピー元 (Source Armature):", EditorStyles.boldLabel);
            var newSourceArmature = (GameObject)EditorGUILayout.ObjectField(sourceArmature, typeof(GameObject), true);

            if (newSourceArmature != sourceArmature)
            {
                sourceArmature = newSourceArmature;
                FindComponentsInSourceArmature();
            }

            if (sourceArmature != null && !IsValidArmature(sourceArmature))
            {
                EditorGUILayout.HelpBox("選択されたオブジェクトは有効なArmatureではありません。", MessageType.Warning);
            }

            GUILayout.Space(10);

            // 設定
            GUILayout.Label("設定", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            isOverrideExistingComponents = EditorGUILayout.ToggleLeft("既存コンポーネントの値を上書き", isOverrideExistingComponents);
            EditorGUI.indentLevel--;

            GUILayout.Space(10);

            // ターゲットArmature選択
            GUILayout.Label("ペースト先 (Target Armatures):", EditorStyles.boldLabel);

            showTargetList = EditorGUILayout.Foldout(showTargetList, $"ターゲットリスト ({targetArmatures.Length} 個)");

            if (showTargetList)
            {
                EditorGUI.indentLevel++;


                // リストのサイズを調整するボタン
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("+", GUILayout.Width(30)))
                {
                    System.Array.Resize(ref targetArmatures, targetArmatures.Length + 1);
                }
                if (GUILayout.Button("-", GUILayout.Width(30)) && targetArmatures.Length > 0)
                {
                    System.Array.Resize(ref targetArmatures, targetArmatures.Length - 1);
                }
                EditorGUILayout.EndHorizontal();

                // 各ターゲットArmatureの設定
                for (int i = 0; i < targetArmatures.Length; i++)
                {
                    if (i >= targetArmatures.Length) break; // 安全性チェック

                    EditorGUILayout.BeginHorizontal();
                    targetArmatures[i] = (GameObject)EditorGUILayout.ObjectField($"Target {i + 1}", targetArmatures[i], typeof(GameObject), true);

                    if (GUILayout.Button("x", GUILayout.Width(20)))
                    {
                        // この要素を削除
                        var newArray = new GameObject[targetArmatures.Length - 1];
                        for (int j = 0, k = 0; j < targetArmatures.Length; j++)
                        {
                            if (j != i) newArray[k++] = targetArmatures[j];
                        }
                        targetArmatures = newArray;
                        break;
                    }
                    EditorGUILayout.EndHorizontal();

                    if (i < targetArmatures.Length && targetArmatures[i] != null && !IsValidArmature(targetArmatures[i]))
                    {
                        EditorGUILayout.HelpBox($"Target {i + 1}: 選択されたオブジェクトは有効なArmatureではありません。", MessageType.Warning);
                    }
                }

                EditorGUI.indentLevel--;
            }

            GUILayout.Space(10);

            // コピーするコンポーネントの設定
            GUILayout.Label("コピーするコンポーネント", EditorStyles.boldLabel);

            copyTransformsFoldout = CopierHelper.Foldout("Transform", copyTransformsFoldout);
            if (copyTransformsFoldout)
            {
                copyTransforms = EditorGUILayout.ToggleLeft("Transform情報をコピー", copyTransforms);

                if (copyTransforms)
                {
                    EditorGUI.indentLevel++;
                    copyTransformsScale = EditorGUILayout.ToggleLeft("スケールをコピー", copyTransformsScale);
                    copyTransformsPosition = EditorGUILayout.ToggleLeft("位置をコピー", copyTransformsPosition);
                    copyTransformsRotation = EditorGUILayout.ToggleLeft("回転をコピー", copyTransformsRotation);
                    EditorGUI.indentLevel--;
                }
            }

            GUILayout.Space(10);

            // カテゴリー別にコンポーネントをグループ化
            var categorizedComponents = componentList.GroupBy(info => info.Category)
                .OrderBy(group => group.Key)
                .ToList();

            // isFoldoutListのサイズを調整
            while (isFoldoutList.Count < categorizedComponents.Count)
            {
                isFoldoutList.Add(true);
            }

            for (int index = 0; index < categorizedComponents.Count; index++)
            {
                var category = categorizedComponents[index];
                // カテゴリーヘッダー
                bool isFoldout = isFoldoutList[index];
                GUILayout.Space(5);
                isFoldout = CopierHelper.Foldout(CopierHelper.GetComponentCategoryName(category.Key), isFoldout);
                isFoldoutList[index] = isFoldout; // 更新
                if (isFoldout)
                {
                    // カテゴリー内のコンポーネント
                    foreach (var info in category.OrderBy(c => c.ComponentName))
                    {
                        bool isSelected = copyComponentList.Contains(info.ComponentNameSpace + "." + info.ComponentName);
                        // コンポーネントのアイコンを表示
                        Texture icon = info.ComponentIcon;
                        if (icon == null)
                        {
                            // アイコンがない場合はデフォルトのアイコンを使用
                            icon = EditorGUIUtility.IconContent("d_UnityEditor.SceneView").image;
                        }

                        bool newSelected = EditorGUILayout.ToggleLeft(
                            new GUIContent(info.ComponentDisplayName, icon),
                             isSelected
                        );

                        if (newSelected != isSelected)
                        {
                            if (newSelected)
                            {
                                copyComponentList.Add(info.ComponentNameSpace + "." + info.ComponentName);
                            }
                            else
                            {
                                copyComponentList.Remove(info.ComponentNameSpace + "." + info.ComponentName);
                            }
                        }
                    }
                }
            }

            GUILayout.Space(15);

            // コピーボタン
            GUI.enabled = sourceArmature != null && IsValidArmature(sourceArmature);
            if (GUILayout.Button("Copy Armature Data", GUILayout.Height(30)))
            {
                CopyArmatureData();
            }
            GUI.enabled = true;

            GUILayout.Space(10);

            // ペーストボタン
            bool hasValidTargets = targetArmatures.Any(t => t != null && IsValidArmature(t));
            GUI.enabled = hasValidTargets && copiedData != null;
            if (GUILayout.Button("Paste Armature Data", GUILayout.Height(30)))
            {
                PasteArmatureData();
            }
            GUI.enabled = true;

            GUILayout.Space(15);

            // コピー済みデータの情報表示
            if (copiedData != null)
            {


                GUILayout.Label("コピー済みデータ:", EditorStyles.boldLabel);
                int totalObjects = CountTotalObjects(copiedData.childrenData);
                EditorGUILayout.HelpBox($"総オブジェクト数: {totalObjects}", MessageType.Info);


                copiedDataScrollPosition = EditorGUILayout.BeginScrollView(copiedDataScrollPosition);

                DisplayChildrenDataRecursive(copiedData.childrenData, 0);

                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// オブジェクトがArmatureかどうかを判定
        /// </summary>
        private bool IsValidArmature(GameObject obj)
        {
            return ValidationHelper.IsValidArmature(obj);
        }

        /// <summary>
        /// ソースArmatureに存在するコンポーネントのリストを検索
        /// </summary>
        private void FindComponentsInSourceArmature()
        {
            componentList.Clear();
            // カテゴリー数が変わった場合のためにisFoldoutListもリセット
            isFoldoutList.Clear();

            if (sourceArmature == null) return;

            // ソースArmatureのすべてのコンポーネントを取得
            var components = sourceArmature.GetComponentsInChildren<Component>(true);
            foreach (var comp in components)
            {
                // コンポーネントが無効またはTransformの場合はスキップ
                if (comp == null || comp is Transform) continue;
                // リスト内に同じコンポーネントが存在しない場合のみ追加
                var componentInfo = new ComponentInfo(comp);
                if (!componentList.Any(c => c.ComponentNameSpace == componentInfo.ComponentNameSpace && c.ComponentName == componentInfo.ComponentName))
                {
                    componentList.Add(componentInfo);
                }
            }
        }

        /// <summary>
        /// Armatureの子オブジェクトのデータをコピー（再帰処理）
        /// </summary>
        private void CopyArmatureData()
        {
            if (sourceArmature == null)
            {
                EditorUtility.DisplayDialog("エラー", "コピー元のArmatureが設定されていません。", "OK");
                return;
            }

            try
            {
                copiedData = new ArmatureData();

                foreach (Transform child in sourceArmature.transform)
                {
                    var childData = CopyChildObjectDataRecursive(child);
                    copiedData.childrenData.Add(childData);
                }

                int totalObjectCount = CountTotalObjects(copiedData.childrenData);
                string message = $"Armature '{sourceArmature.name}' のデータをコピーしました。総オブジェクト数: {totalObjectCount}";
                ArmatureScaleCopierLogger.Log(message);
                EditorUtility.DisplayDialog("完了", message, "OK");
            }
            catch (System.Exception e)
            {
                ArmatureScaleCopierLogger.LogException(e, "コピー処理中");
                EditorUtility.DisplayDialog("エラー", $"コピー処理中にエラーが発生しました: {e.Message}", "OK");
            }
        }

        /// <summary>
        /// 子オブジェクトのデータを再帰的にコピー
        /// </summary>
        private ChildObjectData CopyChildObjectDataRecursive(Transform transform)
        {
            var childData = new ChildObjectData
            {
                name = transform.name,
                localPosition = transform.localPosition,
                localRotation = transform.localRotation,
                localScale = transform.localScale,
                componentData = new List<ComponentData>(),
                childrenData = new List<ChildObjectData>()
            };

            // コンポーネントデータの収集
            var components = transform.GetComponents<Component>();
            foreach (var component in components)
            {
                if (component == null || component is Transform) continue;

                bool shouldCopy = false;

                if (copyComponentList.Contains(component.GetType().Namespace + "." + component.GetType().Name))
                {
                    shouldCopy = true; // コピー対象のコンポーネント
                }

                if (shouldCopy)
                {
                    var compData = new ComponentData
                    {
                        typeName = component.GetType().AssemblyQualifiedName,
                        serializedData = ArmatureScaleCopierLogger.TryExecute(() => JsonUtility.ToJson(component), "{}", "コンポーネントのシリアライズ")
                    };
                    childData.componentData.Add(compData);
                }
            }

            // 子オブジェクトを再帰的に処理
            foreach (Transform child in transform)
            {
                var childObjectData = CopyChildObjectDataRecursive(child);
                childData.childrenData.Add(childObjectData);
            }

            return childData;
        }

        /// <summary>
        /// 総オブジェクト数をカウント（再帰）
        /// </summary>
        private int CountTotalObjects(List<ChildObjectData> childrenData)
        {
            if (childrenData == null) return 0;

            int count = childrenData.Count;
            foreach (var child in childrenData)
            {
                if (child?.childrenData != null)
                {
                    count += CountTotalObjects(child.childrenData);
                }
            }
            return count;
        }

        /// <summary>
        /// コピーしたデータをターゲットのArmatureにペースト（再帰処理）
        /// </summary>
        private void PasteArmatureData()
        {
            if (copiedData == null)
            {
                EditorUtility.DisplayDialog("エラー", "コピーされたデータがありません。まず「Copy Armature Data」を実行してください。", "OK");
                return;
            }

            var validTargets = targetArmatures.Where(t => t != null && IsValidArmature(t)).ToArray();
            if (validTargets.Length == 0)
            {
                EditorUtility.DisplayDialog("エラー", "有効なターゲットArmatureが見つかりません。", "OK");
                return;
            }

            // コピー対象のコンポーネントが選択されているかチェック
            if (copyComponentList.Count == 0 && !(copyTransforms && (copyTransformsScale || copyTransformsPosition || copyTransformsRotation)))
            {
                EditorUtility.DisplayDialog("警告", "コピーする項目が選択されていません。Transform情報またはコンポーネントを選択してください。", "OK");
                return;
            }

            // 確認ダイアログ
            string transformInfo = "";
            if (copyTransforms)
            {
                var transformParts = new List<string>();
                if (copyTransformsPosition) transformParts.Add("位置");
                if (copyTransformsRotation) transformParts.Add("回転");
                if (copyTransformsScale) transformParts.Add("スケール");
                transformInfo = transformParts.Count > 0 ? string.Join(", ", transformParts) : "なし";
            }
            else
            {
                transformInfo = "コピーしない";
            }

            bool proceed = EditorUtility.DisplayDialog(
                "確認",
                $"{validTargets.Length}個のArmatureにデータをペーストしますか？\n\n" +
                $"・Transform情報: {transformInfo}\n" +
                $"・コンポーネント数: {copyComponentList.Count}個\n" +
                $"・既存コンポーネントの上書き: {(isOverrideExistingComponents ? "はい" : "いいえ")}",
                "実行", "キャンセル");

            if (!proceed) return;

            try
            {
                int successCount = 0;
                foreach (var target in validTargets)
                {
                    try
                    {
                        Undo.RegisterCompleteObjectUndo(target, "Paste Armature Data");

                        foreach (var childData in copiedData.childrenData)
                        {
                            PasteChildObjectDataRecursive(childData, target.transform);
                        }

                        ArmatureScaleCopierLogger.Log($"Armature '{target.name}' にデータをペーストしました。");
                        successCount++;
                    }
                    catch (System.Exception ex)
                    {
                        ArmatureScaleCopierLogger.LogException(ex, $"'{target.name}'への適用中");
                    }
                }
            }
            catch (System.Exception e)
            {
                ArmatureScaleCopierLogger.LogException(e, "ペースト処理中");
                EditorUtility.DisplayDialog("エラー", $"ペースト処理中にエラーが発生しました: {e.Message}", "OK");
            }
        }

        /// <summary>
        /// 子オブジェクトのデータを再帰的にペースト
        /// </summary>
        private void PasteChildObjectDataRecursive(ChildObjectData childData, Transform parentTransform)
        {
            Transform existingChild = parentTransform.Find(childData.name);

            // 対応する名前のオブジェクトが見つからない場合はスキップ
            if (existingChild == null)
                return;

            GameObject targetChild = existingChild.gameObject;
            Undo.RegisterCompleteObjectUndo(targetChild, "Update Child Object");

            // Transform情報の適用
            if (copyTransforms)
            {
                if (copyTransformsScale)
                    targetChild.transform.localScale = childData.localScale;
                if (copyTransformsPosition)
                    targetChild.transform.localPosition = childData.localPosition;
                if (copyTransformsRotation)
                    targetChild.transform.localRotation = childData.localRotation;
            }

            // コンポーネントの適用
            foreach (var compData in childData.componentData)
            {
                try
                {
                    var componentType = System.Type.GetType(compData.typeName);
                    if (componentType != null)
                    {
                        var existingComponent = targetChild.GetComponent(componentType);
                        if (existingComponent != null)
                        {
                            if (!isOverrideExistingComponents)
                                continue; // 既存コンポーネントの値を上書きしない場合はスキップ
                            Undo.RegisterCompleteObjectUndo(existingComponent, "Update Component");
                            JsonUtility.FromJsonOverwrite(compData.serializedData, existingComponent);
                        }
                        else
                        {
                            var newComponent = targetChild.AddComponent(componentType);
                            Undo.RegisterCreatedObjectUndo(newComponent, "Add Component");
                            JsonUtility.FromJsonOverwrite(compData.serializedData, newComponent);
                        }
                    }
                    else
                    {
                        ArmatureScaleCopierLogger.LogWarning($"コンポーネント型 '{compData.typeName}' が見つかりませんでした。");
                    }
                }
                catch (System.Exception e)
                {
                    ArmatureScaleCopierLogger.LogException(e, $"コンポーネント {compData.typeName} の適用中");
                }
            }

            // 子オブジェクトを再帰的に処理
            if (childData.childrenData != null)
            {
                foreach (var grandChildData in childData.childrenData)
                {
                    if (grandChildData != null)
                    {
                        PasteChildObjectDataRecursive(grandChildData, targetChild.transform);
                    }
                }
            }
        }

        /// <summary>
        /// 子オブジェクトデータを再帰的に表示
        /// </summary>
        private void DisplayChildrenDataRecursive(List<ChildObjectData> childrenData, int indentLevel)
        {
            if (childrenData == null) return;

            string indent = new string(' ', indentLevel * 2);
            foreach (var childData in childrenData)
            {
                if (childData == null) continue;

                EditorGUILayout.LabelField($"{indent}• {childData.name} - {childData.componentData?.Count ?? 0} components");
                if (childData.childrenData != null && childData.childrenData.Count > 0)
                {
                    DisplayChildrenDataRecursive(childData.childrenData, indentLevel + 1);
                }
            }
        }

        /// <summary>
        /// ModularAvatarコンポーネントかどうかを判定
        /// </summary>
        private bool IsModularAvatarComponent(Component component)
        {
            return CopierHelper.IsModularAvatarComponent(component);
        }
    }

    /// <summary>
    /// コピーするArmatureのデータ
    /// </summary>
    [System.Serializable]
    public class ArmatureData
    {
        public List<ChildObjectData> childrenData = new List<ChildObjectData>();
    }

    /// <summary>
    /// 子オブジェクトのデータ
    /// </summary>
    [System.Serializable]
    public class ChildObjectData
    {
        public string name;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
        public List<ComponentData> componentData;
        public List<ChildObjectData> childrenData;
    }

    /// <summary>
    /// コンポーネントのデータ
    /// </summary>
    [System.Serializable]
    public class ComponentData
    {
        public string typeName;
        public string serializedData;
    }
}
