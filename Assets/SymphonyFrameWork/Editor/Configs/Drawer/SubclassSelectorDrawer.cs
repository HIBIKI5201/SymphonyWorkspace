using SymphonyFrameWork.Attribute;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace SymphonyFrameWork.Editor
{
    /// <summary>
    ///     サブクラスセレクターのカスタムプロパティドロワー。
    /// </summary>
    [CustomPropertyDrawer(typeof(SubclassSelectorAttribute))]
    public sealed class SubclassSelectorDrawer : PropertyDrawer
    {
        /// <summary>
        ///     プロパティのGUIを描画する。
        /// </summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // ManagedReference型でない場合は処理を中断する。
            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            // ラベルとポップアップを含む一行を描画するためのRectを準備。
            // EditorGUI.PrefixLabel は、positionからラベル部分を切り出し、残りのRectを返す。
            Rect firstLineRect = position;
            firstLineRect.height = EditorGUIUtility.singleLineHeight;
            Rect controlRect = EditorGUI.PrefixLabel(firstLineRect, label); // ラベル部分を確保し、残りをコントロール用とする。

            // 型情報を取得し、キャッシュが存在しない場合は作成する。
            var baseType = GetType(property);
            if (baseType == null)
            {
                // baseTypeがnullの場合でも、EditorGUI.PropertyFieldは呼び出す必要がある。
                // この状況ではポップアップは表示せず、通常のプロパティとして描画する。
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }


            var cacheKey = (baseType, ((SubclassSelectorAttribute)attribute).IsIncludeMono());
            if (!s_TypeCache.TryGetValue(cacheKey, out var cachedData))
            {
                cachedData = CreateInheritedTypesCache(baseType, cacheKey.Item2);
                s_TypeCache.Add(cacheKey, cachedData);
            }
            var (inheritedTypes, typePopupNameArray, typeFullNameArray) = cachedData;

            // 現在選択されている型のインデックスを取得する。見つからない場合は<null> (インデックス0) にする。
            int currentTypeIndex = Array.IndexOf(typeFullNameArray, property.managedReferenceFullTypename);
            if (currentTypeIndex < 0) { currentTypeIndex = 0; }

            // 型を選択するためのポップアップGUIを描画する。
            int selectedTypeIndex = EditorGUI.Popup(controlRect, currentTypeIndex, typePopupNameArray);

            // 選択が変更された場合、プロパティに新しいインスタンスを割り当てる。
            if (currentTypeIndex != selectedTypeIndex)
            {
                Type selectedType = inheritedTypes[selectedTypeIndex];
                property.managedReferenceValue =
                    selectedType == null ? null : Activator.CreateInstance(selectedType);
            }

            // <null>が選択されていない場合のみ、デフォルトのプロパティフィールドを描画する。
            // その際、描画Rectを一行下にずらし、自身のラベルは描画しない。
            if (property.managedReferenceValue != null)
            {
                Rect fieldPosition = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight, position.width, position.height - EditorGUIUtility.singleLineHeight);
                EditorGUI.PropertyField(fieldPosition, property, GUIContent.none, true);
            }
        }

        /// <summary>
        ///     プロパティの高さに基づいてGUIの高さを計算する。
        /// </summary>
        /// <param name="property">高さ計算の対象となるプロパティ。</param>
        /// <param name="label">プロパティの表示ラベル。</param>
        /// <returns>プロパティの高さ。</returns>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // ManagedReference型でない場合は、通常のプロパティの高さを返す。
            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                return EditorGUI.GetPropertyHeight(property, label, true);
            }

            // 基底型が取得できない場合も、通常のプロパティの高さを返す。
            // (例: エラーになっている場合など)
            var baseType = GetType(property);
            if (baseType == null)
            {
                return EditorGUI.GetPropertyHeight(property, label, true);
            }

            // ポップアップ選択部分のための1行分の高さ。
            float totalHeight = EditorGUIUtility.singleLineHeight;

            // 何らかのサブクラスが選択されている場合、その内容の表示に必要な高さを追加する。
            // EditorGUI.PropertyFieldにGUIContent.noneを渡すと、そのプロパティ自身のラベルは描画されない。
            // しかし、PropertyFieldの描画には少なくとも1行分の高さ (たとえばFoldout矢印のため) が必要。
            // そのため、GUIContent.noneを渡した場合でも、そのプロパティ自身が表示されるための最低限の高さは含まれる。
            if (property.managedReferenceValue != null)
            {
                totalHeight += EditorGUI.GetPropertyHeight(property, GUIContent.none, true);
            }

            return totalHeight;
        }

        /// <summary>
        ///     指定された基底クラスを継承する全ての型情報を収集し、キャッシュデータを作成する。
        /// </summary>
        /// <param name="baseType"> 基底クラスの型。 </param>
        /// <param name="includeMono"> MonoBehaviourを継承する型を含めるかどうか。 </param>
        /// <returns> 派生型情報のキャッシュデータ。 </returns>
        private static (Type[], string[], string[]) CreateInheritedTypesCache(Type baseType, bool includeMono)
        {
            Type monoType = typeof(MonoBehaviour);

            // abstractクラスも含む、すべての派生型リストを作成する (カテゴリ判定用)
            Type[] allInheritedTypesWithAbstract = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => baseType.IsAssignableFrom(p) && p.IsClass && (!monoType.IsAssignableFrom(p) || includeMono))
                .ToArray();

            // ユーザーが選択可能な、abstractでない派生型リストを作成する。
            Type[] selectableTypesArray = allInheritedTypesWithAbstract
                .Where(t => !t.IsAbstract)
                .ToArray();

            // <null>オプションを先頭に追加する。
            Type[] finalSelectableTypes = selectableTypesArray.Prepend(null).ToArray();

            // ポップアップ表示用の型名配列を作成する。
            string[] typePopupNameArray = GetPopupArray(finalSelectableTypes, baseType, allInheritedTypesWithAbstract);

            // 完全修飾名の配列を作成する。
            string[] typeFullNameArray = finalSelectableTypes
                .Select(type => type == null ? string.Empty : $"{type.Assembly.GetName().Name} {type.FullName}")
                .ToArray();

            // 最終的に返却するのは、選択可能な型と、それに対応する表示名。
            return (finalSelectableTypes, typePopupNameArray, typeFullNameArray);
        }

        /// <summary>
        ///     ポップアップ表示用の型名配列を取得する。
        /// </summary>
        /// <param name="finalSelectableTypes"> nullを含む選択可能な型一覧。 </param>
        /// <param name="baseType"> SerializeReferenceフィールドの宣言型。 </param>
        /// <param name="allInheritedTypesWithAbstract"> カテゴリ判定に使用する全派生型。 </param>
        /// <returns> 継承階層をカテゴリとして表現した表示名一覧。 </returns>
        private static string[] GetPopupArray(Type[] finalSelectableTypes, Type baseType, Type[] allInheritedTypesWithAbstract)
        {
            return finalSelectableTypes.Select(type =>
            {
                if (type == null) return "<null>";

                string displayName;
                Type parent = type.BaseType;

                // 親クラスが「中間クラス」(abstract含む)である場合、カテゴリ分けする。
                // 判定には allInheritedTypesWithAbstract を使用する。
                if (parent != null && parent != baseType && allInheritedTypesWithAbstract.Contains(parent))
                {
                    displayName = $"{parent.Name}/{type.Name}";
                }
                // 自身が他クラスの「中間クラス」(abstract含む)である場合、自身をカテゴリのルートとして表示する。
                // このロジックは、abstractクラス自身は選択肢にないので、具象クラスが中間クラスになる場合のみ適用される。
                else if (allInheritedTypesWithAbstract.Any(t => t.BaseType == type))
                {
                    displayName = $"{type.Name}/{type.Name}";
                }
                // 上記以外の場合は、カテゴリ分けせずに表示する。
                else
                {
                    displayName = type.Name;
                }

                // ネストクラスの '+' を '/' に置換する。
                if (displayName.Contains('+'))
                {
                    displayName = displayName.Replace('+', '/');
                }

                return displayName;
            }).ToArray();
        }

        /// <summary>
        /// ポップアップGUIの描画範囲を取得する。
        /// </summary>
        /// <param name="currentPosition">現在のプロパティの描画範囲。</param>
        /// <returns>ポップアップの描画範囲。</returns>
        private Rect GetPopupPosition(Rect currentPosition)
        {
            Rect popupPosition = new Rect(currentPosition);
            popupPosition.width -= EditorGUIUtility.labelWidth;
            popupPosition.x += EditorGUIUtility.labelWidth;
            popupPosition.height = EditorGUIUtility.singleLineHeight;
            return popupPosition;
        }

        /// <summary>
        ///     SerializedPropertyから、そのプロパティの基底となる型を取得する。
        /// </summary>
        /// <param name="property">対象のプロパティ。</param>
        /// <returns>プロパティの基底型。</returns>
        private static Type GetType(SerializedProperty property)
        {
            FieldInfo fieldInfo = GetFieldInfo(property);
            if (fieldInfo == null)
            {
                Debug.LogWarning($"Could not find field for property {property.propertyPath}");
                return null;
            }

            Type fieldType = fieldInfo.FieldType;

            // 配列の場合は、その要素の型を返す。
            if (fieldType.IsArray)
            {
                return fieldType.GetElementType();
            }

            // List<>の場合は、そのジェネリック引数の型を返す。
            if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                return fieldType.GetGenericArguments()[0];
            }

            return fieldType;
        }

        /// <summary>
        ///     SerializedPropertyのパスを解析し、対応するFieldInfoを取得する。
        /// </summary>
        /// <param name="property">対象のプロパティ。</param>
        /// <returns>対応するFieldInfo。</returns>
        private static FieldInfo GetFieldInfo(SerializedProperty property)
        {
            // 配列のパスを解析しやすいように置換する。 e.g. "array.Array.data[0]" -> "array[0]"
            string[] pathElements = property.propertyPath.Replace(".Array.data[", "[").Split('.');
            Type currentContainingType = property.serializedObject.targetObject.GetType(); // 現在のフィールドを含むオブジェクトの型
            FieldInfo fieldAtLastElement = null; // パス内の最終要素に対応するFieldInfo

            for (int i = 0; i < pathElements.Length; i++)
            {
                string element = pathElements[i];

                if (element.Contains("["))
                {
                    // 配列/リスト要素のアクセス
                    string fieldName = element.Substring(0, element.IndexOf("["));

                    FieldInfo collectionField = null;
                    for (Type t = currentContainingType; collectionField == null && t != null; t = t.BaseType)
                    {
                        collectionField = t.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    }

                    if (collectionField == null) return null; // コレクションフィールドが見つからない

                    Type elementType = null;
                    if (collectionField.FieldType.IsArray)
                    {
                        elementType = collectionField.FieldType.GetElementType();
                    }
                    else if (collectionField.FieldType.IsGenericType && collectionField.FieldType.GetGenericTypeDefinition() == typeof(List<>))
                    {
                        elementType = collectionField.FieldType.GetGenericArguments()[0];
                    }
                    else
                    {
                        return null; // 配列でもListでもない場合は解析不能。
                    }

                    // 次の要素の探索のため、現在のコンテナ型を要素の型に更新
                    currentContainingType = elementType;

                    // もしこれがパスの最終要素であれば、fieldAtLastElementを更新する。
                    // GetTypeでは配列/リストの型を取得して、そこから要素型を抽出する既存ロジックと整合性を維持する。
                    if (i == pathElements.Length - 1)
                    {
                        fieldAtLastElement = collectionField; // この場合はコレクション自体のFieldInfoを返す
                    }
                }
                else
                {
                    // 通常のフィールドアクセス

                    FieldInfo actualField = null;
                    for (Type t = currentContainingType; actualField == null && t != null; t = t.BaseType)
                    {
                        actualField = t.GetField(element, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    }

                    if (actualField == null) return null; // フィールドが見つからない

                    if (i == pathElements.Length - 1)
                    {
                        // これがパスの最終要素であれば、このFieldInfoを返す
                        fieldAtLastElement = actualField;
                    }
                    else
                    {
                        // 中間フィールドの場合、次のフィールドの探索のための型を更新する。
                        // SerializedProperty.FindProperty は 'targetObject'から全てのパスを解決するので、
                        // 中間パスを構築してそのプロパティを取得する。
                        // pathElementsは配列アクセスを"name[index]"の形に簡略化済みのため、
                        // FindPropertyが要求する本来の"name.Array.data[index]"形式に戻してから結合する。
                        string intermediatePath = BuildPropertyPath(pathElements, i + 1);
                        SerializedProperty intermediateProperty = property.serializedObject.FindProperty(intermediatePath);

                        if (intermediateProperty != null && intermediateProperty.propertyType == SerializedPropertyType.ManagedReference)
                        {
                            // ManagedReferenceの場合、その参照が持つ実際のインスタンスの型を取得する。
                            // インスタンスが存在しない場合は、宣言されたフィールドの型（インターフェースなど）にフォールバック。
                            currentContainingType = intermediateProperty.managedReferenceValue?.GetType() ?? actualField.FieldType;
                        }
                        else
                        {
                            // ManagedReferenceでない場合は、フィールドの宣言型をそのまま使用。
                            currentContainingType = actualField.FieldType;
                        }
                    }
                }
            }
            return fieldAtLastElement;
        }

        /// <summary>
        ///     簡略化されたパス要素("name[index]"形式)から、SerializedProperty.FindPropertyが解決できる
        ///     本来のUnityパス("name.Array.data[index]"形式)を再構築する。
        /// </summary>
        /// <param name="pathElements"> 簡略化済みのパス要素配列。 </param>
        /// <param name="count"> 先頭から何要素を対象に含めるか。 </param>
        /// <returns> Unity標準形式のプロパティパス。 </returns>
        private static string BuildPropertyPath(string[] pathElements, int count)
        {
            string[] segments = new string[count];
            for (int i = 0; i < count; i++)
            {
                string element = pathElements[i];
                int bracketIndex = element.IndexOf('[');
                if (bracketIndex < 0)
                {
                    segments[i] = element;
                    continue;
                }

                string fieldName = element.Substring(0, bracketIndex);
                string indexPart = element.Substring(bracketIndex);
                segments[i] = fieldName + ".Array.data" + indexPart;
            }

            return string.Join(".", segments);
        }

        /// <summary>
        ///     型情報をキャッシュするための静的な辞書。
        ///     Key: 基底クラスの型
        ///     Value: (派生型の配列, ポップアップ表示用の型名の配列, 完全修飾名の配列)
        /// </summary>
        private static readonly Dictionary<(Type, bool), (Type[] types, string[] names, string[] fullNames)> s_TypeCache = new();
    }
}
