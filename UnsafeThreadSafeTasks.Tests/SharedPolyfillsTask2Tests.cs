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
    /// Tests for SharedPolyfills types created in task-2:
    /// AbsolutePath struct, TaskEnvironment class,
    /// IMultiThreadableTask interface, MSBuildMultiThreadableTaskAttribute.
    /// </summary>
    public class SharedPolyfillsTask2Tests
    {
        #region AbsolutePath - constructor and Value

        [Fact]
        public void AbsolutePath_Constructor_NullPath_ThrowsArgumentNullException_WithParamName()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => new AbsolutePath(null!));
            Assert.Equal("path", ex.ParamName);
        }

        [Theory]
        [InlineData(@"C:\test\file.txt")]
        [InlineData(@"D:\")]
        [InlineData("relative/path")]
        [InlineData("")]
        [InlineData("   ")]
        public void AbsolutePath_Value_RoundTrips(string input)
        {
            var path = new AbsolutePath(input);
            Assert.Equal(input, path.Value);
        }

        [Fact]
        public void AbsolutePath_DefaultStruct_Value_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, default(AbsolutePath).Value);
        }

        #endregion

        #region AbsolutePath - implicit operator and ToString

        [Fact]
        public void AbsolutePath_ImplicitConversion_MatchesValueProperty()
        {
            var path = new AbsolutePath(@"C:\dir\file.cs");
            string result = path;
            Assert.Equal(path.Value, result);
        }

        [Fact]
        public void AbsolutePath_ToString_ReturnsBareField()
        {
            // ToString returns _value directly (not Value property), so default is null
            Assert.Null(default(AbsolutePath).ToString());
        }

        [Fact]
        public void AbsolutePath_ToString_NonDefault_ReturnsStoredPath()
        {
            var path = new AbsolutePath("C:/forward/slash");
            Assert.Equal("C:/forward/slash", path.ToString());
        }

        #endregion

        #region AbsolutePath - GetCanonicalForm

        [Theory]
        [InlineData(@"C:\a\..\b\file.txt", @"C:\b\file.txt")]
        [InlineData(@"C:\a\.\b\file.txt", @"C:\a\b\file.txt")]
        [InlineData(@"C:\a\b\c\..\..\file.txt", @"C:\a\file.txt")]
        public void AbsolutePath_GetCanonicalForm_ResolvesRelativeSegments(string input, string expected)
        {
            Assert.Equal(expected, new AbsolutePath(input).GetCanonicalForm());
        }

        [Fact]
        public void AbsolutePath_GetCanonicalForm_DefaultStruct_ReturnsNull()
        {
            Assert.Null(default(AbsolutePath).GetCanonicalForm());
        }

        [Fact]
        public void AbsolutePath_GetCanonicalForm_EmptyString_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, new AbsolutePath("").GetCanonicalForm());
        }

        [Fact]
        public void AbsolutePath_GetCanonicalForm_NormalizesForwardSlashes()
        {
            var path = new AbsolutePath("C:/dir/file.txt");
            Assert.Equal(@"C:\dir\file.txt", path.GetCanonicalForm());
        }

        #endregion

        #region AbsolutePath - equality (IEquatable, operators, boxed)

        [Fact]
        public void AbsolutePath_Equals_SamePath_ReturnsTrue()
        {
            var a = new AbsolutePath(@"C:\path");
            var b = new AbsolutePath(@"C:\path");
            Assert.True(a.Equals(b));
        }

        [Theory]
        [InlineData(@"C:\TEST", @"c:\test")]
        [InlineData(@"C:\Dir\FILE.TXT", @"c:\dir\file.txt")]
        public void AbsolutePath_Equals_CaseInsensitive(string left, string right)
        {
            Assert.True(new AbsolutePath(left).Equals(new AbsolutePath(right)));
        }

        [Fact]
        public void AbsolutePath_Equals_DifferentPaths_ReturnsFalse()
        {
            Assert.False(new AbsolutePath(@"C:\a").Equals(new AbsolutePath(@"C:\b")));
        }

        [Fact]
        public void AbsolutePath_Equals_BoxedAbsolutePath_ReturnsTrue()
        {
            var a = new AbsolutePath(@"C:\x");
            object b = new AbsolutePath(@"C:\x");
            Assert.True(a.Equals(b));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("C:\\x")]
        [InlineData(42)]
        public void AbsolutePath_Equals_NonAbsolutePathObject_ReturnsFalse(object? other)
        {
            Assert.False(new AbsolutePath(@"C:\x").Equals(other));
        }

        [Fact]
        public void AbsolutePath_EqualityOperator_CaseInsensitive()
        {
            Assert.True(new AbsolutePath(@"C:\A") == new AbsolutePath(@"c:\a"));
        }

        [Fact]
        public void AbsolutePath_InequalityOperator_DifferentPaths()
        {
            Assert.True(new AbsolutePath(@"C:\a") != new AbsolutePath(@"C:\b"));
        }

        [Fact]
        public void AbsolutePath_Operators_Consistent()
        {
            var a = new AbsolutePath(@"C:\same");
            var b = new AbsolutePath(@"C:\same");
            Assert.True(a == b);
            Assert.False(a != b);
            Assert.Equal(a.Equals(b), a == b);
        }

        [Fact]
        public void AbsolutePath_DefaultVsConstructedEmpty_NotEqual()
        {
            // default has _value == null, constructed empty has _value == ""
            Assert.False(default(AbsolutePath).Equals(new AbsolutePath("")));
        }

        [Fact]
        public void AbsolutePath_DefaultVsDefault_Equal()
        {
            Assert.True(default(AbsolutePath) == default(AbsolutePath));
        }

        #endregion

        #region AbsolutePath - GetHashCode

        [Fact]
        public void AbsolutePath_GetHashCode_EqualPaths_SameHash()
        {
            var a = new AbsolutePath(@"C:\Test");
            var b = new AbsolutePath(@"c:\test");
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        [Fact]
        public void AbsolutePath_GetHashCode_DefaultStruct_ReturnsZero()
        {
            Assert.Equal(0, default(AbsolutePath).GetHashCode());
        }

        [Fact]
        public void AbsolutePath_GetHashCode_Consistent()
        {
            var path = new AbsolutePath(@"C:\consistent");
            Assert.Equal(path.GetHashCode(), path.GetHashCode());
        }

        #endregion

        #region AbsolutePath - readonly struct contract

        [Fact]
        public void AbsolutePath_IsValueType()
        {
            Assert.True(typeof(AbsolutePath).IsValueType);
        }

        [Fact]
        public void AbsolutePath_IsReadOnlyStruct()
        {
            var isReadOnly = typeof(AbsolutePath).GetCustomAttributes()
                .Any(a => a.GetType().FullName == "System.Runtime.CompilerServices.IsReadOnlyAttribute");
            Assert.True(isReadOnly, "AbsolutePath should be a readonly struct");
        }

        [Fact]
        public void AbsolutePath_ImplementsIEquatable()
        {
            Assert.True(typeof(IEquatable<AbsolutePath>).IsAssignableFrom(typeof(AbsolutePath)));
        }

        [Fact]
        public void AbsolutePath_Namespace_IsMicrosoftBuildFramework()
        {
            Assert.Equal("Microsoft.Build.Framework", typeof(AbsolutePath).Namespace);
        }

        #endregion

        #region AbsolutePath - collection interop

        [Fact]
        public void AbsolutePath_HashSet_DeduplicatesCaseInsensitive()
        {
            var set = new HashSet<AbsolutePath>
            {
                new AbsolutePath(@"C:\dir"),
                new AbsolutePath(@"C:\DIR"),
                new AbsolutePath(@"C:\other"),
            };
            Assert.Equal(2, set.Count);
        }

        [Fact]
        public void AbsolutePath_Dictionary_CaseInsensitiveLookup()
        {
            var dict = new Dictionary<AbsolutePath, string>();
            dict[new AbsolutePath(@"C:\key")] = "val";
            Assert.Equal("val", dict[new AbsolutePath(@"C:\KEY")]);
        }

        [Fact]
        public void AbsolutePath_List_Contains_UsesEquals()
        {
            var list = new List<AbsolutePath> { new AbsolutePath(@"C:\item") };
            Assert.Contains(new AbsolutePath(@"C:\ITEM"), list);
        }

        #endregion

        #region AbsolutePath - concurrency

        [Fact]
        public void AbsolutePath_ConcurrentCreation_NoDataCorruption()
        {
            var results = new AbsolutePath[100];
            Parallel.For(0, 100, i =>
            {
                results[i] = new AbsolutePath($@"C:\path\{i}\file.txt");
            });
            for (int i = 0; i < 100; i++)
            {
                Assert.Equal($@"C:\path\{i}\file.txt", results[i].Value);
            }
        }

        #endregion

        #region TaskEnvironment - ProjectDirectory

        [Fact]
        public void TaskEnvironment_ProjectDirectory_DefaultsToEmpty()
        {
            Assert.Equal(string.Empty, new TaskEnvironment().ProjectDirectory);
        }

        [Fact]
        public void TaskEnvironment_ProjectDirectory_SetAndGet()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\proj" };
            Assert.Equal(@"C:\proj", env.ProjectDirectory);
        }

        [Fact]
        public void TaskEnvironment_ProjectDirectory_Reassignment_AffectsGetAbsolutePath()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\first" };
            var p1 = env.GetAbsolutePath("f.txt");
            env.ProjectDirectory = @"D:\second";
            var p2 = env.GetAbsolutePath("f.txt");
            Assert.Equal(@"C:\first\f.txt", p1.Value);
            Assert.Equal(@"D:\second\f.txt", p2.Value);
        }

        #endregion

        #region TaskEnvironment - GetAbsolutePath

        [Fact]
        public void TaskEnvironment_GetAbsolutePath_RelativePath_CombinesWithProjectDir()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\project" };
            Assert.Equal(@"C:\project\src\main.cs", env.GetAbsolutePath(@"src\main.cs").Value);
        }

        [Fact]
        public void TaskEnvironment_GetAbsolutePath_AbsolutePath_IgnoresProjectDir()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\project" };
            Assert.Equal(@"D:\other.txt", env.GetAbsolutePath(@"D:\other.txt").Value);
        }

        [Fact]
        public void TaskEnvironment_GetAbsolutePath_DotDot_Resolves()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\a\b" };
            Assert.Equal(@"C:\a\file.txt", env.GetAbsolutePath(@"..\file.txt").Value);
        }

        [Fact]
        public void TaskEnvironment_GetAbsolutePath_Dot_ResolvesToProjectDir()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\project" };
            Assert.Equal(@"C:\project", env.GetAbsolutePath(".").Value);
        }

        [Fact]
        public void TaskEnvironment_GetAbsolutePath_ReturnsAbsolutePathType()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\p" };
            Assert.IsType<AbsolutePath>(env.GetAbsolutePath("x"));
        }

        [Fact]
        public void TaskEnvironment_GetAbsolutePath_EmptyString_ResolvesToProjectDir()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\project" };
            Assert.Equal(@"C:\project", env.GetAbsolutePath("").Value);
        }

        #endregion

        #region TaskEnvironment - environment variables

        [Fact]
        public void TaskEnvironment_GetEnvironmentVariable_Unset_ReturnsNull()
        {
            Assert.Null(new TaskEnvironment().GetEnvironmentVariable("NONEXISTENT"));
        }

        [Fact]
        public void TaskEnvironment_SetAndGet_EnvironmentVariable()
        {
            var env = new TaskEnvironment();
            env.SetEnvironmentVariable("KEY", "VALUE");
            Assert.Equal("VALUE", env.GetEnvironmentVariable("KEY"));
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
            var ex = Assert.Throws<ArgumentNullException>(() =>
                new TaskEnvironment().SetEnvironmentVariable(null!, "v"));
            Assert.Equal("name", ex.ParamName);
        }

        [Fact]
        public void TaskEnvironment_SetEnvironmentVariable_NullValue_Throws()
        {
            var ex = Assert.Throws<ArgumentNullException>(() =>
                new TaskEnvironment().SetEnvironmentVariable("k", null!));
            Assert.Equal("value", ex.ParamName);
        }

        [Fact]
        public void TaskEnvironment_EnvironmentVariables_IsolatedPerInstance()
        {
            var env1 = new TaskEnvironment();
            var env2 = new TaskEnvironment();
            env1.SetEnvironmentVariable("ISO", "yes");
            Assert.Null(env2.GetEnvironmentVariable("ISO"));
        }

        [Fact]
        public void TaskEnvironment_MultipleVariables_AllRetrievable()
        {
            var env = new TaskEnvironment();
            for (int i = 0; i < 10; i++)
                env.SetEnvironmentVariable($"VAR_{i}", $"val_{i}");
            for (int i = 0; i < 10; i++)
                Assert.Equal($"val_{i}", env.GetEnvironmentVariable($"VAR_{i}"));
        }

        #endregion

        #region TaskEnvironment - GetProcessStartInfo

        [Fact]
        public void TaskEnvironment_GetProcessStartInfo_SetsWorkingDirectory()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\work" };
            Assert.Equal(@"C:\work", env.GetProcessStartInfo().WorkingDirectory);
        }

        [Fact]
        public void TaskEnvironment_GetProcessStartInfo_IncludesAllEnvVars()
        {
            var env = new TaskEnvironment();
            env.SetEnvironmentVariable("A", "1");
            env.SetEnvironmentVariable("B", "2");
            var psi = env.GetProcessStartInfo();
            Assert.Equal("1", psi.Environment["A"]);
            Assert.Equal("2", psi.Environment["B"]);
        }

        [Fact]
        public void TaskEnvironment_GetProcessStartInfo_ReturnsNewInstanceEachCall()
        {
            var env = new TaskEnvironment();
            Assert.NotSame(env.GetProcessStartInfo(), env.GetProcessStartInfo());
        }

        [Fact]
        public void TaskEnvironment_GetProcessStartInfo_ReturnsProcessStartInfoType()
        {
            Assert.IsType<ProcessStartInfo>(new TaskEnvironment().GetProcessStartInfo());
        }

        [Fact]
        public void TaskEnvironment_GetProcessStartInfo_MutatingResult_DoesNotAffectSource()
        {
            var env = new TaskEnvironment();
            env.SetEnvironmentVariable("SAFE", "original");
            var psi = env.GetProcessStartInfo();
            psi.Environment["SAFE"] = "modified";
            Assert.Equal("original", env.GetEnvironmentVariable("SAFE"));
        }

        [Fact]
        public void TaskEnvironment_GetProcessStartInfo_SnapshotsCurrentState()
        {
            var env = new TaskEnvironment();
            env.SetEnvironmentVariable("X", "1");
            var psi1 = env.GetProcessStartInfo();

            env.SetEnvironmentVariable("Y", "2");
            var psi2 = env.GetProcessStartInfo();

            Assert.False(psi1.Environment.ContainsKey("Y"));
            Assert.True(psi2.Environment.ContainsKey("Y"));
        }

        #endregion

        #region TaskEnvironment - namespace and type

        [Fact]
        public void TaskEnvironment_IsClass()
        {
            Assert.True(typeof(TaskEnvironment).IsClass);
        }

        [Fact]
        public void TaskEnvironment_Namespace_IsMicrosoftBuildFramework()
        {
            Assert.Equal("Microsoft.Build.Framework", typeof(TaskEnvironment).Namespace);
        }

        #endregion

        #region IMultiThreadableTask

        [Fact]
        public void IMultiThreadableTask_IsInterface()
        {
            Assert.True(typeof(IMultiThreadableTask).IsInterface);
        }

        [Fact]
        public void IMultiThreadableTask_Namespace_IsMicrosoftBuildFramework()
        {
            Assert.Equal("Microsoft.Build.Framework", typeof(IMultiThreadableTask).Namespace);
        }

        [Fact]
        public void IMultiThreadableTask_HasTaskEnvironmentProperty_ReadWrite()
        {
            var prop = typeof(IMultiThreadableTask).GetProperty("TaskEnvironment");
            Assert.NotNull(prop);
            Assert.Equal(typeof(TaskEnvironment), prop!.PropertyType);
            Assert.True(prop.CanRead);
            Assert.True(prop.CanWrite);
        }

        [Fact]
        public void IMultiThreadableTask_HasNoNonPropertyMethods()
        {
            var methods = typeof(IMultiThreadableTask)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName)
                .ToList();
            Assert.Empty(methods);
        }

        private class Task2Impl : IMultiThreadableTask
        {
            public TaskEnvironment TaskEnvironment { get; set; } = new TaskEnvironment();
        }

        [Fact]
        public void IMultiThreadableTask_CanBeImplemented_AndUsed()
        {
            IMultiThreadableTask task = new Task2Impl();
            var env = new TaskEnvironment { ProjectDirectory = @"C:\impl" };
            task.TaskEnvironment = env;
            Assert.Same(env, task.TaskEnvironment);
        }

        #endregion

        #region MSBuildMultiThreadableTaskAttribute

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
        public void MSBuildMultiThreadableTaskAttribute_Namespace_IsMicrosoftBuildFramework()
        {
            Assert.Equal("Microsoft.Build.Framework", typeof(MSBuildMultiThreadableTaskAttribute).Namespace);
        }

        [Fact]
        public void MSBuildMultiThreadableTaskAttribute_TargetsClassOnly_NotMultiple_NotInherited()
        {
            var usage = typeof(MSBuildMultiThreadableTaskAttribute).GetCustomAttribute<AttributeUsageAttribute>();
            Assert.NotNull(usage);
            Assert.Equal(AttributeTargets.Class, usage!.ValidOn);
            Assert.False(usage.AllowMultiple);
            Assert.False(usage.Inherited);
        }

        [Fact]
        public void MSBuildMultiThreadableTaskAttribute_HasNoPublicDeclaredMembers()
        {
            var props = typeof(MSBuildMultiThreadableTaskAttribute)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            var methods = typeof(MSBuildMultiThreadableTaskAttribute)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Assert.Empty(props);
            Assert.Empty(methods);
        }

        [MSBuildMultiThreadableTask]
        private class Task2AnnotatedClass { }

        [Fact]
        public void MSBuildMultiThreadableTaskAttribute_CanBeDetectedOnClass()
        {
            var attr = Attribute.GetCustomAttribute(typeof(Task2AnnotatedClass),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
            Assert.IsType<MSBuildMultiThreadableTaskAttribute>(attr);
        }

        #endregion

        #region Cross-type integration

        [MSBuildMultiThreadableTask]
        private class Task2FullTask : IMultiThreadableTask
        {
            public TaskEnvironment TaskEnvironment { get; set; } = new TaskEnvironment();
        }

        [Fact]
        public void Integration_AttributeAndInterface_CoexistOnSameClass()
        {
            var task = new Task2FullTask();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
            Assert.NotNull(typeof(Task2FullTask).GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>());
        }

        [Fact]
        public void Integration_TaskEnvironment_ProducesAbsolutePath_UsableInCollections()
        {
            var task = new Task2FullTask();
            task.TaskEnvironment = new TaskEnvironment { ProjectDirectory = @"C:\build" };
            task.TaskEnvironment.SetEnvironmentVariable("CONFIG", "Release");

            AbsolutePath path = task.TaskEnvironment.GetAbsolutePath(@"bin\Release\app.dll");
            Assert.Equal(@"C:\build\bin\Release\app.dll", path.Value);
            Assert.Equal("Release", task.TaskEnvironment.GetEnvironmentVariable("CONFIG"));

            var set = new HashSet<AbsolutePath> { path, new AbsolutePath(@"C:\BUILD\BIN\RELEASE\APP.DLL") };
            Assert.Single(set);
        }

        [Fact]
        public void Integration_ProcessStartInfo_ReflectsFullState()
        {
            var task = new Task2FullTask();
            task.TaskEnvironment.ProjectDirectory = @"C:\build";
            task.TaskEnvironment.SetEnvironmentVariable("OUT", @"bin\release");

            var psi = task.TaskEnvironment.GetProcessStartInfo();
            Assert.Equal(@"C:\build", psi.WorkingDirectory);
            Assert.Equal(@"bin\release", psi.Environment["OUT"]);
        }

        [Fact]
        public void Integration_TwoEnvironments_SameProject_ProduceEqualPaths()
        {
            var env1 = new TaskEnvironment { ProjectDirectory = @"C:\same" };
            var env2 = new TaskEnvironment { ProjectDirectory = @"C:\same" };
            Assert.Equal(env1.GetAbsolutePath("f.txt"), env2.GetAbsolutePath("f.txt"));
        }

        [Fact]
        public void Integration_TwoEnvironments_DifferentProject_ProduceDifferentPaths()
        {
            var env1 = new TaskEnvironment { ProjectDirectory = @"C:\a" };
            var env2 = new TaskEnvironment { ProjectDirectory = @"C:\b" };
            Assert.NotEqual(env1.GetAbsolutePath("f.txt"), env2.GetAbsolutePath("f.txt"));
        }

        [Fact]
        public void Integration_AbsolutePathFromEnv_ImplicitConversion()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\proj" };
            string path = env.GetAbsolutePath("out.dll");
            Assert.Equal(@"C:\proj\out.dll", path);
        }

        [Fact]
        public void Integration_AbsolutePathFromEnv_CanonicalFormMatchesValue()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\proj" };
            var path = env.GetAbsolutePath("sub");
            Assert.Equal(path.Value, path.GetCanonicalForm());
        }

        #endregion
    }
}
