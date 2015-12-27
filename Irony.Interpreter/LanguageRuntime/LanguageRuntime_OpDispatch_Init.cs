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
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using Irony.Parsing;

namespace Irony.Interpreter
{
	/// <summary>
	/// Initialization of Runtime
	/// </summary>
	public partial class LanguageRuntime
	{
		/// <summary>
		/// Note: ran some primitive tests, and it appears that use of smart boxing makes it slower
		/// by about 5-10%; so disabling it for now
		/// </summary>
		public bool SmartBoxingEnabled = false;

		private const int _boxesMiddle = 2048;

		private static ExpressionType[] _overflowOperators = new ExpressionType[] {
			ExpressionType.Add, ExpressionType.AddChecked, ExpressionType.Subtract, ExpressionType.SubtractChecked,
			ExpressionType.Multiply, ExpressionType.MultiplyChecked, ExpressionType.Power};

		/// <summary>
		/// Smart boxing: boxes for a bunch of integers are preallocated
		/// </summary>
		private object[] boxes = new object[4096];

		private bool supportsBigInt;
		private bool supportsComplex;
		private bool supportsRational;

		protected virtual void InitOperatorImplementations()
		{
			this.supportsComplex = this.Language.Grammar.LanguageFlags.IsSet(LanguageFlags.SupportsComplex);
			this.supportsBigInt = this.Language.Grammar.LanguageFlags.IsSet(LanguageFlags.SupportsBigInt);
			this.supportsRational = this.Language.Grammar.LanguageFlags.IsSet(LanguageFlags.SupportsRational);

			// TODO: add support for Rational
			if (this.SmartBoxingEnabled)
				this.InitBoxes();

			this.InitTypeConverters();
			this.InitBinaryOperatorImplementationsForMatchedTypes();
			this.InitUnaryOperatorImplementations();
			this.CreateBinaryOperatorImplementationsForMismatchedTypes();
			this.CreateOverflowHandlers();
		}

		/// <summary>
		/// The value of smart boxing is questionable - so far did not see perf improvements, so currently it is disabled
		/// </summary>
		private void InitBoxes()
		{
			for (int i = 0; i < this.boxes.Length; i++)
			{
				this.boxes[i] = i - _boxesMiddle;
			}
		}

		#region Utility methods for adding converters and binary implementations

		protected OperatorImplementation AddBinary(ExpressionType op, Type baseType, BinaryOperatorMethod binaryMethod)
		{
			return this.AddBinary(op, baseType, binaryMethod, null);
		}

		protected OperatorImplementation AddBinary(ExpressionType op, Type commonType,
						 BinaryOperatorMethod binaryMethod, UnaryOperatorMethod resultConverter)
		{
			var key = new OperatorDispatchKey(op, commonType, commonType);
			var impl = new OperatorImplementation(key, commonType, binaryMethod, null, null, resultConverter);
			this.OperatorImplementations[key] = impl;

			return impl;
		}

		protected OperatorImplementation AddBinaryBoxed(ExpressionType op, Type baseType,
			 BinaryOperatorMethod boxedBinaryMethod, BinaryOperatorMethod noBoxMethod)
		{
			// First create implementation without boxing
			var noBoxImpl = this.AddBinary(op, baseType, noBoxMethod);

			if (!this.SmartBoxingEnabled)
				return noBoxImpl;

			// The boxedImpl will overwrite noBoxImpl in the dictionary
			var boxedImpl = this.AddBinary(op, baseType, boxedBinaryMethod);
			boxedImpl.NoBoxImplementation = noBoxImpl;

			return boxedImpl;
		}

		protected OperatorImplementation AddConverter(Type fromType, Type toType, UnaryOperatorMethod method)
		{
			var key = new OperatorDispatchKey(ExpressionType.ConvertChecked, fromType, toType);
			var impl = new OperatorImplementation(key, toType, method);
			this.OperatorImplementations[key] = impl;

			return impl;
		}

		protected OperatorImplementation AddUnary(ExpressionType op, Type commonType, UnaryOperatorMethod unaryMethod)
		{
			var key = new OperatorDispatchKey(op, commonType);
			var impl = new OperatorImplementation(key, commonType, null, unaryMethod, null, null);
			this.OperatorImplementations[key] = impl;

			return impl;
		}

		#endregion Utility methods for adding converters and binary implementations

		#region Initializing type converters

		public static object ConvertAnyIntToBigInteger(object value)
		{
			long l = Convert.ToInt64(value);
			return new BigInteger(l);
		}

		public static object ConvertAnyToComplex(object value)
		{
			double d = Convert.ToDouble(value);
			return new Complex(d, 0);
		}

		/// <summary>
		/// Some specialized convert implementation methods
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static object ConvertAnyToString(object value)
		{
			return value == null ? string.Empty : value.ToString();
		}

		public static object ConvertBigIntToComplex(object value)
		{
			BigInteger bi = (BigInteger) value;
			return new Complex((double) bi, 0);
		}

