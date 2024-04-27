using System;
using System.Text.RegularExpressions;
using Estrelica.Archer.Content;
using Estrelica.Archer.Entity;
using Estrelica.Archer.Metadata.Field;

namespace EstrelicaCoreArcherAPIExample
{
	public class Program
	{
		/// <summary>
		///  This demonstrates the implementation of a simple task (see README.md for requirements) using Estrelica.Core.
		/// </summary>
		public static void Main(string[] args)
		{
			try
			{
				// Step 1: Instantiate Estrelica.Core and authenticate with Archer (using the shared appSettings.json file located upstream and/or secrets.json)
				var core = Estrelica.CoreConfig.Load(w => Console.WriteLine(w.Message), @"..\..\..\..\..\..\Estrelica.Demo.Common\appSettings.json");

				// Step 2: Find the "Section" level of the "Policies" application and the "Section Name" Text field within it
				// (Note that each of the "ByName" calls below will raise an exception if the named entity
				// cannot be found, so we don't need to perform those checks ourselves in code here.)
				var sectionLevel = core.Metadata.ApplicationByName("Policies").Level("Section");
				var nameField = sectionLevel.Fields.ByName<ITextField>("Section Name");

				// Step 3: Create a regex pattern for the substring we want to replace in the misspelled Section names
				// (i.e. "Controll" wherever it appears, aside from "Controlling" and "Controller")
				var regexPattern = new Regex(@"Controll(?!(ing|er))", RegexOptions.Compiled);

				// Step 4: Perform a search "WHERE [Section Name] LIKE '%Controll%'" and fix any names that match the regex pattern
				foreach (var record in sectionLevel.Content(options => options
					.AddDisplayField(nameField) // We only need the name field itself returned in the results
					.AddFilterCondition(nameField.CreateCondition(ValuesOperator.Contains, "Controll")) // where the name field contains 'Controll'
				))
				{
					string originalName = record.Value(nameField);
					if (regexPattern.IsMatch(originalName))
					{
						// Step 5: Put the record in edit mode, modify the name, and send it back to Archer
						var editedRecord = record.ForEdit(); 
						editedRecord.Field(nameField).Value = regexPattern.Replace(originalName, "Control");
						editedRecord.SaveChanges();
					}
				}
			}
			catch(Exception ex)
			{
				Console.WriteLine(ex.Message);
			}
		}
	}
}
