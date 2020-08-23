// Copyright (c) Edgardo Zoppi.  All Rights Reserved.  Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Model.Types;

namespace Model
{
	public enum ExceptionHandlerBlockKind
	{
		Try,
		Filter,
		Catch,
		Fault,
		Finally
	}

	public interface IExceptionHandlerBlock
	{
		ExceptionHandlerBlockKind Kind { get; }
		string Start { get; }
		string End { get; }
	}

	public interface IExceptionHandler : IExceptionHandlerBlock
	{
	}

	public class ProtectedBlock : IExceptionHandlerBlock
	{
		public ExceptionHandlerBlockKind Kind { get; private set; }
		public string Start { get; set; }
		public string End { get; set; }
		public IExceptionHandler Handler { get; set; }

		public ProtectedBlock(uint start, uint end) : this($"L_{start:X4}", $"L_{end:X4}")
		{
		}

		public ProtectedBlock(string start, string end)
		{
			this.Kind = ExceptionHandlerBlockKind.Try;
			this.Start = start;
			this.End = end;
		}

		public override string ToString()
		{
			return string.Format("try {0} to {1} {2}", this.Start, this.End, this.Handler);
		}
	}

	public class FilterExceptionHandler : IExceptionHandler
	{
		public ExceptionHandlerBlockKind Kind { get; private set; }
		public string FilterStart { get; set; }
		public string Start { get; set; }
		public string End { get; set; }
		public IType ExceptionType { get; set; }

		public FilterExceptionHandler(uint filterStart, uint start, uint end, IType exceptionType)
			: this($"L_{filterStart:X4}", $"L_{start:X4}", $"L_{end:X4}", exceptionType)
		{
		}

		public FilterExceptionHandler(string filterStart, string start, string end, IType exceptionType)
		{
			this.Kind = ExceptionHandlerBlockKind.Filter;
			this.FilterStart = filterStart;
			this.Start = start;
			this.End = end;
			this.ExceptionType = exceptionType;
		}

		public override string ToString()
		{
			return string.Format("filter {0} {1} to {2} handler {2} to {3}", this.ExceptionType, this.FilterStart, this.Start, this.End);
		}
	}

	public class CatchExceptionHandler : IExceptionHandler
	{
		public ExceptionHandlerBlockKind Kind { get; private set; }
		public string Start { get; set; }
		public string End { get; set; }
		public IType ExceptionType { get; set; }

		public CatchExceptionHandler(uint start, uint end, IType exceptionType) : this($"L_{start:X4}", $"L_{end:X4}", exceptionType)
		{
		}

		public CatchExceptionHandler(string start, string end, IType exceptionType)
		{
			this.Kind = ExceptionHandlerBlockKind.Catch;
			this.Start = start;
			this.End = end;
			this.ExceptionType = exceptionType;
		}

		public override string ToString()
		{
			return string.Format("catch {0} handler {1} to {2}", this.ExceptionType, this.Start, this.End);
		}
	}

	public class FaultExceptionHandler : IExceptionHandler
	{
		public ExceptionHandlerBlockKind Kind { get; private set; }
		public string Start { get; set; }
		public string End { get; set; }

		public FaultExceptionHandler(uint start, uint end) : this($"L_{start:X4}", $"L_{end:X4}")
		{
		}

		public FaultExceptionHandler(string start, string end)
		{
			this.Kind = ExceptionHandlerBlockKind.Fault;
			this.Start = start;
			this.End = end;
		}

		public override string ToString()
		{
			return string.Format("fault handler {0} to {1}", this.Start, this.End);
		}
	}

	public class FinallyExceptionHandler : IExceptionHandler
	{
		public ExceptionHandlerBlockKind Kind { get; private set; }
		public string Start { get; set; }
		public string End { get; set; }

		public FinallyExceptionHandler(uint start, uint end) : this($"L_{start:X4}", $"L_{end:X4}")
		{
		}

		public FinallyExceptionHandler(string start, string end)
		{
			this.Kind = ExceptionHandlerBlockKind.Finally;
			this.Start = start;
			this.End = end;
		}

		public override string ToString()
		{
			return string.Format("finally handler {0} to {1}", this.Start, this.End);
		}
	}
}