		public virtual void InitTypeConverters()
		{
			Type targetType;

			// ->string
			targetType = typeof(string);
			this.AddConverter(typeof(char), targetType, ConvertAnyToString);
			this.AddConverter(typeof(sbyte), targetType, ConvertAnyToString);
			this.AddConverter(typeof(byte), targetType, ConvertAnyToString);
			this.AddConverter(typeof(Int16), targetType, ConvertAnyToString);
			this.AddConverter(typeof(UInt16), targetType, ConvertAnyToString);
			this.AddConverter(typeof(Int32), targetType, ConvertAnyToString);
			this.AddConverter(typeof(UInt32), targetType, ConvertAnyToString);
			this.AddConverter(typeof(Int64), targetType, ConvertAnyToString);
			this.AddConverter(typeof(UInt64), targetType, ConvertAnyToString);
			this.AddConverter(typeof(Single), targetType, ConvertAnyToString);

			if (this.supportsBigInt)
				this.AddConverter(typeof(BigInteger), targetType, ConvertAnyToString);

			if (this.supportsComplex)
				this.AddConverter(typeof(Complex), targetType, ConvertAnyToString);

			// ->Complex
			if (this.supportsComplex)
			{
				targetType = typeof(Complex);
				this.AddConverter(typeof(sbyte), targetType, ConvertAnyToComplex);
				this.AddConverter(typeof(byte), targetType, ConvertAnyToComplex);
				this.AddConverter(typeof(Int16), targetType, ConvertAnyToComplex);
				this.AddConverter(typeof(UInt16), targetType, ConvertAnyToComplex);
				this.AddConverter(typeof(Int32), targetType, ConvertAnyToComplex);
				this.AddConverter(typeof(UInt32), targetType, ConvertAnyToComplex);
				this.AddConverter(typeof(Int64), targetType, ConvertAnyToComplex);
				this.AddConverter(typeof(UInt64), targetType, ConvertAnyToComplex);
				this.AddConverter(typeof(Single), targetType, ConvertAnyToComplex);

				if (this.supportsBigInt)
					this.AddConverter(typeof(BigInteger), targetType, ConvertBigIntToComplex);
			}

			// ->BigInteger
			if (this.supportsBigInt)
			{
				targetType = typeof(BigInteger);
				this.AddConverter(typeof(sbyte), targetType, ConvertAnyIntToBigInteger);
				this.AddConverter(typeof(byte), targetType, ConvertAnyIntToBigInteger);
				this.AddConverter(typeof(Int16), targetType, ConvertAnyIntToBigInteger);
				this.AddConverter(typeof(UInt16), targetType, ConvertAnyIntToBigInteger);
				this.AddConverter(typeof(Int32), targetType, ConvertAnyIntToBigInteger);
				this.AddConverter(typeof(UInt32), targetType, ConvertAnyIntToBigInteger);
				this.AddConverter(typeof(Int64), targetType, ConvertAnyIntToBigInteger);
				this.AddConverter(typeof(UInt64), targetType, ConvertAnyIntToBigInteger);
			}

			// ->Double
			targetType = typeof(double);
			this.AddConverter(typeof(sbyte), targetType, value => (double) (sbyte) value);
			this.AddConverter(typeof(byte), targetType, value => (double) (byte) value);
			this.AddConverter(typeof(Int16), targetType, value => (double) (Int16) value);
			this.AddConverter(typeof(UInt16), targetType, value => (double) (UInt16) value);
			this.AddConverter(typeof(Int32), targetType, value => (double) (Int32) value);
			this.AddConverter(typeof(UInt32), targetType, value => (double) (UInt32) value);
			this.AddConverter(typeof(Int64), targetType, value => (double) (Int64) value);
			this.AddConverter(typeof(UInt64), targetType, value => (double) (UInt64) value);
			this.AddConverter(typeof(Single), targetType, value => (double) (Single) value);

			if (this.supportsBigInt)
				this.AddConverter(typeof(BigInteger), targetType, value => ((double) (BigInteger) value));

			// ->Single
			targetType = typeof(Single);
			this.AddConverter(typeof(sbyte), targetType, value => (Single) (sbyte) value);
			this.AddConverter(typeof(byte), targetType, value => (Single) (byte) value);
			this.AddConverter(typeof(Int16), targetType, value => (Single) (Int16) value);
			this.AddConverter(typeof(UInt16), targetType, value => (Single) (UInt16) value);
			this.AddConverter(typeof(Int32), targetType, value => (Single) (Int32) value);
			this.AddConverter(typeof(UInt32), targetType, value => (Single) (UInt32) value);
			this.AddConverter(typeof(Int64), targetType, value => (Single) (Int64) value);
			this.AddConverter(typeof(UInt64), targetType, value => (Single) (UInt64) value);

			if (this.supportsBigInt)
				this.AddConverter(typeof(BigInteger), targetType, value => (Single) (BigInteger) value);

			// ->UInt64
			targetType = typeof(UInt64);
			this.AddConverter(typeof(sbyte), targetType, value => (UInt64) (sbyte) value);
			this.AddConverter(typeof(byte), targetType, value => (UInt64) (byte) value);
			this.AddConverter(typeof(Int16), targetType, value => (UInt64) (Int16) value);
			this.AddConverter(typeof(UInt16), targetType, value => (UInt64) (UInt16) value);
			this.AddConverter(typeof(Int32), targetType, value => (UInt64) (Int32) value);
			this.AddConverter(typeof(UInt32), targetType, value => (UInt64) (UInt32) value);
			this.AddConverter(typeof(Int64), targetType, value => (UInt64) (Int64) value);

			// ->Int64
			targetType = typeof(Int64);
			this.AddConverter(typeof(sbyte), targetType, value => (Int64) (sbyte) value);
			this.AddConverter(typeof(byte), targetType, value => (Int64) (byte) value);
			this.AddConverter(typeof(Int16), targetType, value => (Int64) (Int16) value);
			this.AddConverter(typeof(UInt16), targetType, value => (Int64) (UInt16) value);
			this.AddConverter(typeof(Int32), targetType, value => (Int64) (Int32) value);
			this.AddConverter(typeof(UInt32), targetType, value => (Int64) (UInt32) value);

			// ->UInt32
			targetType = typeof(UInt32);
			this.AddConverter(typeof(sbyte), targetType, value => (UInt32) (sbyte) value);
			this.AddConverter(typeof(byte), targetType, value => (UInt32) (byte) value);
			this.AddConverter(typeof(Int16), targetType, value => (UInt32) (Int16) value);
			this.AddConverter(typeof(UInt16), targetType, value => (UInt32) (UInt16) value);
			this.AddConverter(typeof(Int32), targetType, value => (UInt32) (Int32) value);

			// ->Int32
			targetType = typeof(Int32);
			this.AddConverter(typeof(sbyte), targetType, value => (Int32) (sbyte) value);
			this.AddConverter(typeof(byte), targetType, value => (Int32) (byte) value);
			this.AddConverter(typeof(Int16), targetType, value => (Int32) (Int16) value);
			this.AddConverter(typeof(UInt16), targetType, value => (Int32) (UInt16) value);

			// ->UInt16
			targetType = typeof(UInt16);
			this.AddConverter(typeof(sbyte), targetType, value => (UInt16) (sbyte) value);
			this.AddConverter(typeof(byte), targetType, value => (UInt16) (byte) value);
			this.AddConverter(typeof(Int16), targetType, value => (UInt16) (Int16) value);

			// ->Int16
			targetType = typeof(Int16);
			this.AddConverter(typeof(sbyte), targetType, value => (Int16) (sbyte) value);
			this.AddConverter(typeof(byte), targetType, value => (Int16) (byte) value);

			// ->byte
			targetType = typeof(byte);
			this.AddConverter(typeof(sbyte), targetType, value => (byte) (sbyte) value);
		}

