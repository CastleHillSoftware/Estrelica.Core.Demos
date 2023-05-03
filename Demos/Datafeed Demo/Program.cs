using Estrelica.Interfaces;
using Estrelica.Utility;
using Estrelica.Archer.Content;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Estrelica.Demo.DatafeedExplorer
{
	class Program
	{
		/// <summary>
		/// This application demontstrates how to use the Estrelica Core to monitor datafeeds.  Archer's REST API features several methods to 
		/// retrieve information about the execution history and errors of a particular datafeed, given its GUID id, but unfortunately 
		/// does not provide any discovery mechanism to identify what those datafeed GUIDs are, or any other details about the datafeeds
		/// (e.g. Name, type, status, mappings, target level, etc.).
		/// 
		/// This gap is covered by the extended APIFacade.GetDatafeedsByLevelId() method, which returns this information for all datafeeds
		/// targeting a specific level in the authenticated Archer instance.  Therefore, this demo application requires that extensions are 
		/// available for that instance (included in the Professional and Enterprise license levels).  If neither extension path is available
		/// this application will terminate at startup.
		/// 
		/// The APIFacade.GetDatafeedsByLevelId() method is leveraged by the MetadataResolver to allow access to datafeeds in various
		/// ways, i.e. directly by Id or Guid, by level Id, or even as a complete list of all datafeeds in the target instance.
		/// Furthermore, the returned IDatafeed result(s) implement callbacks to the aforementioned Archer REST API methods to retrieve
		/// execution history and error details for each of those datafeeds.  This application will demonstrate various ways to iterate
		/// through an instance's datafeeds and explore those details about each.
		/// 
		/// Overall datafeed history should be used sparingly, due to a defect in Archer's implementation of the REST API /core/datafeed/history
		/// method.  The documentation for this method states that it supports OData paging (via $top and $skip parameters), but (as of 
		/// v6.9) this is not true.  This means that any calls to that method will return the entire history of a given datafeed in a single
		/// response, which can consume large amounts of memory on the calling system if the datafeed has a large number of history records.
		/// In light of this, the code below will prompt for confirmation before allowing datafeed execution history to be retrieved.
		/// 
		/// In most cases (e.g. for a monitoring application which executes more frequently that the datafeed itself) this problem can
		/// be mitigated by simply using the IDatafeed.LastRun property instead.  This returns the most recent execution history record
		/// for a given datafeed (via the /core/datafeed/history/recent) method, which does not incur the overhead of the full history method.
		/// If your use case only needs to know what the datafeed did on its last execution, and not any of the executions that preceded it,
		/// This property is the best way to evalute that.
		/// 
		/// This demo was knocked out in a couple of afternoons just to show how easy it is to query Archer about its datafeeds and dig
		/// into the details of each when you have the Estrelica Core in your toolkit.  It is not intended as a feature-complete production 
		/// application but instead only as an inspiration for you to create that feature-complete killer app yourself.
		/// 
		/// </summary>
		/// <param name="args"></param>

		static void Main(string[] args)
		{
			try
			{
				// Allow a particular instance config override to be selected via the command line.  If not provided, default to the base settings.
				string overrideKey = args.Length > 0 ? args[0] : null;

				// Instantiate Estrelica.Core and authenticate with Archer...
				core = CoreConfig.Load(
					w => Utilities.Log(w.Message, LogLevel.Warning),

					// The configuration under which CoreConfig will instantiate the Core is defined via JSON files.
					// This requires that you modify the file at
					//		..\..\..\..\..\Estrelica.Demo.Common\appSettingsSample.json (i.e. in the Estrelica.Demo.Common project)
					// and/or a local user secrets file at
					//		%appdata%\Microsoft\UserSecrets\Estrelica.Core.Demo\secrets.json
					// with your CastleHill Software authentication key and your Archer instance details and credentials.

					// See https://castlehillsoftware.github.io/Estrelica.Core.Demos/articles/manage_configuration.html
					// for more information on managing your configuration.

					// "appConfigFilename" specifies a JSON app settings file where your configuration is stored.  If not
					// explicitly provided this will default to "appSettings.json" in the current executing directory.
					// The string below will direct it to use the common appSettings.json file found in the Estrelica.Demo.Common project.
					appConfigFilename: @"..\..\..\..\..\Estrelica.Demo.Common\appSettings.json",

					// "userSecretsId" specifies the (optional) Id of a JSON user secrets file on the local machine containing values
					// which should override the corresponding values in the app settings file.  If not explicitly provided, none of the values
					// in the app settings JSON file will be overridden.  If a userSecretsId is provided, overloads will be loaded from
					// the file found at %appdata%\Microsoft\UserSecrets\xxxx\secrets.json on the local filesystem, where xxxx is the Id
					// string you've specified here.
					userSecretsId: "Estrelica.Core.Demo",

					// (Note that "Estrelica.Core.Demo" is specified in this project's .csproj file as the <UserSecretsId> for this project.
					// This means that you can easily edit the content of the file at %appdata%\Microsoft\UserSecrets\Estrelica.Core.Demo\secrets.json
					// by simply right-clicking the project in the Solution Explorer and choosing "Manage User Secrets".  If you elect to use
					// a different userSecretsId in the future, be sure to update the <UserSecretsId> node in your .csproj file in order to
					// maintain this editor association.)

					// "configOverrideKey" specifies a particular instance configuration to be selected from the file(s) above.
					// If not explicitly specified *or* if the app settings/user secrets files have nothing configured for this instance
					// name, the default (base) Archer configuration will be used.
					configOverrideKey: overrideKey);

				if (core.APIFacade.ExtensionsAvailable() == Archer.Utility.APISource.None)
				{
					throw new InvalidOperationException("This application requires Estrelica.Core extensions in order to retrieve Datafeed information from Archer");
				}
				
				// Show the available datafeeds
				ShowDatafeeds();
			}
			catch (Exception ex)
			{
				Utilities.Log(ex.ToString(), LogLevel.Error);
			}
		}

		#region Boilerplate code for displaying paged results and getting a selection response from the user

		static Func<IEnumerable<string>, IEnumerable<string>, char> getResponse = (mainOptions, subOptions) =>
		{
			HashSet<char> allowedKeys = new HashSet<char>();

			Action<IEnumerable<string>> showOptions = options =>
			{
				if (options != null)
				{
					foreach (var option in options)
					{
						if (option.Length > 1)
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

		static bool Confirm(string yesDescription, string noDescription)
		{
			return getResponse(new string[] { 'Y'+yesDescription, 'N'+noDescription }, null) == 'Y';
		}

		static List<string> GetPageOptions(int currentPage, int totalRecords, int pageSize = 10)
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

		#endregion

		static Estrelica.Core core = null;

		static void ShowDatafeeds()
		{
			Console.WriteLine($"Fetching datafeeds from instance {core.SessionProvider.Instance}...");
			
			IDatafeed[] allDatafeeds = core.Report.AllDatafeeds.ToArray();
			
			bool showDatafeeds = true;
			bool includeDataImports = true;
			int currentPage = 0;

			while (showDatafeeds)
			{
				Console.Clear();

				// Filter the complete set of datafeeds to eliminate data imports if the option was selected
				var datafeeds = allDatafeeds.Where(d => includeDataImports || d.DatafeedType != DatafeedType.DataImport);

				// Display the datafeeds in pages of 10, so the user can select one to evaluate via the '0' - '9' key
				IDatafeed[] thisPage = datafeeds.Skip(currentPage * 10).Take(10).ToArray();

				Console.WriteLine($"Showing {thisPage.Count()} of {datafeeds.Count()} total datafeeds from instance {core.SessionProvider.Instance}");
				Console.WriteLine();

				List<string> pageOptions = GetPageOptions(currentPage, datafeeds.Count());
				pageOptions.Add("TToggle Data Imports"); // allow the user to filter "DataImport" feeds out of the displayed results
				pageOptions.Add("RRefresh Datafeeds");
				pageOptions.Add("XExit");

				char response = getResponse(Enumerable.Range(0, thisPage.Count()).Select(i => $"{(Char)(i + 0x30)}{thisPage[0+i].Name} ({thisPage[0+i].DatafeedType})"), pageOptions);
				switch (response)
				{
					case 'X': { showDatafeeds = false; break; }
					case 'N': { currentPage++; break; };
					case 'P': { currentPage--; break; };
					case 'T': { includeDataImports = !includeDataImports; currentPage = 0; break; }
					case 'R': { core.Report.ClearCache();  allDatafeeds = core.Report.AllDatafeeds.ToArray(); break; }
					case char c when c >= '0' && c <= '9': { ShowDatafeedDetails(thisPage[((int)c) - 0x30]); break; }
				}
			}
		}

		static void ShowDatafeedDetails(IDatafeed datafeed)
		{
			bool showDatafeed = true;
			while (showDatafeed)
			{
				Console.Clear();
				Console.WriteLine("Details for datafeed id: " + datafeed.Id);
				Console.WriteLine();
				Console.WriteLine("Name: " + datafeed.Name);
				Console.WriteLine("Type: " + datafeed.DatafeedType);
				Console.WriteLine();
				Console.WriteLine($"Target level: {datafeed.Level.Name} (from {datafeed.Level.Module.ModuleType} {datafeed.Level.Module.Name})");
				Console.WriteLine($"Key fields: {datafeed.KeyFields.Select(f => $"{f.Name} ({f.FieldType})").Conjoin(", ")}");
				Console.WriteLine($"Mapped fields: {datafeed.MappedFields.Select(f => $"{f.Name} ({f.FieldType})").Conjoin(", ")}");
				Console.WriteLine();
				Console.WriteLine("Next scheduled parent: " + datafeed.NextScheduledParent);
				Console.WriteLine("Last updated: " + datafeed.UpdateInformation.UpdateDate);

				var lastRun = datafeed.LastRun;
				if (lastRun != null)
				{
					Console.WriteLine("Last execution: ");// + JsonConvert.SerializeObject(lastRun));
					Console.WriteLine("  Job Id: " + lastRun.JobId);
					Console.WriteLine("  Manually started: " + lastRun.WasManuallyStarted);
					Console.WriteLine("  Start time: " + lastRun.StartTime);
					Console.WriteLine("  End time: " + lastRun.EndTime);
					Console.WriteLine("  Status: " + lastRun.Status);
					Console.WriteLine("  Source records processed: " + lastRun.SourceRecordsProcessed);
					int totalAffectedRecords =
						lastRun.TargetRecords.Created + lastRun.TargetRecords.Updated + lastRun.TargetRecords.Deleted + lastRun.TargetRecords.Failed +
						lastRun.ChildRecords.Created + lastRun.ChildRecords.Updated + lastRun.ChildRecords.Deleted + lastRun.ChildRecords.Failed +
						lastRun.SubFormRecords.Created + lastRun.SubFormRecords.Updated + lastRun.SubFormRecords.Deleted + lastRun.SubFormRecords.Failed;

					Console.WriteLine("  Output records affected: " + totalAffectedRecords);
					Console.WriteLine($"    Target records => Created: {lastRun.TargetRecords.Created}  Updated: {lastRun.TargetRecords.Updated}  Deleted: {lastRun.TargetRecords.Deleted}  Failed: {lastRun.TargetRecords.Failed}");
					Console.WriteLine($"    Child records => Created: {lastRun.ChildRecords.Created}  Updated: {lastRun.ChildRecords.Updated}  Deleted: {lastRun.ChildRecords.Deleted}  Failed: {lastRun.ChildRecords.Failed}");
					Console.WriteLine($"    Subform records => Created: {lastRun.SubFormRecords.Created}  Updated: {lastRun.SubFormRecords.Updated}  Deleted: {lastRun.SubFormRecords.Deleted}  Failed: {lastRun.SubFormRecords.Failed}");
					Console.WriteLine("  Messages: ");
					
					var messages = lastRun.Messages;
					if (messages == null || messages.Count() == 0)
					{
						Console.WriteLine("\t(none)");
					}
					else 
					{ 
						foreach (var message in messages)
						{
							Console.WriteLine($"\t{message.DatafeedMessageId}: {message.DatafeedMessage} {message.DatafeedMessageParameters}" + (message.Row > -1 ? $" (Row: {message.Row})" : null));
						}
					}
				}

				Console.WriteLine();
				char response = getResponse(new string[] { "EExecute now", "HShow history", "XExit" }, null);
				switch (response)
				{
					case 'X': { showDatafeed = false; break; }
					case 'E': { ExecuteDatafeed(datafeed); break; }
					case 'H': { ShowDatafeedHistory(datafeed);  break; }
				}
			}
		}

		static void ExecuteDatafeed(IDatafeed datafeed)
		{
			Console.WriteLine();
			Console.WriteLine($"This will schedule the datafeed '{datafeed.Name}' for execution on {core.SessionProvider.Instance}.  Are you sure you want to do this?");
			if (Confirm("Yes, let's get started", "No, go back"))
			{
				Console.WriteLine("Do you want to run its dependent reference feeds as well?");
				bool includeReferenceFeeds = Confirm("Run this datafeed and its reference feeds", "Run this datafeed only");
				try
				{
					core.APIFacade.ExecuteDatafeed(datafeed.Guid, includeReferenceFeeds);
					Console.WriteLine();
					Console.WriteLine($"'{datafeed.Name}' has been scheduled to execute on {core.SessionProvider.Instance}.  Check the datafeed's details in a few minutes to see its status.");
				}
				catch(Exception ex)
                {
					Utilities.Log("An error was returned by the server when attempting to schedule this datafeed:", LogLevel.Warning);
					Utilities.Log(ex);
                }
				Console.WriteLine("Hit any key to return to status.");
				Console.ReadKey();
			}
		}

		static bool historyWarningAccepted = false;
		static bool sortHistoryDescending = true;

		static void ShowDatafeedHistory(IDatafeed datafeed)
		{
			Console.Clear();
			if (!historyWarningAccepted)
			{
				Console.WriteLine("Due to a defect in Archer's REST API, datafeed history results may not be retrieved via pages.  This means that the entire history " +
					"of a given datafeed must be retrieved in one HTTP call, which may result in high RAM consumption if the datafeed has a lot of history.  Do you want to proceed?");

				if (Confirm("Yes I know what I'm doing and can live with the consequences", "No, don't do this"))
				{
					historyWarningAccepted = true;
					Console.WriteLine();
				}
				else
				{
					return;
				}
			}

			Console.WriteLine("Retrieving history for datafeed: " + datafeed.Name);
			var history = datafeed.History.ToArray();

			int currentPage = 0;
			bool showHistory = true;
			int detailId = -1;

			while(showHistory)
			{
				var sortedHistory = sortHistoryDescending ? history.OrderByDescending(h => h.StartTime) : history.OrderBy(h => h.StartTime);

				var thisPage = sortedHistory.Skip(currentPage * 10).Take(10).ToArray();
				Console.Clear();
				Console.WriteLine($"Press 0-{thisPage.Length-1} to show execution messages");
				Console.WriteLine("\tDate\tStatus\tCount");

				List<string> pageOptions = GetPageOptions(currentPage, history.Count());
				pageOptions.Add($"SSort {(sortHistoryDescending ? "old" : "new")}est to {(sortHistoryDescending ? "new" : "old")}est");
				pageOptions.Add("XExit");

				Func<int, string> generateDisplayItem = index =>
				{
					var historyItem = thisPage[0 + index];
					// create a line for the basic statistics of this history item
					string result = $"{(Char)(index + 0x30)}\t{historyItem.StartTime}\t{historyItem.Status}\t{historyItem.SourceRecordsProcessed}";
					if (index == detailId) // this one is selected for detail, so append its messages
					{
						result += Environment.NewLine + Environment.NewLine;
						if (historyItem.Messages == null || historyItem.Messages.Count() == 0)
						{
							result += "\t(no messages)";
						}
						else
						{
							result += historyItem.Messages.Select(message => 
							$"\t{message.DatafeedMessageId}: {message.DatafeedMessage} {message.DatafeedMessageParameters}" + (message.Row > -1 ? $" (Row: {message.Row})" : null)
							).Conjoin(Environment.NewLine);
						}
						result += Environment.NewLine;
					}
					return result;
				};

				char response = getResponse(Enumerable.Range(0, thisPage.Count()).Select(i => generateDisplayItem(i)), pageOptions);
				switch (response)
				{
					case 'X': { showHistory = false; break; }
					case 'N': { currentPage++; break; };
					case 'P': { currentPage--; break; };
					case 'S': { sortHistoryDescending = !sortHistoryDescending; currentPage = 0; break; }
					case char c when c >= '0' && c <= '9': { detailId = ((int)c) - 0x30; break; }
				}

			}


		}

	}
}
