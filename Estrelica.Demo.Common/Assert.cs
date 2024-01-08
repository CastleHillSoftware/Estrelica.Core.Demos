using System;

namespace Estrelica.Demo
{
	public static class Assert
	{
		public static bool IsTrue(string message, bool condition)
		{
			Utilities.Log(message, condition);
			return condition;
		}

		public static bool IsFalse(string message, bool condition) => IsTrue(message, !condition);

		public static bool ThrowsException<E>(string message, Action action) where E : Exception
		{
			E expectedException = null;
			Exception unexpectedException = null;
			try
			{
				action.Invoke();
			}
			catch (Exception ex)
			{
				expectedException = ex as E;
				unexpectedException = ex;
			}

			bool result = IsTrue(message, expectedException != null);
			if (result)
			{
				Utilities.Log($"  Got expected exception: {expectedException.GetType().FullName}: '{expectedException.Message}'", ConsoleColor.Cyan);
			}
			else
            {
				var error = $"  Did not get expected exception " + typeof(E).FullName;
				if (unexpectedException != null)
				{
					error += $".  Instead got unexpected exception {unexpectedException.GetType().FullName}: '{unexpectedException.Message}'";
				}
				Utilities.Log(error, ConsoleColor.Red);
            }
			return result;
		}

		public static bool ThrowsNoException(string message, Action action)
		{
			Exception unexpectedException = null;
			try
			{
				action.Invoke();
			}
			catch(Exception ex)
			{
				unexpectedException = ex;
			}
			bool result = IsTrue(message, unexpectedException == null);
			if (!result)
			{
				Utilities.Log($"  Got unexpected exception: {unexpectedException.GetType().FullName}: '{unexpectedException.Message}'", ConsoleColor.Red);
			}
			return result;
		}

		public static bool IsGreater<T>(string message, T value, T targetValue) where T : IComparable<T> => IsTrue(message, value.CompareTo(targetValue) == 1);

		public static bool IsLess(string message, int value, int targetValue) => IsTrue(message, value < targetValue);
		public static bool AreEqual<T>(string message, T expectedValue, T actualValue)
		{
			// Handle all the null cases first, so the .Equals() call below doesn't throw an exception
			// If both are null, the result is true
			bool result = false;
			var bothNull = (expectedValue is null && actualValue is null);
			if (bothNull)
			{
				result = IsTrue(message, true);
			}
			// If either is null but not both, result is false
			else if ((expectedValue is null || actualValue is null) && !bothNull)
			{
				result = IsTrue(message, false);
			}
			// Neither is null, so allow the equality check
			else
			{
				result = IsTrue(message, expectedValue.Equals(actualValue));
			}
			if (!result)
			{
				Utilities.Log($"**  Expected: {expectedValue} Actual: {actualValue}", ConsoleColor.Yellow);
			}
			return result;
		}

		public static bool AreNotEqual<T>(string message, T unexpectedValue, T actualValue)
		{
			// Handle all the null cases first, so the .Equals() call below doesn't throw an exception
			// If both are null, the result is false
			bool result = false;
			var bothNull = (unexpectedValue is null && actualValue is null);
			if (bothNull)
			{
				result = IsTrue(message, false);
			}
			// If either is null but not both, result is true
			else if ((unexpectedValue is null || actualValue is null) && !bothNull)
			{
				result = IsTrue(message, true);
			}
			// Neither is null, so allow the equality check
			else 
			{
				result = IsTrue(message, !unexpectedValue.Equals(actualValue));
			}
			if (!result)
			{
				Utilities.Log($"**  Unexpected: {actualValue}", ConsoleColor.Yellow);
			}
			return result;
		}

		public static bool IsGreaterThanZero(string message, int value) => IsGreater(message, value, 0);

		public static bool IsNull<T>(string message, T value) where T : class => IsTrue(message, value is null);
		public static bool IsNotNull<T>(string message, T value) where T : class => IsTrue(message, !(value is null));

	}

}
