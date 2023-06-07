using Estrelica;
using Estrelica.Interfaces;
using Estrelica.Utility;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Linq;

namespace Estrelica.Demo
{
	public static class Utilities
	{
		private static int successCount = 0;
		private static int failureCount = 0;

		public static void Log(string message, LogLevel logLevel)
		{
			ConsoleColor? color = logLevel switch
			{
				LogLevel.Warning => ConsoleColor.Yellow,
				LogLevel.Error => ConsoleColor.Red,
				_ => null
			};
			Log(message, color);
		}

		public static void Log(string message, bool success)
		{
			Log(message, success ? ConsoleColor.Green : ConsoleColor.Red);
			if (success)
			{
				successCount++;
			}
			else
			{
				failureCount++;
			}
		}

		public static void Log(Exception ex)
		{
			Log($"{ex.GetType().Name}: {ex.Message}", ConsoleColor.Red);
		}

		public static void LogResults()
		{
			Log("");
			Log($"Executed {successCount + failureCount} tests of which {failureCount} did not meet expectations", failureCount == 0);
		}

		public static void Log(string message, ConsoleColor? color = null)
		{
			if (color.HasValue)
			{
				Console.ForegroundColor = color.Value;
			}
			Console.WriteLine(message);
			if (color.HasValue)
			{
				Console.ResetColor();
			}
		}

		public static void Pause(string prompt = null, ConsoleColor? color = null)
		{
			Log("");
			if (prompt != null)
			{
				Log(prompt, color);
				Log("");
			}
			Log("Hit any key to proceed...", color);
			Console.ReadKey();
			Log("");
		}

		public static char Prompt(string prompt, string acceptedKeys = "YN", ConsoleColor? color = ConsoleColor.Cyan)
		{
			acceptedKeys = acceptedKeys?.Trim().ToUpper() ?? String.Empty;
			if (acceptedKeys.Length == 0)
			{
				acceptedKeys = "YN";
			}
			var keyChars = acceptedKeys.ToCharArray();
			string message = $"{prompt} ({keyChars.Conjoin("//").TrimEnd('/')}) ";
			Log(message, color);
			char? response = null;
			while (!response.HasValue)
			{
				var result = Char.ToUpper(Console.ReadKey(true).KeyChar);
				response = keyChars.FirstOrDefault(c => c == result);
			}
			Log(response.Value.ToString());
			return response.Value;
		}

		public static IConfiguration LoadConfigFromFile(string appConfigFilename = null, string userSecretsId = "Estrelica.Core.Demo")
		{
			appConfigFilename = appConfigFilename ?? "appSettings.json";
			if (!Path.IsPathRooted(appConfigFilename))
			{
				appConfigFilename = Path.Combine(System.Environment.CurrentDirectory, appConfigFilename);
			}

			bool configFileOptional = !userSecretsId.IsNullOrEmpty();
			if (!configFileOptional && !File.Exists(appConfigFilename))
			{
				throw new FileNotFoundException($"Configuration file not found", appConfigFilename);
			}

			var configBuilder = new ConfigurationBuilder()
				.AddJsonFile(appConfigFilename, configFileOptional);

			if (configFileOptional) // means we have a user secrets Id specified, so include it as well...
			{
				configBuilder.AddUserSecrets(userSecretsId, true);
			}

			return configBuilder.Build();
		}

	}
}
