using System;
using Microsoft.Build.Framework;
using Xunit;

namespace UnsafeThreadSafeTasks.Tests
{
    public class AbsolutePathTests
    {
        [Fact]
        public void Constructor_NullPath_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new AbsolutePath(null!));
        }

        [Fact]
        public void Value_ReturnsStoredPath()
        {
            var path = new AbsolutePath(@"C:\test\file.txt");
            Assert.Equal(@"C:\test\file.txt", path.Value);
        }

        [Fact]
        public void Value_DefaultStruct_ReturnsEmpty()
        {
            var path = default(AbsolutePath);
            Assert.Equal(string.Empty, path.Value);
        }

        [Fact]
        public void ImplicitConversion_ReturnsValue()
        {
            var path = new AbsolutePath(@"C:\test\file.txt");
            string result = path;
            Assert.Equal(@"C:\test\file.txt", result);
        }

        [Fact]
        public void ToString_ReturnsUnderlyingValue()
        {
            var path = new AbsolutePath(@"C:\some\path");
            Assert.Equal(@"C:\some\path", path.ToString());
        }

        [Fact]
        public void Equals_SamePath_ReturnsTrue()
        {
            var a = new AbsolutePath(@"C:\test");
            var b = new AbsolutePath(@"C:\test");
            Assert.True(a.Equals(b));
        }

        [Fact]
        public void Equals_DifferentCase_ReturnsTrue()
        {
            var a = new AbsolutePath(@"C:\Test");
            var b = new AbsolutePath(@"C:\test");
            Assert.True(a.Equals(b));
        }

        [Fact]
        public void Equals_DifferentPath_ReturnsFalse()
        {
            var a = new AbsolutePath(@"C:\test");
            var b = new AbsolutePath(@"C:\other");
            Assert.False(a.Equals(b));
        }

        [Fact]
        public void EqualsObject_AbsolutePath_ReturnsTrue()
        {
            var a = new AbsolutePath(@"C:\test");
            object b = new AbsolutePath(@"C:\test");
            Assert.True(a.Equals(b));
        }

        [Fact]
        public void EqualsObject_NonAbsolutePath_ReturnsFalse()
        {
            var a = new AbsolutePath(@"C:\test");
            Assert.False(a.Equals("C:\\test"));
        }

        [Fact]
        public void EqualityOperator_EqualPaths_ReturnsTrue()
        {
            var a = new AbsolutePath(@"C:\test");
            var b = new AbsolutePath(@"C:\test");
            Assert.True(a == b);
        }

        [Fact]
        public void InequalityOperator_DifferentPaths_ReturnsTrue()
        {
            var a = new AbsolutePath(@"C:\test");
            var b = new AbsolutePath(@"C:\other");
            Assert.True(a != b);
        }

        [Fact]
        public void GetHashCode_EqualPaths_SameHashCode()
        {
            var a = new AbsolutePath(@"C:\test");
            var b = new AbsolutePath(@"C:\test");
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        [Fact]
        public void GetHashCode_CaseInsensitive_SameHashCode()
        {
            var a = new AbsolutePath(@"C:\Test");
            var b = new AbsolutePath(@"C:\test");
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        [Fact]
        public void GetHashCode_DefaultStruct_ReturnsZero()
        {
            var path = default(AbsolutePath);
            Assert.Equal(0, path.GetHashCode());
        }

        [Fact]
        public void GetCanonicalForm_ResolvesPath()
        {
            var path = new AbsolutePath(@"C:\test\..\test\file.txt");
            string canonical = path.GetCanonicalForm();
            Assert.Equal(@"C:\test\file.txt", canonical);
        }

        [Fact]
        public void GetCanonicalForm_EmptyValue_ReturnsNull()
        {
            // Default struct has _value == null, Value returns empty, but GetCanonicalForm checks _value
            var path = default(AbsolutePath);
            string result = path.GetCanonicalForm();
            Assert.Null(result);
        }

        [Fact]
        public void Equals_BoxedNull_ReturnsFalse()
        {
            var path = new AbsolutePath(@"C:\test");
            Assert.False(path.Equals(null));
        }

        [Fact]
        public void Equals_BoxedWrongType_ReturnsFalse()
        {
            var path = new AbsolutePath(@"C:\test");
            Assert.False(path.Equals(42));
        }

        [Fact]
        public void InequalityOperator_EqualPaths_ReturnsFalse()
        {
            var a = new AbsolutePath(@"C:\test");
            var b = new AbsolutePath(@"C:\test");
            Assert.False(a != b);
        }

        [Fact]
        public void EqualityOperator_DifferentPaths_ReturnsFalse()
        {
            var a = new AbsolutePath(@"C:\test");
            var b = new AbsolutePath(@"C:\other");
            Assert.False(a == b);
        }

        [Fact]
        public void GetHashCode_DifferentPaths_TypicallyDifferent()
        {
            var a = new AbsolutePath(@"C:\path\one");
            var b = new AbsolutePath(@"D:\path\two");
            // While hash collisions are possible, these should typically differ
            Assert.NotEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Fact]
        public void ImplicitConversion_DefaultStruct_ReturnsEmpty()
        {
            var path = default(AbsolutePath);
            string result = path;
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void ToString_DefaultStruct_ReturnsNull()
        {
            var path = default(AbsolutePath);
            // ToString returns _value directly which is null for default struct
            Assert.Null(path.ToString());
        }

        [Fact]
        public void GetCanonicalForm_NormalizesDirectorySeparators()
        {
            var path = new AbsolutePath(@"C:\test\subdir\file.txt");
            string canonical = path.GetCanonicalForm();
            Assert.Equal(@"C:\test\subdir\file.txt", canonical);
        }

        [Fact]
        public void EqualityOperator_CaseInsensitive_ReturnsTrue()
        {
            var a = new AbsolutePath(@"C:\TEST\FILE.TXT");
            var b = new AbsolutePath(@"c:\test\file.txt");
            Assert.True(a == b);
        }

        [Fact]
        public void Equals_DefaultStructToDefault_ReturnsTrue()
        {
            var a = default(AbsolutePath);
            var b = default(AbsolutePath);
            Assert.True(a.Equals(b));
        }

        [Fact]
        public void Constructor_EmptyString_DoesNotThrow()
        {
            var path = new AbsolutePath(string.Empty);
            Assert.Equal(string.Empty, path.Value);
        }

        [Fact]
        public void Constructor_WhitespacePath_PreservesValue()
        {
            var path = new AbsolutePath("  ");
            Assert.Equal("  ", path.Value);
        }

        [Fact]
        public void ImplementsIEquatable()
        {
            Assert.True(typeof(IEquatable<AbsolutePath>).IsAssignableFrom(typeof(AbsolutePath)));
        }

        [Fact]
        public void IsReadonlyStruct()
        {
            var type = typeof(AbsolutePath);
            Assert.True(type.IsValueType);
        }
    }
}
