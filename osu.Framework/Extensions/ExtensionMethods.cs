﻿// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osuTK;

// this is an abusive thing to do, but it increases the visibility of Extension Methods to virtually every file.

namespace osu.Framework.Extensions
{
    /// <summary>
    /// This class holds extension methods for various purposes and should not be used explicitly, ever.
    /// </summary>
    public static class ExtensionMethods
    {
        /// <summary>
        /// Adds the given item to the list according to standard sorting rules. Do not use on unsorted lists.
        /// </summary>
        /// <param name="list">The list to take values</param>
        /// <param name="item">The item that should be added.</param>
        /// <returns>The index in the list where the item was inserted.</returns>
        public static int AddInPlace<T>(this List<T> list, T item)
        {
            int index = list.BinarySearch(item);
            if (index < 0) index = ~index; // BinarySearch hacks multiple return values with 2's complement.
            list.Insert(index, item);
            return index;
        }

        /// <summary>
        /// Adds the given item to the list according to the comparers sorting rules. Do not use on unsorted lists.
        /// </summary>
        /// <param name="list">The list to take values</param>
        /// <param name="item">The item that should be added.</param>
        /// <param name="comparer">The comparer that should be used for sorting.</param>
        /// <returns>The index in the list where the item was inserted.</returns>
        public static int AddInPlace<T>(this List<T> list, T item, IComparer<T> comparer)
        {
            int index = list.BinarySearch(item, comparer);
            if (index < 0) index = ~index; // BinarySearch hacks multiple return values with 2's complement.
            list.Insert(index, item);
            return index;
        }

        /// <summary>
        /// Try to get a value from the <paramref name="dictionary"/>. Returns a default(TValue) if the key does not exist.
        /// </summary>
        /// <param name="dictionary">The dictionary.</param>
        /// <param name="lookup">The lookup key.</param>
        [Obsolete("Use System.Collections.Generic.CollectionExtensions.GetValueOrDefault instead.")] // Can be removed 20220115
        public static TValue GetOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey lookup) => dictionary.GetValueOrDefault(lookup);

        /// <summary>
        /// Converts a rectangular array to a jagged array.
        /// <para>
        /// The jagged array will contain empty arrays if there are no columns in the rectangular array.
        /// </para>
        /// </summary>
        /// <param name="rectangular">The rectangular array.</param>
        /// <returns>The jagged array.</returns>
        public static T[][] ToJagged<T>(this T[,] rectangular)
        {
            if (rectangular == null)
                return null;

            var jagged = new T[rectangular.GetLength(0)][];

            for (int r = 0; r < rectangular.GetLength(0); r++)
            {
                jagged[r] = new T[rectangular.GetLength(1)];
                for (int c = 0; c < rectangular.GetLength(1); c++)
                    jagged[r][c] = rectangular[r, c];
            }

            return jagged;
        }

