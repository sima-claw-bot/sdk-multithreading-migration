#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Framework;
using Xunit;

namespace UnsafeThreadSafeTasks.Tests
{
    /// <summary>
    /// Tests for SharedPolyfills types: AbsolutePath, TaskEnvironment,
    /// IMultiThreadableTask, and MSBuildMultiThreadableTaskAttribute.
    /// Covers structural contracts, boundary conditions, and cross-type behavior.
    /// </summary>
    public class SharedPolyfillsTypeTests
    {
        #region AbsolutePath struct contract

        [Fact]
        public void AbsolutePath_IsReadonlyStruct()
        {
            var type = typeof(AbsolutePath);
            Assert.True(type.IsValueType);
            // readonly structs have IsReadOnly custom modifier on their methods
            Assert.False(type.IsClass);
        }

        [Fact]
        public void AbsolutePath_LivesInExpectedNamespace()
        {
            Assert.Equal("Microsoft.Build.Framework", typeof(AbsolutePath).Namespace);
        }

        [Fact]
        public void AbsolutePath_Constructor_StoresExactPath()
        {
            string input = @"C:\some\path\to\file.cs";
            var path = new AbsolutePath(input);
            Assert.Equal(input, path.Value);
            Assert.Equal(input, (string)path);
            Assert.Equal(input, path.ToString());
        }

