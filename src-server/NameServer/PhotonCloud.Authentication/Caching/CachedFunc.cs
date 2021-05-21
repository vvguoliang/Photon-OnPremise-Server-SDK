// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CachedFunc.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the CachedFunc type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

//namespace Photon.VirtualApps.Master.Caching
//{
//    using System;

//    public class CachedFunc<T, TResult> : CachedFuncBase<T, TResult>
//    {
//        #region Constants and Fields

//        private readonly Func<T, TResult> function;

//        #endregion

//        #region Constructors and Destructors

//        public CachedFunc(Func<T, TResult> function, TimeSpan updateInterval, TimeSpan resultExpiration)
//            : base(updateInterval, resultExpiration)
//        {
//            this.function = function;
//        }

//        #endregion

//        #region Methods

//        protected override TResult Invoke(T args)
//        {
//            return this.function(args);
//        }

//        #endregion
//    }

//    public class CachedFunc<T1, T2, TResult> : CachedFuncBase<FuncParams<T1, T2>, TResult>
//    {
//        #region Constants and Fields

//        private readonly Func<T1, T2, TResult> function;

//        #endregion

//        #region Constructors and Destructors

//        public CachedFunc(Func<T1, T2, TResult> function, TimeSpan updateInterval, TimeSpan resultExpiration)
//            : base(updateInterval, resultExpiration)
//        {
//            this.function = function;
//        }

//        #endregion

//        #region Public Methods

//        public TResult GetResult(T1 para1, T2 para2)
//        {
//            return this.GetResult(new FuncParams<T1, T2>(para1, para2));
//        }

//        #endregion

//        #region Methods

//        protected override TResult Invoke(FuncParams<T1, T2> args)
//        {
//            return this.function(args.Param1, args.Param2);
//        }

//        #endregion
//    }

//    public class CachedFunc<T1, T2, T3, TResult> : CachedFuncBase<FuncParams<T1, T2, T3>, TResult>
//    {
//        #region Constants and Fields

//        private readonly Func<T1, T2, T3, TResult> function;

//        #endregion

//        #region Constructors and Destructors

//        public CachedFunc(Func<T1, T2, T3, TResult> function, TimeSpan updateInterval, TimeSpan resultExpiration)
//            : base(updateInterval, resultExpiration)
//        {
//            this.function = function;
//        }

//        #endregion

//        #region Public Methods

//        public TResult GetResult(T1 para1, T2 para2, T3 para3)
//        {
//            return this.GetResult(new FuncParams<T1, T2, T3>(para1, para2, para3));
//        }

//        #endregion

//        #region Methods

//        protected override TResult Invoke(FuncParams<T1, T2, T3> args)
//        {
//            return this.function(args.Param1, args.Param2, args.Param3);
//        }

//        #endregion
//    }

//    public class CachedFunc<T1, T2, T3, T4, TResult> : CachedFuncBase<FuncParams<T1, T2, T3, T4>, TResult>
//    {
//        #region Constants and Fields

//        private readonly Func<T1, T2, T3, T4, TResult> function;

//        #endregion

//        #region Constructors and Destructors

//        public CachedFunc(Func<T1, T2, T3, T4, TResult> function, TimeSpan updateInterval, TimeSpan resultExpiration)
//            : base(updateInterval, resultExpiration)
//        {
//            this.function = function;
//        }

//        #endregion

//        #region Public Methods

//        public TResult GetResult(T1 para1, T2 para2, T3 para3, T4 para4)
//        {
//            return this.GetResult(new FuncParams<T1, T2, T3, T4>(para1, para2, para3, para4));
//        }

//        #endregion

//        #region Methods

//        protected override TResult Invoke(FuncParams<T1, T2, T3, T4> args)
//        {
//            return this.function(args.Param1, args.Param2, args.Param3, args.Param4);
//        }

//        #endregion
//    }

//    public class FuncParams<T1, T2> : IEquatable<FuncParams<T1, T2>>
//    {
//        #region Constants and Fields

//        public readonly int HashCode;

//        public readonly T1 Param1;

//        public readonly T2 Param2;

//        #endregion

//        #region Constructors and Destructors

//        public FuncParams(T1 para1, T2 para2)
//        {
//            this.Param1 = para1;
//            this.Param2 = para2;
//            this.HashCode = para1.GetHashCode() | para2.GetHashCode();
//        }

//        #endregion

//        #region Public Methods

