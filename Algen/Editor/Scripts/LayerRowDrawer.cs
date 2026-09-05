using UnityEditor;
using UnityEngine;

namespace AglenRealms.WorldCore.Editor
{
    internal struct LayerRowOptions
    {
        public bool showEnableCheckbox;
        public bool showHeight;
        public bool showVisibility;
        public bool showDelete;
        public bool canDelete;
        public string suffixLabel;
        public float reservedRightWidth;
        public Rect[] selectionExcludeRects;
        public float leftInset;
        public string heightLabel;
        public string heightLabelTooltip;
        public float heightFieldWidth;
        public bool heightAsInteger;
    }

    internal struct LayerRowInput
    {
        public string name;
        public string nameControlName;
        public bool enabled;
        public bool visible;
        public float height;
        public bool isActive;
    }

    internal struct LayerRowOutput
    {
        public bool selectRow;
        public bool enableChanged;
        public bool enabled;
        public bool nameChanged;
        public string name;
        public bool visibilityToggled;
        public bool heightChanged;
        public float height;
        public bool deleteClicked;
    }

    internal static class LayerRowDrawer
    {
        private const float EnableCheckboxWidth = 18f;
        private const float VisibilityButtonWidth = 26f;
        private const float HeightLabelWidth = 14f;
        private const float DefaultHeightFieldWidth = 44f;
        private const float DeleteButtonWidth = 22f;
        private const float ColumnSpacing = 4f;
        private const float SuffixLabelWidth = 72f;
        private const float NameFieldMinWidth = 40f;

        private static readonly Color ActiveRowColor = new Color(0.28f, 0.52f, 0.82f, 0.18f);
        private const float CenteredIconSize = 16f;

        public static LayerRowOutput Draw(LayerRowInput input, LayerRowOptions options)
        {
            float rowHeight = EditorGUIUtility.singleLineHeight + 2f;
            Rect rowRect = EditorGUILayout.GetControlRect(false, rowHeight);
            return DrawInRect(rowRect, input, options);
        }

