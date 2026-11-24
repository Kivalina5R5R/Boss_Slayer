using System.Collections.Generic;
using UnityEngine;

namespace Deepspace
{
    // 2026-11-19: loads Resources assets in the background (Resources.LoadAsync) and keeps them referenced, so a later
    // Resources.Load of the same path returns at once instead of reading and decoding the file on the frame a screen
    // changes. Missing paths are simply skipped.
    public sealed class ResourcePreloader
    {
        readonly List<ResourceRequest> requests = new List<ResourceRequest>();

        public ResourcePreloader(IEnumerable<string> paths)
        {
            foreach (var path in paths) requests.Add(Resources.LoadAsync(path));
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
