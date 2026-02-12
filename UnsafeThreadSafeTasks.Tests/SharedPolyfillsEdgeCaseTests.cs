#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Xunit;

namespace UnsafeThreadSafeTasks.Tests
{
    /// <summary>
    /// Additional edge-case tests for SharedPolyfills types (AbsolutePath, TaskEnvironment,
    /// IMultiThreadableTask, MSBuildMultiThreadableTaskAttribute) covering gaps in existing tests.
    /// </summary>
    public class SharedPolyfillsEdgeCaseTests
    {
        #region AbsolutePath struct semantics

        [Fact]
        public void AbsolutePath_IsValueType()
        {
            Assert.True(typeof(AbsolutePath).IsValueType);
            Assert.False(typeof(AbsolutePath).IsClass);
        }

        [Fact]
        public void AbsolutePath_ImplementsIEquatable()
        {
            Assert.Contains(typeof(IEquatable<AbsolutePath>), typeof(AbsolutePath).GetInterfaces());
        }

        [Fact]
        public void AbsolutePath_Equals_IsSymmetric()
        {
            var a = new AbsolutePath(@"C:\Foo\Bar");
            var b = new AbsolutePath(@"c:\foo\bar");
            Assert.True(a.Equals(b));
            Assert.True(b.Equals(a));
        }

        [Fact]
        public void AbsolutePath_Equals_IsTransitive()
        {
            var a = new AbsolutePath(@"C:\Test");
            var b = new AbsolutePath(@"c:\test");
            var c = new AbsolutePath(@"C:\TEST");
            Assert.True(a.Equals(b));
            Assert.True(b.Equals(c));
            Assert.True(a.Equals(c));
        }

        [Fact]
        public void AbsolutePath_Equals_IsReflexive()
        {
            var a = new AbsolutePath(@"C:\test");
            Assert.True(a.Equals(a));
        }

        [Fact]
        public void AbsolutePath_GetHashCode_ConsistentAcrossMultipleCalls()
        {
            var path = new AbsolutePath(@"C:\consistent\path");
            int hash1 = path.GetHashCode();
            int hash2 = path.GetHashCode();
            int hash3 = path.GetHashCode();
            Assert.Equal(hash1, hash2);
            Assert.Equal(hash2, hash3);
        }

        #endregion

        #region AbsolutePath in collections

        [Fact]
        public void AbsolutePath_HashSet_CaseInsensitiveDeduplicate()
        {
            var set = new HashSet<AbsolutePath>
            {
                new AbsolutePath(@"C:\test"),
                new AbsolutePath(@"C:\TEST"),
                new AbsolutePath(@"c:\Test")
            };
            Assert.Single(set);
        }

        [Fact]
        public void AbsolutePath_HashSet_Contains_CaseInsensitive()
        {
            var set = new HashSet<AbsolutePath> { new AbsolutePath(@"C:\MyDir\File.txt") };
            Assert.Contains(new AbsolutePath(@"c:\mydir\file.txt"), set);
        }

        [Fact]
        public void AbsolutePath_List_Contains_UsesEquals()
        {
            var list = new List<AbsolutePath> { new AbsolutePath(@"C:\item") };
            Assert.Contains(new AbsolutePath(@"C:\ITEM"), list);
        }

        [Fact]
        public void AbsolutePath_Linq_Distinct_RemovesCaseInsensitiveDuplicates()
        {
            var paths = new[]
            {
                new AbsolutePath(@"C:\a"),
                new AbsolutePath(@"C:\A"),
                new AbsolutePath(@"C:\b")
            };
            var distinct = paths.Distinct().ToList();
            Assert.Equal(2, distinct.Count);
        }

        #endregion

        #region AbsolutePath special path forms

