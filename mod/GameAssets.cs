using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace SephiriaCompanion
{
    public sealed class GameAssets
    {
        const int Capacity = 512;
        readonly ConcurrentDictionary<string, byte[]> data = new ConcurrentDictionary<string, byte[]>();
        readonly HashSet<string> requested = new HashSet<string>();
        readonly Queue<Func<string>> pending = new Queue<Func<string>>();
        public int Count => data.Count;
        public int Queued => pending.Count;
        public double LastWorkMs { get; private set; }
        public bool TryGet(string name, out byte[] bytes) => data.TryGetValue(name, out bytes);

        public string Texture(Texture2D texture)
        {
            if (texture == null) return null;
            string file = "map-" + texture.GetInstanceID() + ".png";
            Schedule(file, () => texture == null || !texture.isReadable ? null : texture.EncodeToPNG());
            return "/" + file;
        }

        public string Sprite(Sprite sprite)
        {
            if (sprite == null) return null;
            string file = "icon-" + sprite.GetInstanceID() + ".png";
            Schedule(file, () => CopySprite(sprite));
            return "/" + file;
        }

        void Schedule(string file, Func<byte[]> encode)
        {
            if (requested.Count >= Capacity || !requested.Add(file)) return;
            pending.Enqueue(() =>
            {
                try { var bytes = encode(); if (bytes != null) { data[file] = bytes; return "/" + file; } }
                catch (Exception ex) { UnityEngine.Debug.LogWarning("[SephiriaCompanion] Asset: " + ex.GetType().Name); }
                return null;
            });
        }

        // At most one GPU readback/PNG encoding per frame, never in a state capture.
        public string ProcessOne()
        {
            if (pending.Count == 0) return null;
            var clock = Stopwatch.StartNew();
            string path = pending.Dequeue()();
            LastWorkMs = clock.Elapsed.TotalMilliseconds;
            return path;
        }

        static byte[] CopySprite(Sprite sprite)
        {
            if (sprite == null) return null;
            var rect = sprite.textureRect;
            int width = Math.Min(256, Math.Max(1, (int)rect.width));
            int height = Math.Min(256, Math.Max(1, (int)rect.height));
            RenderTexture temporary = null;
            Texture2D copy = null;
            var previous = RenderTexture.active;
            try
            {
                temporary = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
                temporary.filterMode = FilterMode.Point;
                var source = sprite.texture;
                Graphics.Blit(source, temporary, new Vector2(rect.width / source.width, rect.height / source.height),
                    new Vector2(rect.x / source.width, rect.y / source.height));
                RenderTexture.active = temporary;
                copy = new Texture2D(width, height, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                copy.Apply();
                return copy.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = previous;
                if (temporary != null) RenderTexture.ReleaseTemporary(temporary);
                if (copy != null) UnityEngine.Object.Destroy(copy);
            }
        }
    }
}
