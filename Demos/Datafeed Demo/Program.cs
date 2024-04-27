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
		/// This application demonstrates how to use Estrelica.Core to evaluate, monitor and execute datafeeds.
		/// 
		/// Archer's API features several methods to retrieve information about the execution history and errors of a particular datafeed, 
		/// but provides limited information about the datafeeds themselves (Name, Guid and Active status).  This is enough information to perform
		/// general interaction with datafeeds to retrieve history, "Last Run" status, and even send them to the job queue for execution.
		/// 
		/// If API Extensions are available, more details about each datafeed is available, including the targeted Level, key fields, field
		/// mappings, next scheduled parent datafeed, next scheduled child datafeeds, and last update information.
		/// </summary>
		/// <param name="args"></param>

		static void Main(string[] args)
		{
			try
			{
				// Allow a particular instance config override to be selected via the command line.  If not provided, default to the base settings.
				string overrideKey = args.Length > 0 ? args[0] : null;

				// Note: If you have extensions available, it's generally a good idea to test your code with and without them just to make
				// sure it will behave correctly in other environments where extensions are not available.  This app demonstrates one way to do that,
				// using an optional command-line parameter expressing an override key (you can set it in your project properties).

				// If the override key is not null and ends with "_NE" ("No Extensions") we'll switch off the extensions (via the
				// core.APIFacade.EnableExtensions setting to follow), causing Estrelica.Core to only use those features available via the standard
				// Archer APIs.  (If you haven't set up any override keys in your appSettings.json or user secrets file, you can simply pass the
				// string "_NE" alone to disable extensions in your default Archer environment.)

				bool disableExtensions = overrideKey?.EndsWith("_NE") ?? false;
				if (disableExtensions)
				{
					overrideKey = overrideKey.RemoveEnd("_NE").NullIfEmpty();
				}

				// Instantiate Estrelica.Core and authenticate with Archer...
				core = CoreConfig.Load(
					w => Utilities.Log(w.Message, LogLevel.Warning),
					// See notes in Program.cs from Content Demo for details on what these settings mean
					appConfigFilename: @"..\..\..\..\..\Estrelica.Demo.Common\appSettings.json",
					configOverrideKey: overrideKey);

				// See notes above about what's going on here:
				core.APIFacade.EnableExtensions = !disableExtensions;

				extendedAPIAvailable = core.APIFacade.ExtensionsAvailable() != Archer.Utility.APISource.None;
				if (!extendedAPIAvailable)
				{
					Console.WriteLine();
					Utilities.Log("API Extensions are unavailable or disabled in this Archer instance.  " +
						"Therefore this application will not be able to display full details about datafeeds (e.g. field mappings, target level, etc.) " +
						"or any information about data imports.", LogLevel.Warning);
					Console.WriteLine();
					Console.WriteLine("Hit any key to continue without API Extensions");
					Console.ReadKey();
				}

				// Show the available datafeeds
				ShowDatafeeds();
			}
			catch (Exception ex)
			{
				Utilities.Log(ex.ToString(), LogLevel.Error);
			}
		}

		static Estrelica.Core core = null;
		static bool extendedAPIAvailable = false;

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

				List<string> pageOptions = Utilities.GetPageOptions(currentPage, datafeeds.Count());
				if (extendedAPIAvailable)
				{
					// Data Imports are not included in the results returned by Archer's own API, but are available from the Extended API.
					// Therefore we'll just skip this option if we're only loading datafeeds from Archer...

					pageOptions.Add("TToggle Data Imports"); // allow the user to filter "DataImport" feeds out of the displayed results
				}
				pageOptions.Add("RRefresh Datafeeds");
				pageOptions.Add("XExit");

				char response = Utilities.getResponse(Enumerable.Range(0, thisPage.Count()).Select(i => $"{(Char)(i + 0x30)}{thisPage[0+i].Name} ({thisPage[0+i].DatafeedType})"), pageOptions);
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
				Console.WriteLine("Details for datafeed id: " + datafeed.Guid);
				Console.WriteLine();
				Console.WriteLine("Name: " + datafeed.Name);
				Console.WriteLine("Type: " + datafeed.DatafeedType);
				Console.WriteLine("Status: " + datafeed.Status);
				Console.WriteLine();
				if (extendedAPIAvailable)
				{
					// This information is only available if the datafeeds were retrieved from the Extended API.  Archer only returns the 
					// basic info above (Name, Guid, and Status) for standard service datafeeds, and nothing about other datafeed types (i.e.
					// DatafeedType.DataImport or DatafeedType.Firehose).  Therefore we'll only display this information if we've determined that
					// the Extended API is available:
					Console.WriteLine($"Target level: '{datafeed.Level.Name}' (from {datafeed.Level.Module.ModuleType} '{datafeed.Level.Module.Name}')");
					Console.WriteLine($"Key fields: {datafeed.KeyFields.Select(f => $"{f.Name} ({f.FieldType})").Conjoin(", ")}");
					Console.WriteLine($"Mapped fields: {datafeed.MappedFields.Select(f => $"{f.Name} ({f.FieldType})").Conjoin(", ")}");
					Console.WriteLine();
					Console.WriteLine("Next scheduled parent: " + (datafeed.NextScheduledParent?.Name ?? "(none)"));
					Console.WriteLine("Next scheduled child(ren): " + (datafeed.NextScheduledChildren.Select(cf => cf.Name).Conjoin() ?? "(none)"));
					Console.WriteLine("Last updated: " + datafeed.UpdateInformation.UpdateDate);
				}

				var lastRun = datafeed.LastRun;
				if (lastRun != null)
				{
					Console.WriteLine("Last execution: ");
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
				char response = Utilities.getResponse(new string[] { "EExecute now", "RRefresh Execution Status", "HShow history", "XExit" }, null);

				switch (response)
				{
					case 'X': { showDatafeed = false; break; }
					case 'R': { 
							// After executing a datafeed, its LastRun will progress through various stages as it executes.
							// Calling .Refresh() on the datafeed will discard whatever LastRun (and History) might have been loaded
							// so that it will be re-fetched via another API call on the next attempt to access it above:
							datafeed.Refresh(); break; 
						}
					case 'E': { ExecuteDatafeed(datafeed); break; }
					case 'H': { ShowDatafeedHistory(datafeed);  break; }
				}
			}
		}

		static void ExecuteDatafeed(IDatafeed datafeed)
		{
			Console.WriteLine();
			Console.WriteLine($"This will schedule the datafeed '{datafeed.Name}' for execution on {core.SessionProvider.Instance}.  Are you sure you want to do this?");
			if (Utilities.Confirm("Yes, let's get started", "No, go back"))
			{
				bool includeReferenceFeeds = false;
				if (datafeed.NextScheduledChildren.Count() > 0)
				{
					Console.WriteLine();
					Console.WriteLine("Do you want to run its dependent reference feeds as well?");
					includeReferenceFeeds = Utilities.Confirm("Run this datafeed and its reference feeds", "Run this datafeed only");
				}
				Console.WriteLine();
				try
				{
					datafeed.Execute(includeReferenceFeeds);
					Console.WriteLine($"'{datafeed.Name}' has been scheduled to execute on {core.SessionProvider.Instance}.  " +
						"Check the datafeed's details in a few minutes to see its 'Last Execution' status.  You may want to use the 'Refresh Execution Status' option on the previous screen " +
						"periodically to see the status changes that occur during this execution.");
				}
				catch(Exception ex)
                {
					Utilities.Log("An error was returned by the server when attempting to schedule this datafeed:", LogLevel.Warning);
					Utilities.Log(ex);
                }
				Console.WriteLine();
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

				if (Utilities.Confirm("Yes I know what I'm doing and can live with the consequences", "No, don't do this"))
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

				List<string> pageOptions = Utilities.GetPageOptions(currentPage, history.Count());
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

				char response = Utilities.getResponse(Enumerable.Range(0, thisPage.Count()).Select(i => generateDisplayItem(i)), pageOptions);
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
