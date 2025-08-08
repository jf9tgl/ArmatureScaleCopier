using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace ShimotukiRieru.ArmatureScaleCopier
{
    /// <summary>
    /// Armature関連の一括操作ユーティリティ
    /// </summary>
    public class AvatarBatchCopier : EditorWindow
    {
        private Vector2 scrollPosition;
        private List<GameObject> ArmatureObjects = new List<GameObject>();
        private GameObject sourceArmature;
        private GameObject previousSourceArmature; // 前回のソースArmature（変更検知用）
        private GameObject searchRoot; // 検索対象のルートオブジェクト
        private GameObject previousSearchRoot; // 前回の検索対象（変更検知用）
        private bool autoFindArmatures = true;
        private bool isOverrideExistingComponents = true; // 既存コンポーネントの値を上書きするか
        private bool includeInactive = false;
        private bool copyTransformsFoldout = true;
        private bool copyTransformsScale = true;
        private bool copyTransformsPosition = false;
        private bool copyTransformsRotation = false;
        private List<ComponentInfo> componentList = new List<ComponentInfo>(); // コピー元に存在するコンポーネントタイプのリスト
        private List<string> copyComponentList = new List<string>(); // コピー対象のコンポーネントのネームスペースのリスト
        private List<bool> isFoldoutList = new List<bool>(); // 各カテゴリーの折りたたみ状態

        [MenuItem("Tools/ArmatureScaleCopier/Avatar Batch Copier")]
        public static void ShowWindow()
        {
            GetWindow<AvatarBatchCopier>("Avatar Batch Copier");
        }

        private void OnEnable()
        {
            if (autoFindArmatures)
            {
                FindAllArmatures();
                FindComponentsInSourceArmature();
            }
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            // 検索するアバター
            GUILayout.Label("検索するアバター", EditorStyles.boldLabel);
            searchRoot = (GameObject)EditorGUILayout.ObjectField("検索対象オブジェクト", searchRoot, typeof(GameObject), true);

            // 検索対象が変更された場合の自動再検索
            if (autoFindArmatures && searchRoot != previousSearchRoot)
            {
                previousSearchRoot = searchRoot;

                // そのオブジェクト内にあるArmatureがあればsourceArmatureに代入
                if (searchRoot != null)
                {
                    var armatureTransform = searchRoot.transform.Find("Armature");
                    sourceArmature = armatureTransform?.gameObject;
                }
                else
                {
                    sourceArmature = null; // searchRootがnullの場合はsourceArmatureもnullにする
                }

                // Armatureオブジェクトを再検索
                FindAllArmatures();
                FindComponentsInSourceArmature();
            }

            GUILayout.Space(10);

            // 設定
            GUILayout.Label("設定", EditorStyles.boldLabel);
            autoFindArmatures = EditorGUILayout.ToggleLeft("自動的にArmatureを検索", autoFindArmatures);
            includeInactive = EditorGUILayout.ToggleLeft("非アクティブオブジェクトも含む", includeInactive);
            isOverrideExistingComponents = EditorGUILayout.ToggleLeft("既存コンポーネントの値を上書き", isOverrideExistingComponents);

            GUILayout.Space(10);


            if (GUILayout.Button("Armatureオブジェクトを再検索"))
            {
                FindAllArmatures();
                FindComponentsInSourceArmature();
            }

            GUILayout.Space(15);



            // コピーを行うコンポーネントの設定
            // 検出したコンポーネントのリストをカテゴリー別に表示
            GUILayout.Label("コピーするコンポーネント", EditorStyles.boldLabel);

            copyTransformsFoldout = CopierHelper.Foldout("Transform", copyTransformsFoldout);
            if (copyTransformsFoldout)
            {
                copyTransformsScale = EditorGUILayout.ToggleLeft("スケール", copyTransformsScale);
                copyTransformsPosition = EditorGUILayout.ToggleLeft("位置", copyTransformsPosition);
                copyTransformsRotation = EditorGUILayout.ToggleLeft("回転", copyTransformsRotation);
            }
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

            GUILayout.Space(10);

            // ソースArmature
            GUILayout.Label("コピー元 Armature", EditorStyles.boldLabel);
            var newSourceArmature = (GameObject)EditorGUILayout.ObjectField(sourceArmature, typeof(GameObject), true);

            // ソースArmatureが変更された場合
            if (newSourceArmature != previousSourceArmature)
            {
                sourceArmature = newSourceArmature;
                previousSourceArmature = sourceArmature;
                FindComponentsInSourceArmature();
                FindAllArmatures(); // ソース変更時にArmature一覧も更新
            }

            if (sourceArmature != null && !IsValidArmature(sourceArmature))
            {
                EditorGUILayout.HelpBox("選択されたオブジェクトは有効なArmatureではありません。", MessageType.Warning);
            }

            GUILayout.Space(15);

            // Armature一覧

            if (searchRoot != null)
            {
                string searchScopeText = $"'{searchRoot.name}' 内の";
                GUILayout.Label($"{searchScopeText}Armatureオブジェクト ({ArmatureObjects.Count}個)", EditorStyles.boldLabel);
            }

            if (ArmatureObjects.Count == 0)
            {
                if (searchRoot != null)
                {
                    string noResultMessage = $"'{searchRoot.name}' 内にArmatureオブジェクトが見つかりません。";
                    EditorGUILayout.HelpBox(noResultMessage, MessageType.Info);
                }
            }
            else
            {
                // 全選択/全解除ボタン
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("すべて選択"))
                {
                    SelectAllArmatures(true);
                }
                if (GUILayout.Button("すべて解除"))
                {
                    SelectAllArmatures(false);
                }
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(10);

                // Armature一覧表示
                for (int i = 0; i < ArmatureObjects.Count; i++)
                {
                    if (i >= ArmatureObjects.Count) break; // 安全性チェック

                    if (ArmatureObjects[i] == null)
                    {
                        ArmatureObjects.RemoveAt(i);
                        i--;
                        continue;
                    }

                    EditorGUILayout.BeginHorizontal();

                    bool wasSelected = Selection.gameObjects.Contains(ArmatureObjects[i]);
                    bool isSelected = EditorGUILayout.Toggle(wasSelected, GUILayout.Width(20));

                    if (isSelected != wasSelected)
                    {
                        if (isSelected)
                        {
                            var newSelection = Selection.gameObjects.ToList();
                            newSelection.Add(ArmatureObjects[i]);
                            Selection.objects = newSelection.ToArray();
                        }
                        else
                        {
                            var newSelection = Selection.gameObjects.ToList();
                            newSelection.Remove(ArmatureObjects[i]);
                            Selection.objects = newSelection.ToArray();
                        }
                    }

                    EditorGUILayout.ObjectField(ArmatureObjects[i], typeof(GameObject), true);

                    // 総子オブジェクト数の表示（再帰的）
                    int totalChildren = CountAllChildren(ArmatureObjects[i].transform);
                    EditorGUILayout.LabelField($"({totalChildren} objects)", GUILayout.Width(100));

                    EditorGUILayout.EndHorizontal();
                }

                GUILayout.Space(15);

                // 一括操作ボタン
                GUILayout.Label("一括操作", EditorStyles.boldLabel);

                GUI.enabled = sourceArmature != null && Selection.gameObjects.Length > 0;
                if (GUILayout.Button("選択したArmatureにペースト", GUILayout.Height(30)))
                {
                    BatchCopyToSelected();
                }
                GUI.enabled = true;
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// 指定範囲内のすべてのArmatureオブジェクトを検索
        /// </summary>
        private void FindAllArmatures()
        {
            ArmatureObjects.Clear();

            if (searchRoot == null)
            {
                // 検索対象が指定されていない場合は何もしない
                return;
            }

            try
            {
                // 指定オブジェクト内から検索
                var allObjects = searchRoot.GetComponentsInChildren<Transform>(includeInactive)
                    .Select(t => t.gameObject)
                    .Where(obj => obj != null)
                    .ToArray();

                foreach (var obj in allObjects)
                {
                    if (IsValidArmature(obj) &&
                        obj.scene.isLoaded && // シーンに存在するオブジェクトのみ
                        !EditorUtility.IsPersistent(obj)) // プレハブではないオブジェクトのみ
                    {
                        if (obj == sourceArmature)
                        {
                            // ソースArmatureは除外
                            continue;
                        }
                        ArmatureObjects.Add(obj);
                    }
                }

                ArmatureObjects.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
            }
            catch (System.Exception e)
            {
                ArmatureScaleCopierLogger.LogException(e, "Armature検索中");
            }
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
        /// オブジェクトがArmatureかどうかを判定
        /// </summary>
        private bool IsValidArmature(GameObject obj)
        {
            return ValidationHelper.IsValidArmature(obj);
        }

        /// <summary>
        /// すべてのArmatureの選択状態を変更
        /// </summary>
        private void SelectAllArmatures(bool select)
        {
            if (select)
            {
                Selection.objects = ArmatureObjects.Where(obj => obj != null).ToArray();
            }
            else
            {
                Selection.objects = new Object[0];
            }
        }

        /// <summary>
        /// 選択されたArmatureにソースのデータを一括コピー（再帰処理）
        /// </summary>
        private void BatchCopyToSelected()
        {
            if (sourceArmature == null)
            {
                EditorUtility.DisplayDialog("エラー", "コピー元のArmatureが設定されていません。", "OK");
                return;
            }

            var selectedArmatures = Selection.gameObjects.Where(obj =>
                obj != null && obj != sourceArmature && IsValidArmature(obj)).ToArray();

            if (selectedArmatures.Length == 0)
            {
                EditorUtility.DisplayDialog("エラー", "コピー先のArmatureオブジェクトが選択されていません。", "OK");
                return;
            }

            // コピー対象のコンポーネントが選択されているかチェック
            if (copyComponentList.Count == 0 && !(copyTransformsPosition || copyTransformsRotation || copyTransformsScale))
            {
                EditorUtility.DisplayDialog("警告", "コピーする項目が選択されていません。Transform情報またはコンポーネントを選択してください。", "OK");
                return;
            }

            // 確認ダイアログ
            bool proceed = EditorUtility.DisplayDialog(
                "確認",
                $"{selectedArmatures.Length}個のArmatureに '{sourceArmature.name}' のデータをコピーしますか？\n\n" +
                $"・コンポーネント数: {copyComponentList.Count}個\n" +
                $"・コンポーネントの値を上書き: {(isOverrideExistingComponents ? "はい" : "いいえ")}\n" +
                $"・Transform情報のコピー: {(copyTransformsPosition ? "位置" : "")} {(copyTransformsRotation ? "回転" : "")} {(copyTransformsScale ? "スケール" : "")}\n",
                "実行", "キャンセル");

            if (!proceed) return;

            try
            {
                // ソースデータの収集（再帰的）
                var sourceData = new ArmatureData();
                foreach (Transform child in sourceArmature.transform)
                {
                    var childData = CopyChildObjectDataRecursive(child);
                    sourceData.childrenData.Add(childData);
                }

                // 各ターゲットに適用
                int successCount = 0;
                foreach (var target in selectedArmatures)
                {
                    try
                    {
                        Undo.RegisterCompleteObjectUndo(target, "Batch Copy Armature Data");

                        foreach (var childData in sourceData.childrenData)
                        {
                            ApplyChildObjectDataRecursive(childData, target.transform);
                        }
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
                ArmatureScaleCopierLogger.LogException(e, "一括コピー処理中");
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

                bool shouldCopyComponent = false;

                if (copyComponentList.Contains(component.GetType().Namespace + "." + component.GetType().Name))
                {
                    shouldCopyComponent = true; // コピー対象のコンポーネント
                }

                if (!shouldCopyComponent) continue;

                var compData = new ComponentData
                {
                    typeName = component.GetType().AssemblyQualifiedName,
                    serializedData = ArmatureScaleCopierLogger.TryExecute(() => JsonUtility.ToJson(component), "{}", "コンポーネントのシリアライズ")
                };
                childData.componentData.Add(compData);
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
        /// 子オブジェクトのデータを再帰的に適用
        /// </summary>
        private void ApplyChildObjectDataRecursive(ChildObjectData childData, Transform parentTransform)
        {
            Transform existingChild = parentTransform.Find(childData.name);
            GameObject targetChild;

            if (existingChild == null)
                return;

            targetChild = existingChild.gameObject;
            Undo.RegisterCompleteObjectUndo(targetChild, "Update Child Object");

            // Transformの適用
            if (copyTransformsPosition)
                targetChild.transform.localPosition = childData.localPosition;
            if (copyTransformsRotation)
                targetChild.transform.localRotation = childData.localRotation;
            if (copyTransformsScale)
                targetChild.transform.localScale = childData.localScale;

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
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"コンポーネント {compData.typeName} の適用に失敗しました: {e.Message}");
                }
            }

            // 子オブジェクトを再帰的に処理
            if (childData.childrenData != null)
            {
                foreach (var grandChildData in childData.childrenData)
                {
                    if (grandChildData != null)
                    {
                        ApplyChildObjectDataRecursive(grandChildData, targetChild.transform);
                    }
                }
            }
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
        /// Transform階層の総子オブジェクト数をカウント（再帰）
        /// </summary>
        private int CountAllChildren(Transform parent)
        {
            int count = parent.childCount;
            foreach (Transform child in parent)
            {
                count += CountAllChildren(child);
            }
            return count;
        }
    }
}
