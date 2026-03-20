/*
Copyright (c) 2026 Xavier Arpa López Thomas Peter ('xavierarpa')

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace SceneAddMenuInator
{
    internal sealed class SceneAddMenuInatorWindow : EditorWindow
    {
        [MenuItem("Tools/SceneAddMenuInator")]
        private static void ShowWindow()
        {
            var wnd = GetWindow<SceneAddMenuInatorWindow>("SceneAddMenuInator");
            wnd.minSize = new Vector2(500, 600);
        }

        #region Data

        [Serializable]
        private class EntryConfig
        {
            public string relativePath = "";
            public string displayName = "";
            public int priorityOffset;
            public bool excluded;
        }

        [Serializable]
        private class Config
        {
            public string sourceFolderPath = "";
            public string menuBasePath = "Scenes/";
            public string menuName = "";
            public int basePriority;
            public int separatorGap = 11;
            public string namespaceName = "";
            public string className = "GeneratedSceneMenu";
            public string outputFolderPath = "";
            public List<EntryConfig> entries = new();
        }

        private struct EntryView
        {
            public int configIndex;
            public string effectiveName;
            public string menuItemPath;
            public string methodName;
            public string group;
            public string collapsedDir;
            public int finalPriority;
            public int depth;
        }

        #endregion

        private Config config = new();
        private DefaultAsset sourceFolder;
        private DefaultAsset outputFolder;
        private readonly List<EntryView> views = new();
        private readonly List<string> groups = new();
        private Vector2 entriesScroll;
        private Vector2 previewScroll;
        private bool needsRebuild = true;
        private bool needsSave;

        private string PrefsKey => "SceneAddMenuInator_" + Application.dataPath.GetHashCode();

        #region Lifecycle

        private void OnEnable()
        {
            LoadConfig();
            needsRebuild = true;
        }

        private void OnDisable()
        {
            PersistConfig();
        }

        private void LoadConfig()
        {
            string json = EditorPrefs.GetString(PrefsKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                config = JsonUtility.FromJson<Config>(json);
            }

            if (!string.IsNullOrEmpty(config.sourceFolderPath))
            {
                sourceFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(config.sourceFolderPath);
            }

            if (!string.IsNullOrEmpty(config.outputFolderPath))
            {
                outputFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(config.outputFolderPath);
            }
        }

        private void PersistConfig()
        {
            config.sourceFolderPath = sourceFolder != null ? AssetDatabase.GetAssetPath(sourceFolder) : "";
            config.outputFolderPath = outputFolder != null ? AssetDatabase.GetAssetPath(outputFolder) : "";
            EditorPrefs.SetString(PrefsKey, JsonUtility.ToJson(config));
        }

        #endregion

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            GUILayout.Label("SceneAddMenuInator", new GUIStyle(EditorStyles.boldLabel) { fontSize = 15 });
            EditorGUILayout.LabelField("Genera scripts de MenuItem para carga aditiva de escenas.", EditorStyles.miniLabel);

            EditorGUILayout.Space(8);
            DrawSource();
            EditorGUILayout.Space(4);
            DrawMenuSettings();
            EditorGUILayout.Space(4);
            DrawOutput();
            EditorGUILayout.Space(8);

            if (needsRebuild)
            {
                ScanAndMerge();
                RebuildViews();
                needsRebuild = false;
            }

            DrawEntries();
            EditorGUILayout.Space(4);
            DrawPreview();
            EditorGUILayout.Space(8);
            DrawActions();

            if (needsSave)
            {
                PersistConfig();
                needsSave = false;
            }
        }

        #region Draw

        private void DrawSource()
        {
            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            sourceFolder = (DefaultAsset)EditorGUILayout.ObjectField("Scene Folder", sourceFolder, typeof(DefaultAsset), false);
            if (EditorGUI.EndChangeCheck())
            {
                needsRebuild = true;
                needsSave = true;
            }
        }

        private void DrawMenuSettings()
        {
            EditorGUILayout.LabelField("Menu", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();

            config.menuBasePath = EditorGUILayout.TextField("Base Path", config.menuBasePath);
            config.menuName = EditorGUILayout.TextField("Menu Name", config.menuName);
            config.basePriority = EditorGUILayout.IntField("Priority", config.basePriority);
            config.separatorGap = Mathf.Max(0, EditorGUILayout.IntField("Separator Gap", config.separatorGap));

            if (EditorGUI.EndChangeCheck())
            {
                RebuildViews();
                needsSave = true;
            }

            if (config.separatorGap >= 10)
            {
                EditorGUILayout.HelpBox("Se insertarán separadores entre grupos de primer nivel.", MessageType.Info);
            }
        }

        private void DrawOutput()
        {
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();

            config.namespaceName = EditorGUILayout.TextField("Namespace", config.namespaceName);
            config.className = EditorGUILayout.TextField("Class Name", config.className);
            outputFolder = (DefaultAsset)EditorGUILayout.ObjectField("Output Folder", outputFolder, typeof(DefaultAsset), false);

            if (EditorGUI.EndChangeCheck())
            {
                needsSave = true;
            }

            string outPath = outputFolder != null ? AssetDatabase.GetAssetPath(outputFolder) : "Assets";
            EditorGUILayout.LabelField("  →", $"{outPath}/{config.className}.cs", EditorStyles.miniLabel);
        }

        private void DrawEntries()
        {
            int activeCount = config.entries.Count(e => !e.excluded);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Entries ({activeCount}/{config.entries.Count})", EditorStyles.boldLabel);
            if (GUILayout.Button("Refresh", GUILayout.Width(60)))
            {
                ClearSelectionAndFocus();
                needsRebuild = true;
            }
            EditorGUILayout.EndHorizontal();

            if (config.entries.Count == 0)
            {
                EditorGUILayout.HelpBox("Selecciona una carpeta con archivos .unity (escenas).", MessageType.Info);
                return;
            }

            entriesScroll = EditorGUILayout.BeginScrollView(entriesScroll, EditorStyles.helpBox, GUILayout.MaxHeight(250));

            bool changed = false;
            string currentGroup = null;

            foreach (var view in views)
            {
                var entry = config.entries[view.configIndex];

                if (view.group != currentGroup)
                {
                    currentGroup = view.group;
                    string label = string.IsNullOrEmpty(view.group) ? "(root)" : view.group;
                    EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
                }

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.BeginHorizontal();

                bool enabled = !entry.excluded;
                enabled = EditorGUILayout.Toggle(enabled, GUILayout.Width(16));
                entry.excluded = !enabled;

                string placeholder = Path.GetFileName(entry.relativePath);
                entry.displayName = EditorGUILayout.TextField(entry.displayName);

                if (string.IsNullOrEmpty(entry.displayName) && Event.current.type == EventType.Repaint)
                {
                    Rect rect = GUILayoutUtility.GetLastRect();
                    rect.x += 3;
                    var style = new GUIStyle(EditorStyles.label)
                    {
                        fontStyle = FontStyle.Italic,
                        normal = { textColor = new Color(1f, 1f, 1f, 0.3f) }
                    };
                    GUI.Label(rect, placeholder, style);
                }

                EditorGUILayout.LabelField("+", GUILayout.Width(10));
                entry.priorityOffset = EditorGUILayout.IntField(entry.priorityOffset, GUILayout.Width(35));

                EditorGUILayout.EndHorizontal();

                if (EditorGUI.EndChangeCheck())
                {
                    changed = true;
                }
            }

            EditorGUILayout.EndScrollView();

            if (changed)
            {
                needsSave = true;
                Repaint();
            }
        }

        private void DrawPreview()
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            var activeViews = views.Where(v => !config.entries[v.configIndex].excluded).ToList();

            if (activeViews.Count == 0)
            {
                EditorGUILayout.HelpBox("No hay entradas activas.", MessageType.Info);
                return;
            }

            previewScroll = EditorGUILayout.BeginScrollView(previewScroll, EditorStyles.helpBox, GUILayout.MaxHeight(180));

            string effectiveName = GetEffectiveName();
            string basePath = config.menuBasePath.TrimEnd('/') + "/";
            EditorGUILayout.LabelField(basePath + effectiveName, EditorStyles.boldLabel);

            string lastGroup = null;
            foreach (var v in activeViews)
            {
                if (config.separatorGap >= 10 && v.group != lastGroup && lastGroup != null)
                {
                    GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1));
                }
                lastGroup = v.group;

                var entry = config.entries[v.configIndex];
                string name = string.IsNullOrEmpty(entry.displayName)
                    ? Path.GetFileName(entry.relativePath)
                    : entry.displayName;
                string indent = new(' ', (v.depth + 1) * 3);
                EditorGUILayout.LabelField($"{indent}{name}");
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawActions()
        {
            int activeCount = views.Count(v => !config.entries[v.configIndex].excluded);
            bool valid = activeCount > 0 && !string.IsNullOrWhiteSpace(config.className);

            EditorGUI.BeginDisabledGroup(!valid);

            if (GUILayout.Button("Generate Script", GUILayout.Height(30)))
            {
                SaveScript();
            }

            EditorGUI.EndDisabledGroup();
        }

        #endregion

        #region Logic

        private string GetEffectiveName()
        {
            if (!string.IsNullOrWhiteSpace(config.menuName))
            {
                return config.menuName.Trim();
            }

            if (sourceFolder != null)
            {
                return Path.GetFileName(AssetDatabase.GetAssetPath(sourceFolder));
            }

            return "Menu";
        }

        private static void ClearSelectionAndFocus()
        {
            GUI.FocusControl(null);
            EditorGUI.FocusTextInControl(string.Empty);
            GUIUtility.keyboardControl = 0;
        }

        private void ScanAndMerge()
        {
            if (sourceFolder == null)
            {
                config.entries.Clear();
                return;
            }

            string folderPath = AssetDatabase.GetAssetPath(sourceFolder);
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                config.entries.Clear();
                return;
            }

            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string absFolder = Path.GetFullPath(Path.Combine(projectRoot, folderPath));

            if (!Directory.Exists(absFolder))
            {
                config.entries.Clear();
                return;
            }

            string[] files = Directory.GetFiles(absFolder, "*.unity", SearchOption.AllDirectories);
            var foundPaths = new HashSet<string>();

            foreach (string file in files)
            {
                string absFile = Path.GetFullPath(file);
                string relative = absFile[(absFolder.Length + 1)..].Replace('\\', '/').Replace(".unity", "");
                foundPaths.Add(relative);
            }

            config.entries.RemoveAll(e => !foundPaths.Contains(e.relativePath));

            var existing = new HashSet<string>(config.entries.Select(e => e.relativePath));
            foreach (string path in foundPaths.OrderBy(p => p))
            {
                if (!existing.Contains(path))
                {
                    config.entries.Add(new EntryConfig { relativePath = path });
                }
            }
        }

        private void RebuildViews()
        {
            views.Clear();
            groups.Clear();

            string effectiveName = GetEffectiveName();
            string fullMenuRoot = config.menuBasePath.TrimEnd('/') + "/" + effectiveName + "/";

            var collapsedDirs = ComputeCollapsedDirs();

            for (int i = 0; i < config.entries.Count; i++)
            {
                var entry = config.entries[i];

                string fileName = Path.GetFileName(entry.relativePath);
                string displayName = string.IsNullOrEmpty(entry.displayName) ? fileName : entry.displayName;

                int slashIdx = entry.relativePath.IndexOf('/');
                string group = slashIdx >= 0 ? entry.relativePath[..slashIdx] : "";

                if (!groups.Contains(group))
                {
                    groups.Add(group);
                }

                string collapsed = collapsedDirs[i];
                string menuPath = string.IsNullOrEmpty(collapsed)
                    ? fullMenuRoot + displayName
                    : fullMenuRoot + collapsed + "/" + displayName;

                int groupIndex = groups.IndexOf(group);
                int gap = config.separatorGap > 0 ? config.separatorGap : 1;
                int finalPriority = config.basePriority + groupIndex * gap + entry.priorityOffset;

                views.Add(new EntryView
                {
                    configIndex = i,
                    effectiveName = displayName,
                    menuItemPath = menuPath,
                    methodName = Sanitize(entry.relativePath),
                    group = group,
                    collapsedDir = collapsed,
                    finalPriority = finalPriority,
                    depth = collapsed.Count(c => c == '/') + (string.IsNullOrEmpty(collapsed) ? 0 : 1),
                });
            }

            views.Sort((a, b) =>
            {
                int cmp = a.finalPriority.CompareTo(b.finalPriority);
                return cmp != 0 ? cmp : string.Compare(a.menuItemPath, b.menuItemPath, StringComparison.Ordinal);
            });
        }

        private string[] ComputeCollapsedDirs()
        {
            var dirParts = new string[config.entries.Count];
            for (int i = 0; i < config.entries.Count; i++)
            {
                dirParts[i] = Path.GetDirectoryName(config.entries[i].relativePath)?.Replace('\\', '/') ?? "";
            }

            bool changed = true;
            while (changed)
            {
                changed = false;
                var childCounts = new Dictionary<string, int>();

                for (int i = 0; i < config.entries.Count; i++)
                {
                    if (config.entries[i].excluded) { continue; }

                    string dir = dirParts[i];
                    if (string.IsNullOrEmpty(dir)) { continue; }

                    if (!childCounts.ContainsKey(dir))
                    {
                        childCounts[dir] = CountDirectChildren(dir, dirParts);
                    }
                }

                for (int i = 0; i < config.entries.Count; i++)
                {
                    string dir = dirParts[i];
                    if (string.IsNullOrEmpty(dir)) { continue; }

                    if (childCounts.TryGetValue(dir, out int count) && count <= 1)
                    {
                        int lastSlash = dir.LastIndexOf('/');
                        dirParts[i] = lastSlash >= 0 ? dir[..lastSlash] : "";
                        changed = true;
                    }
                }
            }

            return dirParts;
        }

        private int CountDirectChildren(string dir, string[] allDirParts)
        {
            var children = new HashSet<string>();
            string prefix = dir + "/";

            for (int i = 0; i < config.entries.Count; i++)
            {
                if (config.entries[i].excluded) { continue; }

                if (allDirParts[i] == dir)
                {
                    children.Add("F:" + i);
                }
                else if (allDirParts[i].StartsWith(prefix))
                {
                    string remainder = allDirParts[i][prefix.Length..];
                    int slash = remainder.IndexOf('/');
                    string subdir = slash >= 0 ? remainder[..slash] : remainder;
                    children.Add("D:" + subdir);
                }
            }

            return children.Count;
        }

        private string GenerateCode()
        {
            if (sourceFolder == null)
            {
                return "";
            }

            string folderPath = AssetDatabase.GetAssetPath(sourceFolder);
            string effectiveName = GetEffectiveName();
            string fullMenuRoot = config.menuBasePath.TrimEnd('/') + "/" + effectiveName + "/";

            var activeViews = views.Where(v => !config.entries[v.configIndex].excluded).ToList();
            if (activeViews.Count == 0)
            {
                return "";
            }

            var sb = new StringBuilder();
            bool hasNs = !string.IsNullOrWhiteSpace(config.namespaceName);
            string t = hasNs ? "    " : "";

            sb.AppendLine("// <auto-generated by SceneAddMenuInator>");
            sb.AppendLine("using UnityEditor;");
            sb.AppendLine("using UnityEditor.SceneManagement;");
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();

            if (hasNs)
            {
                sb.AppendLine($"namespace {config.namespaceName.Trim()}");
                sb.AppendLine("{");
            }

            sb.AppendLine($"{t}internal static class {config.className}");
            sb.AppendLine($"{t}{{");

            sb.AppendLine($"{t}    private const string MenuPath = \"{fullMenuRoot}\";");
            sb.AppendLine($"{t}    private const string ScenesPath = \"{folderPath}/\";");
            sb.AppendLine($"{t}    private const int BasePriority = {config.basePriority};");

            var subfolders = groups.Where(g => g.Length > 0).ToList();
            foreach (string folder in subfolders)
            {
                string constName = "MenuPath_" + Sanitize(folder);
                sb.AppendLine($"{t}    private const string {constName} = MenuPath + \"{folder}/\";");
            }

            sb.AppendLine();

            string currentGroup = null;
            foreach (var v in activeViews)
            {
                var entry = config.entries[v.configIndex];

                if (v.group != currentGroup)
                {
                    if (currentGroup != null)
                    {
                        sb.AppendLine();
                    }
                    currentGroup = v.group;
                }

                string displayName = v.effectiveName;
                string menuExpr;

                if (string.IsNullOrEmpty(v.collapsedDir))
                {
                    menuExpr = $"MenuPath + \"{displayName}\"";
                }
                else if (v.collapsedDir == v.group)
                {
                    string constName = "MenuPath_" + Sanitize(v.group);
                    menuExpr = $"{constName} + \"{displayName}\"";
                }
                else
                {
                    menuExpr = $"MenuPath + \"{v.collapsedDir}/{displayName}\"";
                }

                int offset = v.finalPriority - config.basePriority;
                string priExpr = offset == 0 ? "BasePriority" : $"BasePriority + {offset}";

                sb.AppendLine($"{t}    [MenuItem({menuExpr}, false, {priExpr})]");
                sb.AppendLine($"{t}    private static void {v.methodName}() => LoadSceneAdditive(\"{entry.relativePath}\");");
            }

            sb.AppendLine();
            AppendHelpers(sb, t);
            sb.AppendLine($"{t}}}");

            if (hasNs)
            {
                sb.AppendLine("}");
            }

            return sb.ToString();
        }

        private static void AppendHelpers(StringBuilder sb, string t)
        {
            sb.AppendLine($"{t}    private static void LoadSceneAdditive(string sceneName)");
            sb.AppendLine($"{t}    {{");
            sb.AppendLine($"{t}        string path = ScenesPath + sceneName + \".unity\";");
            sb.AppendLine($"{t}        SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);");
            sb.AppendLine();
            sb.AppendLine($"{t}        if (sceneAsset == null)");
            sb.AppendLine($"{t}        {{");
            sb.AppendLine($"{t}            Debug.LogError($\"Scene not found at path: {{path}}\");");
            sb.AppendLine($"{t}            return;");
            sb.AppendLine($"{t}        }}");
            sb.AppendLine();
            sb.AppendLine($"{t}        if (EditorApplication.isPlaying)");
            sb.AppendLine($"{t}        {{");
            sb.AppendLine($"{t}            Debug.LogWarning(\"Cannot load scenes additively in Play mode from this menu.\");");
            sb.AppendLine($"{t}            return;");
            sb.AppendLine($"{t}        }}");
            sb.AppendLine();
            sb.AppendLine($"{t}        EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);");
            sb.AppendLine($"{t}    }}");
        }

        private void SaveScript()
        {
            string code = GenerateCode();
            if (string.IsNullOrEmpty(code))
            {
                return;
            }

            string outPath = outputFolder != null
                ? AssetDatabase.GetAssetPath(outputFolder)
                : "Assets";

            string filePath = $"{outPath}/{config.className}.cs";
            string fullPath = Path.GetFullPath(filePath);

            if (File.Exists(fullPath))
            {
                if (!EditorUtility.DisplayDialog(
                    "Overwrite?",
                    $"Ya existe:\n{filePath}\n\n¿Sobreescribir?",
                    "Sí", "Cancelar"))
                {
                    return;
                }
            }

            File.WriteAllText(fullPath, code, Encoding.UTF8);
            AssetDatabase.Refresh();

            var asset = AssetDatabase.LoadAssetAtPath<MonoScript>(filePath);
            if (asset != null)
            {
                EditorGUIUtility.PingObject(asset);
            }

            int count = views.Count(v => !config.entries[v.configIndex].excluded);
            Debug.Log($"SceneAddMenuInator: {filePath} ({count} items)");
        }

        private static string Sanitize(string path)
        {
            string result = Regex.Replace(path, @"[^a-zA-Z0-9]", "_");
            result = Regex.Replace(result, @"_+", "_");
            result = result.Trim('_');
            if (result.Length > 0 && char.IsDigit(result[0]))
            {
                result = "_" + result;
            }
            return result;
        }

        #endregion
    }
}