        [Fact]
        public void AbsolutePath_Constructor_NullThrowsWithCorrectParamName()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => new AbsolutePath(null!));
            Assert.Equal("path", ex.ParamName);
        }

        [Fact]
        public void AbsolutePath_DefaultStruct_ValueReturnsEmpty_ToStringReturnsNull()
        {
            var path = default(AbsolutePath);
            Assert.Equal(string.Empty, path.Value);
            Assert.Null(path.ToString());
        }

        [Fact]
        public void AbsolutePath_GetCanonicalForm_ResolvesMultipleParentSegments()
        {
            var path = new AbsolutePath(@"C:\a\b\c\..\..\file.txt");
            Assert.Equal(@"C:\a\file.txt", path.GetCanonicalForm());
        }

        [Fact]
        public void AbsolutePath_GetCanonicalForm_DefaultStruct_ReturnsNull()
        {
            var path = default(AbsolutePath);
            Assert.Null(path.GetCanonicalForm());
        }

        [Fact]
        public void AbsolutePath_Equals_NullAndNonAbsolutePathReturnFalse()
        {
            var path = new AbsolutePath(@"C:\x");
            Assert.False(path.Equals((object?)null));
            Assert.False(path.Equals((object)"C:\\x"));
            Assert.False(path.Equals((object)42));
        }

        [Fact]
        public void AbsolutePath_Operators_ConsistentWithEquals()
        {
            var a = new AbsolutePath(@"C:\abc");
            var b = new AbsolutePath(@"C:\ABC");
            var c = new AbsolutePath(@"D:\other");

            Assert.True(a == b);
            Assert.False(a != b);
            Assert.True(a.Equals(b));

            Assert.False(a == c);
            Assert.True(a != c);
            Assert.False(a.Equals(c));
        }

        [Fact]
        public void AbsolutePath_GetHashCode_EqualForCaseVariants()
        {
            var paths = new[]
            {
                new AbsolutePath(@"C:\Folder\File.txt"),
                new AbsolutePath(@"c:\folder\file.txt"),
                new AbsolutePath(@"C:\FOLDER\FILE.TXT"),
            };

            var hashes = paths.Select(p => p.GetHashCode()).Distinct().ToList();
            Assert.Single(hashes);
        }

        [Fact]
        public void AbsolutePath_GetHashCode_DefaultStructReturnsZero()
        {
            Assert.Equal(0, default(AbsolutePath).GetHashCode());
        }

        [Fact]
        public void AbsolutePath_ImplicitConversion_DefaultReturnsEmpty()
        {
            string result = default(AbsolutePath);
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void AbsolutePath_CanBeStoredInHashSetAndRetrievedCaseInsensitively()
        {
            var set = new HashSet<AbsolutePath>
            {
                new AbsolutePath(@"C:\Alpha"),
                new AbsolutePath(@"C:\Beta")
            };

            Assert.Contains(new AbsolutePath(@"c:\alpha"), set);
            Assert.Contains(new AbsolutePath(@"C:\BETA"), set);
            Assert.DoesNotContain(new AbsolutePath(@"C:\Gamma"), set);
        }

        [Fact]
        public void AbsolutePath_Equality_DefaultAndEmptyStringAreNotEqual()
        {
            var defaultPath = default(AbsolutePath);
            var emptyPath = new AbsolutePath(string.Empty);
            // default has _value == null, empty has _value == ""
            // OrdinalIgnoreCase.Equals(null, "") returns false
            Assert.False(defaultPath.Equals(emptyPath));
        }

        #endregion

        #region TaskEnvironment

        [Fact]
        public void TaskEnvironment_IsClass()
        {
            Assert.True(typeof(TaskEnvironment).IsClass);
            Assert.False(typeof(TaskEnvironment).IsAbstract);
        }

        [Fact]
        public void TaskEnvironment_LivesInExpectedNamespace()
        {
            Assert.Equal("Microsoft.Build.Framework", typeof(TaskEnvironment).Namespace);
        }

        [Fact]
        public void TaskEnvironment_SetEnvironmentVariable_NullName_ThrowsWithParamName()
        {
            var env = new TaskEnvironment();
            var ex = Assert.Throws<ArgumentNullException>(() => env.SetEnvironmentVariable(null!, "v"));
            Assert.Equal("name", ex.ParamName);
        }

        [Fact]
        public void TaskEnvironment_SetEnvironmentVariable_NullValue_ThrowsWithParamName()
        {
            var env = new TaskEnvironment();
            var ex = Assert.Throws<ArgumentNullException>(() => env.SetEnvironmentVariable("k", null!));
            Assert.Equal("value", ex.ParamName);
        }

        [Fact]
        public void TaskEnvironment_GetAbsolutePath_AbsoluteInput_IgnoresProjectDirectory()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\project" };
            var result = env.GetAbsolutePath(@"D:\absolute\path.dll");
            Assert.Equal(@"D:\absolute\path.dll", result.Value);
        }

        [Fact]
        public void TaskEnvironment_GetAbsolutePath_RelativeWithDotDot_Resolves()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\project\sub" };
            var result = env.GetAbsolutePath(@"..\other\file.txt");
            Assert.Equal(@"C:\project\other\file.txt", result.Value);
        }

        [Fact]
        public void TaskEnvironment_GetProcessStartInfo_AfterOverwrite_UsesLatestValue()
        {
            var env = new TaskEnvironment();
            env.SetEnvironmentVariable("X", "old");
            env.SetEnvironmentVariable("X", "new");
            var psi = env.GetProcessStartInfo();
            Assert.Equal("new", psi.Environment["X"]);
        }

        [Fact]
        public void TaskEnvironment_GetProcessStartInfo_WorkingDirectoryMatchesProjectDir()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\build\output" };
            var psi = env.GetProcessStartInfo();
            Assert.Equal(@"C:\build\output", psi.WorkingDirectory);
        }

        [Fact]
        public void TaskEnvironment_GetProcessStartInfo_ReturnsDistinctInstances()
        {
            var env = new TaskEnvironment();
            var a = env.GetProcessStartInfo();
            var b = env.GetProcessStartInfo();
            Assert.NotSame(a, b);
        }

        [Fact]
        public void TaskEnvironment_GetProcessStartInfo_MutatingReturnDoesNotAffectSource()
        {
            var env = new TaskEnvironment();
            env.SetEnvironmentVariable("A", "1");
            var psi = env.GetProcessStartInfo();
            psi.Environment["A"] = "modified";
            psi.WorkingDirectory = "changed";

            Assert.Equal("1", env.GetEnvironmentVariable("A"));
            Assert.Equal(string.Empty, env.ProjectDirectory);
        }

        [Fact]
        public void TaskEnvironment_GetAbsolutePath_ReturnedPath_CanBeComparedWithOperators()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\root" };
            AbsolutePath a = env.GetAbsolutePath("file.txt");
            AbsolutePath b = env.GetAbsolutePath("FILE.TXT");
            Assert.True(a == b);
        }

        [Fact]
        public void TaskEnvironment_GetEnvironmentVariable_KeyNotSet_ReturnsNull()
        {
            var env = new TaskEnvironment();
            Assert.Null(env.GetEnvironmentVariable("DOES_NOT_EXIST"));
        }

        [Fact]
        public void TaskEnvironment_EnvironmentVariablesAreIsolatedPerInstance()
        {
            var env1 = new TaskEnvironment();
            var env2 = new TaskEnvironment();
            env1.SetEnvironmentVariable("KEY", "val1");
            Assert.Null(env2.GetEnvironmentVariable("KEY"));
        }

        #endregion

        #region IMultiThreadableTask interface

        private class StubTask : IMultiThreadableTask
        {
            public TaskEnvironment TaskEnvironment { get; set; } = new TaskEnvironment();
        }

        [Fact]
        public void IMultiThreadableTask_IsInterface()
        {
            Assert.True(typeof(IMultiThreadableTask).IsInterface);
        }

        [Fact]
        public void IMultiThreadableTask_LivesInExpectedNamespace()
        {
            Assert.Equal("Microsoft.Build.Framework", typeof(IMultiThreadableTask).Namespace);
        }

        [Fact]
        public void IMultiThreadableTask_TaskEnvironmentProperty_ReadWrite()
        {
            var prop = typeof(IMultiThreadableTask).GetProperty(nameof(IMultiThreadableTask.TaskEnvironment));
            Assert.NotNull(prop);
            Assert.True(prop!.CanRead);
            Assert.True(prop.CanWrite);
            Assert.Equal(typeof(TaskEnvironment), prop.PropertyType);
        }

        [Fact]
        public void IMultiThreadableTask_Implementation_SharesTaskEnvironmentByReference()
        {
            var task = new StubTask();
            var env = new TaskEnvironment { ProjectDirectory = @"C:\shared" };
            task.TaskEnvironment = env;

            IMultiThreadableTask asInterface = task;
            Assert.Same(env, asInterface.TaskEnvironment);

            // Mutating through interface affects the same object
            asInterface.TaskEnvironment.SetEnvironmentVariable("X", "1");
            Assert.Equal("1", task.TaskEnvironment.GetEnvironmentVariable("X"));
        }

        [Fact]
        public void IMultiThreadableTask_HasNoMethods()
        {
            var methods = typeof(IMultiThreadableTask).GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            // Properties generate get_ and set_ methods
            Assert.All(methods, m => Assert.True(m.IsSpecialName));
        }

        #endregion

        #region MSBuildMultiThreadableTaskAttribute

        [Fact]
        public void MSBuildMultiThreadableTaskAttribute_LivesInExpectedNamespace()
        {
            Assert.Equal("Microsoft.Build.Framework", typeof(MSBuildMultiThreadableTaskAttribute).Namespace);
        }

        [Fact]
        public void MSBuildMultiThreadableTaskAttribute_DerivesFromAttribute()
        {
            Assert.True(typeof(Attribute).IsAssignableFrom(typeof(MSBuildMultiThreadableTaskAttribute)));
        }

        [Fact]
        public void MSBuildMultiThreadableTaskAttribute_IsSealed()
        {
            Assert.True(typeof(MSBuildMultiThreadableTaskAttribute).IsSealed);
        }

        [Fact]
        public void MSBuildMultiThreadableTaskAttribute_AllowMultipleFalse_InheritedFalse()
        {
            var usage = typeof(MSBuildMultiThreadableTaskAttribute).GetCustomAttribute<AttributeUsageAttribute>();
            Assert.NotNull(usage);
            Assert.Equal(AttributeTargets.Class, usage!.ValidOn);
            Assert.False(usage.AllowMultiple);
            Assert.False(usage.Inherited);
        }

        [Fact]
        public void MSBuildMultiThreadableTaskAttribute_HasParameterlessConstructorOnly()
        {
            var ctors = typeof(MSBuildMultiThreadableTaskAttribute)
                .GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            Assert.Single(ctors);
            Assert.Empty(ctors[0].GetParameters());
        }

        [MSBuildMultiThreadableTask]
        private class MarkedTask : IMultiThreadableTask
        {
            public TaskEnvironment TaskEnvironment { get; set; } = new TaskEnvironment();
        }

        [Fact]
        public void MSBuildMultiThreadableTaskAttribute_CanDecorateIMultiThreadableTaskImpl()
        {
            var attr = typeof(MarkedTask).GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>();
            Assert.NotNull(attr);
            Assert.True(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(MarkedTask)));
        }

        #endregion

        #region Cross-type integration

        [Fact]
        public void FullWorkflow_CreateEnvironment_ResolvePath_CompareResults()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\workspace" };
            env.SetEnvironmentVariable("CONFIG", "Release");

            AbsolutePath outputPath = env.GetAbsolutePath(@"bin\Release\app.dll");
            Assert.Equal(@"C:\workspace\bin\Release\app.dll", outputPath.Value);

            // Canonical form should match for already-resolved paths
            Assert.Equal(outputPath.Value, outputPath.GetCanonicalForm());

            // ProcessStartInfo should carry environment and directory
            var psi = env.GetProcessStartInfo();
            Assert.Equal(@"C:\workspace", psi.WorkingDirectory);
            Assert.Equal("Release", psi.Environment["CONFIG"]);
        }

        [Fact]
        public void AbsolutePathsFromDifferentEnvironments_SameResolvedPath_AreEqual()
        {
            var env1 = new TaskEnvironment { ProjectDirectory = @"C:\a\b" };
            var env2 = new TaskEnvironment { ProjectDirectory = @"C:\a" };

            AbsolutePath p1 = env1.GetAbsolutePath("file.txt");
            AbsolutePath p2 = env2.GetAbsolutePath(@"b\file.txt");

            Assert.Equal(p1, p2);
            Assert.True(p1 == p2);
        }

        [Fact]
        public void AbsolutePathFromEnvironment_UsableAsDictionaryKey()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\proj" };
            var dict = new Dictionary<AbsolutePath, string>();

            AbsolutePath key = env.GetAbsolutePath("output.dll");
            dict[key] = "built";

            // Case-insensitive lookup
            AbsolutePath lookup = new AbsolutePath(@"C:\PROJ\OUTPUT.DLL");
            Assert.True(dict.ContainsKey(lookup));
            Assert.Equal("built", dict[lookup]);
        }

        #endregion
    }
}
