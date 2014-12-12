﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Backend.Utils
{
	public class Map<TKey, TValue, TCollection> : Dictionary<TKey, TCollection>
		where TCollection : ICollection<TValue>, new()
	{
		public void Add(TKey key)
		{
			this.AddKeyAndGetValues(key);
		}

		public void Add(TKey key, TValue value)
		{
			var collection = this.AddKeyAndGetValues(key);
			collection.Add(value);
		}

		public void AddRange(TKey key, IEnumerable<TValue> values)
		{
			var collection = this.AddKeyAndGetValues(key);
			collection.AddRange(values);
		}

		private TCollection AddKeyAndGetValues(TKey key)
		{
			TCollection result;

			if (this.ContainsKey(key))
			{
				result = this[key];
			}
			else
			{
				result = new TCollection();
				this.Add(key, result);
			}

			return result;
		}
	}

	public class MapSet<TKey, TValue> : Map<TKey, TValue, HashSet<TValue>> { }

	public class MapList<TKey, TValue> : Map<TKey, TValue, List<TValue>> { }
}