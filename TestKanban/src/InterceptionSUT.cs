using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UnitTest
{
    public class InterceptionSUT : InterceptionSUTInterface
    {
        public void VoidMethod(out int param)
        {
            param = 10;
        }
        public int Times2(int n)
        {
            return n * 2;
        }

        public bool GenericMethod<T>(T x, T y)
        {
            return x.Equals(y);
        }

        public async Task<int> AyncMethod(int n)
        {
            int result = 0;
            await Task.Run(() =>
            {
                Thread.Sleep(100);
                result = n * 2;
            });
            return result;
        }

        public async Task<bool> AyncTemplateMethod<T>(T x, T y)
        {
            bool result = false;
            await Task.Run(() =>
            {
                Thread.Sleep(100);
                result = GenericMethod<T>(x, y);
            });
            return result;
        }

        public async Task<T> AyncTemplateAdd<T>(T x, T y)
        {
            T result = default(T);
            await Task.Run(() =>
            {
                Thread.Sleep(100);
                result = Add<T>(x, y);
            });
            return result;
        }

        T Add<T>(T left, T right)
        {
            // Note: Need to add a reference to Microsoft.CSharp.dll for using dynamic type
            return ((dynamic)left + (dynamic)right);
        }

        public T GenericMethodReturnDefault<T>()
        {
            return default(T);
        }

        public async Task<T> AyncTemplateMethodReturnDefault<T>()
        {
            T result = default(T);
            await Task.Run(() =>
            {
                Thread.Sleep(100);
                result = GenericMethodReturnDefault<T>();
            });
            return result;
        }
    }
}
