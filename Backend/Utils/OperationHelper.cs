// Copyright (c) Edgardo Zoppi.  All Rights Reserved.  Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Model;
using Bytecode = Model.Bytecode;
using Tac = Model.ThreeAddressCode.Instructions;

namespace Backend.Utils
{
	public static class OperationHelper
	{
		public static Tac.MethodCallOperation ToMethodCallOperation(Bytecode.MethodCallOperation operation)
		{
			switch (operation)
			{
				case Bytecode.MethodCallOperation.Static: return Tac.MethodCallOperation.Static;
				case Bytecode.MethodCallOperation.Virtual: return Tac.MethodCallOperation.Virtual;
				case Bytecode.MethodCallOperation.Jump: return Tac.MethodCallOperation.Jump;

				default: throw operation.ToUnknownValueException();
			}
		}

		public static Tac.ConvertOperation ToConvertOperation(Bytecode.ConvertOperation operation)
		{
			switch (operation)
			{
				case Bytecode.ConvertOperation.Box: return Tac.ConvertOperation.Box;
				case Bytecode.ConvertOperation.Cast: return Tac.ConvertOperation.Cast;
				case Bytecode.ConvertOperation.Conv: return Tac.ConvertOperation.Conv;
				case Bytecode.ConvertOperation.Unbox: return Tac.ConvertOperation.Unbox;
				case Bytecode.ConvertOperation.UnboxPtr: return Tac.ConvertOperation.UnboxPtr;
				case Bytecode.ConvertOperation.IsInst: return Tac.ConvertOperation.IsInst;
				default: throw operation.ToUnknownValueException();
			}
		}

		public static Tac.BranchOperation ToBranchOperation(Bytecode.BranchOperation operation)
		{
			switch (operation)
			{
				case Bytecode.BranchOperation.False:
				case Bytecode.BranchOperation.True:
				case Bytecode.BranchOperation.Eq: return Tac.BranchOperation.Eq;
				case Bytecode.BranchOperation.Ge: return Tac.BranchOperation.Ge;
				case Bytecode.BranchOperation.Gt: return Tac.BranchOperation.Gt;
				case Bytecode.BranchOperation.Le: return Tac.BranchOperation.Le;
				case Bytecode.BranchOperation.Lt: return Tac.BranchOperation.Lt;
				case Bytecode.BranchOperation.Neq: return Tac.BranchOperation.Neq;

				default: throw operation.ToUnknownValueException();
			}
		}

		public static Tac.UnaryOperation ToUnaryOperation(Bytecode.BasicOperation operation)
		{
			switch (operation)
			{
				case Bytecode.BasicOperation.Neg: return Tac.UnaryOperation.Neg;
				case Bytecode.BasicOperation.Not: return Tac.UnaryOperation.Not;

				default: throw operation.ToUnknownValueException();
			}
		}

		public static Tac.BinaryOperation ToBinaryOperation(Bytecode.BasicOperation operation)
		{
			switch (operation)
			{
				case Bytecode.BasicOperation.Add:	return Tac.BinaryOperation.Add;
				case Bytecode.BasicOperation.And:	return Tac.BinaryOperation.And;
				case Bytecode.BasicOperation.Eq:	return Tac.BinaryOperation.Eq;
				case Bytecode.BasicOperation.Gt:	return Tac.BinaryOperation.Gt;
				case Bytecode.BasicOperation.Lt:	return Tac.BinaryOperation.Lt;
				case Bytecode.BasicOperation.Div:	return Tac.BinaryOperation.Div;
				case Bytecode.BasicOperation.Mul:	return Tac.BinaryOperation.Mul;
				case Bytecode.BasicOperation.Or:	return Tac.BinaryOperation.Or;
				case Bytecode.BasicOperation.Rem:	return Tac.BinaryOperation.Rem;
				case Bytecode.BasicOperation.Shl:	return Tac.BinaryOperation.Shl;
				case Bytecode.BasicOperation.Shr:	return Tac.BinaryOperation.Shr;
				case Bytecode.BasicOperation.Sub:	return Tac.BinaryOperation.Sub;
				case Bytecode.BasicOperation.Xor:	return Tac.BinaryOperation.Xor;

				default: throw operation.ToUnknownValueException();
			}
		}

		public static bool GetUnaryConditionalBranchValue(Bytecode.BranchOperation operation)
		{
			switch (operation)
			{
				case Bytecode.BranchOperation.False: return false;
				case Bytecode.BranchOperation.True:  return true;

				default: throw operation.ToUnknownValueException();
			}
		}