        [Fact]
        public void AbsolutePath_RootPath_PreservesValue()
        {
            var path = new AbsolutePath(@"C:\");
            Assert.Equal(@"C:\", path.Value);
        }

        [Fact]
        public void AbsolutePath_TrailingBackslash_PreservesValue()
        {
            var path = new AbsolutePath(@"C:\test\");
            Assert.Equal(@"C:\test\", path.Value);
        }

        [Fact]
        public void AbsolutePath_ForwardSlashes_PreservesValue()
        {
            var path = new AbsolutePath("C:/test/file.txt");
            Assert.Equal("C:/test/file.txt", path.Value);
        }

        [Fact]
        public void AbsolutePath_GetCanonicalForm_ForwardSlashes_Normalized()
        {
            var path = new AbsolutePath("C:/test/file.txt");
            string canonical = path.GetCanonicalForm();
            Assert.Equal(@"C:\test\file.txt", canonical);
        }

        [Fact]
        public void AbsolutePath_GetCanonicalForm_TrailingBackslash()
        {
            var path = new AbsolutePath(@"C:\test\");
            string canonical = path.GetCanonicalForm();
            Assert.Equal(@"C:\test\", canonical);
        }

        [Fact]
        public void AbsolutePath_GetCanonicalForm_RootPath()
        {
            var path = new AbsolutePath(@"C:\");
            string canonical = path.GetCanonicalForm();
            Assert.Equal(@"C:\", canonical);
        }

        [Fact]
        public void AbsolutePath_PathWithSpaces_GetCanonicalForm()
        {
            var path = new AbsolutePath(@"C:\My Folder\My File.txt");
            string canonical = path.GetCanonicalForm();
            Assert.Equal(@"C:\My Folder\My File.txt", canonical);
        }

        #endregion

        #region AbsolutePath operator consistency

        [Fact]
        public void AbsolutePath_EqualityAndInequality_AreConsistent()
        {
            var a = new AbsolutePath(@"C:\test");
            var b = new AbsolutePath(@"C:\test");
            var c = new AbsolutePath(@"C:\other");

            Assert.True(a == b);
            Assert.False(a != b);
            Assert.False(a == c);
            Assert.True(a != c);
        }

        [Fact]
        public void AbsolutePath_EqualsAndOperator_AreConsistent()
        {
            var a = new AbsolutePath(@"C:\path");
            var b = new AbsolutePath(@"C:\PATH");
            Assert.Equal(a.Equals(b), a == b);
        }

        [Fact]
        public void AbsolutePath_ImplicitConversion_MatchesValue()
        {
            var path = new AbsolutePath(@"C:\test\file.txt");
            string implicitResult = path;
            Assert.Equal(path.Value, implicitResult);
        }

        [Fact]
        public void AbsolutePath_ToString_MatchesImplicitConversion_WhenNotDefault()
        {
            var path = new AbsolutePath(@"C:\test");
            string toString = path.ToString();
            string implicitConv = path;
            Assert.Equal(toString, implicitConv);
        }

        #endregion

        #region TaskEnvironment edge cases

        [Fact]
        public void TaskEnvironment_GetAbsolutePath_NestedRelative_Resolves()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\project" };
            AbsolutePath result = env.GetAbsolutePath(@"sub\deep\file.txt");
            Assert.Equal(@"C:\project\sub\deep\file.txt", result.Value);
        }

        [Fact]
        public void TaskEnvironment_GetAbsolutePath_DotSegment_Resolves()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\project" };
            AbsolutePath result = env.GetAbsolutePath(@".\file.txt");
            Assert.Equal(@"C:\project\file.txt", result.Value);
        }

        [Fact]
        public void TaskEnvironment_MultipleEnvVars_AllRetrievable()
        {
            var env = new TaskEnvironment();
            env.SetEnvironmentVariable("VAR1", "value1");
            env.SetEnvironmentVariable("VAR2", "value2");
            env.SetEnvironmentVariable("VAR3", "value3");

            Assert.Equal("value1", env.GetEnvironmentVariable("VAR1"));
            Assert.Equal("value2", env.GetEnvironmentVariable("VAR2"));
            Assert.Equal("value3", env.GetEnvironmentVariable("VAR3"));
        }

        [Fact]
        public void TaskEnvironment_GetProcessStartInfo_ContainsAllVariables()
        {
            var env = new TaskEnvironment();
            env.SetEnvironmentVariable("A", "1");
            env.SetEnvironmentVariable("B", "2");
            env.SetEnvironmentVariable("C", "3");

            var psi = env.GetProcessStartInfo();
            Assert.Equal("1", psi.Environment["A"]);
            Assert.Equal("2", psi.Environment["B"]);
            Assert.Equal("3", psi.Environment["C"]);
        }

        [Fact]
        public void TaskEnvironment_GetAbsolutePath_ReturnsAbsolutePathType()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\root" };
            object result = env.GetAbsolutePath("item");
            Assert.IsType<AbsolutePath>(result);
        }

        [Fact]
        public void TaskEnvironment_ProjectDirectory_CanBeReassigned()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\first" };
            Assert.Equal(@"C:\first", env.ProjectDirectory);
            env.ProjectDirectory = @"D:\second";
            Assert.Equal(@"D:\second", env.ProjectDirectory);
        }

        [Fact]
        public void TaskEnvironment_GetAbsolutePath_UsesCurrentProjectDirectory()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\first" };
            AbsolutePath path1 = env.GetAbsolutePath("file.txt");
            env.ProjectDirectory = @"D:\second";
            AbsolutePath path2 = env.GetAbsolutePath("file.txt");

            Assert.Equal(@"C:\first\file.txt", path1.Value);
            Assert.Equal(@"D:\second\file.txt", path2.Value);
        }

        #endregion

        #region IMultiThreadableTask contract

        private class SimpleTask : IMultiThreadableTask
        {
            public TaskEnvironment TaskEnvironment { get; set; } = new TaskEnvironment();
        }

        [Fact]
        public void IMultiThreadableTask_IsInterface()
        {
            Assert.True(typeof(IMultiThreadableTask).IsInterface);
        }

        [Fact]
        public void IMultiThreadableTask_HasSingleProperty()
        {
            var properties = typeof(IMultiThreadableTask).GetProperties();
            Assert.Single(properties);
            Assert.Equal(nameof(IMultiThreadableTask.TaskEnvironment), properties[0].Name);
        }

        [Fact]
        public void IMultiThreadableTask_PropertyType_IsTaskEnvironment()
        {
            var prop = typeof(IMultiThreadableTask).GetProperty(nameof(IMultiThreadableTask.TaskEnvironment));
            Assert.NotNull(prop);
            Assert.Equal(typeof(TaskEnvironment), prop!.PropertyType);
        }

        [Fact]
        public void IMultiThreadableTask_CanCastImplementationToInterface()
        {
            var task = new SimpleTask();
            IMultiThreadableTask interfaceRef = task;
            Assert.NotNull(interfaceRef);
            Assert.Same(task.TaskEnvironment, interfaceRef.TaskEnvironment);
        }

        #endregion

        #region MSBuildMultiThreadableTaskAttribute contract

        [Fact]
        public void MSBuildMultiThreadableTaskAttribute_IsSealed()
        {
            Assert.True(typeof(MSBuildMultiThreadableTaskAttribute).IsSealed);
        }

        [Fact]
        public void MSBuildMultiThreadableTaskAttribute_InNamespace()
        {
            Assert.Equal("Microsoft.Build.Framework", typeof(MSBuildMultiThreadableTaskAttribute).Namespace);
        }

        [Fact]
        public void MSBuildMultiThreadableTaskAttribute_HasNoPublicProperties()
        {
            var props = typeof(MSBuildMultiThreadableTaskAttribute)
                .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
            Assert.Empty(props);
        }

        [Fact]
        public void MSBuildMultiThreadableTaskAttribute_HasNoPublicMethods_BeyondObject()
        {
            var methods = typeof(MSBuildMultiThreadableTaskAttribute)
                .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
            Assert.Empty(methods);
        }

        #endregion

        #region Cross-type integration

        [MSBuildMultiThreadableTask]
        private class IntegratedTask : IMultiThreadableTask
        {
            public TaskEnvironment TaskEnvironment { get; set; } = new TaskEnvironment();
        }

        [Fact]
        public void IntegratedTask_AttributeAndInterface_WorkTogether()
        {
            var task = new IntegratedTask();
            task.TaskEnvironment = new TaskEnvironment { ProjectDirectory = @"C:\build" };

            // Verify attribute is present
            var attrs = typeof(IntegratedTask).GetCustomAttributes(typeof(MSBuildMultiThreadableTaskAttribute), false);
            Assert.Single(attrs);

            // Verify interface functionality
            IMultiThreadableTask itask = task;
            AbsolutePath path = itask.TaskEnvironment.GetAbsolutePath("output.dll");
            Assert.Equal(@"C:\build\output.dll", path.Value);
        }

        [Fact]
        public void IntegratedTask_EnvironmentVariables_FlowToProcessStartInfo()
        {
            var task = new IntegratedTask();
            task.TaskEnvironment.ProjectDirectory = @"C:\project";
            task.TaskEnvironment.SetEnvironmentVariable("BUILD_CONFIG", "Release");

            var psi = task.TaskEnvironment.GetProcessStartInfo();
            Assert.Equal(@"C:\project", psi.WorkingDirectory);
            Assert.Equal("Release", psi.Environment["BUILD_CONFIG"]);
        }

        [Fact]
        public void IntegratedTask_AbsolutePath_CanCompareResultsFromDifferentEnvironments()
        {
            var env1 = new TaskEnvironment { ProjectDirectory = @"C:\project" };
            var env2 = new TaskEnvironment { ProjectDirectory = @"C:\project" };

            AbsolutePath path1 = env1.GetAbsolutePath("file.txt");
            AbsolutePath path2 = env2.GetAbsolutePath("file.txt");
            Assert.Equal(path1, path2);
        }

        #endregion
    }
}
