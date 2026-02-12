using Microsoft.Build.Framework;
using Xunit;

namespace UnsafeThreadSafeTasks.Tests
{
    public class IMultiThreadableTaskTests
    {
        private class TestTask : IMultiThreadableTask
        {
            public TaskEnvironment TaskEnvironment { get; set; } = new TaskEnvironment();
        }

        [Fact]
        public void ImplementingClass_CanGetAndSetTaskEnvironment()
        {
            var task = new TestTask();
            var env = new TaskEnvironment { ProjectDirectory = @"C:\test" };
            task.TaskEnvironment = env;
            Assert.Same(env, task.TaskEnvironment);
        }

        [Fact]
        public void Interface_IsAssignableFromImplementation()
        {
            Assert.True(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(TestTask)));
        }

        [Fact]
        public void TaskEnvironment_DefaultValue_IsNotNull()
        {
            var task = new TestTask();
            Assert.NotNull(task.TaskEnvironment);
        }

        [Fact]
        public void TaskEnvironment_PropertyHasGetterAndSetter()
        {
            var prop = typeof(IMultiThreadableTask).GetProperty(nameof(IMultiThreadableTask.TaskEnvironment));
            Assert.NotNull(prop);
            Assert.True(prop!.CanRead);
            Assert.True(prop.CanWrite);
        }
    }
}
