using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Photon.Common.Tools
{
    public class DictTools
    {
        public static T2 GetValue<T1, T2>(Dictionary<T1, T2> dict, T1 key)
        {
            T2 value;
            bool isSuccess = dict.TryGetValue(key, out value);
            if (isSuccess)
            {
                return value;
            }
            else
            {
                return default(T2);
            }
        }
    }
}
