using System;
using Microsoft.Build.Framework;
using Xunit;

namespace UnsafeThreadSafeTasks.Tests
{
    public class TaskEnvironmentTests
    {
        [Fact]
        public void ProjectDirectory_DefaultsToEmpty()
        {
            var env = new TaskEnvironment();
            Assert.Equal(string.Empty, env.ProjectDirectory);
        }

        [Fact]
        public void ProjectDirectory_SetAndGet()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\project" };
            Assert.Equal(@"C:\project", env.ProjectDirectory);
        }

        [Fact]
        public void GetAbsolutePath_RelativePath_ResolvesAgainstProjectDirectory()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\project" };
            AbsolutePath result = env.GetAbsolutePath("subdir");
            Assert.Equal(@"C:\project\subdir", result.Value);
        }

        [Fact]
        public void GetAbsolutePath_AbsolutePath_ReturnsAsIs()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\project" };
            AbsolutePath result = env.GetAbsolutePath(@"D:\other\file.txt");
            Assert.Equal(@"D:\other\file.txt", result.Value);
        }

        [Fact]
        public void GetEnvironmentVariable_NotSet_ReturnsNull()
        {
            var env = new TaskEnvironment();
            Assert.Null(env.GetEnvironmentVariable("MISSING"));
        }

        [Fact]
        public void SetAndGetEnvironmentVariable_ReturnsValue()
        {
            var env = new TaskEnvironment();
            env.SetEnvironmentVariable("MY_VAR", "my_value");
            Assert.Equal("my_value", env.GetEnvironmentVariable("MY_VAR"));
        }

        [Fact]
        public void SetEnvironmentVariable_OverwritesExisting()
        {
            var env = new TaskEnvironment();
            env.SetEnvironmentVariable("KEY", "old");
            env.SetEnvironmentVariable("KEY", "new");
            Assert.Equal("new", env.GetEnvironmentVariable("KEY"));
        }

        [Fact]
        public void SetEnvironmentVariable_NullName_ThrowsArgumentNullException()
        {
            var env = new TaskEnvironment();
            Assert.Throws<ArgumentNullException>(() => env.SetEnvironmentVariable(null!, "value"));
        }

        [Fact]
        public void SetEnvironmentVariable_NullValue_ThrowsArgumentNullException()
        {
            var env = new TaskEnvironment();
            Assert.Throws<ArgumentNullException>(() => env.SetEnvironmentVariable("key", null!));
        }

        [Fact]
        public void GetProcessStartInfo_SetsWorkingDirectory()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\project" };
            var psi = env.GetProcessStartInfo();
            Assert.Equal(@"C:\project", psi.WorkingDirectory);
        }

        [Fact]
        public void GetProcessStartInfo_IncludesEnvironmentVariables()
        {
            var env = new TaskEnvironment();
            env.SetEnvironmentVariable("FOO", "bar");
            env.SetEnvironmentVariable("BAZ", "qux");

            var psi = env.GetProcessStartInfo();
            Assert.Equal("bar", psi.Environment["FOO"]);
            Assert.Equal("qux", psi.Environment["BAZ"]);
        }

        [Fact]
        public void GetProcessStartInfo_ReturnsNewInstanceEachCall()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\project" };
            var psi1 = env.GetProcessStartInfo();
            var psi2 = env.GetProcessStartInfo();
            Assert.NotSame(psi1, psi2);
        }

        [Fact]
        public void GetAbsolutePath_WithDotDotSegments_ResolvesCanonically()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\project\subdir" };
            AbsolutePath result = env.GetAbsolutePath(@"..\file.txt");
            Assert.Equal(@"C:\project\file.txt", result.Value);
        }

        [Fact]
        public void GetAbsolutePath_ReturnsAbsolutePathStruct()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\project" };
            AbsolutePath result = env.GetAbsolutePath("test.txt");
            Assert.IsType<AbsolutePath>(result);
        }

        [Fact]
        public void GetEnvironmentVariable_AfterSet_ReturnsCorrectValue()
        {
            var env = new TaskEnvironment();
            env.SetEnvironmentVariable("A", "1");
            env.SetEnvironmentVariable("B", "2");
            Assert.Equal("1", env.GetEnvironmentVariable("A"));
            Assert.Equal("2", env.GetEnvironmentVariable("B"));
        }

        [Fact]
        public void GetProcessStartInfo_NoVariables_HasEmptyWorkingDirectory()
        {
            var env = new TaskEnvironment();
            var psi = env.GetProcessStartInfo();
            Assert.Equal(string.Empty, psi.WorkingDirectory);
        }

        [Fact]
        public void GetAbsolutePath_EmptyString_ResolvesToProjectDirectory()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\project" };
            AbsolutePath result = env.GetAbsolutePath(string.Empty);
            Assert.Equal(@"C:\project", result.Value);
        }

        [Fact]
        public void GetEnvironmentVariable_EmptyStringKey_ReturnsNull()
        {
            var env = new TaskEnvironment();
            Assert.Null(env.GetEnvironmentVariable(string.Empty));
        }

        [Fact]
        public void SetEnvironmentVariable_EmptyStringKeyAndValue_Succeeds()
        {
            var env = new TaskEnvironment();
            env.SetEnvironmentVariable(string.Empty, string.Empty);
            Assert.Equal(string.Empty, env.GetEnvironmentVariable(string.Empty));
        }

        [Fact]
        public void GetProcessStartInfo_DoesNotMutateOriginalEnvironment()
        {
            var env = new TaskEnvironment();
            env.SetEnvironmentVariable("KEY", "value");
            var psi = env.GetProcessStartInfo();
            psi.Environment["KEY"] = "modified";
            Assert.Equal("value", env.GetEnvironmentVariable("KEY"));
        }

        [Fact]
        public void GetAbsolutePath_ReturnedPath_ImplicitlyConvertsToString()
        {
            var env = new TaskEnvironment { ProjectDirectory = @"C:\project" };
            AbsolutePath result = env.GetAbsolutePath("file.txt");
            string asString = result;
            Assert.Equal(@"C:\project\file.txt", asString);
        }
    }
}
