using Estrelica.Interfaces;
using Estrelica.Utility;
using Estrelica.Archer.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using Estrelica.Archer.Metadata;
using Newtonsoft.Json;
using Estrelica.Archer.Metadata.Field;

namespace Estrelica.Demo.ReportExplorer
{
	class Program
	{
		/// <summary>
		/// This application demonstrates how to use the Estrelica Core to view and evaluate Reports and retrieve Report content.
		/// </summary>
		/// <param name="args"></param>

		static void Main(string[] args)
		{
			try
			{
				// Allow a particular instance config override to be selected via the command line.  If not provided, default to the base settings.
				string overrideKey = args.Length > 0 ? args[0] : null;

				// Instantiate Estrelica.Core and authenticate with Archer...
				core = CoreConfig.Load(w => Utilities.Log(w.Message, LogLevel.Warning),
					// See notes in Program.cs from Content Demo for details on what these settings mean
					appConfigFilename: @"..\..\..\..\..\Estrelica.Demo.Common\appSettings.json",
					configOverrideKey: overrideKey);

				// Archer's API only provides basic information about standard Content reports (Name, Description, Guid and Module/Questionnaire).
				// CastleHill Software's API Extensions make more information available about reports, as well as the capability of
				// working with Statistics reports.  We'll use the extendedAPIAvailable variable later to decide what we are able
				// to show later in the code.
				extendedAPIAvailable = core.APIFacade.ExtensionsAvailable() != Archer.Utility.APISource.None;

				Console.WriteLine($"Fetching applications and questionnaires with reports from instance {core.SessionProvider.Instance}...");

				// Show the available Applications and Questionnaires (reports are not supported for Subforms)
				ShowApplicationsAndQuestionnairesWithReports();
			}
			catch (Exception ex)
			{
				Utilities.Log(ex.ToString(), LogLevel.Error);
			}
		}

		static Estrelica.Core core = null;
		static bool extendedAPIAvailable = false;

		static void ShowApplicationsAndQuestionnairesWithReports() => Utilities.ShowPages($"Showing {{0}} of {{1}} applications/questionnaires from instance '{core.SessionProvider.Instance}'",
				core.Metadata.AllModules(forceSubformDiscovery: false).Where(a => a.HasReports).ToArray(), module => module.Name,
				module => ShowReportsForModule(module));

		static void ShowReportsForModule(IArcherModule module) => Utilities.ShowPages($"Showing {{0}} of {{1}} reports from {module.ModuleType} '{module.Name}'",
				module.Reports, report => $"{report.Name} ({(report.IsStatisticsReport ? "Statistics" : "Content")})", report => ShowReport(report));

		static void ShowReport(IArcherReport report)
		{
			bool showReport = true;
			while (showReport)
			{
				Console.Clear();
				Console.WriteLine("Details for report id: " + report.Guid);
				Console.WriteLine();
				Console.WriteLine("Name: " + report.Name);
				Console.WriteLine($"{report.Module.ModuleType}: " + report.Module.Name);
				Console.WriteLine("Type: " + (report.IsStatisticsReport ? "Statistics" : "Content"));
				Console.WriteLine("Description: " + (report.Description.NullIfEmpty() ?? "(no description available)"));
				Console.WriteLine();
				char response = Utilities.getResponse(new string[] { "RRun this report", 
					// Fields are not currently available for Statistics reports, so we'll skip that option for them
					report.IsStatisticsReport ? null : "FShow fields", "SShow Search XML", 
					extendedAPIAvailable ? "DShow Details" : null, "XExit" }, null);

				switch (response)
				{
					case 'X': { showReport = false; break; }
					// Note that most properties about reports (basically everything above) are available on their common IReport interface, but 
					// since Content reports and Statistics reports return two different types of result data, their type-specific .Content() methods
					// are implemented on their respective sub-interfaces, IContentReport and IStatisticsReport, so we need to cast them here to
					// make those calls:
					case 'R': { if (report is IContentReport cp) { RunReport(cp); } else if (report is IStatisticsReport sp) { RunReport(sp); } break; }
					case 'F': { if (report is IContentReport cp) { ShowReportFields(cp); } break; }
					case 'S': { ShowSearchXml(report); break; }
					case 'D': { ShowReportExtendedDetails(report); break; }
				}
			}
		}