		#endregion Initializing type converters

		#region Binary operators implementations

		/// <summary>
		/// Creates a binary implementations for an operator with mismatched argument types.
		/// Determines common type, retrieves implementation for operator with both args of common type, then creates
		/// implementation for mismatched types using type converters (by converting to common type)
		/// </summary>
		/// <param name="op"></param>
		/// <param name="arg1Type"></param>
		/// <param name="arg2Type"></param>
		/// <returns></returns>
		public OperatorImplementation CreateBinaryOperatorImplementation(ExpressionType op, Type arg1Type, Type arg2Type)
		{
			Type commonType = this.GetCommonTypeForOperator(op, arg1Type, arg2Type);
			if (commonType == null)
				return null;

			// Get base method for the operator and common type
			var baseImpl = this.FindBaseImplementation(op, commonType);
			if (baseImpl == null)
			{
				// Try up-type
				commonType = this.GetUpType(commonType);
				if (commonType == null)
					return null;

				baseImpl = this.FindBaseImplementation(op, commonType);
			}

			if (baseImpl == null)
				return null;

			// Create implementation and save it in implementations table
			var impl = this.CreateBinaryOperatorImplementation(op, arg1Type, arg2Type, commonType, baseImpl.BaseBinaryMethod, baseImpl.ResultConverter);
			OperatorImplementations[impl.Key] = impl;

			return impl;
		}

		// Generates binary implementations for mismatched argument types
		public virtual void CreateBinaryOperatorImplementationsForMismatchedTypes()
		{
			// Find all data types are there
			var allTypes = new HashSet<Type>();
			var allBinOps = new HashSet<ExpressionType>();
			foreach (var kv in OperatorImplementations)
			{
				allTypes.Add(kv.Key.Arg1Type);

				if (kv.Value.BaseBinaryMethod != null)
					allBinOps.Add(kv.Key.Op);
			}

			foreach (var arg1Type in allTypes)
			{
				foreach (var arg2Type in allTypes)
				{
					if (arg1Type != arg2Type)
					{
						foreach (ExpressionType op in allBinOps)
						{
							this.CreateBinaryOperatorImplementation(op, arg1Type, arg2Type);
						}
					}
				}
			}
		}

