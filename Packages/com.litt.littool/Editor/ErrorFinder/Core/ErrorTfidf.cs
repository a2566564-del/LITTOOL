using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Littool.ErrorFinder
{
    /// <summary>
    /// Character n-gram TF-IDF with cosine similarity, the same as scikit-learn's
    /// TfidfVectorizer(analyzer="char_wb", ngram_range=(2, 5), sublinear_tf=True) used by the simulator:
    /// n-grams inside words padded with a space, tf = 1 + ln(count), idf = ln((n + 1) / (df + 1)) + 1, L2-normalised rows.
    /// </summary>
    internal sealed class ErrorTfidf
    {
        const int MinN = 2;
        const int MaxN = 5;
        static readonly Regex s_MultiSpace = new Regex(@"\s\s+", RegexOptions.CultureInvariant);

        readonly Dictionary<string, int> m_Vocabulary = new Dictionary<string, int>(StringComparer.Ordinal);
        readonly double[] m_Idf;
        // postings: term -> (document, weight) of the normalised document vectors
        readonly List<KeyValuePair<int, double>>[] m_Postings;

        public int Count { get; }

        public ErrorTfidf(IList<string> documents)
        {
            Count = documents.Count;
            var counts = new List<Dictionary<int, int>>(documents.Count);
            var df = new List<int>();
            foreach (var doc in documents)
            {
                var tf = new Dictionary<int, int>();
                foreach (var gram in CharWbNgrams(doc))
                {
                    if (!m_Vocabulary.TryGetValue(gram, out int id))
                    {
                        id = m_Vocabulary.Count;
                        m_Vocabulary.Add(gram, id);
                        df.Add(0);
                    }
                    tf.TryGetValue(id, out int c);
                    tf[id] = c + 1;
                }
                foreach (var id in tf.Keys) df[id]++;
                counts.Add(tf);
            }

            m_Idf = new double[df.Count];
            for (int t = 0; t < df.Count; t++) m_Idf[t] = Math.Log((Count + 1.0) / (df[t] + 1.0)) + 1.0;

            m_Postings = new List<KeyValuePair<int, double>>[df.Count];
            for (int d = 0; d < counts.Count; d++)
            {
                var weights = Weigh(counts[d]);
                foreach (var kv in weights)
                {
                    var list = m_Postings[kv.Key] ?? (m_Postings[kv.Key] = new List<KeyValuePair<int, double>>());
                    list.Add(new KeyValuePair<int, double>(d, kv.Value));
                }
            }
        }

        /// <summary>Cosine similarity of <paramref name="query"/> (already normalised text) with every document.</summary>
        public double[] Similarities(string query)
        {
            var result = new double[Count];
            var tf = new Dictionary<int, int>();
            foreach (var gram in CharWbNgrams(query))
            {
                if (!m_Vocabulary.TryGetValue(gram, out int id)) continue;  // unseen n-grams are ignored, as in scikit-learn
                tf.TryGetValue(id, out int c);
                tf[id] = c + 1;
            }
            if (tf.Count == 0) return result;
            foreach (var kv in Weigh(tf))
            {
                var postings = m_Postings[kv.Key];
                if (postings == null) continue;
                foreach (var p in postings) result[p.Key] += kv.Value * p.Value;
            }
            return result;
        }

        List<KeyValuePair<int, double>> Weigh(Dictionary<int, int> tf)
        {
            var list = new List<KeyValuePair<int, double>>(tf.Count);
            double sum = 0.0;
            foreach (var kv in tf)
            {
                double w = (1.0 + Math.Log(kv.Value)) * m_Idf[kv.Key];
                list.Add(new KeyValuePair<int, double>(kv.Key, w));
                sum += w * w;
            }
            double norm = Math.Sqrt(sum);
            if (norm > 0.0)
            {
                for (int i = 0; i < list.Count; i++) list[i] = new KeyValuePair<int, double>(list[i].Key, list[i].Value / norm);
            }
            return list;
        }

        /// <summary>scikit-learn _char_wb_ngrams with ngram_range (2, 5) on lower-cased text.</summary>
        static IEnumerable<string> CharWbNgrams(string text)
        {
            text = s_MultiSpace.Replace((text ?? string.Empty).ToLowerInvariant(), " ");
            foreach (var word in text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
            {
                string w = " " + word + " ";
                int len = w.Length;
                for (int n = MinN; n <= MaxN; n++)
                {
                    int offset = 0;
                    yield return w.Substring(0, Math.Min(n, len));
                    while (offset + n < len)
                    {
                        offset++;
                        yield return w.Substring(offset, n);
                    }
                    if (offset == 0) break;  // a short word (len < n) is counted once
                }
            }
        }
    }
}
