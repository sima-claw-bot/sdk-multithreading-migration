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
    /// Tests for SharedPolyfills types: AbsolutePath struct, TaskEnvironment class,
    /// IMultiThreadableTask interface, and MSBuildMultiThreadableTaskAttribute.
    /// Created for task-2 validation.
    /// </summary>
    public class SharedPolyfillsTypeTests
    {
        #region AbsolutePath struct tests

        [Fact]
        public void AbsolutePath_IsReadOnlyValueType()
        {
            var type = typeof(AbsolutePath);
            Assert.True(type.IsValueType);
            Assert.True(type.GetCustomAttributes().Any(a => a.GetType().Name == "IsReadOnlyAttribute"),
                "AbsolutePath must be a readonly struct");
        }

        [Fact]
        public void AbsolutePath_Constructor_StoresPath()
        {
            var path = new AbsolutePath(@"C:\mydir\file.txt");
            Assert.Equal(@"C:\mydir\file.txt", path.Value);
        }

        [Fact]
        public void AbsolutePath_Constructor_NullPath_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new AbsolutePath(null!));
        }

        [Fact]
        public void AbsolutePath_Constructor_EmptyPath_Allowed()
        {
            var path = new AbsolutePath(string.Empty);
            Assert.Equal(string.Empty, path.Value);
        }

        [Fact]
        public void AbsolutePath_DefaultStruct_Value_ReturnsEmptyString()
        {
            var path = default(AbsolutePath);
            Assert.Equal(string.Empty, path.Value);
        }

        [Fact]
        public void AbsolutePath_ImplicitConversionToString()
        {
            var path = new AbsolutePath(@"C:\folder\file.cs");
            string result = path;
            Assert.Equal(@"C:\folder\file.cs", result);
        }

        [Fact]
        public void AbsolutePath_ImplicitConversion_DefaultReturnsEmpty()
        {
            var path = default(AbsolutePath);
            string result = path;
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void AbsolutePath_ToString_ReturnsRawValue()
        {
            var path = new AbsolutePath("C:/mixed/path");
            Assert.Equal("C:/mixed/path", path.ToString());
        }

        [Fact]
        public void AbsolutePath_ToString_DefaultReturnsNull()
        {
            var path = default(AbsolutePath);
            Assert.Null(path.ToString());
        }

        [Fact]
        public void AbsolutePath_GetCanonicalForm_ResolvesParentSegments()
        {
            var path = new AbsolutePath(@"C:\a\b\..\c\file.txt");
            Assert.Equal(@"C:\a\c\file.txt", path.GetCanonicalForm());
        }

        [Fact]
        public void AbsolutePath_GetCanonicalForm_ResolvesDotSegments()
        {
            var path = new AbsolutePath(@"C:\a\.\b\file.txt");
            Assert.Equal(@"C:\a\b\file.txt", path.GetCanonicalForm());
        }

        [Fact]
        public void AbsolutePath_GetCanonicalForm_DefaultReturnsNull()
        {
            var path = default(AbsolutePath);
            Assert.Null(path.GetCanonicalForm());
        }

        [Fact]
        public void AbsolutePath_GetCanonicalForm_EmptyReturnsEmpty()
        {
            var path = new AbsolutePath(string.Empty);
            Assert.Equal(string.Empty, path.GetCanonicalForm());
        }

        [Fact]
        public void AbsolutePath_Equals_SamePath_ReturnsTrue()
        {
            var a = new AbsolutePath(@"C:\test\path");
            var b = new AbsolutePath(@"C:\test\path");
            Assert.True(a.Equals(b));
        }

        [Fact]
        public void AbsolutePath_Equals_CaseInsensitive()
        {
            var a = new AbsolutePath(@"C:\TEST\PATH");
            var b = new AbsolutePath(@"c:\test\path");
            Assert.True(a.Equals(b));
        }

        [Fact]
        public void AbsolutePath_Equals_DifferentPath_ReturnsFalse()
        {
            var a = new AbsolutePath(@"C:\path1");
            var b = new AbsolutePath(@"C:\path2");
            Assert.False(a.Equals(b));
        }

        [Fact]
        public void AbsolutePath_Equals_BoxedAbsolutePath()
        {
            var a = new AbsolutePath(@"C:\test");
            object b = new AbsolutePath(@"C:\test");
            Assert.True(a.Equals(b));
        }

        [Fact]
        public void AbsolutePath_Equals_BoxedNonAbsolutePath_ReturnsFalse()
        {
            var a = new AbsolutePath(@"C:\test");
            Assert.False(a.Equals("C:\\test"));
            Assert.False(a.Equals(42));
            Assert.False(a.Equals(null));
        }

        [Fact]
        public void AbsolutePath_EqualityOperator_Works()
        {
            var a = new AbsolutePath(@"C:\test");
            var b = new AbsolutePath(@"C:\TEST");
            Assert.True(a == b);
        }

        [Fact]
        public void AbsolutePath_InequalityOperator_Works()
        {
            var a = new AbsolutePath(@"C:\test");
            var b = new AbsolutePath(@"C:\other");
            Assert.True(a != b);
        }

        [Fact]
        public void AbsolutePath_GetHashCode_EqualPathsReturnSameHash()
        {
            var a = new AbsolutePath(@"C:\Test\Path");
            var b = new AbsolutePath(@"c:\test\path");
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        [Fact]
        public void AbsolutePath_GetHashCode_DefaultReturnsZero()
        {
            Assert.Equal(0, default(AbsolutePath).GetHashCode());
        }

        [Fact]
        public void AbsolutePath_ImplementsIEquatable()
        {
            Assert.True(typeof(IEquatable<AbsolutePath>).IsAssignableFrom(typeof(AbsolutePath)));
        }

        [Fact]
        public void AbsolutePath_InNamespace_MicrosoftBuildFramework()
        {
            Assert.Equal("Microsoft.Build.Framework", typeof(AbsolutePath).Namespace);
        }

        [Fact]
        public void AbsolutePath_CanBeUsedInHashSet()
        {
            var set = new HashSet<AbsolutePath>
            {
                new AbsolutePath(@"C:\test"),
                new AbsolutePath(@"C:\TEST"),
                new AbsolutePath(@"C:\other")
            };
            Assert.Equal(2, set.Count);
        }

        [Fact]
        public void AbsolutePath_CanBeUsedAsDictionaryKey_CaseInsensitive()
        {
            var dict = new Dictionary<AbsolutePath, string>();
            dict[new AbsolutePath(@"C:\Key")] = "value1";
            dict[new AbsolutePath(@"C:\KEY")] = "value2";
            Assert.Single(dict);
            Assert.Equal("value2", dict[new AbsolutePath(@"c:\key")]);
        }

        [Fact]
        public void AbsolutePath_Equals_IsSymmetric()
        {
            var a = new AbsolutePath(@"C:\Sym");
            var b = new AbsolutePath(@"c:\sym");
            Assert.Equal(a.Equals(b), b.Equals(a));
        }

        [Fact]
        public void AbsolutePath_Equals_IsReflexive()
        {
            var a = new AbsolutePath(@"C:\Reflexive");
            Assert.True(a.Equals(a));
        }

        [Fact]
        public void AbsolutePath_Equals_IsTransitive()
        {
            var a = new AbsolutePath(@"C:\Trans");
            var b = new AbsolutePath(@"c:\trans");
            var c = new AbsolutePath(@"C:\TRANS");
            Assert.True(a.Equals(b));
            Assert.True(b.Equals(c));
            Assert.True(a.Equals(c));
        }

        [Fact]
        public void AbsolutePath_DefaultVsConstructedEmpty_AreNotEqual()
        {
            var def = default(AbsolutePath);
            var empty = new AbsolutePath(string.Empty);
            Assert.False(def.Equals(empty));
        }

        [Fact]
        public void AbsolutePath_HasImplicitStringOperator()
        {
            var op = typeof(AbsolutePath).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .SingleOrDefault(m => m.Name == "op_Implicit" && m.ReturnType == typeof(string));
            Assert.NotNull(op);
        }

        [Fact]
        public void AbsolutePath_PreservesWhitespace()
        {
            var path = new AbsolutePath("  spaces  ");
            Assert.Equal("  spaces  ", path.Value);
        }

        #endregion

        #region TaskEnvironment tests

        [Fact]
        public void TaskEnvironment_IsClass()
        {
            Assert.True(typeof(TaskEnvironment).IsClass);
        }

        [Fact]
        public void TaskEnvironment_InNamespace_MicrosoftBuildFramework()
        {
            Assert.Equal("Microsoft.Build.Framework", typeof(TaskEnvironment).Namespace);
        }

        [Fact]
        public void TaskEnvironment_ProjectDirectory_DefaultsToEmpty()
        {
            var env = new TaskEnvironment();
            Assert.Equal(string.Empty, env.ProjectDirectory);
        }

        [Fact]
        public void TaskEnvironment_ProjectDirectory_CanBeSet()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\myproject" };
            Assert.Equal(@"C:\myproject", env.ProjectDirectory);
        }

        [Fact]
        public void TaskEnvironment_GetAbsolutePath_CombinesProjectDirAndRelativePath()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\project" };
            AbsolutePath result = env.GetAbsolutePath(@"src\file.cs");
            Assert.Equal(@"C:\project\src\file.cs", result.Value);
        }

        [Fact]
        public void TaskEnvironment_GetAbsolutePath_AbsolutePathIgnoresProjectDir()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\project" };
            AbsolutePath result = env.GetAbsolutePath(@"D:\absolute\path.txt");
            Assert.Equal(@"D:\absolute\path.txt", result.Value);
        }

        [Fact]
        public void TaskEnvironment_GetAbsolutePath_ReturnsAbsolutePathStruct()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\proj" };
            var result = env.GetAbsolutePath("file.cs");
            Assert.IsType<AbsolutePath>(result);
        }

        [Fact]
        public void TaskEnvironment_GetAbsolutePath_ResolvesParentSegments()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\a\b" };
            AbsolutePath result = env.GetAbsolutePath(@"..\file.txt");
            Assert.Equal(@"C:\a\file.txt", result.Value);
        }

        [Fact]
        public void TaskEnvironment_GetAbsolutePath_ResolvesDotSegment()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\project" };
            AbsolutePath result = env.GetAbsolutePath(".");
            Assert.Equal(@"C:\project", result.Value);
        }

        [Fact]
        public void TaskEnvironment_GetEnvironmentVariable_ReturnsNullWhenNotSet()
        {
            var env = new TaskEnvironment();
            Assert.Null(env.GetEnvironmentVariable("NONEXISTENT"));
        }

        [Fact]
        public void TaskEnvironment_SetAndGetEnvironmentVariable()
        {
            var env = new TaskEnvironment();
            env.SetEnvironmentVariable("MY_VAR", "my_value");
            Assert.Equal("my_value", env.GetEnvironmentVariable("MY_VAR"));
        }

        [Fact]
        public void TaskEnvironment_SetEnvironmentVariable_Overwrites()
        {
            var env = new TaskEnvironment();
            env.SetEnvironmentVariable("K", "old");
            env.SetEnvironmentVariable("K", "new");
            Assert.Equal("new", env.GetEnvironmentVariable("K"));
        }

        [Fact]
        public void TaskEnvironment_SetEnvironmentVariable_NullName_Throws()
        {
            var env = new TaskEnvironment();
            var ex = Assert.Throws<ArgumentNullException>(() => env.SetEnvironmentVariable(null!, "v"));
            Assert.Equal("name", ex.ParamName);
        }

        [Fact]
        public void TaskEnvironment_SetEnvironmentVariable_NullValue_Throws()
        {
            var env = new TaskEnvironment();
            var ex = Assert.Throws<ArgumentNullException>(() => env.SetEnvironmentVariable("k", null!));
            Assert.Equal("value", ex.ParamName);
        }

        [Fact]
        public void TaskEnvironment_GetProcessStartInfo_SetsWorkingDirectory()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\work" };
            var psi = env.GetProcessStartInfo();
            Assert.Equal(@"C:\work", psi.WorkingDirectory);
        }

        [Fact]
        public void TaskEnvironment_GetProcessStartInfo_IncludesEnvVars()
        {
            var env = new TaskEnvironment();
            env.SetEnvironmentVariable("X", "1");
            env.SetEnvironmentVariable("Y", "2");
            var psi = env.GetProcessStartInfo();
            Assert.Equal("1", psi.Environment["X"]);
            Assert.Equal("2", psi.Environment["Y"]);
        }

        [Fact]
        public void TaskEnvironment_GetProcessStartInfo_ReturnsNewInstance()
        {
            var env = new TaskEnvironment();
            var a = env.GetProcessStartInfo();
            var b = env.GetProcessStartInfo();
            Assert.NotSame(a, b);
        }

        [Fact]
        public void TaskEnvironment_GetProcessStartInfo_ReturnsProcessStartInfoType()
        {
            var env = new TaskEnvironment();
            Assert.IsType<ProcessStartInfo>(env.GetProcessStartInfo());
        }

        [Fact]
        public void TaskEnvironment_EnvironmentVariablesIsolatedPerInstance()
        {
            var env1 = new TaskEnvironment();
            var env2 = new TaskEnvironment();
            env1.SetEnvironmentVariable("ISOLATED", "yes");
            Assert.Null(env2.GetEnvironmentVariable("ISOLATED"));
        }

        [Fact]
        public void TaskEnvironment_GetProcessStartInfo_MutatingPsiDoesNotAffectSource()
        {
            var env = new TaskEnvironment();
            env.SetEnvironmentVariable("SAFE", "original");
            var psi = env.GetProcessStartInfo();
            psi.Environment["SAFE"] = "modified";
            Assert.Equal("original", env.GetEnvironmentVariable("SAFE"));
        }

        [Fact]
        public void TaskEnvironment_ProjectDirectory_ReassignmentAffectsAbsolutePath()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\first" };
            AbsolutePath p1 = env.GetAbsolutePath("file.txt");
            env.ProjectDirectory = @"D:\second";
            AbsolutePath p2 = env.GetAbsolutePath("file.txt");
            Assert.Equal(@"C:\first\file.txt", p1.Value);
            Assert.Equal(@"D:\second\file.txt", p2.Value);
        }

        #endregion

        #region IMultiThreadableTask tests

        [Fact]
        public void IMultiThreadableTask_IsInterface()
        {
            Assert.True(typeof(IMultiThreadableTask).IsInterface);
        }

        [Fact]
        public void IMultiThreadableTask_InNamespace_MicrosoftBuildFramework()
        {
            Assert.Equal("Microsoft.Build.Framework", typeof(IMultiThreadableTask).Namespace);
        }

        [Fact]
        public void IMultiThreadableTask_HasTaskEnvironmentProperty()
        {
            var prop = typeof(IMultiThreadableTask).GetProperty("TaskEnvironment");
            Assert.NotNull(prop);
            Assert.Equal(typeof(TaskEnvironment), prop!.PropertyType);
            Assert.True(prop.CanRead);
            Assert.True(prop.CanWrite);
        }

        [Fact]
        public void IMultiThreadableTask_HasNoAdditionalMethods()
        {
            var methods = typeof(IMultiThreadableTask)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName)
                .ToList();
            Assert.Empty(methods);
        }

        private class TaskImpl : IMultiThreadableTask
        {
            public TaskEnvironment TaskEnvironment { get; set; } = new TaskEnvironment();
        }

        [Fact]
        public void IMultiThreadableTask_CanBeImplemented()
        {
            IMultiThreadableTask task = new TaskImpl();
            Assert.NotNull(task.TaskEnvironment);
        }

        [Fact]
        public void IMultiThreadableTask_TaskEnvironment_CanBeReplaced()
        {
            IMultiThreadableTask task = new TaskImpl();
            var env = new TaskEnvironment { ProjectDirectory = @"C:\replaced" };
            task.TaskEnvironment = env;
            Assert.Same(env, task.TaskEnvironment);
        }

        #endregion

        #region MSBuildMultiThreadableTaskAttribute tests

        [Fact]
        public void MSBuildMultiThreadableTaskAttribute_DerivesFromAttribute()
        {
            Assert.True(typeof(MSBuildMultiThreadableTaskAttribute).IsSubclassOf(typeof(Attribute)));
        }

        [Fact]
        public void MSBuildMultiThreadableTaskAttribute_IsSealed()
        {
            Assert.True(typeof(MSBuildMultiThreadableTaskAttribute).IsSealed);
        }

        [Fact]
        public void MSBuildMultiThreadableTaskAttribute_InNamespace_MicrosoftBuildFramework()
        {
            Assert.Equal("Microsoft.Build.Framework", typeof(MSBuildMultiThreadableTaskAttribute).Namespace);
        }

        [Fact]
        public void MSBuildMultiThreadableTaskAttribute_TargetsClassOnly()
        {
            var usage = typeof(MSBuildMultiThreadableTaskAttribute).GetCustomAttribute<AttributeUsageAttribute>();
            Assert.NotNull(usage);
            Assert.Equal(AttributeTargets.Class, usage!.ValidOn);
        }

        [Fact]
        public void MSBuildMultiThreadableTaskAttribute_AllowMultiple_False()
        {
            var usage = typeof(MSBuildMultiThreadableTaskAttribute).GetCustomAttribute<AttributeUsageAttribute>();
            Assert.False(usage!.AllowMultiple);
        }

        [Fact]
        public void MSBuildMultiThreadableTaskAttribute_Inherited_False()
        {
            var usage = typeof(MSBuildMultiThreadableTaskAttribute).GetCustomAttribute<AttributeUsageAttribute>();
            Assert.False(usage!.Inherited);
        }

        [Fact]
        public void MSBuildMultiThreadableTaskAttribute_CanInstantiate()
        {
            var attr = new MSBuildMultiThreadableTaskAttribute();
            Assert.NotNull(attr);
            Assert.IsAssignableFrom<Attribute>(attr);
        }

        [Fact]
        public void MSBuildMultiThreadableTaskAttribute_HasNoPublicDeclaredProperties()
        {
            var props = typeof(MSBuildMultiThreadableTaskAttribute)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Assert.Empty(props);
        }

        [MSBuildMultiThreadableTask]
        private class AnnotatedClass { }

        [Fact]
        public void MSBuildMultiThreadableTaskAttribute_CanBeAppliedToClass()
        {
            var attr = Attribute.GetCustomAttribute(typeof(AnnotatedClass), typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
            Assert.IsType<MSBuildMultiThreadableTaskAttribute>(attr);
        }

        #endregion

        #region Integration tests

        [MSBuildMultiThreadableTask]
        private class IntegratedTask : IMultiThreadableTask
        {
            public TaskEnvironment TaskEnvironment { get; set; } = new TaskEnvironment();
        }

        [Fact]
        public void Integration_AttributeAndInterface_CoexistOnSameClass()
        {
            var task = new IntegratedTask();
            // Has attribute
            var attr = typeof(IntegratedTask).GetCustomAttributes(typeof(MSBuildMultiThreadableTaskAttribute), false);
            Assert.Single(attr);
            // Implements interface
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void Integration_TaskEnvironmentProducesAbsolutePath_WithCorrectValue()
        {
            IMultiThreadableTask task = new IntegratedTask();
            task.TaskEnvironment = new TaskEnvironment { ProjectDirectory = @"C:\build" };
            task.TaskEnvironment.SetEnvironmentVariable("CONFIG", "Release");

            AbsolutePath output = task.TaskEnvironment.GetAbsolutePath(@"bin\Release\app.dll");
            Assert.Equal(@"C:\build\bin\Release\app.dll", output.Value);
            Assert.Equal("Release", task.TaskEnvironment.GetEnvironmentVariable("CONFIG"));
        }

        [Fact]
        public void Integration_AbsolutePathsFromSameEnv_AreEqual()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\project" };
            AbsolutePath a = env.GetAbsolutePath("file.cs");
            AbsolutePath b = env.GetAbsolutePath("FILE.CS");
            Assert.True(a == b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        [Fact]
        public void Integration_ProcessStartInfo_ReflectsAllState()
        {
            var task = new IntegratedTask();
            task.TaskEnvironment.ProjectDirectory = @"C:\build";
            task.TaskEnvironment.SetEnvironmentVariable("A", "1");
            task.TaskEnvironment.SetEnvironmentVariable("B", "2");

            var psi = task.TaskEnvironment.GetProcessStartInfo();
            Assert.Equal(@"C:\build", psi.WorkingDirectory);
            Assert.Equal("1", psi.Environment["A"]);
            Assert.Equal("2", psi.Environment["B"]);
        }

        [Fact]
        public void Integration_AbsolutePath_InHashSet_Deduplicates()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\project" };
            var set = new HashSet<AbsolutePath>
            {
                env.GetAbsolutePath("a.cs"),
                env.GetAbsolutePath("A.CS"),
                env.GetAbsolutePath("b.cs")
            };
            Assert.Equal(2, set.Count);
        }

        [Fact]
        public void Integration_TwoEnvironments_SameProjectDir_ProduceEqualPaths()
        {
            var env1 = new TaskEnvironment { ProjectDirectory = @"C:\same" };
            var env2 = new TaskEnvironment { ProjectDirectory = @"C:\same" };
            Assert.Equal(env1.GetAbsolutePath("file.txt"), env2.GetAbsolutePath("file.txt"));
        }

        [Fact]
        public void Integration_TwoEnvironments_DifferentProjectDir_ProduceDifferentPaths()
        {
            var env1 = new TaskEnvironment { ProjectDirectory = @"C:\dir1" };
            var env2 = new TaskEnvironment { ProjectDirectory = @"C:\dir2" };
            Assert.NotEqual(env1.GetAbsolutePath("file.txt"), env2.GetAbsolutePath("file.txt"));
        }

        [Fact]
        public void Integration_AbsolutePathFromEnv_CanonicalFormMatchesValue()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\project" };
            AbsolutePath path = env.GetAbsolutePath("subdir");
            Assert.Equal(path.Value, path.GetCanonicalForm());
        }

        [Fact]
        public void Integration_AbsolutePathFromEnv_ImplicitStringConversion()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\project" };
            string path = env.GetAbsolutePath("test.dll");
            Assert.Equal(@"C:\project\test.dll", path);
        }

        #endregion
    }
}
