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
    /// Contract and behavioral tests for all SharedPolyfills types:
    /// AbsolutePath, TaskEnvironment, IMultiThreadableTask, MSBuildMultiThreadableTaskAttribute.
    /// </summary>
    public class SharedPolyfillsContractTests
    {
        #region AbsolutePath struct contract

        [Fact]
        public void AbsolutePath_IsReadOnlyStruct()
        {
            var type = typeof(AbsolutePath);
            Assert.True(type.IsValueType);

            var isReadOnly = type.GetCustomAttributes()
                .Any(a => a.GetType().FullName == "System.Runtime.CompilerServices.IsReadOnlyAttribute");
            Assert.True(isReadOnly, "AbsolutePath should be a readonly struct (missing IsReadOnlyAttribute)");
        }

        [Fact]
        public void AbsolutePath_LivesInMsBuildFrameworkNamespace()
        {
            Assert.Equal("Microsoft.Build.Framework", typeof(AbsolutePath).Namespace);
        }

        [Fact]
        public void AbsolutePath_ImplementsIEquatableOfSelf()
        {
            var iface = typeof(AbsolutePath).GetInterfaces()
                .SingleOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEquatable<>));
            Assert.NotNull(iface);
            Assert.Equal(typeof(AbsolutePath), iface!.GetGenericArguments()[0]);
        }

        [Fact]
        public void AbsolutePath_HasImplicitStringOperator()
        {
            var op = typeof(AbsolutePath).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == "op_Implicit" && m.ReturnType == typeof(string))
                .SingleOrDefault();
            Assert.NotNull(op);
        }

        [Fact]
        public void AbsolutePath_HasEqualityAndInequalityOperators()
        {
            var eqOp = typeof(AbsolutePath).GetMethod("op_Equality", BindingFlags.Public | BindingFlags.Static);
            var neOp = typeof(AbsolutePath).GetMethod("op_Inequality", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(eqOp);
            Assert.NotNull(neOp);
        }

        [Theory]
        [InlineData(@"C:\test")]
        [InlineData(@"D:\folder\subfolder\file.txt")]
        [InlineData("/unix/style/path")]
        [InlineData("relative")]
        public void AbsolutePath_ValueProperty_RoundTrips(string input)
        {
            var path = new AbsolutePath(input);
            Assert.Equal(input, path.Value);
        }

        [Theory]
        [InlineData(@"C:\a", @"C:\A", true)]
        [InlineData(@"C:\a", @"C:\b", false)]
        [InlineData(@"C:\test\file.txt", @"c:\TEST\FILE.TXT", true)]
        public void AbsolutePath_Equality_CaseInsensitive(string left, string right, bool expected)
        {
            var a = new AbsolutePath(left);
            var b = new AbsolutePath(right);
            Assert.Equal(expected, a == b);
            Assert.Equal(expected, a.Equals(b));
            Assert.Equal(!expected, a != b);
        }

        [Fact]
        public void AbsolutePath_GetHashCode_NullField_ReturnsZero()
        {
            // default struct has _value == null
            var path = default(AbsolutePath);
            Assert.Equal(0, path.GetHashCode());
        }

        [Fact]
        public void AbsolutePath_GetCanonicalForm_MultipleParentSegments()
        {
            var path = new AbsolutePath(@"C:\a\b\c\..\..\file.txt");
            Assert.Equal(@"C:\a\file.txt", path.GetCanonicalForm());
        }

        [Fact]
        public void AbsolutePath_Equals_DefaultVsConstructedEmpty_DiffersOnNullField()
        {
            var def = default(AbsolutePath);
            var empty = new AbsolutePath(string.Empty);
            // default has _value == null, constructed empty has _value == ""
            // OrdinalIgnoreCase.Equals(null, "") returns false
            Assert.False(def.Equals(empty));
        }

        [Fact]
        public void AbsolutePath_ImplicitConversion_CanAssignToString()
        {
            AbsolutePath path = new AbsolutePath(@"C:\test");
            string s = path; // implicit conversion
            Assert.Equal(@"C:\test", s);
        }

        [Fact]
        public void AbsolutePath_ToString_ReturnsRawValue()
        {
            var path = new AbsolutePath("C:/mixed/slashes");
            Assert.Equal("C:/mixed/slashes", path.ToString());
        }

        #endregion

        #region TaskEnvironment contract

        [Fact]
        public void TaskEnvironment_IsReferenceType()
        {
            Assert.True(typeof(TaskEnvironment).IsClass);
        }

        [Fact]
        public void TaskEnvironment_LivesInMsBuildFrameworkNamespace()
        {
            Assert.Equal("Microsoft.Build.Framework", typeof(TaskEnvironment).Namespace);
        }

        [Fact]
        public void TaskEnvironment_ProjectDirectory_DefaultEmpty()
        {
            var env = new TaskEnvironment();
            Assert.Equal(string.Empty, env.ProjectDirectory);
        }

        [Fact]
        public void TaskEnvironment_GetAbsolutePath_ReturnsAbsolutePathStruct()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\project" };
            var result = env.GetAbsolutePath("file.cs");
            Assert.IsType<AbsolutePath>(result);
        }

        [Fact]
        public void TaskEnvironment_GetAbsolutePath_AbsoluteInput_IgnoresProjectDir()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\project" };
            var result = env.GetAbsolutePath(@"D:\other\path.txt");
            Assert.Equal(@"D:\other\path.txt", result.Value);
        }

        [Fact]
        public void TaskEnvironment_GetAbsolutePath_RelativeInput_CombinesWithProjectDir()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\project" };
            var result = env.GetAbsolutePath(@"sub\file.cs");
            Assert.Equal(@"C:\project\sub\file.cs", result.Value);
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
        public void TaskEnvironment_EnvironmentVariables_Isolated_PerInstance()
        {
            var env1 = new TaskEnvironment();
            var env2 = new TaskEnvironment();
            env1.SetEnvironmentVariable("X", "1");
            Assert.Null(env2.GetEnvironmentVariable("X"));
        }

        [Fact]
        public void TaskEnvironment_GetProcessStartInfo_WorkingDirectoryMatchesProjectDir()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\my\project" };
            var psi = env.GetProcessStartInfo();
            Assert.Equal(@"C:\my\project", psi.WorkingDirectory);
        }

        [Fact]
        public void TaskEnvironment_GetProcessStartInfo_EnvVars_CopiedCorrectly()
        {
            var env = new TaskEnvironment();
            env.SetEnvironmentVariable("BUILD", "Release");
            env.SetEnvironmentVariable("PLATFORM", "x64");
            var psi = env.GetProcessStartInfo();
            Assert.Equal("Release", psi.Environment["BUILD"]);
            Assert.Equal("x64", psi.Environment["PLATFORM"]);
        }

        [Fact]
        public void TaskEnvironment_GetProcessStartInfo_IsNewInstance()
        {
            var env = new TaskEnvironment();
            var a = env.GetProcessStartInfo();
            var b = env.GetProcessStartInfo();
            Assert.NotSame(a, b);
        }

        [Fact]
        public void TaskEnvironment_GetAbsolutePath_DotDot_Resolves()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\a\b" };
            var result = env.GetAbsolutePath(@"..\c.txt");
            Assert.Equal(@"C:\a\c.txt", result.Value);
        }

        [Fact]
        public void TaskEnvironment_GetAbsolutePath_Dot_ResolvesToProjectDir()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\project" };
            var result = env.GetAbsolutePath(".");
            Assert.Equal(@"C:\project", result.Value);
        }

        #endregion

        #region IMultiThreadableTask contract

        [Fact]
        public void IMultiThreadableTask_IsInterface()
        {
            Assert.True(typeof(IMultiThreadableTask).IsInterface);
        }

        [Fact]
        public void IMultiThreadableTask_LivesInMsBuildFrameworkNamespace()
        {
            Assert.Equal("Microsoft.Build.Framework", typeof(IMultiThreadableTask).Namespace);
        }

        [Fact]
        public void IMultiThreadableTask_HasTaskEnvironmentProperty_WithGetAndSet()
        {
            var prop = typeof(IMultiThreadableTask).GetProperty("TaskEnvironment");
            Assert.NotNull(prop);
            Assert.Equal(typeof(TaskEnvironment), prop!.PropertyType);
            Assert.NotNull(prop.GetGetMethod());
            Assert.NotNull(prop.GetSetMethod());
        }

        [Fact]
        public void IMultiThreadableTask_HasNoMethods()
        {
            // Only getter and setter methods from the property; no additional methods
            var methods = typeof(IMultiThreadableTask)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName) // exclude property get/set
                .ToList();
            Assert.Empty(methods);
        }

        private class StubTask : IMultiThreadableTask
        {
            public TaskEnvironment TaskEnvironment { get; set; } = new TaskEnvironment();
        }

        [Fact]
        public void IMultiThreadableTask_Implementation_RoundTripsTaskEnvironment()
        {
            IMultiThreadableTask task = new StubTask();
            var env = new TaskEnvironment { ProjectDirectory = @"C:\work" };
            task.TaskEnvironment = env;
            Assert.Same(env, task.TaskEnvironment);
        }

        #endregion

        #region MSBuildMultiThreadableTaskAttribute contract

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
        public void MSBuildMultiThreadableTaskAttribute_LivesInMsBuildFrameworkNamespace()
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
        public void MSBuildMultiThreadableTaskAttribute_AllowMultiple_IsFalse()
        {
            var usage = typeof(MSBuildMultiThreadableTaskAttribute).GetCustomAttribute<AttributeUsageAttribute>();
            Assert.NotNull(usage);
            Assert.False(usage!.AllowMultiple);
        }

        [Fact]
        public void MSBuildMultiThreadableTaskAttribute_Inherited_IsFalse()
        {
            var usage = typeof(MSBuildMultiThreadableTaskAttribute).GetCustomAttribute<AttributeUsageAttribute>();
            Assert.NotNull(usage);
            Assert.False(usage!.Inherited);
        }

        [MSBuildMultiThreadableTask]
        private class AnnotatedTask : IMultiThreadableTask
        {
            public TaskEnvironment TaskEnvironment { get; set; } = new TaskEnvironment();
        }

        [Fact]
        public void MSBuildMultiThreadableTaskAttribute_CanDetectOnAnnotatedClass()
        {
            var attr = Attribute.GetCustomAttribute(typeof(AnnotatedTask), typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
            Assert.IsType<MSBuildMultiThreadableTaskAttribute>(attr);
        }

        #endregion

        #region Cross-type integration

        [Fact]
        public void Integration_AnnotatedTask_UsesTaskEnvironment_ProducesAbsolutePath()
        {
            IMultiThreadableTask task = new AnnotatedTask();
            task.TaskEnvironment = new TaskEnvironment { ProjectDirectory = @"C:\build" };
            task.TaskEnvironment.SetEnvironmentVariable("CONFIG", "Debug");

            AbsolutePath outPath = task.TaskEnvironment.GetAbsolutePath(@"bin\output.dll");
            Assert.Equal(@"C:\build\bin\output.dll", outPath.Value);
            Assert.Equal("Debug", task.TaskEnvironment.GetEnvironmentVariable("CONFIG"));
        }

        [Fact]
        public void Integration_AbsolutePathFromTaskEnvironment_CanBeCompared()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\project" };
            AbsolutePath a = env.GetAbsolutePath("file.cs");
            AbsolutePath b = env.GetAbsolutePath("FILE.CS");
            Assert.True(a == b);
        }

        [Fact]
        public void Integration_ProcessStartInfo_IncludesAllSetVariables()
        {
            var task = new AnnotatedTask();
            task.TaskEnvironment.ProjectDirectory = @"C:\build";
            task.TaskEnvironment.SetEnvironmentVariable("A", "1");
            task.TaskEnvironment.SetEnvironmentVariable("B", "2");

            var psi = task.TaskEnvironment.GetProcessStartInfo();
            Assert.Equal(@"C:\build", psi.WorkingDirectory);
            Assert.Equal("1", psi.Environment["A"]);
            Assert.Equal("2", psi.Environment["B"]);
        }

        [Fact]
        public void Integration_AbsolutePath_CanBeUsedInHashSet()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\project" };
            var set = new HashSet<AbsolutePath>
            {
                env.GetAbsolutePath("a.cs"),
                env.GetAbsolutePath("A.CS"),
                env.GetAbsolutePath("b.cs")
            };
            // a.cs and A.CS should deduplicate
            Assert.Equal(2, set.Count);
        }

        [Fact]
        public void Integration_TwoEnvironments_SameRelativePath_SameProjectDir_EqualAbsolutePaths()
        {
            var env1 = new TaskEnvironment { ProjectDirectory = @"C:\root" };
            var env2 = new TaskEnvironment { ProjectDirectory = @"C:\root" };
            Assert.Equal(env1.GetAbsolutePath("x.txt"), env2.GetAbsolutePath("x.txt"));
        }

        [Fact]
        public void Integration_TwoEnvironments_DifferentProjectDir_DifferentAbsolutePaths()
        {
            var env1 = new TaskEnvironment { ProjectDirectory = @"C:\dir1" };
            var env2 = new TaskEnvironment { ProjectDirectory = @"C:\dir2" };
            Assert.NotEqual(env1.GetAbsolutePath("x.txt"), env2.GetAbsolutePath("x.txt"));
        }

        #endregion
    }
}
