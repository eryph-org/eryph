using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;

namespace HyperVPlus.VmManagement
{
    public class TypedPsObject<T>
    {
        public T Value { get; private set; }
        public PSObject PsObject { get; }
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1,1);

        public TypedPsObject(PSObject psObject)
        {
            PsObject = psObject;
        }

        public static implicit operator T(TypedPsObject<T> typed)
        {
            return typed.Value;
        }


        public async Task<TypedPsObjectSynchronization> AquireLockAsync()
        {
            await _semaphore.WaitAsync().ConfigureAwait(false);
            return new TypedPsObjectSynchronization(_semaphore);
        }

        public IEnumerable<TypedPsObject<TSub>> GetList<TSub>(
            Expression<Func<T,IList<TSub>>> listProperty,
            Func<TSub, bool> predicateFunc)
        {
            var getExpression = listProperty.Compile();

            var paramType = listProperty.Parameters[0].Type;  // first parameter of expression
            var property = paramType.GetMember((listProperty.Body as MemberExpression)?.Member.Name)[0];

            var list = getExpression(this);
            var psList = PsObject.Properties[property.Name].Value as IList;

            //if (psList?.Count != list.Count)
            //{
            //    Refresh();
            //    list = getExpression(this);         
            //}

            for (var index = 0; index < list.Count; index++)
            {
                var entry = list[index];

                if (!predicateFunc(entry)) continue;
                Debug.Assert(psList != null, nameof(psList) + " != null");
                var psSubObject = new PSObject(psList[index]);

                yield return new TypedPsObject<TSub>(psSubObject);
            }
        }

    }

    public class TypedPsObjectSynchronization : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;

        public TypedPsObjectSynchronization(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }


        public void Dispose()
        {
            _semaphore.Release(1);
        }
    }
}