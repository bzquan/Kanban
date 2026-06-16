using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Practices.Unity.InterceptionExtension;
using static Microsoft.Practices.Unity.InterceptionExtension.AsyncSupportExtension;
using MongoDB.Driver;
using System.Reflection;
using System.Collections.Concurrent;

namespace Kanban.Model
{
    /// <summary>
    /// See https://msdn.microsoft.com/en-us/magazine/dn574805.aspx
    /// </summary>
    public class SupressUpdateInterceptionBehavior : IInterceptionBehavior
    {
        string m_RetrieveMethodName;
        string m_UpdateMethodName;

        public SupressUpdateInterceptionBehavior(string retrieveMethodName, string updateMethodName)
        {
            m_RetrieveMethodName = retrieveMethodName;
            m_UpdateMethodName = updateMethodName;
        }

        bool SuppressUpdating { get; set; } = false;

        public IMethodReturn Invoke(IMethodInvocation input, GetNextInterceptionBehaviorDelegate getNext)
        {
            string methodName = input.MethodBase.Name;
            if (IsRetrievingMethod(methodName))
            {
                SuppressUpdating = true; // Before invoking the method on the original target.
                IMethodReturn result = getNext()(input, getNext);
                result.InvokeAfterCall(input, new Action<IMethodReturn>(x => SuppressUpdating = false));
            }

            if (SuppressUpdating && IsUpdatingMethod(methodName))
            {
                Task<UpdateResult> result_temp = new Task<UpdateResult>(() => new UpdateResult.Acknowledged(matchedCount: 0, modifiedCount: 0, upsertedId: null));
                return input.CreateMethodReturn(result_temp);
            }

            return getNext()(input, getNext);
        }
        bool IsRetrievingMethod(string methodName) => (methodName == m_RetrieveMethodName);
        bool IsUpdatingMethod(string methodName) => (methodName == m_UpdateMethodName);

        public IEnumerable<Type> GetRequiredInterfaces()
        {
            return Type.EmptyTypes;
        }

        public bool WillExecute
        {
            get { return true; }
        }
    }
}