		private static void WaitForKey()
		{
			Console.WriteLine();
			Console.WriteLine("Hit any key to return");
			Console.ReadKey();
		}

		private static Random rnd = new Random();
		private static void RunReport(IContentReport report)
		{
			Console.Clear();
			Console.WriteLine($"Executing report '{report.Name}'...");

			// report.DisplayFields returns an IEnumerable<IDisplayField> representing a hierarchy of all the report's display fields
			// and (nested beneath some of them) any contained display fields on the report.  We only care about the top-level (non-
			// contained) fields for this example, so we'll capture those here.
			var displayFields = report.DisplayFields.Select(df => df.Field).ToArray();

			// Each content page returned by Archer will indicate the total number of records expected to be returned.  However, we can't
			// identify this until we actually start processing those pages.  The report.Content() method allows us to specify
			// a callback method that will let us know when Estrelica.Core identifies that total and what that value is.
			// We'll use this later to display progress and to determine whether we've reached the end of the search.
			int expectedRecordCount = 0;
			var records = report.Content(options => options
				.RecordCountCallback((rct, count) => { if (rct == RecordCountType.Expected) { expectedRecordCount = count; }})
			);

			// Make sure we have at least one record before proceeding.  This call will cause the first page to be retrieved, and
			// if any records exist on it, we'll know we can continue.  In processing that page, Estrelica.Core will also invoke
			// the callback established above.
			bool showRecords = records.FirstOrDefault() != null;

			if (!showRecords)
			{
				Console.Clear();
				Console.WriteLine("The report returned no records");
				WaitForKey();
			}
			else
			{
				int recordsPerPage = 5;
				int currentPage = 0;
				while (showRecords)
				{
					Console.Clear();
					int recordsOnPage = 0;

					foreach (var record in records.Skip(currentPage * recordsPerPage).Take(recordsPerPage))
					{
						recordsOnPage++;
						Console.WriteLine($"Content Id: {record.Id}");

						// Here we'll identify ten random fields and show what's in each of them for this record.  We'll limit this to the simple field types
						// (no History Log, Scheduler, reference fields, etc.) so the code doesn't get too overwhelming.

						var fields = displayFields.Where(ft => ft.FieldType == FieldType.FirstPublishedDate || ft.FieldType == FieldType.LastUpdatedDate ||
							ft.FieldType == FieldType.Date || ft.FieldType == FieldType.Text || ft.FieldType == FieldType.ValuesList ||
							ft.FieldType == FieldType.UsersGroupsList || ft.FieldType == FieldType.Numeric || ft.FieldType == FieldType.Attachment ||
							ft.FieldType == FieldType.Image || ft.FieldType == FieldType.ExternalLinks)
							.OrderBy(f => rnd.Next()).Take(10).ToArray();

						if (fields.Count() == 0)
						{
							Console.WriteLine("Unable to identify any candidate fields for this record");
						}
						else
						{
							foreach (var field in fields)
							{
								Console.Write($"{field.Name} ({field.FieldType}): ");
								switch (field.FieldType)
								{
									case FieldType.Text: { Console.WriteLine(record.Value((ITextField)field) ?? "(no value)"); break; }
									case FieldType.Date:
									case FieldType.FirstPublishedDate:
									case FieldType.LastUpdatedDate: { Console.WriteLine(record.Value((IBaseDateField)field)?.ToString() ?? "(no value)"); break; }
									case FieldType.ValuesList: { Console.WriteLine(record.Value((IValuesListField)field).Values.Select(v => v.Name)?.Conjoin() ?? "(no values selected)"); break; }
									case FieldType.UsersGroupsList:
										{
											var userGroupSelection = record.Value((IUserGroupListField)field);
											Console.Write($"Users: {(userGroupSelection.Users.Select(u => u.UserName).Conjoin() ?? "(no users selected)")}  ");
											Console.WriteLine($"Groups: {(userGroupSelection.Groups.Select(g => g.Name).Conjoin() ?? "(no groups selected)")}");
											break;
										}
									case FieldType.Numeric: { Console.WriteLine(record.Value((INumericField)field)?.ToString() ?? "(no value)"); break; }
									case FieldType.Attachment:
									case FieldType.Image: { Console.WriteLine(record.Value((IDocumentField)field).Select(f => f.Filename).Conjoin() ?? "(no documents)"); break; }
									case FieldType.ExternalLinks: { Console.WriteLine(record.Value((IExternalLinksField)field).Select(link => $"{link.Name} ({link.Url})").Conjoin() ?? "(no links)"); break; }
									default: Console.WriteLine(); break;
								}
							}
						}
						Console.WriteLine();
					}

					int firstRecordIndex = currentPage * recordsPerPage;
					Console.WriteLine($"Showing records {firstRecordIndex + 1} to {firstRecordIndex + recordsOnPage} of {expectedRecordCount} total records");
					Console.WriteLine();

					var options = new string[] { currentPage > 0 ? "PPrevious Page" : null,
						firstRecordIndex + recordsOnPage < expectedRecordCount ? "NNext Page" : null,
						"XExit"};

					char response = Utilities.getResponse(options, null);
					switch (response)
					{
						case 'P': { currentPage--; break; }
						case 'N': { currentPage++; break; }
						case 'X': { showRecords = false; break; }
					}
				}
			}
		}

