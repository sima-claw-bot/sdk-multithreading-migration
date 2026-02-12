#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Build.Framework;
using Xunit;

namespace UnsafeThreadSafeTasks.Tests
{
    /// <summary>
    /// Integration and additional edge-case tests for SharedPolyfills types:
    /// AbsolutePath, TaskEnvironment, IMultiThreadableTask, MSBuildMultiThreadableTaskAttribute.
    /// </summary>
    public class SharedPolyfillsIntegrationTests
    {
        #region AbsolutePath additional edge cases

        [Fact]
        public void AbsolutePath_CanBeUsedAsDictionaryKey()
        {
            var dict = new Dictionary<AbsolutePath, int>();
            var key = new AbsolutePath(@"C:\test");
            dict[key] = 42;
            Assert.Equal(42, dict[key]);
        }

        [Fact]
        public void AbsolutePath_DictionaryKey_CaseInsensitiveLookup()
        {
            var dict = new Dictionary<AbsolutePath, int>();
            dict[new AbsolutePath(@"C:\Test")] = 1;

            // Same path different case should overwrite due to GetHashCode + Equals
            dict[new AbsolutePath(@"C:\TEST")] = 2;
            Assert.Equal(2, dict[new AbsolutePath(@"c:\test")]);
        }

        [Fact]
        public void AbsolutePath_EqualityOperator_BothDefault_ReturnsTrue()
        {
            var a = default(AbsolutePath);
            var b = default(AbsolutePath);
            Assert.True(a == b);
        }

        [Fact]
        public void AbsolutePath_InequalityOperator_DefaultVsNonDefault_ReturnsTrue()
        {
            var a = default(AbsolutePath);
            var b = new AbsolutePath(@"C:\test");
            Assert.True(a != b);
        }

        [Fact]
        public void AbsolutePath_GetCanonicalForm_WithCurrentDirSegment()
        {
            var path = new AbsolutePath(@"C:\test\.\file.txt");
            string canonical = path.GetCanonicalForm();
            Assert.Equal(@"C:\test\file.txt", canonical);
        }

        [Fact]
        public void AbsolutePath_LongPath_PreservesValue()
        {
            string longPath = @"C:\" + new string('a', 200) + @"\file.txt";
            var path = new AbsolutePath(longPath);
            Assert.Equal(longPath, path.Value);
        }

        [Fact]
        public void AbsolutePath_ImplicitConversion_PreservesOriginalValue()
        {
            var path = new AbsolutePath(@"C:\My Path\With Spaces\file.txt");
            string str = path;
            Assert.Equal(@"C:\My Path\With Spaces\file.txt", str);
        }

        #endregion

        #region TaskEnvironment integration with AbsolutePath

        [Fact]
        public void TaskEnvironment_GetAbsolutePath_ReturnedPathHasCorrectValue()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\project" };
            AbsolutePath path = env.GetAbsolutePath("bin");
            Assert.Equal(@"C:\project\bin", path.Value);
        }

