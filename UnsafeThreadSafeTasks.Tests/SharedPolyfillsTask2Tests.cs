#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Xunit;

namespace UnsafeThreadSafeTasks.Tests
{
    /// <summary>
    /// Tests for SharedPolyfills types created in task-2: AbsolutePath struct,
    /// TaskEnvironment class, IMultiThreadableTask interface, and
    /// MSBuildMultiThreadableTaskAttribute.
    /// Focuses on thread-safety, concurrency, and boundary scenarios.
    /// </summary>
    public class SharedPolyfillsTask2Tests
    {
        #region AbsolutePath concurrent usage

        [Fact]
        public async Task AbsolutePath_ConcurrentReads_AreThreadSafe()
        {
            var path = new AbsolutePath(@"C:\concurrent\test.txt");
            var tasks = Enumerable.Range(0, 100).Select(_ => Task.Run(() =>
            {
                Assert.Equal(@"C:\concurrent\test.txt", path.Value);
                Assert.Equal(@"C:\concurrent\test.txt", (string)path);
                Assert.Equal(@"C:\concurrent\test.txt", path.ToString());
            }));
            await Task.WhenAll(tasks);
        }

        [Fact]
        public void AbsolutePath_ConcurrentHashCodeCalls_ReturnConsistentValue()
        {
            var path = new AbsolutePath(@"C:\hash\test.txt");
            int expected = path.GetHashCode();
            var results = new int[100];
            Parallel.For(0, 100, i => results[i] = path.GetHashCode());
            Assert.All(results, h => Assert.Equal(expected, h));
        }

        [Fact]
        public void AbsolutePath_ConcurrentEqualityChecks_AreConsistent()
        {
            var a = new AbsolutePath(@"C:\eq\test");
            var b = new AbsolutePath(@"C:\EQ\TEST");
            var results = new bool[100];
            Parallel.For(0, 100, i => results[i] = a.Equals(b));
            Assert.All(results, r => Assert.True(r));
        }

        #endregion

        #region AbsolutePath boundary values

        [Fact]
        public void AbsolutePath_SingleCharPath_PreservesValue()
        {
            var path = new AbsolutePath("X");
            Assert.Equal("X", path.Value);
        }

        [Fact]
        public void AbsolutePath_UnicodeCharacters_PreservesValue()
        {
            var path = new AbsolutePath(@"C:\données\fichier.txt");
            Assert.Equal(@"C:\données\fichier.txt", path.Value);
        }

        [Fact]
        public void AbsolutePath_VeryLongPath_PreservesValue()
        {
            string longSegment = new string('z', 250);
            string longPath = $@"C:\{longSegment}\file.txt";
            var path = new AbsolutePath(longPath);
            Assert.Equal(longPath, path.Value);
        }

        [Fact]
        public void AbsolutePath_SpecialCharactersInPath_PreservesValue()
        {
            var path = new AbsolutePath(@"C:\path with (parens) & special!");
            Assert.Equal(@"C:\path with (parens) & special!", path.Value);
        }

        [Fact]
        public void AbsolutePath_EqualityOperator_DefaultVsConstructedNull_ThrowsOnConstruction()
        {
            Assert.Throws<ArgumentNullException>(() => new AbsolutePath(null!));
        }

        #endregion

        #region AbsolutePath equality contract completeness

        [Fact]
        public void AbsolutePath_Equals_ObjectOverload_WithNull_ReturnsFalse()
        {
            var path = new AbsolutePath(@"C:\test");
            Assert.False(path.Equals((object?)null));
        }

        [Fact]
        public void AbsolutePath_Equals_ObjectOverload_WithDifferentType_ReturnsFalse()
        {
            var path = new AbsolutePath(@"C:\test");
            Assert.False(path.Equals(3.14));
        }

        [Fact]
        public void AbsolutePath_Equals_ObjectOverload_WithBoxedEqual_ReturnsTrue()
        {
            var a = new AbsolutePath(@"C:\box");
            object b = new AbsolutePath(@"c:\BOX");
            Assert.True(a.Equals(b));
        }

        [Fact]
        public void AbsolutePath_DefaultEquality_BothOperators()
        {
            var a = default(AbsolutePath);
            var b = default(AbsolutePath);
            Assert.True(a == b);
            Assert.False(a != b);
        }

        [Fact]
        public void AbsolutePath_GetHashCode_EqualObjects_SameHash()
        {
            var a = new AbsolutePath(@"C:\CONSISTENT");
            var b = new AbsolutePath(@"c:\consistent");
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
            Assert.True(a.Equals(b));
        }

        #endregion

        #region TaskEnvironment concurrency

