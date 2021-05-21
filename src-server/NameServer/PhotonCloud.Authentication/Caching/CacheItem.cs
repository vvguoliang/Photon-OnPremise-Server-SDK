// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CacheItem.cs" company="Exit Games GmbH">
//   Copyright (c) Exit Games GmbH.  All rights reserved.
// </copyright>
// <summary>
//   Defines the CacheItem type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Photon.VirtualApps.Master.Caching
{
    using System;

    public class CacheItem<T>
    {
        /// <summary>
        /// Gets the UTC creation time of the cache item.
        /// </summary>
        public readonly DateTime UtcCreated;

        /// <summary>
        /// Gets the value.
        /// </summary>
        public readonly T Value;

        /// <summary>
        /// Initializes a new instance of the <see cref="CacheItem{T}"/> class.
        /// </summary>
        public CacheItem()
        {
            this.Value = default(T);
            this.UtcCreated = DateTime.MinValue;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CacheItem{T}"/> class.
        /// </summary>
        /// <param name="item">The item.</param>
        public CacheItem(T item)
        {
            this.Value = item;
            this.UtcCreated = DateTime.UtcNow;
        }

        /// <summary>
        /// Gets the itema age.
        /// </summary>
        public TimeSpan ItemAge
        {
            get
            {
                return DateTime.UtcNow.Subtract(this.UtcCreated);
            }
        }
    }
}
