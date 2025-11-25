using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FavoriteAssetsWindow
{
    [Serializable]
    internal class SerializableDictionary<TKey, TValue> : IEnumerable<KVPair<TKey, TValue>>
    {
        public List<KVPair<TKey, TValue>> pairs;
        public int Count => pairs.Count;
        public SerializableDictionary()
        {
            pairs = new List<KVPair<TKey, TValue>>();
        }

        public SerializableDictionary(Dictionary<TKey, TValue> dict)
        {
            pairs = new List<KVPair<TKey, TValue>>();
            foreach (var kv in dict)
            {
                pairs.Add(new KVPair<TKey, TValue>(kv));
            }
        }

        public SerializableDictionary(SerializableDictionary<TKey, TValue> dict)
        {
            pairs = new List<KVPair<TKey, TValue>>();
            foreach (var kv in dict)
            {
                pairs.Add(new KVPair<TKey, TValue>(kv.Key, kv.Value));
            }
        }

        public TValue this[TKey key]
        {
            get => FindPair(key).Value;
            set
            {
                var exist = FindPair(key);
                if (exist == null)
                {
                    var pair = new KVPair<TKey, TValue>(key, value);
                    pairs.Add(pair);
                }
                else exist.Value = value;

            }
        }

        public KVPair<TKey, TValue> Get(int index)
        {
            return pairs[index];
        }
        public bool ContainsKey(TKey key)
        {
            var exist = FindPair(key);
            return exist != null;
        }

        public bool ContainsValue(TValue value)
        {
            for (int i = 0; i < pairs.Count; i++)
            {
                if (pairs[i].Value == null)
                {
                    if (value == null) return true;
                    Debug.LogWarning($"There is an empty value | index : {i} | Key : {pairs[i].Key}");
                    continue;
                }
                if (pairs[i].Value.Equals(value)) return true;
            }

            return false;
        }

        public bool Remove(TKey key)
        {
            for (int i = 0; i < pairs.Count; i++)
            {
                if (pairs[i].Key == null)
                {
                    if(key == null)
                    {
                        pairs.RemoveAt(i);
                        return true;
                    }
                    Debug.LogWarning($"There is an empty key | index : {i} | Value : {pairs[i].Value}");
                    continue;
                }
                if (pairs[i].Key.Equals(key))
                {
                    pairs.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        public int RemoveAll(TKey key)
        {
            var count = 0;
            for (int i = pairs.Count - 1; i > 0 ; i--)
            {
                if (pairs[i].Key == null)
                {
                    if(key == null)
                    {
                        pairs.RemoveAt(i);
                        continue;
                    }
                    Debug.LogWarning($"There is an empty key | index : {i} | Value : {pairs[i].Value}");
                    continue;
                }
                if (pairs[i].Key.Equals(key))
                {
                    pairs.RemoveAt(i);
                    count++;
                }
            }

            return count;
        }

        public int RemoveAll(TValue value)
        {
            var count = 0;
            for (int i = pairs.Count - 1; i > 0 ; i--)
            {
                if (pairs[i].Value == null)
                {
                    if(value == null)
                    {
                        pairs.RemoveAt(i);
                        continue;
                    }
                    Debug.LogWarning($"There is an empty value | index : {i} | Key : {pairs[i].Key}");
                    continue;
                }
                if (pairs[i].Value.Equals(value))
                {
                    pairs.RemoveAt(i);
                    count++;
                }
            }

            return count;
        }

        public void Clear()
        {
            pairs.Clear();
        }

        public void Sort(Comparison<KVPair<TKey, TValue>> comparison)
        {
            pairs.Sort(comparison);
        }

        public void Sort(IComparer<KVPair<TKey, TValue>> comparer)
        {
            pairs.Sort(comparer);
        }

        KVPair<TKey, TValue> FindPair(TKey key)
        {
            for (int i = 0; i < pairs.Count; i++)
            {
                if (pairs[i].Key == null)
                {
                    if(key == null) return pairs[i];
                    Debug.LogWarning($"There is an empty key | index : {i} | Value : {pairs[i].Value}");
                    continue;
                }
                if (pairs[i].Key.Equals(key)) return pairs[i];
            }

            return null;
        }

        public IEnumerator<KVPair<TKey, TValue>> GetEnumerator()
        {
            return pairs.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public Dictionary<TKey, TValue> ToDictionary()
        {
            var dict = new Dictionary<TKey, TValue>();
            foreach (var kvPair in this)
            {
                dict[kvPair.Key] = kvPair.Value;
            }
            return dict;
        }
        public bool TryFind(in TKey key, out TValue value)
        {
            for (var i = 0; i < pairs.Count; i++)
            {
                if (pairs[i].Key == null)
                {
                    if(key == null)
                    {
                        value = pairs[i].Value;
                        return true;
                    }
                    Debug.LogWarning($"There is an empty key | index : {i} | Value : {pairs[i].Value}");
                    continue;
                }
                
                if (pairs[i].Key.Equals(key))
                {
                    value = pairs[i].Value;
                    return true;
                }
            }

            value = default;
            return false;
        }
    }
    
    [Serializable]
    public class KVPair<TKey, TValue>
    {
        public TKey Key;
        public TValue Value;
        

        public KVPair(TKey key, TValue value)
        {
            Key = key;
            Value = value;
        }

        public KVPair(KeyValuePair<TKey, TValue> pair)
        {
            var (key, value) = pair;
            Key = key;
            Value = value;
        }
    }
}