		public static bool CanFallThroughNextInstruction(Bytecode.Instruction instruction)
		{
			var result = true;

			if (instruction is Bytecode.BranchInstruction)
			{
				var branch = instruction as Bytecode.BranchInstruction;

				switch (branch.Operation)
				{
					case Bytecode.BranchOperation.Branch:
					case Bytecode.BranchOperation.Leave: result = false; break;
				}
			}
			else if (instruction is Bytecode.BasicInstruction)
			{
				var basic = instruction as Bytecode.BasicInstruction;

				switch (basic.Operation)
				{
					case Bytecode.BasicOperation.Return:
					case Bytecode.BasicOperation.Throw:
					case Bytecode.BasicOperation.Rethrow:
					case Bytecode.BasicOperation.EndFilter:
					case Bytecode.BasicOperation.EndFinally: result = false; break;
				}
			}

			return result;
		}

		public static bool IsBranch(Bytecode.Instruction instruction)
		{
			var result = instruction is Bytecode.BranchInstruction ||
						 instruction is Bytecode.SwitchInstruction;

			return result;
		}

		public static Bytecode.BranchOperation ToBranchOperation(Tac.BranchOperation operation)
		{
			switch (operation)
			{
				case Tac.BranchOperation.Eq: return Bytecode.BranchOperation.Eq;
				case Tac.BranchOperation.Neq: return Bytecode.BranchOperation.Neq;
				case Tac.BranchOperation.Lt: return Bytecode.BranchOperation.Lt;
				case Tac.BranchOperation.Le: return Bytecode.BranchOperation.Le;
				case Tac.BranchOperation.Gt: return Bytecode.BranchOperation.Gt;
				case Tac.BranchOperation.Ge: return Bytecode.BranchOperation.Ge;
				default: throw operation.ToUnknownValueException();
			}
		}

		public static Bytecode.ConvertOperation ToConvertOperation(Tac.ConvertOperation operation)
		{
			switch (operation)
			{
				case Tac.ConvertOperation.Conv: return Bytecode.ConvertOperation.Conv;
				case Tac.ConvertOperation.Cast: return Bytecode.ConvertOperation.Cast;
				case Tac.ConvertOperation.Box: return Bytecode.ConvertOperation.Box;
				case Tac.ConvertOperation.Unbox: return Bytecode.ConvertOperation.Unbox;
				case Tac.ConvertOperation.UnboxPtr: return Bytecode.ConvertOperation.Unbox;
				default: throw operation.ToUnknownValueException();
			}
		}

		public static Bytecode.BasicOperation ToBasicOperation(Tac.UnaryOperation operation)
		{
			switch (operation)
			{
				case Tac.UnaryOperation.Not: return Bytecode.BasicOperation.Not;
				case Tac.UnaryOperation.Neg: return Bytecode.BasicOperation.Neg;
				default: throw operation.ToUnknownValueException();
			}
		}

		public static Bytecode.BasicOperation ToBasicOperation(Tac.BinaryOperation operation)
		{
			switch (operation)
			{
				case Tac.BinaryOperation.Add: return Bytecode.BasicOperation.Add; 
				case Tac.BinaryOperation.Sub: return Bytecode.BasicOperation.Sub; 
				case Tac.BinaryOperation.Mul: return Bytecode.BasicOperation.Mul; 
				case Tac.BinaryOperation.Div: return Bytecode.BasicOperation.Div; 
				case Tac.BinaryOperation.Rem: return Bytecode.BasicOperation.Rem; 
				case Tac.BinaryOperation.And: return Bytecode.BasicOperation.And; 
				case Tac.BinaryOperation.Or: return Bytecode.BasicOperation.Or; 
				case Tac.BinaryOperation.Xor: return Bytecode.BasicOperation.Xor; 
				case Tac.BinaryOperation.Shl: return Bytecode.BasicOperation.Shl; 
				case Tac.BinaryOperation.Shr: return Bytecode.BasicOperation.Shr; 
				case Tac.BinaryOperation.Eq: return Bytecode.BasicOperation.Eq; 
				case Tac.BinaryOperation.Lt: return Bytecode.BasicOperation.Lt; 
				case Tac.BinaryOperation.Gt: return Bytecode.BasicOperation.Gt;
				default: throw operation.ToUnknownValueException();
			}
		}

		public static Bytecode.MethodCallOperation ToMethodCallOperation(Tac.MethodCallOperation operation)
		{
			switch (operation)
			{
				case Tac.MethodCallOperation.Static: return Bytecode.MethodCallOperation.Static;
				case Tac.MethodCallOperation.Virtual: return Bytecode.MethodCallOperation.Virtual;
				case Tac.MethodCallOperation.Jump: return Bytecode.MethodCallOperation.Jump;
				default: throw operation.ToUnknownValueException();
			}
		}
	}
}
