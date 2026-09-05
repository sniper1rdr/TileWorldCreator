using UnityEngine;
using UnityEditor;
using System;

namespace TileWorldCreator
{
    public class ToolPanelUI
    {
        private readonly TileBrush brush;

        public ToolPanelUI(TileBrush brush)
        {
            this.brush = brush;
        }

        public void Draw(
            bool isLevelMode,
            Layer currentLayer,
            Action onClearLevel,
            Action onClearEnvironment)
        {
            EditorGUILayout.LabelField("Tool", EditorStyles.boldLabel);

            if (brush == null)
            {
                EditorGUILayout.HelpBox("Brush not found!", MessageType.Warning);
                return;
            }

            if (currentLayer == null)
            {
                EditorGUILayout.HelpBox("Select a layer first!", MessageType.Warning);
                return;
            }

            // Настройки кисти
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Brush Settings", EditorStyles.miniLabel);

            // Читаем текущее состояние кисти и применяем изменения через сеттеры
            bool paintOnDrag = brush.paintOnDrag;
            bool newPaintOnDrag = EditorGUILayout.Toggle("Paint on Drag", paintOnDrag);
            if (newPaintOnDrag != paintOnDrag)
                brush.SetPaintOnDrag(newPaintOnDrag);

            if (newPaintOnDrag)
            {
                float interval = brush.paintInterval;
                float newInterval = EditorGUILayout.Slider("Speed", interval, 0.01f, 0.2f);
                if (!Mathf.Approximately(newInterval, interval))
                    brush.SetPaintInterval(newInterval);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);

            // Кнопки Brush / Clear
            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = brush.IsActive ? new Color(0.3f, 0.8f, 0.3f) : Color.white;
            string brushLabel = brush.IsActive ? "🖌️ Brush: ON" : "🖌️ Brush: OFF";

            if (GUILayout.Button(brushLabel, GUILayout.Height(35)))
            {
                brush.SetActive(!brush.IsActive);
            }
            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("🗑️ Clear", GUILayout.Height(35)))
            {
                if (isLevelMode)
                    onClearLevel?.Invoke();
                else
                    onClearEnvironment?.Invoke();
            }

            EditorGUILayout.EndHorizontal();

            // Цвет подсветки
            if (brush.IsActive)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Highlight Color:", GUILayout.Width(100));
                Color currentColor = brush.highlightColor;
                Color newColor = EditorGUILayout.ColorField(currentColor);
                if (newColor != currentColor)
                    brush.SetHighlightColor(newColor);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(5);
        }
    }
}
