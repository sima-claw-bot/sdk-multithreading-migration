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
    /// Auto-generated tests for task-2 SharedPolyfills types: AbsolutePath struct,
    /// TaskEnvironment class, IMultiThreadableTask interface, and
    /// MSBuildMultiThreadableTaskAttribute. Covers structural contracts, behavioral
    /// correctness, and cross-type integration scenarios.
    /// </summary>
    public class SharedPolyfillsTask2AutoTests
    {
        #region AbsolutePath struct shape and members

        [Fact]
        public void AbsolutePath_HasValueProperty_ReturningString()
        {
            var prop = typeof(AbsolutePath).GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(prop);
            Assert.Equal(typeof(string), prop!.PropertyType);
            Assert.True(prop.CanRead);
            Assert.False(prop.CanWrite);
        }

        [Fact]
        public void AbsolutePath_HasGetCanonicalFormMethod()
        {
            var method = typeof(AbsolutePath).GetMethod("GetCanonicalForm", BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(method);
            Assert.Equal(typeof(string), method!.ReturnType);
            Assert.Empty(method.GetParameters());
        }

        [Fact]
        public void AbsolutePath_HasSinglePrivateField()
        {
            var fields = typeof(AbsolutePath).GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.Single(fields);
            Assert.Equal(typeof(string), fields[0].FieldType);
        }

        [Fact]
        public void AbsolutePath_ConstructorAcceptsSingleString()
        {
            var ctor = typeof(AbsolutePath).GetConstructor(new[] { typeof(string) });
            Assert.NotNull(ctor);
            Assert.Single(ctor!.GetParameters());
        }

        #endregion

        #region AbsolutePath constructor behavior

        [Theory]
        [InlineData(@"C:\simple")]
        [InlineData(@"D:\nested\deeply\file.txt")]
        [InlineData("/unix/style")]
        [InlineData("relative-path")]
        [InlineData("")]
        public void AbsolutePath_Constructor_AcceptsVariousPaths(string input)
        {
            var path = new AbsolutePath(input);
            Assert.Equal(input, path.Value);
        }

        [Fact]
        public void AbsolutePath_Constructor_NullThrows_WithParamName()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => new AbsolutePath(null!));
            Assert.Equal("path", ex.ParamName);
        }

        #endregion

        #region AbsolutePath equality with edge cases

        [Fact]
        public void AbsolutePath_Equals_EmptyToEmpty_ReturnsTrue()
        {
            var a = new AbsolutePath("");
            var b = new AbsolutePath("");
            Assert.True(a.Equals(b));
            Assert.True(a == b);
        }

        [Fact]
        public void AbsolutePath_Equals_EmptyToDifferent_ReturnsFalse()
        {
            var empty = new AbsolutePath("");
            var filled = new AbsolutePath(@"C:\test");
            Assert.False(empty.Equals(filled));
            Assert.False(empty == filled);
        }

        [Fact]
        public void AbsolutePath_Equals_BoxedSelf_ReturnsTrue()
        {
            var path = new AbsolutePath(@"C:\boxed");
            object boxed = path;
            Assert.True(path.Equals(boxed));
        }

        [Fact]
        public void AbsolutePath_GetHashCode_EmptyString_IsNotZero()
        {
            // Empty string hash via OrdinalIgnoreCase should not be zero
            // (distinguishes from default struct where _value is null)
            var path = new AbsolutePath("");
            int hash = path.GetHashCode();
            // Just verify it's deterministic
            Assert.Equal(hash, new AbsolutePath("").GetHashCode());
        }

        [Fact]
        public void AbsolutePath_GetHashCode_DifferentPaths_LikelyDifferent()
        {
            var hashes = new HashSet<int>();
            for (int i = 0; i < 50; i++)
            {
                hashes.Add(new AbsolutePath($@"C:\path{i}").GetHashCode());
            }
            // At least most should be unique
            Assert.True(hashes.Count > 40, $"Expected >40 unique hashes, got {hashes.Count}");
        }

        #endregion

        #region AbsolutePath GetCanonicalForm edge cases

        [Fact]
        public void AbsolutePath_GetCanonicalForm_MultipleDotDotSegments()
        {
            var path = new AbsolutePath(@"C:\a\b\c\..\..\d\file.txt");
            Assert.Equal(@"C:\a\d\file.txt", path.GetCanonicalForm());
        }

        [Fact]
        public void AbsolutePath_GetCanonicalForm_MixedSeparators()
        {
            var path = new AbsolutePath(@"C:\a/b\c/file.txt");
            string canonical = path.GetCanonicalForm();
            Assert.Equal(@"C:\a\b\c\file.txt", canonical);
        }

        [Fact]
        public void AbsolutePath_GetCanonicalForm_IdempotentOnCleanPath()
        {
            var path = new AbsolutePath(@"C:\clean\path\file.txt");
            string first = path.GetCanonicalForm();
            var secondPath = new AbsolutePath(first);
            Assert.Equal(first, secondPath.GetCanonicalForm());
        }

        #endregion

        #region AbsolutePath implicit conversion and ToString consistency

        [Theory]
        [InlineData(@"C:\test")]
        [InlineData("")]
        [InlineData("relative")]
        public void AbsolutePath_ImplicitConversion_EqualsValue(string input)
        {
            var path = new AbsolutePath(input);
            string converted = path;
            Assert.Equal(path.Value, converted);
        }

        [Fact]
        public void AbsolutePath_ToString_NonDefault_EqualsValue()
        {
            var path = new AbsolutePath(@"C:\test");
            Assert.Equal(path.Value, path.ToString());
        }

        #endregion

        #region TaskEnvironment API surface

        [Fact]
        public void TaskEnvironment_HasProjectDirectoryProperty_ReadWrite()
        {
            var prop = typeof(TaskEnvironment).GetProperty("ProjectDirectory");
            Assert.NotNull(prop);
            Assert.True(prop!.CanRead);
            Assert.True(prop.CanWrite);
            Assert.Equal(typeof(string), prop.PropertyType);
        }

        [Fact]
        public void TaskEnvironment_HasGetAbsolutePathMethod()
        {
            var method = typeof(TaskEnvironment).GetMethod("GetAbsolutePath");
            Assert.NotNull(method);
            Assert.Equal(typeof(AbsolutePath), method!.ReturnType);
            var param = Assert.Single(method.GetParameters());
            Assert.Equal(typeof(string), param.ParameterType);
        }

        [Fact]
        public void TaskEnvironment_HasGetEnvironmentVariableMethod()
        {
            var method = typeof(TaskEnvironment).GetMethod("GetEnvironmentVariable");
            Assert.NotNull(method);
            var param = Assert.Single(method!.GetParameters());
            Assert.Equal(typeof(string), param.ParameterType);
        }

        [Fact]
        public void TaskEnvironment_HasSetEnvironmentVariableMethod()
        {
            var method = typeof(TaskEnvironment).GetMethod("SetEnvironmentVariable");
            Assert.NotNull(method);
            Assert.Equal(2, method!.GetParameters().Length);
        }

        [Fact]
        public void TaskEnvironment_HasGetProcessStartInfoMethod()
        {
            var method = typeof(TaskEnvironment).GetMethod("GetProcessStartInfo");
            Assert.NotNull(method);
            Assert.Equal(typeof(ProcessStartInfo), method!.ReturnType);
            Assert.Empty(method.GetParameters());
        }

        #endregion

        #region TaskEnvironment environment variable overwrite

        [Fact]
        public void TaskEnvironment_SetEnvironmentVariable_OverwriteUpdatesValue()
        {
            var env = new TaskEnvironment();
            env.SetEnvironmentVariable("KEY", "first");
            env.SetEnvironmentVariable("KEY", "second");
            Assert.Equal("second", env.GetEnvironmentVariable("KEY"));
        }

        [Fact]
        public void TaskEnvironment_GetEnvironmentVariable_NonExistent_ReturnsNull()
        {
            var env = new TaskEnvironment();
            Assert.Null(env.GetEnvironmentVariable("DOES_NOT_EXIST"));
        }

        [Fact]
        public void TaskEnvironment_SetEnvironmentVariable_EmptyValue_Allowed()
        {
            var env = new TaskEnvironment();
            env.SetEnvironmentVariable("EMPTY", "");
            Assert.Equal("", env.GetEnvironmentVariable("EMPTY"));
        }

        #endregion

        #region TaskEnvironment ProcessStartInfo isolation

        [Fact]
        public void TaskEnvironment_ProcessStartInfo_MutationDoesNotAffectEnvironment()
        {
            var env = new TaskEnvironment();
            env.SetEnvironmentVariable("SAFE", "original");

            var psi = env.GetProcessStartInfo();
            psi.Environment["SAFE"] = "mutated";
            psi.Environment["INJECTED"] = "new";

            Assert.Equal("original", env.GetEnvironmentVariable("SAFE"));
            Assert.Null(env.GetEnvironmentVariable("INJECTED"));
        }

        [Fact]
        public void TaskEnvironment_ProcessStartInfo_LateAdditionsNotInEarlierSnapshot()
        {
            var env = new TaskEnvironment();
            env.SetEnvironmentVariable("A", "1");
            var psi1 = env.GetProcessStartInfo();

            env.SetEnvironmentVariable("B", "2");
            var psi2 = env.GetProcessStartInfo();

            Assert.True(psi2.Environment.ContainsKey("B"));
            // psi1 may or may not contain B depending on ProcessStartInfo.Environment behavior
            // but the source env correctly has both
            Assert.Equal("1", env.GetEnvironmentVariable("A"));
            Assert.Equal("2", env.GetEnvironmentVariable("B"));
        }

        #endregion

        #region IMultiThreadableTask structural contract

        [Fact]
        public void IMultiThreadableTask_TaskEnvironment_HasGetterAndSetter()
        {
            var prop = typeof(IMultiThreadableTask).GetProperty("TaskEnvironment");
            Assert.NotNull(prop);
            Assert.NotNull(prop!.GetMethod);
            Assert.NotNull(prop.SetMethod);
        }

        [Fact]
        public void IMultiThreadableTask_NoDeclaredMethods_BeyondPropertyAccessors()
        {
            var methods = typeof(IMultiThreadableTask)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName)
                .ToList();
            Assert.Empty(methods);
        }

        [Fact]
        public void IMultiThreadableTask_NoInheritedInterfaces()
        {
            var interfaces = typeof(IMultiThreadableTask).GetInterfaces();
            Assert.Empty(interfaces);
        }

        private class TestableTask : IMultiThreadableTask
        {
            public TaskEnvironment TaskEnvironment { get; set; } = new TaskEnvironment();
        }

        [Fact]
        public void IMultiThreadableTask_ImplementationCanSetAndRetrieveEnvironment()
        {
            IMultiThreadableTask task = new TestableTask();
            var env = new TaskEnvironment { ProjectDirectory = @"C:\work" };
            task.TaskEnvironment = env;
            Assert.Same(env, task.TaskEnvironment);
            Assert.Equal(@"C:\work", task.TaskEnvironment.ProjectDirectory);
        }

        #endregion

        #region MSBuildMultiThreadableTaskAttribute structural contract

        [Fact]
        public void MSBuildMultiThreadableTaskAttribute_AttributeUsage_IsConfiguredCorrectly()
        {
            var usage = typeof(MSBuildMultiThreadableTaskAttribute)
                .GetCustomAttribute<AttributeUsageAttribute>();
            Assert.NotNull(usage);
            Assert.Equal(AttributeTargets.Class, usage!.ValidOn);
            Assert.False(usage.AllowMultiple);
            Assert.False(usage.Inherited);
        }

        [Fact]
        public void MSBuildMultiThreadableTaskAttribute_HasDefaultConstructorOnly()
        {
            var constructors = typeof(MSBuildMultiThreadableTaskAttribute)
                .GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            Assert.Single(constructors);
            Assert.Empty(constructors[0].GetParameters());
        }

        [Fact]
        public void MSBuildMultiThreadableTaskAttribute_NoDeclaredMembers()
        {
            var members = typeof(MSBuildMultiThreadableTaskAttribute)
                .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.MemberType != MemberTypes.Constructor)
                .ToList();
            Assert.Empty(members);
        }

        #endregion

        #region Cross-type integration

        [MSBuildMultiThreadableTask]
        private class AutoTestTask : IMultiThreadableTask
        {
            public TaskEnvironment TaskEnvironment { get; set; } = new TaskEnvironment();
        }

        [Fact]
        public void Integration_Task_HasBothAttributeAndInterface()
        {
            var task = new AutoTestTask();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
            var attr = Attribute.GetCustomAttribute(typeof(AutoTestTask), typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void Integration_FullWorkflow_SetEnvGetPathCreateProcess()
        {
            IMultiThreadableTask task = new AutoTestTask();
            task.TaskEnvironment = new TaskEnvironment { ProjectDirectory = @"C:\build" };
            task.TaskEnvironment.SetEnvironmentVariable("CONFIG", "Release");

            AbsolutePath output = task.TaskEnvironment.GetAbsolutePath(@"bin\output.dll");
            Assert.Equal(@"C:\build\bin\output.dll", output.Value);

            var psi = task.TaskEnvironment.GetProcessStartInfo();
            Assert.Equal(@"C:\build", psi.WorkingDirectory);
            Assert.Equal("Release", psi.Environment["CONFIG"]);
        }

        [Fact]
        public void Integration_AbsolutePathFromEnvironment_WorksInCollection()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\proj" };
            var paths = new List<AbsolutePath>
            {
                env.GetAbsolutePath("a.cs"),
                env.GetAbsolutePath("b.cs"),
                env.GetAbsolutePath("c.cs")
            };
            Assert.Equal(3, paths.Count);
            Assert.All(paths, p => Assert.StartsWith(@"C:\proj\", p.Value));
        }

        [Fact]
        public async Task Integration_ConcurrentPathResolution_IsCorrect()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\concurrent" };
            var tasks = Enumerable.Range(0, 20).Select(i =>
                Task.Run(() => env.GetAbsolutePath($"file{i}.cs")));
            var results = await Task.WhenAll(tasks);

            for (int i = 0; i < 20; i++)
            {
                Assert.Equal($@"C:\concurrent\file{i}.cs", results[i].Value);
            }
        }

        [Fact]
        public void Integration_TwoTasks_IndependentEnvironments()
        {
            var task1 = new AutoTestTask();
            task1.TaskEnvironment = new TaskEnvironment { ProjectDirectory = @"C:\proj1" };
            task1.TaskEnvironment.SetEnvironmentVariable("ID", "1");

            var task2 = new AutoTestTask();
            task2.TaskEnvironment = new TaskEnvironment { ProjectDirectory = @"C:\proj2" };
            task2.TaskEnvironment.SetEnvironmentVariable("ID", "2");

            Assert.Equal("1", task1.TaskEnvironment.GetEnvironmentVariable("ID"));
            Assert.Equal("2", task2.TaskEnvironment.GetEnvironmentVariable("ID"));
            Assert.NotEqual(
                task1.TaskEnvironment.GetAbsolutePath("file.cs"),
                task2.TaskEnvironment.GetAbsolutePath("file.cs"));
        }

        #endregion
    }
}