        public static LayerRowOutput DrawInRect(Rect rowRect, LayerRowInput input, LayerRowOptions options)
        {
            LayerRowOutput output = default;

            if (input.isActive && Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(rowRect, ActiveRowColor);

            float right = rowRect.xMax - Mathf.Max(0f, options.reservedRightWidth);
            Rect deleteRect = default;
            Rect eyeRect = default;
            Rect heightFieldRect = default;
            Rect heightLabelRect = default;
            Rect enableRect = default;
            Rect suffixRect = default;
            Rect nameRect;

            if (options.showDelete)
            {
                deleteRect = new Rect(right - DeleteButtonWidth, rowRect.y, DeleteButtonWidth, rowRect.height);
                right = deleteRect.x - ColumnSpacing;
            }

            if (options.showVisibility)
            {
                eyeRect = new Rect(right - VisibilityButtonWidth, rowRect.y, VisibilityButtonWidth, rowRect.height);
                right = eyeRect.x - ColumnSpacing;
            }

            float heightFieldWidth = options.heightFieldWidth > 0f
                ? options.heightFieldWidth
                : DefaultHeightFieldWidth;

            if (options.showHeight)
            {
                heightFieldRect = new Rect(right - heightFieldWidth, rowRect.y, heightFieldWidth, rowRect.height);
                right = heightFieldRect.x - ColumnSpacing;
                heightLabelRect = new Rect(right - HeightLabelWidth, rowRect.y, HeightLabelWidth, rowRect.height);
                right = heightLabelRect.x - ColumnSpacing;
            }

            float nameX = rowRect.x + Mathf.Max(0f, options.leftInset);
            if (options.showEnableCheckbox)
            {
                enableRect = new Rect(nameX, rowRect.y, EnableCheckboxWidth, rowRect.height);
                nameX = enableRect.xMax + ColumnSpacing;
            }

            if (!string.IsNullOrEmpty(options.suffixLabel))
            {
                suffixRect = new Rect(right - SuffixLabelWidth, rowRect.y, SuffixLabelWidth, rowRect.height);
                right = suffixRect.x - ColumnSpacing;
            }

            float maxNameWidthByHalfRow = rowRect.x + rowRect.width * 0.5f - nameX;
            float availableNameWidth = Mathf.Max(NameFieldMinWidth, Mathf.Min(right - nameX, maxNameWidthByHalfRow));
            nameRect = new Rect(nameX, rowRect.y, availableNameWidth, rowRect.height);

            if (options.showEnableCheckbox)
            {
                EditorGUI.BeginChangeCheck();
                bool newEnabled = EditorGUI.Toggle(enableRect, input.enabled);
                if (EditorGUI.EndChangeCheck())
                {
                    output.enableChanged = true;
                    output.enabled = newEnabled;
                }
            }

            if (options.showHeight)
            {
                string labelText = string.IsNullOrEmpty(options.heightLabel) ? "H" : options.heightLabel;
                GUIContent labelContent = string.IsNullOrEmpty(options.heightLabelTooltip)
                    ? new GUIContent(labelText)
                    : new GUIContent(labelText, options.heightLabelTooltip);
                EditorGUI.LabelField(heightLabelRect, labelContent);

                EditorGUI.BeginChangeCheck();
                if (options.heightAsInteger)
                {
                    int newHeight = EditorGUI.IntField(heightFieldRect, Mathf.RoundToInt(input.height));
                    if (EditorGUI.EndChangeCheck())
                    {
                        output.heightChanged = true;
                        output.height = newHeight;
                    }
                }
                else
                {
                    float newHeight = EditorGUI.FloatField(heightFieldRect, input.height);
                    if (EditorGUI.EndChangeCheck())
                    {
                        output.heightChanged = true;
                        output.height = newHeight;
                    }
                }
            }

            if (options.showVisibility)
            {
                GUIContent visibilityIcon = input.visible
                    ? EditorGUIUtility.IconContent("scenevis_visible_hover")
                    : EditorGUIUtility.IconContent("scenevis_hidden_hover");
                visibilityIcon.tooltip = input.visible ? "Hide layer" : "Show layer";

                if (DrawCenteredIconButton(eyeRect, visibilityIcon))
                    output.visibilityToggled = true;
            }

            if (options.showDelete)
            {
                EditorGUI.BeginDisabledGroup(!options.canDelete);
                if (GUI.Button(deleteRect, "×"))
                    output.deleteClicked = true;
                EditorGUI.EndDisabledGroup();
            }

            if (!string.IsNullOrEmpty(options.suffixLabel))
                EditorGUI.LabelField(suffixRect, options.suffixLabel, EditorStyles.miniLabel);

            if (!string.IsNullOrEmpty(input.nameControlName))
                GUI.SetNextControlName(input.nameControlName);

            EditorGUI.BeginChangeCheck();
            string newName = EditorGUI.TextField(nameRect, input.name);
            if (EditorGUI.EndChangeCheck())
            {
                output.nameChanged = true;
                output.name = newName;
            }

            if (TryConsumeRowSelection(
                    rowRect,
                    nameRect,
                    enableRect,
                    eyeRect,
                    heightLabelRect,
                    heightFieldRect,
                    deleteRect,
                    options.selectionExcludeRects))
                output.selectRow = true;

            return output;
        }

        private static bool TryConsumeRowSelection(
            Rect rowRect,
            Rect nameRect,
            Rect enableRect,
            Rect eyeRect,
            Rect heightLabelRect,
            Rect heightFieldRect,
            Rect deleteRect,
            Rect[] additionalExcludeRects)
        {
            Event e = Event.current;
            if (e.type != EventType.MouseDown || e.button != 0)
                return false;

            if (!rowRect.Contains(e.mousePosition))
                return false;

            if (IsSelectionBlockedRect(e.mousePosition, nameRect))
                return false;

            if (IsSelectionBlockedRect(e.mousePosition, enableRect))
                return false;

            if (IsSelectionBlockedRect(e.mousePosition, eyeRect))
                return false;

            if (IsSelectionBlockedRect(e.mousePosition, heightLabelRect))
                return false;

            if (IsSelectionBlockedRect(e.mousePosition, heightFieldRect))
                return false;

            if (IsSelectionBlockedRect(e.mousePosition, deleteRect))
                return false;

            if (additionalExcludeRects != null)
            {
                for (int i = 0; i < additionalExcludeRects.Length; i++)
                {
                    if (IsSelectionBlockedRect(e.mousePosition, additionalExcludeRects[i]))
                        return false;
                }
            }

            e.Use();
            return true;
        }

        private static bool IsSelectionBlockedRect(Vector2 mousePosition, Rect rect) =>
            rect.width > 0f && rect.height > 0f && rect.Contains(mousePosition);

        private static bool DrawCenteredIconButton(Rect rect, GUIContent content)
        {
            bool clicked = GUI.Button(rect, new GUIContent(string.Empty, content.tooltip), GUIStyle.none);

            if (Event.current.type == EventType.Repaint && content.image != null)
            {
                float size = Mathf.Min(CenteredIconSize, rect.width, rect.height);
                Rect iconRect = new Rect(
                    rect.x + (rect.width - size) * 0.5f,
                    rect.y + (rect.height - size) * 0.5f,
                    size,
                    size);
                GUI.DrawTexture(iconRect, content.image, ScaleMode.ScaleToFit, true);
            }

            return clicked;
        }

        public static LayerRowOptions EnvironmentDefaults(bool canDelete) =>
            new LayerRowOptions
            {
                showEnableCheckbox = false,
                showHeight = true,
                showVisibility = true,
                showDelete = true,
                canDelete = canDelete
            };

        public static LayerRowOptions LandscapeSubLevelDefaults(float reservedRightWidth) =>
            new LayerRowOptions
            {
                showEnableCheckbox = false,
                showHeight = false,
                showVisibility = true,
                showDelete = false,
                reservedRightWidth = reservedRightWidth
            };

        public static LayerRowOptions LandscapeLevelDefaults(
            float reservedRightWidth,
            float leftInset,
            float heightFieldWidth,
            string heightLabelTooltip,
            bool canDelete) =>
            new LayerRowOptions
            {
                showEnableCheckbox = true,
                showHeight = true,
                showVisibility = false,
                showDelete = true,
                canDelete = canDelete,
                reservedRightWidth = reservedRightWidth,
                leftInset = leftInset,
                heightLabel = "Y",
                heightLabelTooltip = heightLabelTooltip,
                heightFieldWidth = heightFieldWidth,
                heightAsInteger = true
            };
    }
}
