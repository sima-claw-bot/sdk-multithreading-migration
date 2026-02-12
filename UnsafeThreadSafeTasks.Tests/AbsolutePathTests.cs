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
    }
}
