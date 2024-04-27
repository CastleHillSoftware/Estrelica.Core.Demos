using Estrelica;
using Estrelica.Interfaces;
using Estrelica.Utility;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
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

		public static void Log(string message, ConsoleColor? color = null, bool newLine = true)
		{
			if (color.HasValue)
			{
				Console.ForegroundColor = color.Value;
			}
			if (newLine)
			{
				Console.WriteLine(message);
			}
			else
			{
				Console.Write(message);
			}
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
			string message = $"{prompt} ({keyChars.Conjoin("/").TrimEnd('/')}) ";
			Log(message, color, false);
			char? response = null;
			while (!response.HasValue)
			{
				var result = Char.ToUpper(Console.ReadKey(true).KeyChar);
				response = keyChars.FirstOrDefault(c => c == result);
			}
			Log(response.Value.ToString(), color);
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

		#region Boilerplate code for displaying paged results and getting a selection response from the user

		public static Func<IEnumerable<string>, IEnumerable<string>, char> getResponse = (mainOptions, subOptions) =>
		{
			HashSet<char> allowedKeys = new HashSet<char>();

			Action<IEnumerable<string>> showOptions = options =>
			{
				if (options != null)
				{
					foreach (var option in options.Where(o => !o.IsNullOrEmpty() && o.Length > 1))
					{
						char key = Char.ToUpper(option[0]);
						string value = option.Substring(1);
						Console.WriteLine($" {key}: {value}");
						if (!allowedKeys.Add(key))
						{
							throw new ArgumentException($"Duplicate key '{key}' in list");
						}
					}
				}
			};

			showOptions(mainOptions);

			if (subOptions != null && subOptions.Count() > 0)
			{
				Console.WriteLine();
				showOptions(subOptions);
			}

			char keyChar = (Char)0;
			while (!allowedKeys.Contains(keyChar))
			{
				keyChar = Char.ToUpper(Console.ReadKey(true).KeyChar);
			}
			return keyChar;
		};

		public static bool Confirm(string yesDescription, string noDescription)
		{
			return getResponse(new string[] { 'Y' + yesDescription, 'N' + noDescription }, null) == 'Y';
		}

		public static List<string> GetPageOptions(int currentPage, int totalRecords, int pageSize = 10)
		{
			List<string> pageOptions = new List<string>();
			if (currentPage > 0)
			{
				pageOptions.Add("PPrevious page");
			}
			if (totalRecords > (currentPage + 1) * pageSize)
			{
				pageOptions.Add("NNext page");
			}
			return pageOptions;
		}

		public static void ShowPages<T>(string pageHeader, IEnumerable<T> items, Func<T, string> itemTextCallback, Action<T> itemSelected,
			List<string> additionalOptions = null, Action<char> otherSelection = null)
		{
			bool continueShowing = true;
			int currentPage = 0;
			while (continueShowing)
			{
				Console.Clear();
				T[] thisPage = items.Skip(currentPage * 10).Take(10).ToArray();
				if (!pageHeader.IsNullOrEmpty())
				{
					Console.WriteLine(pageHeader.Populate(thisPage.Count(), items.Count()));
					Console.WriteLine();
				}
				List<string> pageOptions = Utilities.GetPageOptions(currentPage, items.Count());

				pageOptions.AddAll(additionalOptions?.Where(o => !String.IsNullOrWhiteSpace(o)));

				pageOptions.Add("XExit");
				char response = Utilities.getResponse(Enumerable.Range(0, thisPage.Count()).Select(i => $"{(Char)(i + 0x30)}{itemTextCallback(thisPage[0 + i])}"), pageOptions);
				switch (response)
				{
					case 'X': { continueShowing = false; break; }
					case 'N': { currentPage++; break; };
					case 'P': { currentPage--; break; };
					case char c when c >= '0' && c <= '9': { itemSelected?.Invoke(thisPage[((int)c) - 0x30]); break; }
					default: { otherSelection?.Invoke(response); break; }
				}
			}
		}


		#endregion

	}
}
