using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Common.Utils
{
    public static class ReflectionUtils
    {
        public static TAttrType GetAttributeOfType<TSourceType, TAttrType>() where TAttrType : Attribute
        {
            var type = typeof(TSourceType);

            return GetAttribute<TAttrType>(type);
        }

        public static T GetAttribute<T>(this Type type) where T : Attribute
        {
            var attributes = type.GetCustomAttributes(typeof(T), false);
            return (attributes.Length > 0) ? (T)attributes[0] : null;
        }

        public static T GetAttributeOfType<T>(this PropertyInfo prop) where T : Attribute
        {
            var attributes = prop.GetCustomAttributes(typeof(T), false);
            return (attributes.Length > 0) ? (T)attributes[0] : null;
        }

        public static T GetAttributeOfType<T>(this object value) where T : Attribute
        {
            if(value == null)
                return null;

            var type = value.GetType();
            var memInfo = type.GetMember(value.ToString());
            var attributes = memInfo[0].GetCustomAttributes(typeof(T), false);
            return (attributes.Length > 0) ? (T)attributes[0] : null;
        }

        public static void SetValue(this object obj, string propName, object value)
        {
            var type = obj.GetType();

            var prop = type.GetProperty(propName,
                BindingFlags.SetProperty | BindingFlags.Public | BindingFlags.Instance);
            if(prop != null)
            {
                prop.SetValue(obj, value);
            }
        }

        public static object GetValue(this object obj, string propName)
        {
            var type = obj.GetType();

            var prop = type.GetProperty(propName,
                BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance);

            return prop.GetValue(obj);
        }

        public static object GetValue(this Type type, string propName)
        {
            var prop = type.GetProperty(propName,
                BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

            return prop.GetValue(null);
        }

        public static object GetValueFromPath(this object obj, string propPath, int depth = int.MaxValue)
        {
            var splitedPath = propPath.Split('.');
            var pathParts = splitedPath.Take(Math.Min(splitedPath.Length, depth));

            var result = obj;
            foreach(var pathPart in pathParts)
            {
                if(result == null)
                {
                    return null;
                }
                result = result.GetValue(pathPart);
            }

            return result;
        }

        public static void SetValueFromPath(this object obj, string propPath, object newValue, int depth = int.MaxValue)
        {
            var splitedPath = propPath.Split('.');
            var pathParts = splitedPath.Take(Math.Min(splitedPath.Length, depth));

            // ReSharper disable PossibleMultipleEnumeration
            var getPathParts = pathParts.Take(pathParts.Count() - 1);


            var result = obj;
            foreach(var pathPart in getPathParts)
            {
                result = result.GetValue(pathPart);
            }

            result.SetValue(pathParts.Last(), newValue);
            // ReSharper restore PossibleMultipleEnumeration
        }

        public static bool InvokeEqual(object a, object b)
        {
            if((a == null || b == null) && a == b)
            {
                return true;
            }
            if((a == null || b == null))
            {
                return false;
            }

            var type = a.GetType();

            var equalsMethod = type.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(c =>
                                                                                                {
                                                                                                    if(c.Name != "Equals")
                                                                                                    {
                                                                                                        return false;
                                                                                                    }

                                                                                                    var retType = c.ReturnType == typeof(bool);

                                                                                                    var args = c.GetParameters();
                                                                                                    var argsFlag = args.Count() == 2 && args.All(x => x.ParameterType == type);

                                                                                                    return retType && argsFlag;
                                                                                                }).ToArray();

            if(!equalsMethod.Any())
            {
                return Equals(a, b);
            }

            return (bool)equalsMethod.First().Invoke(null, new[] { a, b });
        }

        public static string GetPropName<TA, TR>(Expression<Func<TA, TR>> propertyExpression)
        {
            var lambda = propertyExpression as LambdaExpression;
            MemberExpression memberExpression;
            if(lambda.Body is UnaryExpression)
            {
                var unaryExpression = (UnaryExpression)lambda.Body;
                memberExpression = (MemberExpression)unaryExpression.Operand;
            }
            else
            {
                memberExpression = (MemberExpression)lambda.Body;
            }
            var propertyInfo = memberExpression.Member;

            return propertyInfo.Name;
        }

        public static string GetPath<TA, TR>(Expression<Func<TA, TR>> expr)
        {
            var stack = new Stack<string>();

            MemberExpression me;
            switch(expr.Body.NodeType)
            {
                case ExpressionType.Convert:
                case ExpressionType.ConvertChecked:
                    var ue = expr.Body as UnaryExpression;
                    me = ((ue != null) ? ue.Operand : null) as MemberExpression;
                    break;
                default:
                    me = expr.Body as MemberExpression;
                    break;
            }

            while(me != null)
            {
                stack.Push(me.Member.Name);
                me = me.Expression as MemberExpression;
            }

            return string.Join(".", stack.ToArray());
        }

        public static TR GetValueFromPath<TA, TR>(this object source, Expression<Func<TA, TR>> pathExpression)
        {
            var path = GetPath(pathExpression);
            return (TR)source.GetValueFromPath(path);
        }

        public static void SetValueFromPath<TA, TR>(this object source, Expression<Func<TA, TR>> pathExpression,
            TR value)
        {
            var path = GetPath(pathExpression);
            source.SetValueFromPath(path, value);
        }

        public static Func<TArg, T> GetMethod<T, TArg>(string name, bool @static = true)
        {
            var type = typeof(T);

            var flags = BindingFlags.InvokeMethod | BindingFlags.Public;

            flags |= @static ? BindingFlags.FlattenHierarchy | BindingFlags.Static : BindingFlags.Instance;


            var methods =
                type.GetMethods(flags)
                    .Where(c => c.Name == name && c.ReturnType == type && c.GetParameters().Length == 1);

            return (Func<TArg, T>)Delegate.CreateDelegate(typeof(Func<TArg, T>), methods.First(), true);
        }

        public static bool IsAssignableFrom<T>(this object obj)
        {
            var type = obj.GetType();
            return type.IsAssignableFrom(typeof(T));
        }
    }
}