        [Fact]
        public void TaskEnvironment_GetAbsolutePath_CanonicalFormMatchesValue()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\project" };
            AbsolutePath path = env.GetAbsolutePath("subdir");
            // GetAbsolutePath already resolves to full path, so canonical should match
            Assert.Equal(path.Value, path.GetCanonicalForm());
        }

        [Fact]
        public void TaskEnvironment_GetAbsolutePath_TwoCalls_SameInput_ReturnEqual()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\project" };
            AbsolutePath a = env.GetAbsolutePath("file.txt");
            AbsolutePath b = env.GetAbsolutePath("file.txt");
            Assert.Equal(a, b);
        }

        [Fact]
        public void TaskEnvironment_GetAbsolutePath_ImplicitStringConversion()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\project" };
            string path = env.GetAbsolutePath("test.dll");
            Assert.Equal(@"C:\project\test.dll", path);
        }

        [Fact]
        public void TaskEnvironment_GetProcessStartInfo_MultipleCalls_Independent()
        {
            var env = new TaskEnvironment();
            env.SetEnvironmentVariable("KEY", "val1");
            var psi1 = env.GetProcessStartInfo();

            env.SetEnvironmentVariable("KEY2", "val2");
            var psi2 = env.GetProcessStartInfo();

            // psi1 was created before KEY2, but psi2 should have both
            Assert.True(psi2.Environment.ContainsKey("KEY2"));
        }

        [Fact]
        public void TaskEnvironment_GetProcessStartInfo_ReturnsProcessStartInfoType()
        {
            var env = new TaskEnvironment();
            var psi = env.GetProcessStartInfo();
            Assert.IsType<ProcessStartInfo>(psi);
        }

        [Fact]
        public void TaskEnvironment_GetEnvironmentVariable_EmptyName_ReturnsNull()
        {
            var env = new TaskEnvironment();
            Assert.Null(env.GetEnvironmentVariable(""));
        }

        [Fact]
        public void TaskEnvironment_SetEnvironmentVariable_EmptyName_DoesNotThrow()
        {
            var env = new TaskEnvironment();
            env.SetEnvironmentVariable("", "value");
            Assert.Equal("value", env.GetEnvironmentVariable(""));
        }

        #endregion

        #region IMultiThreadableTask integration

        private class FullTask : IMultiThreadableTask
        {
            public TaskEnvironment TaskEnvironment { get; set; } = new TaskEnvironment();

            public string DoWork()
            {
                AbsolutePath path = TaskEnvironment.GetAbsolutePath("output.bin");
                return path.Value;
            }
        }

        [Fact]
        public void IMultiThreadableTask_Implementation_CanUseTaskEnvironment()
        {
            var task = new FullTask();
            task.TaskEnvironment = new TaskEnvironment { ProjectDirectory = @"C:\build" };
            Assert.Equal(@"C:\build\output.bin", task.DoWork());
        }

        [Fact]
        public void IMultiThreadableTask_DefaultTaskEnvironment_HasEmptyProjectDirectory()
        {
            var task = new FullTask();
            Assert.Equal(string.Empty, task.TaskEnvironment.ProjectDirectory);
        }

        [Fact]
        public void IMultiThreadableTask_TaskEnvironment_CanBeReassigned()
        {
            var task = new FullTask();
            var env1 = new TaskEnvironment { ProjectDirectory = @"C:\first" };
            var env2 = new TaskEnvironment { ProjectDirectory = @"C:\second" };
            task.TaskEnvironment = env1;
            Assert.Same(env1, task.TaskEnvironment);
            task.TaskEnvironment = env2;
            Assert.Same(env2, task.TaskEnvironment);
        }

        #endregion

        #region MSBuildMultiThreadableTaskAttribute usage

        [MSBuildMultiThreadableTask]
        private class AttributedTask : IMultiThreadableTask
        {
            public TaskEnvironment TaskEnvironment { get; set; } = new TaskEnvironment();
        }

        [Fact]
        public void MSBuildMultiThreadableTaskAttribute_CanApplyToClass()
        {
            var attr = typeof(AttributedTask)
                .GetCustomAttributes(typeof(MSBuildMultiThreadableTaskAttribute), false);
            Assert.Single(attr);
        }

        [Fact]
        public void MSBuildMultiThreadableTaskAttribute_AttributedClass_CanImplementInterface()
        {
            IMultiThreadableTask task = new AttributedTask();
            Assert.NotNull(task);
            Assert.NotNull(task.TaskEnvironment);
        }

        [Fact]
        public void MSBuildMultiThreadableTaskAttribute_InheritsFromSystemAttribute()
        {
            var attr = new MSBuildMultiThreadableTaskAttribute();
            Assert.IsAssignableFrom<Attribute>(attr);
        }

        #endregion
    }
}