		/// <summary>
		/// Important: returns null if fromType == toType
		/// </summary>
		/// <param name="fromType"></param>
		/// <param name="toType"></param>
		/// <returns></returns>
		public virtual UnaryOperatorMethod GetConverter(Type fromType, Type toType)
		{
			if (fromType == toType)
				return (x => x);

			var key = new OperatorDispatchKey(ExpressionType.ConvertChecked, fromType, toType);
			OperatorImplementation impl;
			if (!OperatorImplementations.TryGetValue(key, out impl))
				return null;

			return impl.Arg1Converter;
		}

		// Generates of binary implementations for matched argument types
		public virtual void InitBinaryOperatorImplementationsForMatchedTypes()
		{
			// For each operator, we add a series of implementation methods for same-type operands. They are saved as OperatorImplementation
			// records in OperatorImplementations table. This happens at initialization time.
			// After this initialization (for same-type operands), system adds implementations for all type pairs (ex: int + double),
			// using these same-type implementations and appropriate type converters.
			// Note that arithmetics on byte, sbyte, int16, uint16 are performed in Int32 format (the way it's done in c# I guess)
			// so the result is always Int32. We do not define operators for sbyte, byte, int16 and UInt16 types - they will
			// be processed using Int32 implementation, with appropriate type converters.
			ExpressionType op = ExpressionType.AddChecked;

			this.AddBinaryBoxed(op, typeof(Int32), (x, y) => this.boxes[checked((Int32) x + (Int32) y) + _boxesMiddle], (x, y) => checked((Int32) x + (Int32) y));
			this.AddBinary(op, typeof(UInt32), (x, y) => checked((UInt32) x + (UInt32) y));
			this.AddBinary(op, typeof(Int64), (x, y) => checked((Int64) x + (Int64) y));
			this.AddBinary(op, typeof(UInt64), (x, y) => checked((UInt64) x + (UInt64) y));
			this.AddBinary(op, typeof(Single), (x, y) => (Single) x + (Single) y);
			this.AddBinary(op, typeof(double), (x, y) => (double) x + (double) y);
			this.AddBinary(op, typeof(decimal), (x, y) => (decimal) x + (decimal) y);

			if (this.supportsBigInt)
				this.AddBinary(op, typeof(BigInteger), (x, y) => (BigInteger) x + (BigInteger) y);

			if (this.supportsComplex)
				this.AddBinary(op, typeof(Complex), (x, y) => (Complex) x + (Complex) y);

			this.AddBinary(op, typeof(string), (x, y) => (string) x + (string) y);

			// Force to concatenate as strings
			this.AddBinary(op, typeof(char), (x, y) => ((char) x).ToString() + (char) y);

			op = ExpressionType.SubtractChecked;
			this.AddBinaryBoxed(op, typeof(Int32), (x, y) => this.boxes[checked((Int32) x - (Int32) y) + _boxesMiddle], (x, y) => checked((Int32) x - (Int32) y));
			this.AddBinary(op, typeof(UInt32), (x, y) => checked((UInt32) x - (UInt32) y));
			this.AddBinary(op, typeof(Int64), (x, y) => checked((Int64) x - (Int64) y));
			this.AddBinary(op, typeof(UInt64), (x, y) => checked((UInt64) x - (UInt64) y));
			this.AddBinary(op, typeof(Single), (x, y) => (Single) x - (Single) y);
			this.AddBinary(op, typeof(double), (x, y) => (double) x - (double) y);
			this.AddBinary(op, typeof(decimal), (x, y) => (decimal) x - (decimal) y);

			if (this.supportsBigInt)
				this.AddBinary(op, typeof(BigInteger), (x, y) => (BigInteger) x - (BigInteger) y);

			if (this.supportsComplex)
				this.AddBinary(op, typeof(Complex), (x, y) => (Complex) x - (Complex) y);

			op = ExpressionType.MultiplyChecked;
			this.AddBinaryBoxed(op, typeof(Int32), (x, y) => this.boxes[checked((Int32) x * (Int32) y) + _boxesMiddle], (x, y) => checked((Int32) x * (Int32) y));
			this.AddBinary(op, typeof(UInt32), (x, y) => checked((UInt32) x * (UInt32) y));
			this.AddBinary(op, typeof(Int64), (x, y) => checked((Int64) x * (Int64) y));
			this.AddBinary(op, typeof(UInt64), (x, y) => checked((UInt64) x * (UInt64) y));
			this.AddBinary(op, typeof(Single), (x, y) => (Single) x * (Single) y);
			this.AddBinary(op, typeof(double), (x, y) => (double) x * (double) y);
			this.AddBinary(op, typeof(decimal), (x, y) => (decimal) x * (decimal) y);

			if (this.supportsBigInt)
				this.AddBinary(op, typeof(BigInteger), (x, y) => (BigInteger) x * (BigInteger) y);

			if (this.supportsComplex)
				this.AddBinary(op, typeof(Complex), (x, y) => (Complex) x * (Complex) y);

			op = ExpressionType.Divide;
			this.AddBinary(op, typeof(Int32), (x, y) => checked((Int32) x / (Int32) y));
			this.AddBinary(op, typeof(UInt32), (x, y) => checked((UInt32) x / (UInt32) y));
			this.AddBinary(op, typeof(Int64), (x, y) => checked((Int64) x / (Int64) y));
			this.AddBinary(op, typeof(UInt64), (x, y) => checked((UInt64) x / (UInt64) y));
			this.AddBinary(op, typeof(Single), (x, y) => (Single) x / (Single) y);
			this.AddBinary(op, typeof(double), (x, y) => (double) x / (double) y);
			this.AddBinary(op, typeof(decimal), (x, y) => (decimal) x / (decimal) y);

			if (this.supportsBigInt)
				this.AddBinary(op, typeof(BigInteger), (x, y) => (BigInteger) x / (BigInteger) y);

			if (this.supportsComplex)
				this.AddBinary(op, typeof(Complex), (x, y) => (Complex) x / (Complex) y);

			op = ExpressionType.Modulo;
			this.AddBinary(op, typeof(Int32), (x, y) => checked((Int32) x % (Int32) y));
			this.AddBinary(op, typeof(UInt32), (x, y) => checked((UInt32) x % (UInt32) y));
			this.AddBinary(op, typeof(Int64), (x, y) => checked((Int64) x % (Int64) y));
			this.AddBinary(op, typeof(UInt64), (x, y) => checked((UInt64) x % (UInt64) y));
			this.AddBinary(op, typeof(Single), (x, y) => (Single) x % (Single) y);
			this.AddBinary(op, typeof(double), (x, y) => (double) x % (double) y);
			this.AddBinary(op, typeof(decimal), (x, y) => (decimal) x % (decimal) y);

			if (this.supportsBigInt)
				this.AddBinary(op, typeof(BigInteger), (x, y) => (BigInteger) x % (BigInteger) y);

			// For bitwise operator, we provide explicit implementations for "small" integer types
			op = ExpressionType.And;
			this.AddBinary(op, typeof(bool), (x, y) => (bool) x & (bool) y);
			this.AddBinary(op, typeof(sbyte), (x, y) => (sbyte) x & (sbyte) y);
			this.AddBinary(op, typeof(byte), (x, y) => (byte) x & (byte) y);
			this.AddBinary(op, typeof(Int16), (x, y) => (Int16) x & (Int16) y);
			this.AddBinary(op, typeof(UInt16), (x, y) => (UInt16) x & (UInt16) y);
			this.AddBinary(op, typeof(Int32), (x, y) => (Int32) x & (Int32) y);
			this.AddBinary(op, typeof(UInt32), (x, y) => (UInt32) x & (UInt32) y);
			this.AddBinary(op, typeof(Int64), (x, y) => (Int64) x & (Int64) y);
			this.AddBinary(op, typeof(UInt64), (x, y) => (UInt64) x & (UInt64) y);

			op = ExpressionType.Or;
			this.AddBinary(op, typeof(bool), (x, y) => (bool) x | (bool) y);
			this.AddBinary(op, typeof(sbyte), (x, y) => (sbyte) x | (sbyte) y);
			this.AddBinary(op, typeof(byte), (x, y) => (byte) x | (byte) y);
			this.AddBinary(op, typeof(Int16), (x, y) => (Int16) x | (Int16) y);
			this.AddBinary(op, typeof(UInt16), (x, y) => (UInt16) x | (UInt16) y);
			this.AddBinary(op, typeof(Int32), (x, y) => (Int32) x | (Int32) y);
			this.AddBinary(op, typeof(UInt32), (x, y) => (UInt32) x | (UInt32) y);
			this.AddBinary(op, typeof(Int64), (x, y) => (Int64) x | (Int64) y);
			this.AddBinary(op, typeof(UInt64), (x, y) => (UInt64) x | (UInt64) y);

			op = ExpressionType.ExclusiveOr;
			this.AddBinary(op, typeof(bool), (x, y) => (bool) x ^ (bool) y);
			this.AddBinary(op, typeof(sbyte), (x, y) => (sbyte) x ^ (sbyte) y);
			this.AddBinary(op, typeof(byte), (x, y) => (byte) x ^ (byte) y);
			this.AddBinary(op, typeof(Int16), (x, y) => (Int16) x ^ (Int16) y);
			this.AddBinary(op, typeof(UInt16), (x, y) => (UInt16) x ^ (UInt16) y);
			this.AddBinary(op, typeof(Int32), (x, y) => (Int32) x ^ (Int32) y);
			this.AddBinary(op, typeof(UInt32), (x, y) => (UInt32) x ^ (UInt32) y);
			this.AddBinary(op, typeof(Int64), (x, y) => (Int64) x ^ (Int64) y);
			this.AddBinary(op, typeof(UInt64), (x, y) => (UInt64) x ^ (UInt64) y);

			op = ExpressionType.LessThan;
			this.AddBinary(op, typeof(Int32), (x, y) => checked((Int32) x < (Int32) y), this.BoolResultConverter);
			this.AddBinary(op, typeof(UInt32), (x, y) => checked((UInt32) x < (UInt32) y), this.BoolResultConverter);
			this.AddBinary(op, typeof(Int64), (x, y) => checked((Int64) x < (Int64) y), this.BoolResultConverter);
			this.AddBinary(op, typeof(UInt64), (x, y) => checked((UInt64) x < (UInt64) y), this.BoolResultConverter);
			this.AddBinary(op, typeof(Single), (x, y) => (Single) x < (Single) y, this.BoolResultConverter);
			this.AddBinary(op, typeof(double), (x, y) => (double) x < (double) y, this.BoolResultConverter);
			this.AddBinary(op, typeof(decimal), (x, y) => (decimal) x < (decimal) y);

			if (this.supportsBigInt)
				this.AddBinary(op, typeof(BigInteger), (x, y) => (BigInteger) x < (BigInteger) y, this.BoolResultConverter);

			op = ExpressionType.GreaterThan;
			this.AddBinary(op, typeof(Int32), (x, y) => checked((Int32) x > (Int32) y), this.BoolResultConverter);
			this.AddBinary(op, typeof(UInt32), (x, y) => checked((UInt32) x > (UInt32) y), this.BoolResultConverter);
			this.AddBinary(op, typeof(Int64), (x, y) => checked((Int64) x > (Int64) y), this.BoolResultConverter);
			this.AddBinary(op, typeof(UInt64), (x, y) => checked((UInt64) x > (UInt64) y), this.BoolResultConverter);
			this.AddBinary(op, typeof(Single), (x, y) => (Single) x > (Single) y, this.BoolResultConverter);
			this.AddBinary(op, typeof(double), (x, y) => (double) x > (double) y, this.BoolResultConverter);
			this.AddBinary(op, typeof(decimal), (x, y) => (decimal) x > (decimal) y);

			if (this.supportsBigInt)
				this.AddBinary(op, typeof(BigInteger), (x, y) => (BigInteger) x > (BigInteger) y, this.BoolResultConverter);

			op = ExpressionType.LessThanOrEqual;
			this.AddBinary(op, typeof(Int32), (x, y) => checked((Int32) x <= (Int32) y), this.BoolResultConverter);
			this.AddBinary(op, typeof(UInt32), (x, y) => checked((UInt32) x <= (UInt32) y), this.BoolResultConverter);
			this.AddBinary(op, typeof(Int64), (x, y) => checked((Int64) x <= (Int64) y), this.BoolResultConverter);
			this.AddBinary(op, typeof(UInt64), (x, y) => checked((UInt64) x <= (UInt64) y), this.BoolResultConverter);
			this.AddBinary(op, typeof(Single), (x, y) => (Single) x <= (Single) y, this.BoolResultConverter);
			this.AddBinary(op, typeof(double), (x, y) => (double) x <= (double) y, this.BoolResultConverter);
			this.AddBinary(op, typeof(decimal), (x, y) => (decimal) x <= (decimal) y);

			if (this.supportsBigInt)
				this.AddBinary(op, typeof(BigInteger), (x, y) => (BigInteger) x <= (BigInteger) y, this.BoolResultConverter);

			op = ExpressionType.GreaterThanOrEqual;
			this.AddBinary(op, typeof(Int32), (x, y) => checked((Int32) x >= (Int32) y), this.BoolResultConverter);
			this.AddBinary(op, typeof(UInt32), (x, y) => checked((UInt32) x >= (UInt32) y), this.BoolResultConverter);
			this.AddBinary(op, typeof(Int64), (x, y) => checked((Int64) x >= (Int64) y), this.BoolResultConverter);
			this.AddBinary(op, typeof(UInt64), (x, y) => checked((UInt64) x >= (UInt64) y), this.BoolResultConverter);
			this.AddBinary(op, typeof(Single), (x, y) => (Single) x >= (Single) y, this.BoolResultConverter);
			this.AddBinary(op, typeof(double), (x, y) => (double) x >= (double) y, this.BoolResultConverter);
			this.AddBinary(op, typeof(decimal), (x, y) => (decimal) x >= (decimal) y);

			if (this.supportsBigInt)
				this.AddBinary(op, typeof(BigInteger), (x, y) => (BigInteger) x >= (BigInteger) y, this.BoolResultConverter);

			op = ExpressionType.Equal;
			this.AddBinary(op, typeof(Int32), (x, y) => checked((Int32) x == (Int32) y), this.BoolResultConverter);
			this.AddBinary(op, typeof(UInt32), (x, y) => checked((UInt32) x == (UInt32) y), this.BoolResultConverter);
			this.AddBinary(op, typeof(Int64), (x, y) => checked((Int64) x == (Int64) y), this.BoolResultConverter);
			this.AddBinary(op, typeof(UInt64), (x, y) => checked((UInt64) x == (UInt64) y), this.BoolResultConverter);
			this.AddBinary(op, typeof(Single), (x, y) => (Single) x == (Single) y, this.BoolResultConverter);
			this.AddBinary(op, typeof(double), (x, y) => (double) x == (double) y, this.BoolResultConverter);
			this.AddBinary(op, typeof(decimal), (x, y) => (decimal) x == (decimal) y);

			if (this.supportsBigInt)
				this.AddBinary(op, typeof(BigInteger), (x, y) => (BigInteger) x == (BigInteger) y, this.BoolResultConverter);

			op = ExpressionType.NotEqual;
			this.AddBinary(op, typeof(Int32), (x, y) => checked((Int32) x != (Int32) y), this.BoolResultConverter);
			this.AddBinary(op, typeof(UInt32), (x, y) => checked((UInt32) x != (UInt32) y), this.BoolResultConverter);
			this.AddBinary(op, typeof(Int64), (x, y) => checked((Int64) x != (Int64) y), this.BoolResultConverter);
			this.AddBinary(op, typeof(UInt64), (x, y) => checked((UInt64) x != (UInt64) y), this.BoolResultConverter);
			this.AddBinary(op, typeof(Single), (x, y) => (Single) x != (Single) y, this.BoolResultConverter);
			this.AddBinary(op, typeof(double), (x, y) => (double) x != (double) y, this.BoolResultConverter);
			this.AddBinary(op, typeof(decimal), (x, y) => (decimal) x != (decimal) y);

			if (this.supportsBigInt)
				this.AddBinary(op, typeof(BigInteger), (x, y) => (BigInteger) x != (BigInteger) y, this.BoolResultConverter);
		}

