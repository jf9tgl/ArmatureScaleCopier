using UnityEngine;
using UnityEditor;
using System;
using System.Reflection;

namespace ShimotukiRieru.ArmatureScaleCopier
{
    /// <summary>
    /// いろんな処理をするヘルパークラス
    /// </summary>
    public static class CopierHelper
    {
        private static bool? _isMAAvailable;
        private static Type[] _maComponentTypes;

        /// <summary>
        /// ModularAvatarが利用可能かどうか
        /// </summary>
        public static bool IsModularAvatarAvailable
        {
            get
            {
                if (!_isMAAvailable.HasValue)
                {
                    CheckModularAvatarAvailability();
                }
                return _isMAAvailable.Value;
            }
        }

        /// <summary>
        /// ModularAvatarコンポーネントの型一覧
        /// </summary>
        public static Type[] MAComponentTypes
        {
            get
            {
                if (_maComponentTypes == null)
                {
                    GetMAComponentTypes();
                }
                return _maComponentTypes ?? new Type[0];
            }
        }

        /// <summary>
        /// ModularAvatarの利用可能性をチェック
        /// </summary>
        private static void CheckModularAvatarAvailability()
        {
            try
            {
                // nadena.dev.modular_avatar.core アセンブリの存在をチェック
                var assembly = Assembly.Load("nadena.dev.modular-avatar.core");
                _isMAAvailable = assembly != null;
            }
            catch
            {
                _isMAAvailable = false;
            }
        }

        /// <summary>
        /// ModularAvatarコンポーネントの型を取得
        /// </summary>
        private static void GetMAComponentTypes()
        {
            if (!IsModularAvatarAvailable)
            {
                _maComponentTypes = new Type[0];
                return;
            }

            try
            {
                var assembly = Assembly.Load("nadena.dev.modular-avatar.core");
                var types = assembly.GetTypes();
                var maTypes = new System.Collections.Generic.List<Type>();

                foreach (var type in types)
                {
                    if (typeof(Component).IsAssignableFrom(type) &&
                        type.Namespace?.StartsWith("nadena.dev.modular_avatar") == true)
                    {
                        maTypes.Add(type);
                    }
                }

                _maComponentTypes = maTypes.ToArray();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"ModularAvatarコンポーネント型の取得に失敗しました: {e.Message}");
                _maComponentTypes = new Type[0];
            }
        }

        /// <summary>
        /// 指定されたコンポーネントがModularAvatarコンポーネントかどうかを判定
        /// </summary>
        public static bool IsModularAvatarComponent(Component component)
        {
            if (!IsModularAvatarAvailable || component == null)
                return false;

            var componentType = component.GetType();
            return componentType.Namespace?.StartsWith("nadena.dev.modular_avatar") == true;
        }

        /// <summary>
        /// 指定されたコンポーネントがVRChatのコンポーネントかどうかを判定
        /// </summary>
        public static bool IsVRChatComponent(Component component)
        {
            if (component == null)
                return false;

            var componentType = component.GetType();
            return componentType.Namespace?.StartsWith("VRC") == true;
        }

        /// <summary>
        /// 指定されたコンポーネントがUnityの標準コンポーネントかどうかを判定
        /// </summary>
        public static bool IsUnityStandardComponent(Component component)
        {
            if (component == null)
                return false;

            var componentType = component.GetType();
            return componentType.Namespace?.StartsWith("UnityEngine") == true;
        }

        /// <summary>
        /// コンポーネントの種類を取得
        /// </summary>
        public static ComponentCategory GetComponentCategory(Component component)
        {
            if (component == null)
                return ComponentCategory.Unknown;

            if (component is Transform)
                return ComponentCategory.Transform;

            if (IsModularAvatarComponent(component))
                return ComponentCategory.ModularAvatar;

            if (IsVRChatComponent(component))
                return ComponentCategory.VRChat;

            if (IsUnityStandardComponent(component))
                return ComponentCategory.Unity;

            return ComponentCategory.Other;
        }

        public static string GetComponentCategoryName(ComponentCategory category)
        {
            switch (category)
            {
                case ComponentCategory.Transform:
                    return "Transform";
                case ComponentCategory.ModularAvatar:
                    return "ModularAvatar";
                case ComponentCategory.Unity:
                    return "Unity Standard";
                case ComponentCategory.VRChat:
                    return "VRChat";
                case ComponentCategory.Other:
                    return "Other";
                default:
                    return "Unknown";
            }
        }

        /// <summary>
        /// コンポーネントが安全にコピー可能かどうかを判定
        /// </summary>
        public static bool IsSafeToCopy(Component component)
        {
            if (component == null || component is Transform)
                return false;

            var category = GetComponentCategory(component);

            // ModularAvatarコンポーネントは通常安全
            if (category == ComponentCategory.ModularAvatar)
                return true;

            // 特定のUnityコンポーネントは避ける
            if (component is Renderer || component is Collider || component is Rigidbody)
                return false;

            return true;
        }


