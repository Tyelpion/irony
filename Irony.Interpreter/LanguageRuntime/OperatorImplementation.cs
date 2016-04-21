#region License

/* **********************************************************************************
 * Copyright (c) Roman Ivantsov
 * This source code is subject to terms and conditions of the MIT License
 * for Irony. A copy of the license can be found in the License.txt file
 * at the root of this distribution.
 * By using this source code in any fashion, you are agreeing to be bound by the terms of the
 * MIT License.
 * You must not remove this notice from this software.
 * **********************************************************************************/

#endregion License

using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Irony.Interpreter
{
	public delegate object BinaryOperatorMethod(object arg1, object arg2);

	public delegate object UnaryOperatorMethod(object arg);

	#region OperatorDispatchKey class

	/// <summary>
	/// The struct is used as a key for the dictionary of operator implementations.
	/// Contains types of arguments for a method or operator implementation.
	/// </summary>
	public struct OperatorDispatchKey
	{
		public static readonly OperatorDispatchKeyComparer Comparer = new OperatorDispatchKeyComparer();
		public readonly Type Arg1Type;
		public readonly Type Arg2Type;
		public readonly int HashCode;
		public readonly ExpressionType Op;

		/// <summary>
		/// For binary operators
		/// </summary>
		/// <param name="op"></param>
		/// <param name="arg1Type"></param>
		/// <param name="arg2Type"></param>
		public OperatorDispatchKey(ExpressionType op, Type arg1Type, Type arg2Type)
		{
			this.Op = op;
			this.Arg1Type = arg1Type;
			this.Arg2Type = arg2Type;
			var h0 = (int) this.Op;
			int h1 = this.Arg1Type.GetHashCode();
			int h2 = this.Arg2Type.GetHashCode();
			this.HashCode = unchecked(h0 << 8 ^ h1 << 4 ^ h2);
		}

		/// <summary>
		/// For unary operators
		/// </summary>
		/// <param name="op"></param>
		/// <param name="arg1Type"></param>
		public OperatorDispatchKey(ExpressionType op, Type arg1Type)
		{
			this.Op = op;
			this.Arg1Type = arg1Type;
			this.Arg2Type = null;
			var h0 = (int) this.Op;
			int h1 = this.Arg1Type.GetHashCode();
			int h2 = 0;
			this.HashCode = unchecked(h0 << 8 ^ h1 << 4 ^ h2);
		}

		public override int GetHashCode()
		{
			return this.HashCode;
		}

		public override string ToString()
		{
			return this.Op + "(" + this.Arg1Type + ", " + this.Arg2Type + ")";
		}
	}

	#endregion OperatorDispatchKey class

	#region OperatorDispatchKeyComparer class

	/// <summary>
	/// Note: I believe (guess) that a custom Comparer provided to a Dictionary is a bit more efficient
	/// than implementing IComparable on the key itself
	/// </summary>
	public class OperatorDispatchKeyComparer : IEqualityComparer<OperatorDispatchKey>
	{
		public bool Equals(OperatorDispatchKey x, OperatorDispatchKey y)
		{
			return x.HashCode == y.HashCode && x.Op == y.Op && x.Arg1Type == y.Arg1Type && x.Arg2Type == y.Arg2Type;
		}

		public int GetHashCode(OperatorDispatchKey obj)
		{
			return obj.HashCode;
		}
	}

	#endregion OperatorDispatchKeyComparer class

	///<summary>
	///The OperatorImplementation class represents an implementation of an operator for specific argument types.
	///</summary>
	///<remarks>
	/// The OperatorImplementation is used for holding implementation for binary operators, unary operators,
	/// and type converters (special case of unary operators)
	/// it holds 4 method references for binary operators:
	/// converters for both arguments, implementation method and converter for the result.
	/// For unary operators (and type converters) the implementation is in Arg1Converter
	/// operator (arg1 is used); the converter method is stored in Arg1Converter; the target type is in CommonType
	///</remarks>
	public sealed class OperatorImplementation
	{
		public readonly BinaryOperatorMethod BaseBinaryMethod;

		/// <summary>
		/// The type to which arguments are converted and no-conversion method for this type.
		/// </summary>
		public readonly Type CommonType;

		public readonly OperatorDispatchKey Key;

		/// <summary>
		/// A reference to the actual binary evaluator method - one of EvaluateConvXXX
		/// </summary>
		public BinaryOperatorMethod EvaluateBinary;

		/// <summary>
		/// No-box counterpart for implementations with auto-boxed output. If this field &lt;&gt; null, then this is
		/// implementation with auto-boxed output
		/// </summary>
		public OperatorImplementation NoBoxImplementation;

		/// <summary>
		/// An overflow handler - the implementation to handle arithmetic overflow
		/// </summary>
		public OperatorImplementation OverflowHandler;

		internal UnaryOperatorMethod Arg1Converter;
		internal UnaryOperatorMethod Arg2Converter;
		internal UnaryOperatorMethod ResultConverter;

		/// <summary>
		/// Constructor for binary operators
		/// </summary>
		/// <param name="key"></param>
		/// <param name="resultType"></param>
		/// <param name="baseBinaryMethod"></param>
		/// <param name="arg1Converter"></param>
		/// <param name="arg2Converter"></param>
		/// <param name="resultConverter"></param>
		public OperatorImplementation(OperatorDispatchKey key, Type resultType, BinaryOperatorMethod baseBinaryMethod,
		UnaryOperatorMethod arg1Converter, UnaryOperatorMethod arg2Converter, UnaryOperatorMethod resultConverter)
		{
			this.Key = key;
			this.CommonType = resultType;
			this.Arg1Converter = arg1Converter;
			this.Arg2Converter = arg2Converter;
			this.ResultConverter = resultConverter;
			this.BaseBinaryMethod = baseBinaryMethod;
			this.SetupEvaluationMethod();
		}

		/// <summary>
		/// Constructor for unary operators and type converters
		/// </summary>
		/// <param name="key"></param>
		/// <param name="type"></param>
		/// <param name="method"></param>
		public OperatorImplementation(OperatorDispatchKey key, Type type, UnaryOperatorMethod method)
		{
			this.Key = key;
			this.CommonType = type;
			this.Arg1Converter = method;
			this.Arg2Converter = null;
			this.ResultConverter = null;
			this.BaseBinaryMethod = null;
		}

		public void SetupEvaluationMethod()
		{
			if (this.BaseBinaryMethod == null)
				// Special case - it is unary method, the method itself in Arg1Converter;
				// LanguageRuntime.ExecuteUnaryOperator will handle this properly
				return;

			// Binary operator
			if (this.ResultConverter == null)
			{
				// Without ResultConverter
				if (this.Arg1Converter == null && this.Arg2Converter == null)
					this.EvaluateBinary = this.EvaluateConvNone;

				else if (this.Arg1Converter != null && this.Arg2Converter == null)
					this.EvaluateBinary = this.EvaluateConvLeft;

				else if (this.Arg1Converter == null && this.Arg2Converter != null)
					this.EvaluateBinary = this.EvaluateConvRight;

				else // if (this.Arg1Converter != null && this.Arg2Converter != null)
					this.EvaluateBinary = this.EvaluateConvBoth;
			}
			else
			{
				// With result converter
				if (this.Arg1Converter == null && this.Arg2Converter == null)
					this.EvaluateBinary = this.EvaluateConvNoneConvResult;

				else if (this.Arg1Converter != null && this.Arg2Converter == null)
					this.EvaluateBinary = this.EvaluateConvLeftConvResult;

				else if (this.Arg1Converter == null && this.Arg2Converter != null)
					this.EvaluateBinary = this.EvaluateConvRightConvResult;

				else // if (this.Arg1Converter != null && this.Arg2Converter != null)
					this.EvaluateBinary = this.EvaluateConvBothConvResult;
			}
		}

		public override string ToString()
		{
			return "[OpImpl for " + Key.ToString() + "]";
		}

		private object EvaluateConvBoth(object arg1, object arg2)
		{
			return this.BaseBinaryMethod(this.Arg1Converter(arg1), this.Arg2Converter(arg2));
		}

		private object EvaluateConvBothConvResult(object arg1, object arg2)
		{
			return this.ResultConverter(this.BaseBinaryMethod(this.Arg1Converter(arg1), this.Arg2Converter(arg2)));
		}

		private object EvaluateConvLeft(object arg1, object arg2)
		{
			return this.BaseBinaryMethod(this.Arg1Converter(arg1), arg2);
		}

		private object EvaluateConvLeftConvResult(object arg1, object arg2)
		{
			return this.ResultConverter(this.BaseBinaryMethod(this.Arg1Converter(arg1), arg2));
		}

		private object EvaluateConvNone(object arg1, object arg2)
		{
			return this.BaseBinaryMethod(arg1, arg2);
		}

		private object EvaluateConvNoneConvResult(object arg1, object arg2)
		{
			return this.ResultConverter(this.BaseBinaryMethod(arg1, arg2));
		}

		private object EvaluateConvRight(object arg1, object arg2)
		{
			return this.BaseBinaryMethod(arg1, this.Arg2Converter(arg2));
		}

		private object EvaluateConvRightConvResult(object arg1, object arg2)
		{
			return this.ResultConverter(this.BaseBinaryMethod(arg1, this.Arg2Converter(arg2)));
		}
	}

	public class OperatorImplementationTable : Dictionary<OperatorDispatchKey, OperatorImplementation>
	{
		public OperatorImplementationTable(int capacity) : base(capacity, OperatorDispatchKey.Comparer)
		{ }
	}

	public class TypeConverterTable : Dictionary<OperatorDispatchKey, UnaryOperatorMethod>
	{
		public TypeConverterTable(int capacity) : base(capacity, OperatorDispatchKey.Comparer)
		{ }
	}
}