		public virtual void InitUnaryOperatorImplementations()
		{
			var op = ExpressionType.UnaryPlus;
			this.AddUnary(op, typeof(sbyte), x => +(sbyte) x);
			this.AddUnary(op, typeof(byte), x => +(byte) x);
			this.AddUnary(op, typeof(Int16), x => +(Int16) x);
			this.AddUnary(op, typeof(UInt16), x => +(UInt16) x);
			this.AddUnary(op, typeof(Int32), x => +(Int32) x);
			this.AddUnary(op, typeof(UInt32), x => +(UInt32) x);
			this.AddUnary(op, typeof(Int64), x => +(Int64) x);
			this.AddUnary(op, typeof(UInt64), x => +(UInt64) x);
			this.AddUnary(op, typeof(Single), x => +(Single) x);
			this.AddUnary(op, typeof(double), x => +(double) x);
			this.AddUnary(op, typeof(decimal), x => +(decimal) x);

			if (this.supportsBigInt)
				this.AddUnary(op, typeof(BigInteger), x => +(BigInteger) x);

			op = ExpressionType.Negate;
			this.AddUnary(op, typeof(sbyte), x => -(sbyte) x);
			this.AddUnary(op, typeof(byte), x => -(byte) x);
			this.AddUnary(op, typeof(Int16), x => -(Int16) x);
			this.AddUnary(op, typeof(UInt16), x => -(UInt16) x);
			this.AddUnary(op, typeof(Int32), x => -(Int32) x);
			this.AddUnary(op, typeof(UInt32), x => -(UInt32) x);
			this.AddUnary(op, typeof(Int64), x => -(Int64) x);
			this.AddUnary(op, typeof(Single), x => -(Single) x);
			this.AddUnary(op, typeof(double), x => -(double) x);
			this.AddUnary(op, typeof(decimal), x => -(decimal) x);

			if (this.supportsBigInt)
				this.AddUnary(op, typeof(BigInteger), x => -(BigInteger) x);

			if (this.supportsComplex)
				this.AddUnary(op, typeof(Complex), x => -(Complex) x);

			op = ExpressionType.Not;
			this.AddUnary(op, typeof(bool), x => !(bool) x);
			this.AddUnary(op, typeof(sbyte), x => ~(sbyte) x);
			this.AddUnary(op, typeof(byte), x => ~(byte) x);
			this.AddUnary(op, typeof(Int16), x => ~(Int16) x);
			this.AddUnary(op, typeof(UInt16), x => ~(UInt16) x);
			this.AddUnary(op, typeof(Int32), x => ~(Int32) x);
			this.AddUnary(op, typeof(UInt32), x => ~(UInt32) x);
			this.AddUnary(op, typeof(Int64), x => ~(Int64) x);
		}

