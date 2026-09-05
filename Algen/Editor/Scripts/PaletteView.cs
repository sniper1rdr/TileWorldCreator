using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AglenRealms.WorldCore.Editor
{
    internal static class PaletteView
    {
        private const float PreviewSize = 64f;
        private const float LabelHeight = 16f;
        private const float CellPadding = 6f;
        private const float ScrollbarReserve = 16f;

        public static void DrawInRect(
            Rect viewport,
            IPaletteSource source,
            GameObject activePrefab,
            Action<GameObject> onItemSelected,
            ref Vector2 scrollPosition)
        {
            if (source == null || viewport.width <= 1f || viewport.height <= 1f)
                return;

            if (!source.IsAvailable)
            {
                EditorGUI.HelpBox(viewport, source.EmptyMessage, MessageType.Info);
                return;
            }

            IReadOnlyList<PaletteItem> items = source.GetItems();
            if (items.Count == 0)
            {
                EditorGUI.HelpBox(viewport, source.EmptyMessage, MessageType.Info);
                return;
            }

            float cellSize = PreviewSize + CellPadding + LabelHeight;
            int columns = ResolveColumnCount(viewport.width, cellSize);
            int validCount = CountValidItems(items);
            int rows = Mathf.Max(1, Mathf.CeilToInt(validCount / (float)columns));
            float contentHeight = rows * cellSize;
            Rect contentRect = new Rect(0f, 0f, viewport.width - 2f, contentHeight);

            scrollPosition = GUI.BeginScrollView(viewport, scrollPosition, contentRect);
            DrawGridAt(items, activePrefab, cellSize, columns, onItemSelected);
            GUI.EndScrollView();
        }

        private static int ResolveColumnCount(float viewportWidth, float cellSize)
        {
            if (cellSize <= 0f)
                return 1;

            float availableWidth = Mathf.Max(cellSize, viewportWidth - ScrollbarReserve);
            return Mathf.Max(1, Mathf.FloorToInt(availableWidth / cellSize));
        }

        private static int CountValidItems(IReadOnlyList<PaletteItem> items)
        {
            int count = 0;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].IsValid)
                    count++;
            }

            return count;
        }

        private static void DrawGridAt(
            IReadOnlyList<PaletteItem> items,
            GameObject activePrefab,
            float cellSize,
            int columns,
            Action<GameObject> onItemSelected)
        {
            int column = 0;
            float x = 0f;
            float y = 0f;

            for (int i = 0; i < items.Count; i++)
            {
                PaletteItem item = items[i];
                if (!item.IsValid)
                    continue;

                Rect cellRect = new Rect(x, y, cellSize, cellSize);
                DrawCellAt(cellRect, item, activePrefab, onItemSelected);

                column++;
                if (column >= columns)
                {
                    column = 0;
                    x = 0f;
                    y += cellSize;
                }
                else
                {
                    x += cellSize;
                }
            }
        }

        private static void DrawCellAt(
            Rect cellRect,
            PaletteItem item,
            GameObject activePrefab,
            Action<GameObject> onItemSelected)
        {
            bool isActive = activePrefab == item.Prefab;

            if (Event.current.type == EventType.Repaint)
            {
                Color background = isActive
                    ? new Color(0.28f, 0.52f, 0.82f, 0.45f)
                    : new Color(0f, 0f, 0f, 0.1f);
                EditorGUI.DrawRect(cellRect, background);

                if (isActive)
                {
                    Color border = new Color(0.35f, 0.65f, 1f, 0.95f);
                    EditorGUI.DrawRect(new Rect(cellRect.x, cellRect.y, cellRect.width, 2f), border);
                    EditorGUI.DrawRect(new Rect(cellRect.x, cellRect.yMax - 2f, cellRect.width, 2f), border);
                    EditorGUI.DrawRect(new Rect(cellRect.x, cellRect.y, 2f, cellRect.height), border);
                    EditorGUI.DrawRect(new Rect(cellRect.xMax - 2f, cellRect.y, 2f, cellRect.height), border);
                }
            }

            Rect previewRect = new Rect(
                cellRect.x + (cellRect.width - PreviewSize) * 0.5f,
                cellRect.y + 3f,
                PreviewSize,
                PreviewSize);

            if (Event.current.type == EventType.Repaint)
            {
                Texture preview = PrefabPreviewRenderer.GetPreview(item.Prefab);
                if (preview != null)
                    GUI.DrawTexture(previewRect, preview, ScaleMode.ScaleToFit);
                else
                    EditorGUI.DrawRect(previewRect, new Color(0f, 0f, 0f, 0.08f));
            }

            Rect labelRect = new Rect(cellRect.x + 1f, previewRect.yMax + 1f, cellRect.width - 2f, LabelHeight);
            GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.UpperCenter,
                wordWrap = false,
                clipping = TextClipping.Clip,
                fontSize = 9
            };
            GUI.Label(labelRect, item.Label, labelStyle);

            if (GUI.Button(cellRect, GUIContent.none, GUIStyle.none))
                onItemSelected?.Invoke(item.Prefab);
        }
    }
}
