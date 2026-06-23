using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SchedulerEngineRuntimeTests
{
    internal static class TestAssert
    {
        public static T Throws<T>(Action action)
            where T : Exception
        {
            try
            {
                action();
            }
            catch (T exception)
            {
                return exception;
            }
            catch (Exception exception)
            {
                Assert.Fail($"Expected {typeof(T).Name}, but caught {exception.GetType().Name}: {exception}");
            }

            Assert.Fail($"Expected {typeof(T).Name}, but no exception was thrown.");
            throw new InvalidOperationException("Unreachable assertion path.");
        }

        public static async Task<T> ThrowsAsync<T>(Func<Task> action)
            where T : Exception
        {
            try
            {
                await action();
            }
            catch (T exception)
            {
                return exception;
            }
            catch (Exception exception)
            {
                Assert.Fail($"Expected {typeof(T).Name}, but caught {exception.GetType().Name}: {exception}");
            }

            Assert.Fail($"Expected {typeof(T).Name}, but no exception was thrown.");
            throw new InvalidOperationException("Unreachable assertion path.");
        }
    }
}
