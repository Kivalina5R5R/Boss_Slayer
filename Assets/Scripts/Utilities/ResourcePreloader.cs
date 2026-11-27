using System.Collections.Generic;
using UnityEngine;

namespace BossSlayer
{
    // 2026-11-19: loads art in the background and keeps it referenced, so a later load of the same path returns at once
    // instead of reading and decoding the file on the frame a screen changes. 2026-11-26: art moved out of Resources;
    // this now loads each asked-for path's folder index (GameArt), once per folder. Missing paths are simply skipped.
    public sealed class ResourcePreloader
    {
        readonly List<ResourceRequest> requests = new List<ResourceRequest>();

        public ResourcePreloader(IEnumerable<string> paths)
        {
            var started = new HashSet<string>();
            foreach (var path in paths)
            {
                int slash = path.LastIndexOf('/');
                if (!started.Add(slash < 0 ? "" : path.Substring(0, slash))) continue;
                var request = GameArt.PreloadFolderOf(path);
                if (request != null) requests.Add(request);
            }
        }

        public bool IsDone
        {
            get
            {
                foreach (var request in requests) if (!request.isDone) return false;
                return true;
            }
        }

        public int LoadedCount
        {
            get
            {
                int loaded = 0;
                foreach (var request in requests) if (request.isDone && request.asset != null) loaded++;
                return loaded;
            }
        }

        public int Count => requests.Count;
    }
}
