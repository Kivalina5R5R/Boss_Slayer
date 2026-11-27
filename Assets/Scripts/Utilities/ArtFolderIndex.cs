using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BossSlayer
{
    // The list of assets in one art folder (Art/UI/Start, Art/BG, Audio/SFX...), each under its file name without the
    // extension. One of these sits in Resources/ArtIndex/ per folder so GameArt can load a folder only when the game
    // first asks for something in it. Made and kept up to date by ArtIndexBuilder (menu Boss Slayer > Rebuild Art Index);
    // never edit one by hand.
    public sealed class ArtFolderIndex : ScriptableObject
    {
        [Serializable]
        sealed class Entry
        {
            [SerializeField] string key;
            [SerializeField] Object asset;

            public string Key => key;
            public Object Asset => asset;
        }

        [SerializeField] List<Entry> entries = new List<Entry>();

        public int Count => entries.Count;

        // The first asset named fileName that is a T, or null.
        public T Find<T>(string fileName) where T : Object
        {
            foreach (var entry in entries)
                if (entry.Key == fileName && entry.Asset is T found) return found;
            return null;
        }
    }
}
