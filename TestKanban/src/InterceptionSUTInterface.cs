using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTest
{
    public interface InterceptionSUTInterface
    {
        void VoidMethod(out int param);
        int Times2(int n);
        T GenericMethodReturnDefault<T>();
        bool GenericMethod<T>(T x, T y);
        Task<int> AyncMethod(int n);
        Task<bool> AyncTemplateMethod<T>(T x, T y);
        Task<T> AyncTemplateAdd<T>(T x, T y);
        Task<T> AyncTemplateMethodReturnDefault<T>();
    }
}
