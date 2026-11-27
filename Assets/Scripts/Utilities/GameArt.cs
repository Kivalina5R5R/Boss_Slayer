using System.Collections.Generic;
using UnityEngine;

namespace BossSlayer
{
    // 2026-11-26: the game's art, sounds and videos live in Art/, Audio/, Music/, VDO/, Fonts/ and Prefab/ (sorted like
    // FoodIsekaiZ) instead of Resources. Code loads them by their path under Assets without the extension, for example
    // GameArt.Load<Texture2D>("Art/UI/Start/Logo"). Each folder has a small index in Resources/ArtIndex/, loaded the first
    // time something in that folder is asked for, so art still loads folder by folder instead of all at start.
    // A missing path returns null, like Resources.Load did.
    public static class GameArt
    {
        public const string IndexFolder = "ArtIndex/";

        static readonly Dictionary<string, ArtFolderIndex> folders = new Dictionary<string, ArtFolderIndex>();

        public static T Load<T>(string path) where T : Object
        {
            if (string.IsNullOrEmpty(path)) return null;
            Split(path, out var folder, out var fileName);
            var index = Folder(folder);
            return index == null ? null : index.Find<T>(fileName);
        }

        // Starts loading the folder that holds path in the background; a later Load from that folder is then instant.
        // Returns null when that folder is already loaded or has no index.
        public static ResourceRequest PreloadFolderOf(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            Split(path, out var folder, out _);
            if (folders.ContainsKey(folder)) return null;
            return Resources.LoadAsync<ArtFolderIndex>(IndexFolder + folder);
        }

        static ArtFolderIndex Folder(string folder)
        {
            if (folders.TryGetValue(folder, out var index)) return index;
            index = Resources.Load<ArtFolderIndex>(IndexFolder + folder);
            folders[folder] = index;
            return index;
        }

        static void Split(string path, out string folder, out string fileName)
        {
            int slash = path.LastIndexOf('/');
            folder = slash < 0 ? "" : path.Substring(0, slash);
            fileName = slash < 0 ? path : path.Substring(slash + 1);
        }
    }
}