		private static void RunReport(IStatisticsReport report)
		{
			// Note: The current experimental implementation of IStatisticsReport.Content() simply returns XML strings from Archer's API
			// ExecuteStatisticSearchByReport method.  In a future Estrelica.Core release this will be changed to return a complex object
			// result (similar to IArcherContentAccess as demonstrated by the IContentReport.Content() method above) allowing simpler LINQ
			// evaluation of the statistical results.
			// If you create code now that depends on the XML representation, after that change is made you will still be able to access the XML
			// directly by changing your code to call core.APIFacade.ExecuteStatisticsReport() and passing the report's Id or Guid.
			Console.Clear();
			bool gotResults = false;
			foreach(string result in report.Content())
			{
				// The empty xml below indicates the final page of the search, signalling that all results have been returned
				if (result != "<?xml version=\"1.0\" encoding=\"utf-16\"?><Groups count=\"0\" />")
				{
					Console.WriteLine(result);
					gotResults = true;
				}
			}

			if (!gotResults)
			{
				Console.WriteLine("The report returned no statistics");
			}

			WaitForKey();
		}

		static void ShowReportFields(IContentReport report)
		{
			Console.Clear();
			Console.WriteLine($"Showing display fields for report '{report.Name}'");
			Console.WriteLine();

			var fields = report.DisplayFields?.ToArray();
			if (fields == null || fields.Count() == 0)
			{
				Console.WriteLine("No display fields returned");
				WaitForKey();
			}
			else
			{
				Utilities.ShowPages($"Display fields from report '{report.Name}':", fields, field => $"{field.Field.Name} ({field.Field.FieldType})", null);
			}
		}

		static void ShowSearchXml(IArcherReport report)
		{
			Console.Clear();
			var searchOptionsXml = report.SearchOptions;
			if (searchOptionsXml?.StartsWith("<") ?? false)
			{
				Console.WriteLine(searchOptionsXml);
			}
			else
			{
				if (!searchOptionsXml.IsNullOrEmpty())
				{
					Console.WriteLine(searchOptionsXml);
					Console.WriteLine();
				}
				if (report.IsStatisticsReport && !extendedAPIAvailable)
				{
					Console.WriteLine("Search options are only available for Statistics reports via the CastleHill Extended API");
				}
			}
			WaitForKey();
		}

		static void ShowReportExtendedDetails(IArcherReport report)
		{
			Console.Clear();
			if (report.Details == null)
			{
				Console.WriteLine("Extended details are only available for reports via the CastleHill Extended API");
			}
			else
			{
				Console.WriteLine(JsonConvert.SerializeObject(report.Details));
			}
			WaitForKey();
		}

	}
}
