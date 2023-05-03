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
			try
			{
				action.Invoke();
			}
			catch (E ex)
			{
				expectedException = ex;
			}
			bool result = IsTrue(message, expectedException != null);
			if (result)
			{
				Utilities.Log($"  Got expected exception: {expectedException.GetType().Name}: '{expectedException.Message}'", ConsoleColor.Cyan);
			}
			else
            {
				Utilities.Log($"  Did not get expected exception " + typeof(E).FullName, ConsoleColor.Red);
            }
			return result;
		}

		public static bool IsGreater<T>(string message, T value, T targetValue) where T : IComparable<T> => IsTrue(message, value.CompareTo(targetValue) == 1);

		public static bool IsLess(string message, int value, int targetValue) => IsTrue(message, value < targetValue);
		public static bool AreEqual<T>(string message, T expectedValue, T actualValue)
		{
			if (!IsTrue(message, expectedValue.Equals(actualValue)))
			{
				Utilities.Log($"**  Expected: {expectedValue} Actual: {actualValue}", ConsoleColor.Yellow);
				return false;
			}
			return true;
		}

		public static bool AreNotEqual<T>(string message, T unexpectedValue, T actualValue)
		{
			if (!IsTrue(message, !unexpectedValue.Equals(actualValue)))
			{
				Utilities.Log($"**  Unexpected: {actualValue}", ConsoleColor.Yellow);
				return false;
			}
			return true;
		}

		public static bool IsGreaterThanZero(string message, int value) => IsGreater(message, value, 0);

		public static bool IsNull<T>(string message, T value) where T : class => IsTrue(message, value is null);
		public static bool IsNotNull<T>(string message, T value) where T : class => IsTrue(message, !(value is null));

	}

}
