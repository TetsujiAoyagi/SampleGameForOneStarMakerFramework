#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OneStarMaker.Runtime.DebugCommands
{
    /// <summary>
    /// ロード済みシーンを、コマンドの呼び出し中だけ preorder で歩く。
    /// バッファは呼び出しの finally で空にし、シーンオブジェクトを次のコマンドまで持たない。
    /// 生成したスレッド以外ではシーン API を呼ばない。
    /// </summary>
    public sealed class UnityDebugGameObjectWorld : IDebugGameObjectWorld
    {
        private readonly DebugGameObjectThreadGate _gate = new();
        private readonly List<GameObject> _roots = new(32);
        private readonly List<WalkFrame> _stack = new(64);
        private readonly List<string> _pathNames = new(8);

        private VisitMode _mode;
        private ulong _findId;
        private Transform? _found;
        private int _offset;
        private int _limit;
        private int _seen;
        private List<DebugGameObjectRow>? _page;
        private bool _hasMore;
        private bool _stop;

        public DebugGameObjectReadStatus TryDescribe(ulong instanceId, out DebugGameObjectRow row, out string failure)
        {
            row = default;
            failure = string.Empty;
            if (!_gate.AllowsCaller)
            {
                failure = "wrong-thread";
                return DebugGameObjectReadStatus.Unavailable;
            }

            try
            {
                if (!TryFind(instanceId, out var found) || found == null)
                {
                    return DebugGameObjectReadStatus.NotFound;
                }

                row = CreateRow(found);
                return DebugGameObjectReadStatus.Ok;
            }
            finally
            {
                ReleaseSceneReferences();
            }
        }

        public DebugGameObjectReadStatus TryCollectPage(
            int offset,
            int limit,
            List<DebugGameObjectRow> destination,
            out bool hasMore,
            out string failure)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            destination.Clear();
            hasMore = false;
            failure = string.Empty;
            if (!_gate.AllowsCaller)
            {
                failure = "wrong-thread";
                return DebugGameObjectReadStatus.Unavailable;
            }

            try
            {
                _mode = VisitMode.Page;
                _offset = offset;
                _limit = limit;
                _seen = 0;
                _page = destination;
                _hasMore = false;
                _stop = false;
                WalkScenes();
                hasMore = _hasMore;
                return DebugGameObjectReadStatus.Ok;
            }
            finally
            {
                _page = null;
                ReleaseSceneReferences();
            }
        }

        private bool TryFind(ulong instanceId, out Transform? found)
        {
            _mode = VisitMode.Find;
            _findId = instanceId;
            _found = null;
            _stop = false;
            WalkScenes();
            found = _found;
            return found != null;
        }

        private void WalkScenes()
        {
            var sceneCount = SceneManager.sceneCount;
            for (var sceneIndex = 0; sceneIndex < sceneCount && !_stop; sceneIndex++)
            {
                var scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    continue;
                }

                _roots.Clear();
                scene.GetRootGameObjects(_roots);
                for (var rootIndex = 0; rootIndex < _roots.Count && !_stop; rootIndex++)
                {
                    var root = _roots[rootIndex];
                    if (root == null)
                    {
                        continue;
                    }

                    WalkRoot(root.transform);
                }
            }
        }

        private void WalkRoot(Transform root)
        {
            _stack.Clear();
            _stack.Add(new WalkFrame(root, -1));
            while (_stack.Count > 0 && !_stop)
            {
                var index = _stack.Count - 1;
                var frame = _stack[index];
                var transform = frame.Transform;
                if (transform == null)
                {
                    _stack.RemoveAt(index);
                    continue;
                }

                var gameObject = transform.gameObject;
                if (gameObject == null || IsExcluded(gameObject))
                {
                    _stack.RemoveAt(index);
                    continue;
                }

                if (frame.NextChild < 0)
                {
                    frame.NextChild = 0;
                    _stack[index] = frame;
                    if (!Accept(transform))
                    {
                        _stop = true;
                        return;
                    }
                }

                if (frame.NextChild < transform.childCount)
                {
                    var child = transform.GetChild(frame.NextChild);
                    frame.NextChild++;
                    _stack[index] = frame;
                    if (child != null)
                    {
                        _stack.Add(new WalkFrame(child, -1));
                    }
                }
                else
                {
                    _stack.RemoveAt(index);
                }
            }
        }

        private bool Accept(Transform transform)
        {
            if (_mode == VisitMode.Find)
            {
                var gameObject = transform.gameObject;
                if (gameObject != null && EntityId.ToULong(gameObject.GetEntityId()) == _findId)
                {
                    _found = transform;
                    return false;
                }

                return true;
            }

            if (_seen < _offset)
            {
                _seen++;
                return true;
            }

            if (_page != null && _page.Count < _limit)
            {
                _page.Add(CreateRow(transform));
                _seen++;
                return true;
            }

            _hasMore = true;
            return false;
        }

        private DebugGameObjectRow CreateRow(Transform transform)
        {
            var gameObject = transform.gameObject;
            var hasParent = false;
            var parentId = 0UL;
            var parent = transform.parent;
            if (parent != null)
            {
                var parentObject = parent.gameObject;
                if (parentObject != null)
                {
                    hasParent = true;
                    parentId = EntityId.ToULong(parentObject.GetEntityId());
                }
            }
            return new DebugGameObjectRow(
                EntityId.ToULong(gameObject.GetEntityId()),
                hasParent,
                parentId,
                gameObject.name,
                gameObject.scene.name,
                BuildPath(transform),
                gameObject.activeSelf,
                gameObject.activeInHierarchy,
                transform.GetSiblingIndex(),
                transform.childCount);
        }

        private string BuildPath(Transform transform)
        {
            _pathNames.Clear();
            var node = transform;
            while (node != null && _pathNames.Count <= DebugGameObjectCommands.MaxDisplaySegments)
            {
                var gameObject = node.gameObject;
                _pathNames.Add(gameObject == null ? string.Empty : gameObject.name);
                node = node.parent;
            }

            return DebugGameObjectCommands.FormatDisplayPath(_pathNames);
        }

        private void ReleaseSceneReferences()
        {
            _found = null;
            _page = null;
            _stack.Clear();
            _roots.Clear();
            _pathNames.Clear();
            _stop = false;
        }

        private static bool IsExcluded(GameObject gameObject)
        {
            return (gameObject.hideFlags & HideFlags.HideAndDontSave) == HideFlags.HideAndDontSave;
        }

        private enum VisitMode
        {
            Find = 0,
            Page = 1,
        }

        private struct WalkFrame
        {
            public WalkFrame(Transform transform, int nextChild)
            {
                Transform = transform;
                NextChild = nextChild;
            }

            public Transform Transform;

            public int NextChild;
        }
    }
}
