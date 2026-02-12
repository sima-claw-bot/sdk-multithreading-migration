using System;
using System.Reflection;
using Microsoft.Build.Framework;
using Xunit;

namespace UnsafeThreadSafeTasks.Tests
{
    public class MSBuildMultiThreadableTaskAttributeTests
    {
        [Fact]
        public void CanInstantiate()
        {
            var attr = new MSBuildMultiThreadableTaskAttribute();
            Assert.NotNull(attr);
        }

        [Fact]
        public void IsSealed()
        {
            Assert.True(typeof(MSBuildMultiThreadableTaskAttribute).IsSealed);
        }

        [Fact]
        public void HasAttributeUsage_ClassOnly_NoMultiple_NotInherited()
        {
            var usage = typeof(MSBuildMultiThreadableTaskAttribute)
                .GetCustomAttribute<AttributeUsageAttribute>();

            Assert.NotNull(usage);
            Assert.Equal(AttributeTargets.Class, usage!.ValidOn);
            Assert.False(usage.AllowMultiple);
            Assert.False(usage.Inherited);
        }

        [Fact]
        public void DerivesFromAttribute()
        {
            Assert.True(typeof(Attribute).IsAssignableFrom(typeof(MSBuildMultiThreadableTaskAttribute)));
        }

        [MSBuildMultiThreadableTask]
        private class DecoratedClass { }

        [Fact]
        public void CanBeAppliedToClass()
        {
            var attr = (MSBuildMultiThreadableTaskAttribute)Attribute.GetCustomAttribute(
                typeof(DecoratedClass), typeof(MSBuildMultiThreadableTaskAttribute))!;
            Assert.NotNull(attr);
        }
    }
}