        [Fact]
        public void TaskEnvironment_ConcurrentGetAbsolutePath_ProducesCorrectResults()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\concurrent" };
            var results = new AbsolutePath[50];
            Parallel.For(0, 50, i => results[i] = env.GetAbsolutePath($"file{i}.txt"));
            for (int i = 0; i < 50; i++)
            {
                Assert.Equal($@"C:\concurrent\file{i}.txt", results[i].Value);
            }
        }

        [Fact]
        public void TaskEnvironment_GetProcessStartInfo_EmptyProjectDirectory()
        {
            var env = new TaskEnvironment();
            var psi = env.GetProcessStartInfo();
            Assert.Equal(string.Empty, psi.WorkingDirectory);
        }

        [Fact]
        public void TaskEnvironment_GetProcessStartInfo_NoEnvVars_StillReturnsProcessStartInfo()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\noenvvars" };
            var psi = env.GetProcessStartInfo();
            Assert.IsType<ProcessStartInfo>(psi);
            Assert.Equal(@"C:\noenvvars", psi.WorkingDirectory);
        }

        [Fact]
        public void TaskEnvironment_EnvironmentVariable_SpecialCharValue()
        {
            var env = new TaskEnvironment();
            env.SetEnvironmentVariable("SPECIAL", @"C:\path with spaces\and=equals&ampersand");
            Assert.Equal(@"C:\path with spaces\and=equals&ampersand", env.GetEnvironmentVariable("SPECIAL"));
        }

        [Fact]
        public void TaskEnvironment_SetEnvironmentVariable_EmptyValue_Stores()
        {
            var env = new TaskEnvironment();
            env.SetEnvironmentVariable("EMPTY_VAL", "");
            Assert.Equal("", env.GetEnvironmentVariable("EMPTY_VAL"));
        }

        [Fact]
        public void TaskEnvironment_GetAbsolutePath_ForwardSlashRelative()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\project" };
            AbsolutePath result = env.GetAbsolutePath("src/file.cs");
            Assert.Equal(@"C:\project\src\file.cs", result.Value);
        }

        [Fact]
        public void TaskEnvironment_GetAbsolutePath_MultipleDotDot()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\a\b\c" };
            AbsolutePath result = env.GetAbsolutePath(@"..\..\file.txt");
            Assert.Equal(@"C:\a\file.txt", result.Value);
        }

        #endregion

        #region TaskEnvironment ProcessStartInfo isolation

        [Fact]
        public void TaskEnvironment_GetProcessStartInfo_AddingToResult_DoesNotAffectSource()
        {
            var env = new TaskEnvironment();
            env.SetEnvironmentVariable("ORIG", "value");
            var psi = env.GetProcessStartInfo();
            psi.Environment["NEW"] = "added";
            Assert.Null(env.GetEnvironmentVariable("NEW"));
        }

        [Fact]
        public void TaskEnvironment_GetProcessStartInfo_RemovingFromResult_DoesNotAffectSource()
        {
            var env = new TaskEnvironment();
            env.SetEnvironmentVariable("KEEP", "present");
            var psi = env.GetProcessStartInfo();
            psi.Environment.Remove("KEEP");
            Assert.Equal("present", env.GetEnvironmentVariable("KEEP"));
        }

        [Fact]
        public void TaskEnvironment_TwoInstances_EnvVarsAreFullyIsolated()
        {
            var env1 = new TaskEnvironment();
            var env2 = new TaskEnvironment();
            env1.SetEnvironmentVariable("A", "1");
            env1.SetEnvironmentVariable("B", "2");
            env2.SetEnvironmentVariable("C", "3");

            Assert.Equal("1", env1.GetEnvironmentVariable("A"));
            Assert.Null(env2.GetEnvironmentVariable("A"));
            Assert.Null(env1.GetEnvironmentVariable("C"));
            Assert.Equal("3", env2.GetEnvironmentVariable("C"));
        }

        #endregion

        #region IMultiThreadableTask contract

        [Fact]
        public void IMultiThreadableTask_HasExactlyOneProperty()
        {
            var props = typeof(IMultiThreadableTask)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance);
            Assert.Single(props);
            Assert.Equal("TaskEnvironment", props[0].Name);
        }

        [Fact]
        public void IMultiThreadableTask_HasNoEvents()
        {
            var events = typeof(IMultiThreadableTask)
                .GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Assert.Empty(events);
        }

        [Fact]
        public void IMultiThreadableTask_IsNotGeneric()
        {
            Assert.False(typeof(IMultiThreadableTask).IsGenericType);
        }

        private class ConcurrentTask : IMultiThreadableTask
        {
            public TaskEnvironment TaskEnvironment { get; set; } = new TaskEnvironment();
        }

        [Fact]
        public void IMultiThreadableTask_ConcurrentTaskEnvironmentAccess()
        {
            IMultiThreadableTask task = new ConcurrentTask();
            task.TaskEnvironment = new TaskEnvironment { ProjectDirectory = @"C:\work" };
            var paths = new string[50];
            Parallel.For(0, 50, i =>
            {
                paths[i] = task.TaskEnvironment.GetAbsolutePath($"item{i}.cs").Value;
            });
            for (int i = 0; i < 50; i++)
            {
                Assert.Equal($@"C:\work\item{i}.cs", paths[i]);
            }
        }

        #endregion

        #region MSBuildMultiThreadableTaskAttribute contract

        [Fact]
        public void MSBuildMultiThreadableTaskAttribute_HasParameterlessConstructor()
        {
            var ctor = typeof(MSBuildMultiThreadableTaskAttribute)
                .GetConstructor(Type.EmptyTypes);
            Assert.NotNull(ctor);
        }

        [Fact]
        public void MSBuildMultiThreadableTaskAttribute_HasNoPublicFields()
        {
            var fields = typeof(MSBuildMultiThreadableTaskAttribute)
                .GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Assert.Empty(fields);
        }

        [Fact]
        public void MSBuildMultiThreadableTaskAttribute_IsNotAbstract()
        {
            Assert.False(typeof(MSBuildMultiThreadableTaskAttribute).IsAbstract);
        }

        [Fact]
        public void MSBuildMultiThreadableTaskAttribute_MultipleInstances_AreIndependent()
        {
            var a = new MSBuildMultiThreadableTaskAttribute();
            var b = new MSBuildMultiThreadableTaskAttribute();
            Assert.NotSame(a, b);
        }

        #endregion

        #region Cross-type integration with concurrency

        [MSBuildMultiThreadableTask]
        private class ThreadSafeTask : IMultiThreadableTask
        {
            public TaskEnvironment TaskEnvironment { get; set; } = new TaskEnvironment();
        }

        [Fact]
        public void Integration_ConcurrentAbsolutePathCreation_AllEqualWhenSameInput()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\build" };
            var paths = new AbsolutePath[100];
            Parallel.For(0, 100, i => paths[i] = env.GetAbsolutePath("output.dll"));
            var first = paths[0];
            Assert.All(paths, p => Assert.Equal(first, p));
        }

        [Fact]
        public void Integration_TaskWithEnvironment_ProcessStartInfoContainsAllState()
        {
            IMultiThreadableTask task = new ThreadSafeTask();
            task.TaskEnvironment = new TaskEnvironment { ProjectDirectory = @"C:\int" };
            task.TaskEnvironment.SetEnvironmentVariable("MODE", "test");
            task.TaskEnvironment.SetEnvironmentVariable("VERBOSE", "true");

            var psi = task.TaskEnvironment.GetProcessStartInfo();
            Assert.Equal(@"C:\int", psi.WorkingDirectory);
            Assert.Equal("test", psi.Environment["MODE"]);
            Assert.Equal("true", psi.Environment["VERBOSE"]);

            AbsolutePath outPath = task.TaskEnvironment.GetAbsolutePath("result.bin");
            Assert.Equal(@"C:\int\result.bin", outPath.Value);
        }

        [Fact]
        public void Integration_AbsolutePathEquality_AcrossTaskEnvironments()
        {
            var task1 = new ThreadSafeTask();
            task1.TaskEnvironment = new TaskEnvironment { ProjectDirectory = @"C:\shared" };

            var task2 = new ThreadSafeTask();
            task2.TaskEnvironment = new TaskEnvironment { ProjectDirectory = @"C:\shared" };

            Assert.Equal(
                task1.TaskEnvironment.GetAbsolutePath("file.cs"),
                task2.TaskEnvironment.GetAbsolutePath("FILE.CS"));
        }

        [Fact]
        public void Integration_AbsolutePath_HashSet_DeduplicatesCrossEnvironment()
        {
            var env1 = new TaskEnvironment { ProjectDirectory = @"C:\proj" };
            var env2 = new TaskEnvironment { ProjectDirectory = @"C:\proj" };
            var set = new HashSet<AbsolutePath>
            {
                env1.GetAbsolutePath("a.cs"),
                env2.GetAbsolutePath("A.CS"),
                env1.GetAbsolutePath("b.cs"),
                env2.GetAbsolutePath("B.CS"),
                env1.GetAbsolutePath("c.cs")
            };
            Assert.Equal(3, set.Count);
        }

        #endregion
    }
}