//        public override int GetHashCode()
//        {
//            return this.HashCode;
//        }

//        #endregion

//        #region Implemented Interfaces

//        #region IEquatable<FuncParams<T1,T2>>

//        public bool Equals(FuncParams<T1, T2> other)
//        {
//            return other.Param1.Equals(this.Param1) && other.Param2.Equals(this.Param2);
//        }

//        #endregion

//        #endregion
//    }

//    public class FuncParams<T1, T2, T3> : IEquatable<FuncParams<T1, T2, T3>>
//    {
//        #region Constants and Fields

//        public readonly int HashCode;

//        public readonly T1 Param1;

//        public readonly T2 Param2;

//        public readonly T3 Param3;

//        #endregion

//        #region Constructors and Destructors

//        public FuncParams(T1 para1, T2 para2, T3 para3)
//        {
//            this.Param1 = para1;
//            this.Param2 = para2;
//            this.Param3 = para3;

//            this.HashCode = para1.GetHashCode() | para2.GetHashCode() | para3.GetHashCode();
//        }

//        #endregion

//        #region Public Methods

//        public override int GetHashCode()
//        {
//            return this.HashCode;
//        }

//        #endregion

//        #region Implemented Interfaces

//        #region IEquatable<FuncParams<T1,T2,T3>>

//        public bool Equals(FuncParams<T1, T2, T3> other)
//        {
//            return other.Param1.Equals(this.Param1) && other.Param2.Equals(this.Param2) && other.Param3.Equals(this.Param3);
//        }

//        #endregion

//        #endregion
//    }

//    public class FuncParams<T1, T2, T3, T4> : IEquatable<FuncParams<T1, T2, T3, T4>>
//    {
//        #region Constants and Fields

//        public readonly int HashCode;

//        public readonly T1 Param1;

//        public readonly T2 Param2;

//        public readonly T3 Param3;

//        public readonly T4 Param4;

//        #endregion

//        #region Constructors and Destructors

//        public FuncParams(T1 para1, T2 para2, T3 para3, T4 para4)
//        {
//            this.Param1 = para1;
//            this.Param2 = para2;
//            this.Param3 = para3;
//            this.Param4 = para4;

//            this.HashCode = para1.GetHashCode() | para2.GetHashCode() | para3.GetHashCode() | para4.GetHashCode();
//        }

//        #endregion

//        #region Public Methods

//        public override int GetHashCode()
//        {
//            return this.HashCode;
//        }

//        #endregion

//        #region Implemented Interfaces

//        #region IEquatable<FuncParams<T1,T2,T3,T4>>

//        public bool Equals(FuncParams<T1, T2, T3, T4> other)
//        {
//            return other.Param1.Equals(this.Param1) && other.Param2.Equals(this.Param2) && other.Param3.Equals(this.Param3) && other.Param4.Equals(this.Param4);
//        }

//        #endregion

//        #endregion
//    }

//    public class FuncParams<T1, T2, T3, T4, T5> : IEquatable<FuncParams<T1, T2, T3, T4, T5>>
//    {
//        #region Constants and Fields

//        public readonly int HashCode;

//        public readonly T1 Param1;

//        public readonly T2 Param2;

//        public readonly T3 Param3;

//        public readonly T4 Param4;

//        public readonly T5 Param5;

//        #endregion

//        #region Constructors and Destructors

//        public FuncParams(T1 para1, T2 para2, T3 para3, T4 para4, T5 para5)
//        {
//            this.Param1 = para1;
//            this.Param2 = para2;
//            this.Param3 = para3;
//            this.Param4 = para4;
//            this.Param5 = para5;

//            this.HashCode = para1.GetHashCode() | para2.GetHashCode() | para3.GetHashCode() | para4.GetHashCode() | para5.GetHashCode();
//        }

//        #endregion

//        #region Public Methods

//        public override int GetHashCode()
//        {
//            return this.HashCode;
//        }

//        #endregion

//        #region Implemented Interfaces

//        #region IEquatable<FuncParams<T1,T2,T3,T4,T5>>

//        public bool Equals(FuncParams<T1, T2, T3, T4, T5> other)
//        {
//            return other.Param1.Equals(this.Param1) && other.Param2.Equals(this.Param2) && other.Param3.Equals(this.Param3) && other.Param4.Equals(this.Param4) &&
//                   other.Param5.Equals(this.Param5);
//        }

//        #endregion

//        #endregion
//    }
//}