// MIT License
//
// Copyright (c) 2017 Maarten van Sambeek.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

namespace ConnectQl.ExtensionMethods
{
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;

    using JetBrains.Annotations;

    /// <summary>
    /// The type extensions.
    /// </summary>
    internal static class TypeExtensions
    {
        /// <summary>
        /// Gets a generic method on a type.
        /// </summary>
        /// <param name="type">
        /// The type.
        /// </param>
        /// <param name="name">
        /// The name of the method.
        /// </param>
        /// <param name="parameters">
        /// The types of the parameters.
        /// </param>
        /// <returns>
        /// The <see cref="MethodInfo"/>.
        /// </returns>
        [CanBeNull]
        public static MethodInfo GetGenericMethod(this Type type, string name, params Type[] parameters)
        {
            return type.GetRuntimeMethods().FirstOrDefault(
                m => m.Name == name && m.GetParameters().Select(
                         p => p.ParameterType.IsConstructedGenericType && p.ParameterType.GenericTypeArguments.Any(ta => ta.IsGenericParameter)
                                  ? p.ParameterType.GetGenericTypeDefinition()
                                  : p.ParameterType.IsGenericParameter
                                      ? null
                                      : p.ParameterType).SequenceEqual(parameters));
        }

        /// <summary>
        /// Checks if the type is a <see cref="Task{T}"/>.
        /// </summary>
        /// <param name="type">
        /// The type to check.
        /// </param>
        /// <returns>
        /// <c>true</c> if this is a task, <c>false</c> otherwise.
        /// </returns>
        public static bool IsTask(this Type type)
        {
            return type.IsConstructedGenericType && type.GetGenericTypeDefinition() == typeof(Task<>);
        }

        /// <summary>
        /// Checks if the type is a <see cref="Task{T}"/>, and if so, returns the result type of the task.
        /// </summary>
        /// <param name="type">
        /// The type to unwrap.
        /// </param>
        /// <returns>
        /// A non-task type.
        /// </returns>
        public static Type UnwrapTasks(this Type type)
        {
            return type.IsTask() ? type.GenericTypeArguments[0] : type;
        }

        /// <summary>
        /// Gets the constructor.
        /// </summary>
        /// <param name="type">
        /// The type to get the constructor from.
        /// </param>
        /// <param name="parameters">
        /// The parameters.
        /// </param>
        /// <returns>
        /// The <see cref="ConstructorInfo"/>.
        /// </returns>
        [CanBeNull]
        public static ConstructorInfo GetConstructor(this Type type, params Type[] parameters)
        {
            return type.GetTypeInfo().DeclaredConstructors.FirstOrDefault(c => c.GetParameters().Select(p => p.ParameterType).SequenceEqual(parameters));
        }

        /// <summary>
        /// Gets the interface if the <paramref name="type"/> implements it. When <paramref name="interfaceType"/> is a generic type definition,
        /// the constructed generic interface is returned.
        /// </summary>
        /// <param name="type">
        /// The type.
        /// </param>
        /// <param name="interfaceType">
        /// The interface type.
        /// </param>
        /// <returns>
        /// The <see cref="Type"/>.
        /// </returns>
        [CanBeNull]
        public static Type GetInterface(this Type type, Type interfaceType)
        {
            if (interfaceType.GetTypeInfo().IsGenericTypeDefinition)
            {
                return type.GetTypeInfo().IsGenericType && type.GetGenericTypeDefinition() == interfaceType
                           ? type
                           : type.GetTypeInfo().ImplementedInterfaces.FirstOrDefault(i => i.GetTypeInfo().IsGenericType && i.GetGenericTypeDefinition() == interfaceType);
            }

            return type.GetTypeInfo().ImplementedInterfaces.Contains(interfaceType) || type == interfaceType ? interfaceType : null;
        }

        /// <summary>
        /// Gets the base type if the <paramref name="type"/> implements it. When <paramref name="baseType"/> is a generic type definition,
        /// the constructed generic interface is returned.
        /// </summary>
        /// <param name="type">
        /// The type.
        /// </param>
        /// <param name="baseType">
        /// The base type.
        /// </param>
        /// <returns>
        /// The <see cref="Type"/>.
        /// </returns>
        [CanBeNull]
        public static Type GetBaseType(this Type type, Type baseType)
        {
            var baseTypeInfo = baseType.GetTypeInfo();
            while (type != null)
            {
                var typeInfo = type.GetTypeInfo();

                if (type == baseType || baseTypeInfo.IsGenericTypeDefinition && type.IsConstructedGenericType && type.GetGenericTypeDefinition() == baseType)
                {
                    return type;
                }

                type = typeInfo.BaseType;
            }

            return null;
        }

        /// <summary>
        /// Gets a generic method on a type.
        /// </summary>
        /// <param name="type">
        /// The type.
        /// </param>
        /// <param name="name">
        /// The name of the method.
        /// </param>
        /// <param name="parameters">
        /// The types of the parameters.
        /// </param>
        /// <returns>
        /// The <see cref="MethodInfo"/>.
        /// </returns>
        public static MethodInfo GetMethod(this Type type, string name, params Type[] parameters)
        {
            return type.GetRuntimeMethod(name, parameters);
        }

        /// <summary>
        /// The has interface.
        /// </summary>
        /// <param name="type">
        /// The type.
        /// </param>
        /// <param name="interfaceType">
        /// The interface type.
        /// </param>
        /// <returns>
        /// The <see cref="bool"/>.
        /// </returns>
        public static bool HasInterface(this Type type, Type interfaceType)
        {
            return type.GetInterface(interfaceType) != null;
        }
    }
}