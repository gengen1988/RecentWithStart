using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public class RecentWithStar : EditorWindow
{
    private int _maxHistory;
    private int _currentHistoryIndex;
    private bool _isNavigating;
    private Vector2 _scrollPos;
    private ReorderableList _reorderableStarredList;

    private readonly List<Object> _selectionHistory = new();
    private readonly List<Object> _starredItems = new();

    [MenuItem("Window/Recent With Star")]
    public static void ShowWindow()
    {
        GetWindow<RecentWithStar>();
    }

    private void OnEnable()
    {
        LoadBookmarks();
        _reorderableStarredList = new ReorderableList(
            _starredItems,
            typeof(Object),
            true,
            false,
            false,
            true
        )
        {
            multiSelect = true,
            drawElementCallback = HandleDrawBookmarkElement
        };
        Selection.selectionChanged += HandleSelectionChanged;
        SceneManager.sceneUnloaded += HandleSceneUnloaded;
    }

    private void OnDisable()
    {
        SaveBookmarks();
        _reorderableStarredList = null;
        Selection.selectionChanged -= HandleSelectionChanged;
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
    }

    private void HandleSceneUnloaded(Scene scene)
    {
        // Remove any scene objects from history that might have been part of the unloaded scene
        for (var i = _selectionHistory.Count - 1; i >= 0; i--)
        {
            var obj = _selectionHistory[i];
            // Remove null references or scene objects that no longer exist
            if (!EditorUtility.IsPersistent(obj))
            {
                _selectionHistory.RemoveAt(i);
                // Adjust current index if needed
                if (i <= _currentHistoryIndex && _currentHistoryIndex > 0)
                {
                    _currentHistoryIndex--;
                }
            }
        }

        // Also clean up starred items that might be from the unloaded scene
        for (var i = _starredItems.Count - 1; i >= 0; i--)
        {
            var obj = _starredItems[i];
            if (!EditorUtility.IsPersistent(obj))
            {
                _starredItems.RemoveAt(i);
            }
        }

        // Repaint to reflect changes
        Repaint();
    }

    private void HandleSelectionChanged()
    {
        if (_isNavigating)
        {
            // Skip processing when navigating through history
            // No need to repaint since we're already handling the selection change
            _isNavigating = false;
            return;
        }

        if (!Selection.activeObject)
        {
            return;
        }

        // If we're navigating through history, truncate the forward history
        if (_currentHistoryIndex > 0)
        {
            _selectionHistory.RemoveRange(0, _currentHistoryIndex);
            _currentHistoryIndex = 0;
        }

        // Remove all instances of this object from history to prevent duplicates
        // This handles cases where corrupted data might have multiple entries
        while (_selectionHistory.Contains(Selection.activeObject))
        {
            _selectionHistory.Remove(Selection.activeObject);
        }

        // Add to the beginning of history
        _selectionHistory.Insert(0, Selection.activeObject);

        // remove old records
        while (_selectionHistory.Count > _maxHistory)
        {
            _selectionHistory.RemoveAt(_selectionHistory.Count - 1);
        }

        // Repaint to reflect changes
        Repaint();
    }

    private void OnGUI()
    {
        // Clean up missing assets
        _selectionHistory.RemoveAll(item => item == null);
        _starredItems.RemoveAll(item => item == null);

        // viewport begin
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        // drop zone
        var dropArea = GUILayoutUtility.GetRect(0.0f, 50.0f, GUILayout.ExpandWidth(true));
        var centeredStyle = new GUIStyle(EditorStyles.helpBox) { alignment = TextAnchor.MiddleCenter };
        GUI.Box(dropArea, "Drop new items here", centeredStyle);
        HandleDragAndDrop(dropArea);
        EditorGUILayout.Space();

        // bookmark list
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Starred Items", EditorStyles.boldLabel);

        // if (GUILayout.Button("Save", GUILayout.Width(50)))
        // {
        //     SaveBookmarks();
        // }

        if (GUILayout.Button("Clear", GUILayout.Width(50)))
        {
            _starredItems.Clear();
        }

        EditorGUILayout.EndHorizontal();
        _reorderableStarredList?.DoLayoutList(); // may be null when recompiling

        EditorGUILayout.Space();

        // history list
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Selection History", EditorStyles.boldLabel);

        // Max history input field
        EditorGUILayout.LabelField("Max:", GUILayout.Width(35));
        var newMaxHistory = EditorGUILayout.IntField(_maxHistory, GUILayout.Width(50));
        if (newMaxHistory != _maxHistory && newMaxHistory > 0)
        {
            _maxHistory = newMaxHistory;
            // Trim history if needed
            while (_selectionHistory.Count > _maxHistory)
            {
                _selectionHistory.RemoveAt(_selectionHistory.Count - 1);
            }
        }

        if (GUILayout.Button("Clear", GUILayout.Width(50)))
        {
            _selectionHistory.Clear();
        }

        EditorGUILayout.EndHorizontal();

        foreach (var item in _selectionHistory)
        {
            EditorGUILayout.BeginHorizontal();

            // Add arrow indicator for current navigation index
            if (_selectionHistory.IndexOf(item) == _currentHistoryIndex)
            {
                GUILayout.Label("→", GUILayout.Width(20));
            }
            else
            {
                GUILayout.Label("", GUILayout.Width(20));
            }

            // object entry
            EditorGUILayout.ObjectField(item, typeof(Object), false, GUILayout.ExpandWidth(true));

            // bookmark button
            if (GUILayout.Button("★", GUILayout.Width(20)))
            {
                if (!_starredItems.Contains(item))
                {
                    _starredItems.Add(item);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space();

        // viewport end
        EditorGUILayout.EndScrollView();
    }

    private void HandleDrawBookmarkElement(Rect rect, int index, bool isActive, bool isFocused)
    {
        var item = _starredItems[index];
        rect.y += 2;
        rect.height = EditorGUIUtility.singleLineHeight;
        var fieldRect = new Rect(rect.x, rect.y, rect.width, rect.height);
        EditorGUI.ObjectField(fieldRect, item, typeof(Object), false);
    }

    private void HandleDragAndDrop(Rect dropArea)
    {
        var evt = Event.current;
        switch (evt.type)
        {
            case EventType.DragUpdated:
            case EventType.DragPerform:
                if (!dropArea.Contains(evt.mousePosition))
                {
                    return;
                }

                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();

                    foreach (var draggedObject in DragAndDrop.objectReferences)
                    {
                        if (!AssetDatabase.Contains(draggedObject))
                        {
                            continue;
                        }

                        if (_selectionHistory.Contains(draggedObject))
                        {
                            _selectionHistory.Remove(draggedObject);
                        }

                        _selectionHistory.Insert(0, draggedObject);
                        if (!_starredItems.Contains(draggedObject))
                        {
                            _starredItems.Add(draggedObject);
                        }
                    }
                }

                Event.current.Use();
                break;
        }
    }

    private void SaveBookmarks()
    {
        // Save max history setting
        EditorPrefs.SetInt("RecentWithStar_MaxHistory", _maxHistory);

        // Save starred items
        var validPaths = _starredItems
            .Where(item => item != null)
            .Select(AssetDatabase.GetAssetPath)
            .Where(path => !string.IsNullOrEmpty(path)) // do not persist scene objects
            .ToList();

        // Convert list to JSON and save
        var json = JsonUtility.ToJson(new BookmarkData { paths = validPaths });
        EditorPrefs.SetString("RecentWithStar_StarredItems", json);
    }

    private void LoadBookmarks()
    {
        // Load max history setting
        _maxHistory = EditorPrefs.GetInt("RecentWithStar_MaxHistory", 10);

        // Load starred items
        _starredItems.Clear();
        if (EditorPrefs.HasKey("RecentWithStar_StarredItems"))
        {
            var json = EditorPrefs.GetString("RecentWithStar_StarredItems");
            var data = JsonUtility.FromJson<BookmarkData>(json);

            foreach (var path in data.paths)
            {
                var item = AssetDatabase.LoadAssetAtPath<Object>(path);
                if (item)
                {
                    _starredItems.Add(item);
                }
            }
        }
    }

    [MenuItem("Edit/Selection/Back _M4")]
    private static void Back()
    {
        var window = GetWindow<RecentWithStar>();
        if (window._selectionHistory.Count > 0 && window._currentHistoryIndex > 0)
        {
            window._isNavigating = true;
            window._currentHistoryIndex--;
            Selection.activeObject = window._selectionHistory[window._currentHistoryIndex];
            EditorGUIUtility.PingObject(Selection.activeObject);
        }
    }

    [MenuItem("Edit/Selection/Forward _M3")]
    private static void Forward()
    {
        var window = GetWindow<RecentWithStar>();
        if (window._selectionHistory.Count > 0 && window._currentHistoryIndex < window._selectionHistory.Count - 1)
        {
            window._isNavigating = true;
            window._currentHistoryIndex++;
            Selection.activeObject = window._selectionHistory[window._currentHistoryIndex];
            EditorGUIUtility.PingObject(Selection.activeObject);
        }
    }
}


[Serializable]
public class BookmarkData
{
    public List<string> paths;
}