		protected virtual OperatorImplementation CreateBinaryOperatorImplementation(ExpressionType op, Type arg1Type, Type arg2Type,
			Type commonType, BinaryOperatorMethod method, UnaryOperatorMethod resultConverter)
		{
			var key = new OperatorDispatchKey(op, arg1Type, arg2Type);
			var arg1Converter = arg1Type == commonType ? null : this.GetConverter(arg1Type, commonType);
			var arg2Converter = arg2Type == commonType ? null : this.GetConverter(arg2Type, commonType);
			var impl = new OperatorImplementation(key, commonType, method, arg1Converter, arg2Converter, resultConverter);

			return impl;
		}

		// Creates overflow handlers. For each implementation, checks if operator can overflow;
		// if yes, creates and sets an overflow handler - another implementation that performs
		// operation using "upper" type that wouldn't overflow. For ex: (int * int) has overflow handler (int64 * int64)
		protected virtual void CreateOverflowHandlers()
		{
			foreach (var impl in OperatorImplementations.Values)
			{
				if (!CanOverflow(impl))
					continue;

				var key = impl.Key;
				var upType = this.GetUpType(impl.CommonType);
				if (upType == null)
					continue;

				var upBaseImpl = this.FindBaseImplementation(key.Op, upType);
				if (upBaseImpl == null)
					continue;

				impl.OverflowHandler = this.CreateBinaryOperatorImplementation(key.Op, key.Arg1Type, key.Arg2Type, upType,
					upBaseImpl.BaseBinaryMethod, upBaseImpl.ResultConverter);

				// Do not put OverflowHandler into OperatoImplementations table! - it will override some other, non-overflow impl
			}
		}

