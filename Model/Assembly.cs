// Copyright (c) Edgardo Zoppi.  All Rights Reserved.  Licensed under the MIT License.  See License.txt in the project root for license information.

using Model.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model
{
	public interface IAssemblyReference : IMetadataReference
	{
		string Name { get; }
		Version Version { get; }
		string Culture { get; }
		byte[] PublicKey { get; }
	}

	public class AssemblyReference : IAssemblyReference
	{
		public string Name { get; private set; }
		public Version Version { get; set; }
		public string Culture { get; set; }
		public byte[] PublicKey { get; set; }
		public ISet<CustomAttribute> Attributes { get; private set; }
		public AssemblyReference(string name)
		{
			this.Name = name;
			this.Attributes = new HashSet<CustomAttribute>();
		}

		public override string ToString()
		{
			return this.Name;
		}

		public override int GetHashCode()
		{
			return this.Name.GetHashCode();
		}

		public override bool Equals(object obj)
		{
			var other = obj as AssemblyReference;

			var result = other != null &&
						 this.Name == other.Name;

			return result;
		}
	}

	public class Assembly : IAssemblyReference
	{
		public string Name { get; private set; }
		public IList<IAssemblyReference> References { get; private set; }
		public Namespace RootNamespace { get; set; }
		public Version Version { get; set; }
		public string Culture { get; set; }
		public byte[] PublicKey { get; set; }
		public ISet<CustomAttribute> Attributes { get; private set; }
		public Assembly(string name)
		{
			this.Name = name;
			this.References = new List<IAssemblyReference>();
			this.Attributes = new HashSet<CustomAttribute>();
		}

		public bool MatchReference(IAssemblyReference reference)
		{
			var result = this.Name == reference.Name;
			return result;
		}

		public override string ToString()
		{
			return string.Format("assembly {0}", this.Name);
		}

		public override int GetHashCode()
		{
			return this.Name.GetHashCode();
		}

		public override bool Equals(object obj)
		{
			var other = obj as Assembly;

			var result = other != null &&
						 this.Name == other.Name;

			return result;
		}
	}
}
