using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BossSlayer.EditorTools
{
    // Keeps Resources/ArtIndex/ in step with the art folders GameArt loads from: one ArtFolderIndex per folder, listing
    // every asset in it by file name. Runs by itself whenever a file in those folders is added, moved, renamed or deleted,
    // and from the menu Boss Slayer > Rebuild Art Index.
    public sealed class ArtIndexBuilder : AssetPostprocessor
    {
        const string IndexRoot = "Assets/Resources/" + GameArt.IndexFolder;

        // The folders (and everything under them) that code loads with GameArt.
        static readonly string[] Roots =
        {
            "Assets/Art/UI", "Assets/Art/BG", "Assets/Art/Players", "Assets/Art/Shaders",
            "Assets/Audio", "Assets/Music", "Assets/VDO", "Assets/Fonts", "Assets/Prefab/Bosses", "Assets/Prefab/UI",
        };

        static bool queued;

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (queued) return;
            if (!imported.Concat(deleted).Concat(moved).Concat(movedFrom).Any(IsArtPath)) return;
            queued = true;
            EditorApplication.delayCall += () => { queued = false; Rebuild(); };
        }

        [MenuItem("Boss Slayer/Rebuild Art Index")]
        public static void Rebuild()
        {
            var wanted = new HashSet<string>();
            foreach (var root in Roots)
            {
                if (!AssetDatabase.IsValidFolder(root)) continue;
                foreach (var folder in FoldersUnder(root))
                {
                    var assets = AssetsIn(folder);
                    if (assets.Count == 0) continue;
                    string indexPath = IndexRoot + folder.Substring("Assets/".Length) + ".asset";
                    wanted.Add(indexPath);
                    Write(indexPath, assets);
                }
            }
            RemoveStale(wanted);
            AssetDatabase.SaveAssets();
        }

        static bool IsArtPath(string path) =>
            !path.StartsWith(IndexRoot) && Roots.Any(root => path == root || path.StartsWith(root + "/"));

        static IEnumerable<string> FoldersUnder(string root)
        {
            yield return root;
            foreach (var sub in AssetDatabase.GetSubFolders(root))
                foreach (var folder in FoldersUnder(sub)) yield return folder;
        }

        // The main asset of every file directly inside folder, sorted by path so the index text stays stable.
        static List<(string key, Object asset)> AssetsIn(string folder)
        {
            var list = new List<(string, Object)>();
            string full = Path.GetFullPath(folder);
            foreach (var file in Directory.GetFiles(full).OrderBy(f => f, System.StringComparer.Ordinal))
            {
                if (file.EndsWith(".meta") || file.EndsWith(".cs")) continue;
                string path = folder + "/" + Path.GetFileName(file);
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset != null) list.Add((Path.GetFileNameWithoutExtension(file), asset));
            }
            return list;
        }

        static void Write(string indexPath, List<(string key, Object asset)> assets)
        {
            var index = AssetDatabase.LoadAssetAtPath<ArtFolderIndex>(indexPath);
            if (index == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(indexPath));
                index = ScriptableObject.CreateInstance<ArtFolderIndex>();
                AssetDatabase.CreateAsset(index, indexPath);
            }
            var serialized = new SerializedObject(index);
            var entries = serialized.FindProperty("entries");
            bool same = entries.arraySize == assets.Count;
            for (int i = 0; same && i < assets.Count; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                same = entry.FindPropertyRelative("key").stringValue == assets[i].key
                    && entry.FindPropertyRelative("asset").objectReferenceValue == assets[i].asset;
            }
            if (same) return;
            entries.arraySize = assets.Count;
            for (int i = 0; i < assets.Count; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("key").stringValue = assets[i].key;
                entry.FindPropertyRelative("asset").objectReferenceValue = assets[i].asset;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(index);
        }

        static void RemoveStale(HashSet<string> wanted)
        {
            if (!AssetDatabase.IsValidFolder(IndexRoot.TrimEnd('/'))) return;
            foreach (var guid in AssetDatabase.FindAssets("t:ArtFolderIndex", new[] { IndexRoot.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!wanted.Contains(path)) AssetDatabase.DeleteAsset(path);
            }
        }
    }
}