		private OperatorImplementation FindBaseImplementation(ExpressionType op, Type commonType)
		{
			var baseKey = new OperatorDispatchKey(op, commonType, commonType);

			OperatorImplementation baseImpl;
			this.OperatorImplementations.TryGetValue(baseKey, out baseImpl);

			return baseImpl;
		}

		#endregion Binary operators implementations

		#region Utilities

		/// <summary>
		/// Note bool type at the end - if any of operands is of bool type, convert the other to bool as well
		/// </summary>
		private static TypeList _typesSequence = new TypeList(
			typeof(sbyte), typeof(Int16), typeof(Int32), typeof(Int64), typeof(BigInteger), // typeof(Rational)
			typeof(Single), typeof(Double), typeof(Complex),
			typeof(bool), typeof(char), typeof(string)
		);

		private static TypeList _unsignedTypes = new TypeList(
		  typeof(byte), typeof(UInt16), typeof(UInt32), typeof(UInt64)
		);

		/// <summary>
		/// Returns the type to which arguments should be converted to perform the operation
		/// for a given operator and arguments types.
		/// </summary>
		/// <param name="op">Operator.</param>
		/// <param name="argType1">The type of the first argument.</param>
		/// <param name="argType2">The type of the second argument</param>
		/// <returns>A common type for operation.</returns>
		protected virtual Type GetCommonTypeForOperator(ExpressionType op, Type argType1, Type argType2)
		{
			if (argType1 == argType2)
				return argType1;

			// TODO: see how to handle properly null/NoneValue in expressions
			// var noneType = typeof(NoneClass);
			// if (argType1 == noneType || argType2 == noneType) return noneType;

			// Check for unsigned types and convert to signed versions
			var t1 = this.GetSignedTypeForUnsigned(argType1);
			var t2 = this.GetSignedTypeForUnsigned(argType2);

			// The type with higher index in _typesSequence is the commont type
			var index1 = _typesSequence.IndexOf(t1);
			var index2 = _typesSequence.IndexOf(t2);

			if (index1 >= 0 && index2 >= 0)
				return _typesSequence[Math.Max(index1, index2)];

			// If we have some custom type,
			return null;
		}