        /// <summary>
        /// Converts a jagged array to a rectangular array.
        /// <para>
        /// All elements that did not exist in the original jagged array are initialized to their default values.
        /// </para>
        /// </summary>
        /// <param name="jagged">The jagged array.</param>
        /// <returns>The rectangular array.</returns>
        public static T[,] ToRectangular<T>(this T[][] jagged)
        {
            if (jagged == null)
                return null;

            var rows = jagged.Length;
            var cols = rows == 0 ? 0 : jagged.Max(c => c?.Length ?? 0);

            var rectangular = new T[rows, cols];

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (jagged[r] == null)
                        continue;

                    if (c >= jagged[r].Length)
                        continue;

                    rectangular[r, c] = jagged[r][c];
                }
            }

            return rectangular;
        }

        /// <summary>
        /// Inverts the rows and columns of a rectangular array.
        /// </summary>
        /// <param name="array">The array to invert.</param>
        /// <returns>The inverted array.</returns>
        public static T[,] Invert<T>(this T[,] array)
        {
            if (array == null)
                return null;

            int rows = array.GetLength(0);
            int cols = array.GetLength(1);

            var result = new T[cols, rows];

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                    result[c, r] = array[r, c];
            }

            return result;
        }

        /// <summary>
        /// Inverts the rows and columns of a jagged array.
        /// </summary>
        /// <param name="array">The array to invert.</param>
        /// <returns>The inverted array. This is always a square array.</returns>
        public static T[][] Invert<T>(this T[][] array) => array.ToRectangular().Invert().ToJagged();

        public static string ToResolutionString(this Size size) => $"{size.Width}x{size.Height}";

        public static Type[] GetLoadableTypes(this Assembly assembly)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));

            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                // the following warning disables are caused by netstandard2.1 and net5.0 differences
                // the former declares Types as Type[], while the latter declares as Type?[]:
                // https://docs.microsoft.com/en-us/dotnet/api/system.reflection.reflectiontypeloadexception.types?view=net-5.0#property-value
                // which trips some inspectcode errors which are only "valid" for the first of the two.
                // TODO: remove if netstandard2.1 is removed
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                // ReSharper disable once ConstantConditionalAccessQualifier
                // ReSharper disable once ConstantNullCoalescingCondition
                return e.Types?.Where(t => t != null).ToArray() ?? Array.Empty<Type>();
            }
        }

        /// <summary>
        /// Returns the localisable description of a given object, via (in order):
        /// <list type="number">
        ///   <item>
        ///     <description>Any attached <see cref="LocalisableDescriptionAttribute"/>.</description>
        ///   </item>
        ///   <item>
        ///     <description><see cref="GetDescription"/></description>
        ///   </item>
        /// </list>
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// When the <see cref="LocalisableDescriptionAttribute.Name"/> specified in the <see cref="LocalisableDescriptionAttribute"/>
        /// does not match any of the existing members in <see cref="LocalisableDescriptionAttribute.DeclaringType"/>.
        /// </exception>
        public static LocalisableString GetLocalisableDescription<T>(this T value)
        {
            MemberInfo type;

            if (value is Enum)
                type = value.GetType().GetField(value.ToString());
            else
                type = value.GetType();

            var attribute = type.GetCustomAttribute<LocalisableDescriptionAttribute>();
            if (attribute == null)
                return GetDescription(value);

            var property = attribute.DeclaringType.GetMember(attribute.Name, BindingFlags.Static | BindingFlags.Public).FirstOrDefault();

            switch (property)
            {
                case FieldInfo f:
                    return (LocalisableString)f.GetValue(null).AsNonNull();

                case PropertyInfo p:
                    return (LocalisableString)p.GetValue(null).AsNonNull();

                default:
                    throw new InvalidOperationException($"Member \"{attribute.Name}\" was not found in type {attribute.DeclaringType} (must be a static field or property)");
            }
        }

        /// <summary>
        /// Returns the description of a given object, via (in order):
        /// <list type="number">
        ///   <item>
        ///     <description>Any attached <see cref="DescriptionAttribute"/>.</description>
        ///   </item>
        ///   <item>
        ///     <description>The object's <see cref="object.ToString()"/>.</description>
        ///   </item>
        /// </list>
        /// </summary>
        public static string GetDescription(this object value)
            => value.GetType()
                    .GetField(value.ToString())?
                    .GetCustomAttribute<DescriptionAttribute>()?.Description
               ?? value.ToString();

        private static string toLowercaseHex(this byte[] bytes)
        {
            // Convert.ToHexString is upper-case, so we are doing this ourselves

            return string.Create(bytes.Length * 2, bytes, (span, b) =>
            {
                for (int i = 0; i < b.Length; i++)
                    _ = b[i].TryFormat(span[(i * 2)..], out _, "x2");
            });
        }

        /// <summary>
        /// Gets a SHA-2 (256bit) hash for the given stream, seeking the stream before and after.
        /// </summary>
        /// <param name="stream">The stream to create a hash from.</param>
        /// <returns>A lower-case hex string representation of the hash (64 characters).</returns>
        public static string ComputeSHA2Hash(this Stream stream)
        {
            string hash;

            stream.Seek(0, SeekOrigin.Begin);

            using (var alg = SHA256.Create())
                hash = alg.ComputeHash(stream).toLowercaseHex();

            stream.Seek(0, SeekOrigin.Begin);

            return hash;
        }

        /// <summary>
        /// Gets a SHA-2 (256bit) hash for the given string.
        /// </summary>
        /// <param name="str">The string to create a hash from.</param>
        /// <returns>A lower-case hex string representation of the hash (64 characters).</returns>
        public static string ComputeSHA2Hash(this string str)
        {
            using (var alg = SHA256.Create())
                return alg.ComputeHash(Encoding.UTF8.GetBytes(str)).toLowercaseHex();
        }

        public static string ComputeMD5Hash(this Stream stream)
        {
            string hash;

            stream.Seek(0, SeekOrigin.Begin);
            using (var md5 = MD5.Create())
                hash = md5.ComputeHash(stream).toLowercaseHex();
            stream.Seek(0, SeekOrigin.Begin);

            return hash;
        }

        public static string ComputeMD5Hash(this string input)
        {
            using (var md5 = MD5.Create())
                return md5.ComputeHash(Encoding.UTF8.GetBytes(input)).toLowercaseHex();
        }

        public static DisplayIndex GetIndex(this DisplayDevice display)
        {
            if (display == null) return DisplayIndex.Default;

            for (int i = 0; true; i++)
            {
                var device = DisplayDevice.GetDisplay((DisplayIndex)i);
                if (device == null) return DisplayIndex.Default;
                if (device == display) return (DisplayIndex)i;
            }
        }

        /// <summary>
        /// Standardise the path string using '/' as directory separator.
        /// Useful as output.
        /// </summary>
        /// <param name="path">The path string to standardise.</param>
        /// <returns>The standardised path string.</returns>
        public static string ToStandardisedPath(this string path)
            => path.Replace('\\', '/');

        /// <summary>
        /// Converts an osuTK <see cref="DisplayDevice"/> to a <see cref="Display"/> structure.
        /// </summary>
        /// <param name="device">The <see cref="DisplayDevice"/> to convert.</param>
        /// <returns>A <see cref="Display"/> structure populated with the corresponding properties and <see cref="DisplayMode"/>s.</returns>
        internal static Display ToDisplay(this DisplayDevice device) =>
            new Display((int)device.GetIndex(), device.GetIndex().ToString(), device.Bounds, device.AvailableResolutions.Select(ToDisplayMode).ToArray());

        /// <summary>
        /// Converts an osuTK <see cref="DisplayResolution"/> to a <see cref="DisplayMode"/> structure.
        /// It is not possible to retrieve the pixel format from <see cref="DisplayResolution"/>.
        /// </summary>
        /// <param name="resolution">The <see cref="DisplayResolution"/> to convert.</param>
        /// <returns>A <see cref="DisplayMode"/> structure populated with the corresponding properties.</returns>
        internal static DisplayMode ToDisplayMode(this DisplayResolution resolution) =>
            new DisplayMode(null, new Size(resolution.Width, resolution.Height), resolution.BitsPerPixel, (int)Math.Round(resolution.RefreshRate), 0, 0);
    }
}
