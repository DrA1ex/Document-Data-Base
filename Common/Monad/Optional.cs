using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Common.Monad
{
    [DebuggerDisplay("Optional({IsDefined ? _value.ToString() : \"Empty\"})")]
    public class Optional<T>
    {
        public static Optional<T> Empty()
        {
            return new Optional<T>(false);
        }

        private readonly T _value;

        private Optional(bool defined)
        {
            IsDefined = defined;
        }

        public Optional(T value)
        {
            _value = value;
            IsDefined = _value != null;
        }

        public bool IsDefined { get; private set; }

        public T Get()
        {
            if(!IsDefined)
                throw new FieldAccessException("Value is not defined");

            return _value;
        }

        public Optional<TR> ThenGet<TR>(Func<T, TR> func)
        {
            return IsDefined ? new Optional<TR>(func(_value)) : Optional<TR>.Empty();
        }

        public Optional<TR> ThenGet<TR>(Func<T, Optional<TR>> func)
        {
            return IsDefined ? func(_value) : Optional<TR>.Empty();
        }

        public T OrElseGet(T empty)
        {
            return IsDefined ? _value : empty;
        }

        public Optional<T> OrElseGet(Func<Optional<T>> func)
        {
            return !IsDefined ? func() : this;
        }

        public Optional<T> OrElseGet(Func<T> func)
        {
            return !IsDefined ? new Optional<T>(func()) : this;
        }


        public Optional<T> Then(Action<T> action)
        {
            if(IsDefined)
            {
                action(_value);
            }

            return this;
        }

        public Optional<T> Then(Action func)
        {
            if(IsDefined)
            {
                func();
            }

            return this;
        }

        public Optional<T> ThenIf(Func<T, bool> predicate, Action<T> action)
        {
            if(IsDefined && predicate(_value))
            {
                action(_value);
            }

            return this;
        }

        public Optional<T> ThenIf(bool condition, Action<T> action)
        {
            if(IsDefined && condition)
            {
                action(_value);
            }

            return this;
        }

        public Optional<T> ThenIf(bool condition, Action func)
        {
            if(IsDefined && condition)
            {
                func();
            }

            return this;
        }

        public Optional<T> Otherwise(Action action)
        {
            if(!IsDefined)
            {
                action();
            }

            return this;
        }

        public void Always(Action action)
        {
            action();
        }
    }

    public static class DictionaryOptional
    {
        public static Optional<TR> TryGet<T, TR>(this IDictionary<T, TR> map,T key) 
            where T : class 
            where TR : class
        {
            TR result;
            return map.TryGetValue(key, out result) 
                ? new Optional<TR>(result) 
                : Optional<TR>.Empty();
        }
    }

    public static class EnumerableOptional
    {
        public static Optional<T> Get<T>(this IEnumerable<T> collection, Func<T, bool> predicate) where T: class 
        {
            return new Optional<T>(collection.SingleOrDefault(predicate));
        }
    }
}
