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
    /// Validation tests for task-2 SharedPolyfills types: AbsolutePath struct,
    /// TaskEnvironment class, IMultiThreadableTask interface, and
    /// MSBuildMultiThreadableTaskAttribute. Covers gaps in existing test files.
    /// </summary>
    public class SharedPolyfillsTask2ValidationTests
    {
        #region AbsolutePath — struct layout and interface conformance

        [Fact]
        public void AbsolutePath_IsReadOnlyStruct_ConfirmedViaAttribute()
        {
            var type = typeof(AbsolutePath);
            Assert.True(type.IsValueType);
            var hasReadOnly = type.GetCustomAttributes(false)
                .Any(a => a.GetType().FullName == "System.Runtime.CompilerServices.IsReadOnlyAttribute");
            Assert.True(hasReadOnly, "AbsolutePath should be marked as readonly struct");
        }

        [Fact]
        public void AbsolutePath_HasSinglePrivateField()
        {
            var fields = typeof(AbsolutePath)
                .GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.Single(fields);
            Assert.Equal(typeof(string), fields[0].FieldType);
        }

        [Fact]
        public void AbsolutePath_IEquatable_ExplicitInterfaceCall()
        {
            IEquatable<AbsolutePath> a = new AbsolutePath(@"C:\test");
            var b = new AbsolutePath(@"c:\TEST");
            Assert.True(a.Equals(b));
        }

        #endregion

        #region AbsolutePath — GetCanonicalForm edge cases

        [Fact]
        public void AbsolutePath_GetCanonicalForm_MixedSeparatorsAndParentSegments()
        {
            var path = new AbsolutePath(@"C:/a/b/../c\.\file.txt");
            string canonical = path.GetCanonicalForm();
            Assert.Equal(@"C:\a\c\file.txt", canonical);
        }

        [Fact]
        public void AbsolutePath_GetCanonicalForm_MultipleDotDotFromDeep()
        {
            var path = new AbsolutePath(@"C:\x\y\z\..\..\file.txt");
            Assert.Equal(@"C:\x\file.txt", path.GetCanonicalForm());
        }

        [Fact]
        public void AbsolutePath_GetCanonicalForm_AlreadyCanonical_ReturnsSameValue()
        {
            var path = new AbsolutePath(@"C:\clean\path\file.txt");
            Assert.Equal(@"C:\clean\path\file.txt", path.GetCanonicalForm());
        }

        #endregion

        #region AbsolutePath — operator and equality edge cases

        [Fact]
        public void AbsolutePath_EqualityOperator_DefaultVsNonDefault_ReturnsFalse()
        {
            var def = default(AbsolutePath);
            var nonDef = new AbsolutePath(@"C:\test");
            Assert.False(def == nonDef);
            Assert.True(def != nonDef);
        }

        [Fact]
        public void AbsolutePath_EqualityOperator_NonDefaultVsDefault_ReturnsFalse()
        {
            var nonDef = new AbsolutePath(@"C:\test");
            var def = default(AbsolutePath);
            Assert.False(nonDef == def);
            Assert.True(nonDef != def);
        }

        [Fact]
        public void AbsolutePath_Equals_DefaultVsEmpty_Asymmetric()
        {
            var def = default(AbsolutePath);
            var empty = new AbsolutePath(string.Empty);
            // default has _value=null, empty has _value=""
            Assert.False(def.Equals(empty));
            Assert.False(empty.Equals(def));
        }

        [Fact]
        public void AbsolutePath_GetHashCode_EmptyString_IsNotZero()
        {
            // default (null) returns 0; empty string should produce non-zero hash
            var empty = new AbsolutePath(string.Empty);
            var def = default(AbsolutePath);
            // They should differ because null != ""
            Assert.NotEqual(def.GetHashCode(), empty.GetHashCode());
        }

        [Fact]
        public void AbsolutePath_ImplicitConversion_SameReferenceAsValue()
        {
            var path = new AbsolutePath(@"C:\ref\test");
            string fromValue = path.Value;
            string fromImplicit = path;
            Assert.Equal(fromValue, fromImplicit);
        }

        #endregion

        #region AbsolutePath — special path forms

        [Fact]
        public void AbsolutePath_UNCPath_PreservesValue()
        {
            var path = new AbsolutePath(@"\\server\share\folder\file.txt");
            Assert.Equal(@"\\server\share\folder\file.txt", path.Value);
        }

        [Fact]
        public void AbsolutePath_UNCPath_Equality_CaseInsensitive()
        {
            var a = new AbsolutePath(@"\\SERVER\Share\File.txt");
            var b = new AbsolutePath(@"\\server\share\file.txt");
            Assert.True(a == b);
        }

        [Fact]
        public void AbsolutePath_PathWithDotsInFilename_PreservesValue()
        {
            var path = new AbsolutePath(@"C:\test\my.config.json.bak");
            Assert.Equal(@"C:\test\my.config.json.bak", path.Value);
        }

        #endregion

        #region TaskEnvironment — environment variable case sensitivity

        [Fact]
        public void TaskEnvironment_EnvVarNames_AreCaseSensitive()
        {
            var env = new TaskEnvironment();
            env.SetEnvironmentVariable("MyVar", "lower");
            env.SetEnvironmentVariable("MYVAR", "upper");
            Assert.Equal("lower", env.GetEnvironmentVariable("MyVar"));
            Assert.Equal("upper", env.GetEnvironmentVariable("MYVAR"));
        }

        [Fact]
        public void TaskEnvironment_EnvVar_NotFoundCase_ReturnsNull()
        {
            var env = new TaskEnvironment();
            env.SetEnvironmentVariable("Key", "value");
            Assert.Null(env.GetEnvironmentVariable("key"));
            Assert.Null(env.GetEnvironmentVariable("KEY"));
        }

        #endregion

        #region TaskEnvironment — GetProcessStartInfo snapshot semantics

        [Fact]
        public void TaskEnvironment_GetProcessStartInfo_SnapshotsEnvVars()
        {
            var env = new TaskEnvironment();
            env.SetEnvironmentVariable("A", "1");
            var psi1 = env.GetProcessStartInfo();

            env.SetEnvironmentVariable("B", "2");
            var psi2 = env.GetProcessStartInfo();

            // psi1 was captured before B was set
            Assert.False(psi1.Environment.ContainsKey("B"));
            Assert.True(psi2.Environment.ContainsKey("B"));
            Assert.Equal("2", psi2.Environment["B"]);
        }

        [Fact]
        public void TaskEnvironment_GetProcessStartInfo_OverwrittenVar_ReflectsLatest()
        {
            var env = new TaskEnvironment();
            env.SetEnvironmentVariable("X", "old");
            var psi1 = env.GetProcessStartInfo();
            env.SetEnvironmentVariable("X", "new");
            var psi2 = env.GetProcessStartInfo();

            Assert.Equal("old", psi1.Environment["X"]);
            Assert.Equal("new", psi2.Environment["X"]);
        }

        #endregion

        #region TaskEnvironment — GetAbsolutePath with empty ProjectDirectory

        [Fact]
        public void TaskEnvironment_GetAbsolutePath_EmptyProjectDir_UsesCurrentDirectory()
        {
            var env = new TaskEnvironment(); // ProjectDirectory defaults to ""
            AbsolutePath result = env.GetAbsolutePath("relative.txt");
            // Path.Combine("", "relative.txt") = "relative.txt"
            // Path.GetFullPath("relative.txt") resolves against current directory
            Assert.False(string.IsNullOrEmpty(result.Value));
            Assert.True(System.IO.Path.IsPathRooted(result.Value));
        }

        [Fact]
        public void TaskEnvironment_GetAbsolutePath_AbsoluteInput_OverridesProjectDir()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\should\ignore" };
            AbsolutePath result = env.GetAbsolutePath(@"D:\absolute\path.dll");
            Assert.Equal(@"D:\absolute\path.dll", result.Value);
        }

        #endregion

        #region TaskEnvironment — concurrent operations

        [Fact]
        public async Task TaskEnvironment_ConcurrentGetAbsolutePath_IsConsistent()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\parallel" };
            var tasks = Enumerable.Range(0, 50).Select(i =>
                Task.Run(() => env.GetAbsolutePath($"file{i}.cs")));
            var results = await Task.WhenAll(tasks);
            for (int i = 0; i < 50; i++)
            {
                Assert.Equal($@"C:\parallel\file{i}.cs", results[i].Value);
            }
        }

        #endregion

        #region IMultiThreadableTask — detailed contract

        [Fact]
        public void IMultiThreadableTask_TaskEnvironment_PropertyType_IsTaskEnvironment()
        {
            var prop = typeof(IMultiThreadableTask).GetProperty("TaskEnvironment");
            Assert.NotNull(prop);
            Assert.Equal(typeof(TaskEnvironment), prop!.PropertyType);
        }

        [Fact]
        public void IMultiThreadableTask_HasNoInheritedInterfaces()
        {
            var interfaces = typeof(IMultiThreadableTask).GetInterfaces();
            Assert.Empty(interfaces);
        }

        [Fact]
        public void IMultiThreadableTask_LivesInCorrectNamespace()
        {
            Assert.Equal("Microsoft.Build.Framework", typeof(IMultiThreadableTask).Namespace);
        }

        private class ValidatingTask : IMultiThreadableTask
        {
            public TaskEnvironment TaskEnvironment { get; set; } = new TaskEnvironment();
        }

        [Fact]
        public void IMultiThreadableTask_TaskEnvironmentCanBeSwapped()
        {
            IMultiThreadableTask task = new ValidatingTask();
            var env1 = new TaskEnvironment { ProjectDirectory = @"C:\first" };
            var env2 = new TaskEnvironment { ProjectDirectory = @"C:\second" };
            task.TaskEnvironment = env1;
            Assert.Equal(@"C:\first", task.TaskEnvironment.ProjectDirectory);
            task.TaskEnvironment = env2;
            Assert.Equal(@"C:\second", task.TaskEnvironment.ProjectDirectory);
        }

        #endregion

        #region MSBuildMultiThreadableTaskAttribute — detailed contract

        [Fact]
        public void MSBuildMultiThreadableTaskAttribute_HasParameterlessConstructorOnly()
        {
            var ctors = typeof(MSBuildMultiThreadableTaskAttribute)
                .GetConstructors(BindingFlags.Public | BindingFlags.Instance);
            Assert.Single(ctors);
            Assert.Empty(ctors[0].GetParameters());
        }

        [Fact]
        public void MSBuildMultiThreadableTaskAttribute_HasNoDeclaredMethods()
        {
            var methods = typeof(MSBuildMultiThreadableTaskAttribute)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Assert.Empty(methods);
        }

        [Fact]
        public void MSBuildMultiThreadableTaskAttribute_HasNoDeclaredFields()
        {
            var fields = typeof(MSBuildMultiThreadableTaskAttribute)
                .GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Assert.Empty(fields);
        }

        [Fact]
        public void MSBuildMultiThreadableTaskAttribute_TypeIdIsUnique()
        {
            var a = new MSBuildMultiThreadableTaskAttribute();
            var b = new MSBuildMultiThreadableTaskAttribute();
            // TypeId for non-AllowMultiple attributes should be the same type
            Assert.Equal(a.TypeId, b.TypeId);
        }

        #endregion

        #region Integration — full workflow

        [MSBuildMultiThreadableTask]
        private class FullWorkflowTask : IMultiThreadableTask
        {
            public TaskEnvironment TaskEnvironment { get; set; } = new TaskEnvironment();
        }

        [Fact]
        public void Integration_FullWorkflow_AttributeDetection_InterfaceCast_PathResolution()
        {
            // 1. Detect attribute
            var attr = typeof(FullWorkflowTask)
                .GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>();
            Assert.NotNull(attr);

            // 2. Cast to interface
            IMultiThreadableTask task = new FullWorkflowTask();

            // 3. Configure environment
            task.TaskEnvironment = new TaskEnvironment { ProjectDirectory = @"C:\build\project" };
            task.TaskEnvironment.SetEnvironmentVariable("Configuration", "Release");
            task.TaskEnvironment.SetEnvironmentVariable("Platform", "x64");

            // 4. Resolve paths
            AbsolutePath outputPath = task.TaskEnvironment.GetAbsolutePath(@"bin\Release\output.dll");
            Assert.Equal(@"C:\build\project\bin\Release\output.dll", outputPath.Value);

            // 5. Verify canonical form
            Assert.Equal(outputPath.Value, outputPath.GetCanonicalForm());

            // 6. Create process start info
            ProcessStartInfo psi = task.TaskEnvironment.GetProcessStartInfo();
            Assert.Equal(@"C:\build\project", psi.WorkingDirectory);
            Assert.Equal("Release", psi.Environment["Configuration"]);
            Assert.Equal("x64", psi.Environment["Platform"]);

            // 7. Verify path equality (case insensitive)
            AbsolutePath samePath = task.TaskEnvironment.GetAbsolutePath(@"BIN\RELEASE\OUTPUT.DLL");
            Assert.Equal(outputPath, samePath);
        }

        [Fact]
        public void Integration_AbsolutePathFromDifferentEnvironments_InHashSet()
        {
            var env1 = new TaskEnvironment { ProjectDirectory = @"C:\proj" };
            var env2 = new TaskEnvironment { ProjectDirectory = @"C:\proj" };
            var env3 = new TaskEnvironment { ProjectDirectory = @"D:\other" };

            var set = new HashSet<AbsolutePath>
            {
                env1.GetAbsolutePath("a.cs"),
                env2.GetAbsolutePath("A.CS"),      // duplicate of above
                env1.GetAbsolutePath("b.cs"),
                env3.GetAbsolutePath("a.cs"),       // different project dir
            };
            Assert.Equal(3, set.Count);
        }

        [Fact]
        public void Integration_ProcessStartInfo_IsolatedFromTaskEnvironmentMutations()
        {
            IMultiThreadableTask task = new FullWorkflowTask();
            task.TaskEnvironment = new TaskEnvironment { ProjectDirectory = @"C:\work" };
            task.TaskEnvironment.SetEnvironmentVariable("STAGE", "build");

            var psi = task.TaskEnvironment.GetProcessStartInfo();

            // Mutate the source after snapshot
            task.TaskEnvironment.SetEnvironmentVariable("STAGE", "test");
            task.TaskEnvironment.SetEnvironmentVariable("NEW_VAR", "added");
            task.TaskEnvironment.ProjectDirectory = @"D:\changed";

            // Original PSI should be unaffected
            Assert.Equal(@"C:\work", psi.WorkingDirectory);
            Assert.Equal("build", psi.Environment["STAGE"]);
            Assert.False(psi.Environment.ContainsKey("NEW_VAR"));
        }

        [Fact]
        public async Task Integration_ConcurrentPathResolution_AcrossMultipleTasks()
        {
            var tasks = Enumerable.Range(0, 10).Select(i =>
            {
                IMultiThreadableTask mTask = new FullWorkflowTask();
                mTask.TaskEnvironment = new TaskEnvironment
                {
                    ProjectDirectory = $@"C:\project{i}"
                };
                return Task.Run(() => mTask.TaskEnvironment.GetAbsolutePath("file.cs"));
            });

            var results = await Task.WhenAll(tasks);
            for (int i = 0; i < 10; i++)
            {
                Assert.Equal($@"C:\project{i}\file.cs", results[i].Value);
            }
        }

        #endregion
    }
}
