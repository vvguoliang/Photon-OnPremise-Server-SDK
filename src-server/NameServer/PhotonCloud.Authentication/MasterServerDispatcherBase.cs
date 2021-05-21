
using System.Collections;

namespace PhotonCloud.Authentication
{
    using System.Collections.Generic;

    public abstract class MasterServerDispatcherBase<T> : IEnumerable<T>
    {
        protected List<T> MasterServerInstances { get; set; }

        public T GetInstance(string key)
        {
            int index = GetIndex(key);
            if (index < 0)
            {
                return default(T);
            }
            
            return this.MasterServerInstances[index];
        }

        public int GetIndex(string key)
        {
            if (this.MasterServerInstances == null || this.MasterServerInstances.Count == 0)
            {
                return -1;
            }

            int index = 0;
            if (this.MasterServerInstances.Count > 1)
            {
                uint hash = this.GetHashFnva(key);
                index = (int)(hash % this.MasterServerInstances.Count);
            }

            return index;
        }

        /// <remarks>
        /// Custom hashcode generator is used because the NET Framework does not guarantee that the 
        /// hashcode from an object is the same on different plattforms.
        /// We need unique hashcodes on different plattforms because the game servers must generate 
        /// the same hashcode to get the same master server isnatnce for an application id.
        /// 
        /// From the MSDN:
        /// The .NET Framework does not guarantee the default implementation of the GetHashCode method, 
        /// and the value this method returns may differ between .NET Framework versions and platforms, 
        /// such as 32-bit and 64-bit platforms.
        /// </remarks>
        private uint GetHashFnva(string appId)
        {
            uint h = 2166136261;
            for (int i = 0; i < appId.Length; i++)
            {
                h = (h ^ appId[i]) * 16777619;
            }

            return h;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public IEnumerator<T> GetEnumerator()
        {
            return this.MasterServerInstances.GetEnumerator();
        }
    }
}