        public static bool Foldout(string title, bool display)
        {
            var style = new GUIStyle("ShurikenModuleTitle");
            style.font = new GUIStyle(EditorStyles.label).font;
            style.border = new RectOffset(15, 7, 4, 4);
            style.fixedHeight = 22;
            style.contentOffset = new Vector2(20f, -2f);

            var rect = GUILayoutUtility.GetRect(16f, 22f, style);
            GUI.Box(rect, title, style);

            var e = Event.current;

            var toggleRect = new Rect(rect.x + 4f, rect.y + 2f, 13f, 13f);
            if (e.type == EventType.Repaint)
            {
                EditorStyles.foldout.Draw(toggleRect, false, false, display, false);
            }

            if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
            {
                display = !display;
                e.Use();
            }

            return display;
        }
    }

    /// <summary>
    /// コンポーネントのカテゴリ
    /// </summary>
    public enum ComponentCategory
    {
        Unknown,
        Transform,
        ModularAvatar,
        Unity,
        VRChat,
        Other
    }

    /// <summary>
    /// エラーハンドリングとログ管理
    /// </summary>
    public static class ArmatureScaleCopierLogger
    {
        private const string LOG_PREFIX = "[Armature Scale Copier]";

        public static void Log(string message)
        {
            Debug.Log($"{LOG_PREFIX} {message}");
        }

        public static void LogWarning(string message)
        {
            Debug.LogWarning($"{LOG_PREFIX} {message}");
        }

        public static void LogError(string message)
        {
            Debug.LogError($"{LOG_PREFIX} {message}");
        }

        public static void LogException(Exception exception, string context = "")
        {
            var message = string.IsNullOrEmpty(context)
                ? $"例外が発生しました: {exception.Message}"
                : $"{context}で例外が発生しました: {exception.Message}";

            Debug.LogError($"{LOG_PREFIX} {message}");
            Debug.LogException(exception);
        }

        public static bool TryExecute(System.Action action, string context = "")
        {
            try
            {
                action?.Invoke();
                return true;
            }
            catch (Exception e)
            {
                LogException(e, context);
                return false;
            }
        }

        public static T TryExecute<T>(System.Func<T> func, T defaultValue = default(T), string context = "")
        {
            try
            {
                return func != null ? func() : defaultValue;
            }
            catch (Exception e)
            {
                LogException(e, context);
                return defaultValue;
            }
        }
    }

    /// <summary>
    /// バリデーションヘルパー
    /// </summary>
    public static class ValidationHelper
    {
        /// <summary>
        /// Armatureオブジェクトかどうかを判定
        /// </summary>
        public static bool IsValidArmature(GameObject obj)
        {
            if (obj == null) return false;

            return obj.name.ToLower().Contains("armature");
        }

        /// <summary>
        /// オブジェクトが有効なコピー元として使用できるかを判定
        /// </summary>
        public static bool IsValidSource(GameObject obj)
        {
            if (!IsValidArmature(obj)) return false;

            // 子オブジェクトが存在するかチェック
            return obj.transform.childCount > 0;
        }

        /// <summary>
        /// オブジェクトが有効なコピー先として使用できるかを判定
        /// </summary>
        public static bool IsValidTarget(GameObject obj, GameObject source = null)
        {
            if (!IsValidArmature(obj)) return false;

            // 自分自身をターゲットにしていないかチェック
            if (source != null && obj == source) return false;

            return true;
        }

        /// <summary>
        /// コンポーネントの有効性をチェック
        /// </summary>
        public static bool IsValidComponent(Component component)
        {
            return component != null && !(component is Transform);
        }
    }

    /// <summary>
    /// コンポーネントの情報を保持するクラス
    /// </summary>
    public class ComponentInfo
    {
        public string ComponentNameSpace { get; private set; }
        public string ComponentName { get; private set; }
        public string ComponentDisplayName
        {
            get
            {
                var _componentDisplayName = string.Empty;
                if (string.IsNullOrEmpty(_componentDisplayName))
                {
                    _componentDisplayName = ObjectNames.NicifyVariableName(ComponentType.Name);
                }
                return _componentDisplayName;
            }
        }
        public Type ComponentType { get; private set; }
        public ComponentCategory Category { get; private set; }
        public Texture ComponentIcon { get; private set; }
        public ComponentInfo(Component component)
        {
            if (component == null)
                throw new ArgumentNullException(nameof(component));

            ComponentType = component.GetType();
            ComponentNameSpace = ComponentType.Namespace ?? "Unknown";
            ComponentName = ComponentType.Name;
            Category = CopierHelper.GetComponentCategory(component);
            ComponentIcon = AssetPreview.GetMiniThumbnail(component);
        }
    }



}
