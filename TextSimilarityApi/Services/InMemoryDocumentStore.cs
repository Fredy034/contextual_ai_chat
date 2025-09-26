using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace TextSimilarityApi.Services
{
    public class InMemoryDocumentStore
    {
        private class DocEntry
        {
            public string FileName { get; set; } = string.Empty;
            public float[] Vector { get; set; } = Array.Empty<float>();
            public string Text { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        }

        private readonly ConcurrentDictionary<string, List<DocEntry>> _store = new();
        private readonly TimeSpan _ttl;
        private readonly Timer _cleanupTimer;

        public InMemoryDocumentStore(TimeSpan? ttl = null)
        {
            _ttl = ttl ?? TimeSpan.FromHours(6);
            _cleanupTimer = new Timer(Cleanup, null, _ttl, _ttl);
        }

        /// <summary>
        /// Intenta agregar un documento a la sesión.
        /// Retorna true si se agregó, false si ya existía (texto idéntico).
        /// </summary>
        public bool Add(string sessionId, string fileName, float[] vector, string text)
        {
            if (string.IsNullOrEmpty(sessionId)) return false;

            var list = _store.GetOrAdd(sessionId, _ => new List<DocEntry>());
            lock (list)
            {
                var existing = list.FirstOrDefault(d => string.Equals(d.Text, text, StringComparison.Ordinal));
                if (existing != null)
                {
                    existing.CreatedAt = DateTime.UtcNow;
                    return false;
                }

                list.Add(new DocEntry { FileName = fileName, Vector = vector, Text = text, CreatedAt = DateTime.UtcNow });
                return true;
            }
        }

        /// <summary>
        /// Comprueba si ya existe un documento con exactamente el mismo texto en la sesión.
        /// Si existe, actualiza CreatedAt para refrescar TTL y retorna true.
        /// </summary>
        public bool ContainsText(string sessionId, string text)
        {
            if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(text)) return false;
            if (!_store.TryGetValue(sessionId, out var list)) return false;

            lock (list)
            {
                var existing = list.FirstOrDefault(d => string.Equals(d.Text, text, StringComparison.Ordinal));
                if (existing != null)
                {
                    existing.CreatedAt = DateTime.UtcNow;
                    return true;
                }

                return false;
            }
        }

        public List<(string FileName, float[] Vector, string Text)> GetAllEmbeddings(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId)) return new List<(string, float[], string)>();
            if (!_store.TryGetValue(sessionId, out var list)) return new List<(string, float[], string)>();

            lock (list)
            {
                return list.Select(d => (d.FileName, d.Vector, d.Text)).ToList();
            }
        }

        public List<(string FileName, string Snippet)> GetDocuments(string sessionId, int snippetLength = 200)
        {
            if (string.IsNullOrEmpty(sessionId)) return new List<(string, string)>();
            if (!_store.TryGetValue(sessionId, out var list)) return new List<(string, string)>();

            lock (list)
            {
                return list.Select(d =>
                {
                    var snippet = string.IsNullOrEmpty(d.Text) ? string.Empty
                        : (d.Text.Length > snippetLength ? d.Text.Substring(0, snippetLength) + "..." : d.Text);
                    return (d.FileName, snippet);
                }).ToList();
            }
        }

        private void Cleanup(object? state)
        {
            var cutoff = DateTime.UtcNow - _ttl;
            foreach (var kvp in _store)
            {
                var list = kvp.Value;
                lock (list)
                {
                    list.RemoveAll(d => d.CreatedAt < cutoff);
                    if (list.Count == 0) _store.TryRemove(kvp.Key, out _);
                }
            }
        }
    }
}