		/// <summary>
		/// If a type is one of "unsigned" int types, returns next bigger signed type
		/// </summary>
		/// <param name="type"></param>
		/// <returns></returns>
		protected virtual Type GetSignedTypeForUnsigned(Type type)
		{
			if (!_unsignedTypes.Contains(type))
				return type;

			if (type == typeof(byte) || type == typeof(UInt16))
				return typeof(int);

			if (type == typeof(UInt32))
				return typeof(Int64);

			if (type == typeof(UInt64))
				return typeof(Int64); //let's remain in Int64

			return typeof(BigInteger);
		}

		/// <summary>
		/// Returns the "up-type" to use in operation instead of the type that caused overflow.
		/// </summary>
		/// <param name="type">The base type for operation that caused overflow.</param>
		/// <returns>The type to use for operation.</returns>
		/// <remarks>
		/// Can be overwritten in language implementation to implement different type-conversion policy.
		/// </remarks>
		protected virtual Type GetUpType(Type type)
		{
			// In fact we do not need to care about unsigned types - they are eliminated from common types for operations,
			//  so "type" parameter can never be unsigned type. But just in case...
			if (_unsignedTypes.Contains(type))
				// It will return "upped" type in fact
				return GetSignedTypeForUnsigned(type);

			if (type == typeof(byte) || type == typeof(sbyte) || type == typeof(UInt16) || type == typeof(Int16))
				return typeof(int);

			if (type == typeof(Int32))
				return typeof(Int64);

			if (type == typeof(Int64))
				return typeof(BigInteger);

			return null;
		}

		private static bool CanOverflow(OperatorImplementation impl)
		{
			if (!CanOverflow(impl.Key.Op))
				return false;

			if (impl.CommonType == typeof(Int32) && IsSmallInt(impl.Key.Arg1Type) && IsSmallInt(impl.Key.Arg2Type))
				return false;

			if (impl.CommonType == typeof(double) || impl.CommonType == typeof(Single))
				return false;

			if (impl.CommonType == typeof(BigInteger))
				return false;

			return true;
		}

		private static bool CanOverflow(ExpressionType expression)
		{
			return _overflowOperators.Contains(expression);
		}

		private static bool IsSmallInt(Type type)
		{
			return type == typeof(byte) || type == typeof(sbyte) || type == typeof(Int16) || type == typeof(UInt16);
		}

		#endregion Utilities
	}
}
