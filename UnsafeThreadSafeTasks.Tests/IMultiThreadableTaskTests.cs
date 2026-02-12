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
    }
}
