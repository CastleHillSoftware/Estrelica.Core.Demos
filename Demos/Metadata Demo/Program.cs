using System;
using Estrelica;
using Estrelica.Interfaces;
using Estrelica.Archer.Utility;
using System.Collections.Generic;
using System.Linq;
using Estrelica.Utility;
using Estrelica.Archer.Metadata;
using Estrelica.Archer.Metadata.Field;
using Estrelica.Archer.Metadata.Field.Properties;
using Estrelica.Logging;
using Estrelica.Demo;

namespace SampleApp
{
	class Program
	{
		static void Main(string[] args)
		{
			try
			{
				// This application demonstrates some examples for use of the Estrelica Core in communicating with RSA Archer to retrieve
				// and evaluate metadata.  


				// Here we'll use the Estrelica.CoreConfig utility class to handle the authentication process, allowing for all the 
				// authentication details to be stored in an appSettings.json and/or user secrets file.
				// See https://castlehillsoftware.github.io/Estrelica.Core.Demos/articles/manage_configuration.html
				// for more information on managing your Estrelica.Core configuration for this demo.

				var core = CoreConfig.Load(
						w => Logger.Log(w.Message, LogLevel.Warning),
						userSecretsId: "Estrelica.Core.Demo",
						// "appConfigFilename" specifies a JSON app settings file where your configuration is stored.  If not
						// explicitly provided this will default to "appSettings.json" in the current executing directory.
						// The string below will direct it to use the common appSettings.json file found in the Estrelica.Demo.Common project.
						appConfigFilename: @"..\..\..\..\..\Estrelica.Demo.Common\appSettings.json",
						configOverrideKey: null);

				core.Metadata.CacheTimeoutMinutes = 1;

				Utilities.Log($"This application will run some tests to demonstrate various methods of retrieving metadata from the Archer instance {core.SessionProvider.Instance} at {core.SessionProvider.Url}");

				// Some metadata is only accessible via API extensions, which may or may not be available in your environment
				// (see https://castlehillsoftware.github.io/Estrelica.Core.Demos/articles/extensions.html for details).
				// Here we'll check to see if the API extensions are available:

				string extensionsText = null;
				var extensionsAvailable = core.APIFacade.ExtensionsAvailable();
				if (extensionsAvailable == APISource.None)
				{
					Utilities.Log("Since API extensions are unavailable, these tests will be performed strictly against the standard Archer API.  You can enable the extensions and " +
						$"test more methods by installing the CastleHill Extended API at {core.SessionProvider.Url} or by providing the Estrelica Core with a connection string to the " +
						core.SessionProvider.Instance + " instance database.");
					extensionsText = "no extensions available";
				}
				else
				// If extensions ARE available, we want to disable them in order to simulate what happens when the API Facade
				// is only able to talk to Archer's API, so we'll turn off the extensions here explicitly if they were
				// found to be available above:
				{
					// This setting should rarely be changed in practice, but is useful for testing code to make sure it works as expected in non-extension environments.
					core.APIFacade.EnableExtensions = false;
					extensionsText = "extensions disabled";
				}

				Utilities.Log($"This test will confirm the behavior of GetAllSolutions(throwExceptionIfUnavailable: false) with {extensionsText}.  It is expected to return an empty result.", ConsoleColor.Cyan);
				Utilities.Log("");

				// Without any extensions, the APIFacade methods which depend on them will not be available.  These methods
				// each have an optional "throwExceptionIfUnavailable" boolean parameter which lets you decide how the methods
				// should behave in those circumstances.

				// When "throwExceptionIfUnavailable" is false, the methods will simply return default values (e.g. null in the case
				// of single-value results, empty IEnumerables in the case of multi-value results).
				// When true (the default), a descriptive exception will be thrown.

				// GetAllSolutions() is one example of a method that is only availble via these extensions.  The Archer API
				// has no method to retrieve solution information, so GetAllSolutions() can only function if the API extensions
				// which provide that capability are available.  In the absence of extensions, passing (throwExceptionIfUnavailable: false) will
				// cause the method to return a non-null empty result.  Alternately, passing (throwExceptionIfUnavailable: true)
				// will cause an exception to be raised if the Core finds that the extensions are unavailable or disabled.

				// Given all the above, i.e. that we've either identified that extensions are unavailable OR we've explicitly
				// disabled them, passing (throwExceptionIfUnavailable: false) here should return an empty IEnumerable:
				var allSolutions = core.APIFacade.GetAllSolutions(throwExceptionIfUnavailable: false);

				Utilities.Log("");
				Utilities.Log($"'APIFacade.GetAllSolutions(throwExceptionIfUnavailable: false)' with extensions disabled returned {(allSolutions == null ? "(null)" : (allSolutions.Count() + " results"))}"
					, allSolutions != null && allSolutions.Count() == 0); // an empty result (i.e. an IEnumerable with a count of 0) is expected here

				Utilities.Log("");

				Utilities.Log($"This test will confirm the behavior of GetAllSolutions(throwExceptionIfUnavailable: true) with {extensionsText}.", ConsoleColor.Cyan);
				Utilities.Log("It is expected to throw an exception which will be handled below.", ConsoleColor.Red);
				Utilities.Pause();

				// Now we'll try the same method again but with (throwExceptionIfUnavailable: true) to demonstrate how that works.
				// When true (or not supplied -- the parameter is optional and "true" by default) in the scenario where no extensions
				// are available, a descriptive exception will be raised:
				try
				{
					// We expect the following line to throw an exception, demonstrating what happens when extended methods are called
					// when extensions are unavailable or explicitly disabled:

					allSolutions = core.APIFacade.GetAllSolutions(true);

					// should not get here due to the expected exception thrown
					Utilities.Log($"'APIFacade.GetAllSolutions(throwExceptionIfUnavailable: true)' with extensions disabled returned {(allSolutions == null ? "(null)" : (allSolutions.Count() + " results"))}"
						, false); // we don't expect to get here, so the expectation is always false if we do get here for some reason
				}
				catch(Exception ex)
				{
					// we expect this exception, due to the "throwExceptionIfUnavailable = true" parameter above
					Utilities.Log("'APIFacade.GetAllSolutions(throwExceptionIfUnavailable: true)' with extensions disabled threw this exception: " + ex.Message
						, true); 
				}

				// If extensions are in fact available, let's see how the test above works when we turn extensions back on...
				if (extensionsAvailable != APISource.None)
				{
					Utilities.Log("This test will re-enable extensions and call APIFacade.GetAllSolutions(throwExceptionIfUnavailable: true) to demonstrate that extensions are available.", ConsoleColor.Cyan);
					Utilities.Pause();

					core.APIFacade.EnableExtensions = true;
					try
					{
						allSolutions = core.APIFacade.GetAllSolutions(true);
						// We know we have extensions, so this should return a non-null result with a non-zero count
						Utilities.Log($"'APIFacade.GetAllSolutions(throwExceptionIfUnavailable: true)' with {extensionsAvailable} extensions enabled returned {(allSolutions == null ? "(null)" : (allSolutions.Count() + " results"))}",
							allSolutions != null);
					}
					catch (Exception ex)
					{
						// Should never get here, since we've confirmed that the extensions are available
						Utilities.Log($"'APIFacade.GetAllSolutions(throwExceptionIfUnavailable: true)' with {extensionsAvailable} extensions enabled threw this exception: " + ex.Message, false);
					}
					Utilities.Log("");

				}

				// Let's try some methods which we know only depend on the Archer API, so will work regardless of extensions:

				// First we'll try to retrieve all applications in the system (specifically those the are available to the current user):
				var allApplications = core.APIFacade.GetAllApplications();
				Utilities.Log($"'APIFacade.GetAllApplications()' returned {(allApplications == null ? "(null)" : (allApplications.Count() + " results"))}",
					allApplications != null);

				// Next we'll try to retrieve levels and fields for the first application that was returned.

				// Note that the results returned by the APIFacade are simple key-value pair dictionaries, deserialized
				// from the JSON responses returned by the Archer API methods (specifically, the APIFacade returns implementations
				// of IDictionary<string, dynamic> for single-value methods and IEnumerable<IDictionary<string, dynamic>> for
				// multi-value methods).  This means that once a result is retrieved (e.g. a particular application, level, field,
				// etc.), its properties will be accessed from those dictionaries using the appropriate string key for each property
				// (e.g. "Name", "Id", "FieldType", etc.).

				var firstApplication = allApplications?.FirstOrDefault();
				// It's possible that the current user does not have permissions to any applications, so we must first confirm that
				// we actually found something before proceeding:
				if (firstApplication != null)
				{
					int moduleId = firstApplication["Id"];
					var applicationLevels = core.APIFacade.GetLevelsForModule(moduleId);
					Utilities.Log($"'APIFacade.GetLevelsForModule({moduleId})' returned {(applicationLevels == null ? "(null)" : (applicationLevels.Count() + " results"))}",
						applicationLevels != null);

					// Every module must have at least one level, so if we found a non-null application then we should expect
					// to have at least one level here too:
					var level = applicationLevels?.FirstOrDefault();
					if (level != null)
					{
						int levelId = level["Id"];
						var levelFields = core.APIFacade.GetFieldDefinitionsForLevel(levelId);

						string applicationAndLevelName = $" from level '{level["Name"]}' of application '{firstApplication["Name"]}'";

						string fieldNamesAndTypes = levelFields?.Select(f => $"{f["Name"]} ({f["FieldType"]})").Conjoin(", ");

						Utilities.Log($"'APIFacade.GetFieldDefinitionsForLevel({levelId})' returned {(levelFields == null ? "(null)" : (levelFields.Count() + " results (" + fieldNamesAndTypes + ")"))}",
							fieldNamesAndTypes != null);
					}
				}

				Utilities.Log("The previous tests were conducted using the APIFacade class, which calls the Archer API " +
					(extensionsAvailable != APISource.None ? "and API extension " : null) +
					"methods directly.  The following tests will instead call those methods through the Estelica Core's MetadataResolver class, which implements caching to minimize API calls and deserializes " +
					"the API results into strongly-typed classes, eliminating the need to dereference properties by string identifiers (e.g. using strongly-typed properties like field.FieldType instead of " +
					"field[\"FieldType\"])");

				Utilities.Pause();

				// First let's turn extensions back on if they're available
				if (extensionsAvailable != APISource.None)
				{
					core.APIFacade.EnableExtensions = true;
				}


				// Now we'll mimic the same behavior as was performed above through the APIFacade, this time using the
				// helper methods and strongly-typed classes available through the Metadata resolver.  Note how
				// much less code is required to achieve the same results.
				bool expectedSolutions = core.APIFacade.ExtensionsAvailable() != APISource.None;

				var allSolutionsByResolver = core.Metadata.AllSolutions;

				Utilities.Log($"'Metadata.AllSolutions' returned {(allSolutionsByResolver == null ? "(null)" : (allSolutionsByResolver.Count() + " results"))} with extensions {(extensionsAvailable == APISource.None ? "unavailable" : "enabled")}",
					allSolutionsByResolver != null && (expectedSolutions ? (allSolutionsByResolver.Count() > 0) : (allSolutionsByResolver.Count() == 0)));

				var allApplicationsByResolver = core.Metadata.AllApplications;
				Utilities.Log($"'Metadata.AllApplications' returned {(allApplicationsByResolver == null ? "(null)" : (allApplicationsByResolver.Count() + " results"))}",
					allApplicationsByResolver != null && allApplicationsByResolver.Count() > 0);

				var levelByResolver = allApplicationsByResolver?.FirstOrDefault()?.Level(); // gets the first level from the first IArcherApplication object
				string fieldNamesAndTypesByResolver = levelByResolver?.Fields.Select(f => $"{f.Name} ({f.FieldType})").Conjoin(", ");

				string applicationAndLevelNameByResolver = levelByResolver == null ? null : ($" from level '{levelByResolver.Name}' of application '{levelByResolver.Module.Name}'");

				Utilities.Log($"Metadata resolver returned {(levelByResolver?.Fields == null ? "(null)" : (levelByResolver.Fields.Count() + $" results{applicationAndLevelNameByResolver} (" + fieldNamesAndTypesByResolver + ")"))}",
					fieldNamesAndTypesByResolver != null);

				// The Metadata resolver class (as well as all of the other resolvers provided by the core) does four
				// things:
				//   1. It wraps the APIFacade calls
				//   2. It caches results returned by the API methods (to minimize redundant HTTP traffic)
				//   3. It converts the IDictionary<string, dynamic> results returned by the APIFacade into strongly-typed classes
				//      which expose each entity's attributes via appropriate interfaces and strongly-typed properties
				//   4. It provides helper methods to retrieve specific entities by the standard identifiers (Name,
				//      Alias, Guid).
				//   5. It injects lazy-load callbacks for related entities, exposing them via simple properties
				//      (e.g. application.Levels, level.Fields, valuesListField.ValuesList, valuesList.Values, etc.)
				//      resulting in a virtual object graph that can be easily traversed without making multiple
				//      explicit calls to the APIFacade.

				// Here's an example showing how using the Metadata resolver class and the object graph that it returns makes
				// working with Archer metadata very simple.  For example, assume we want to identify the integer field Ids of all 
				// Text fields found in the "Section" level of the "Policies" application.
				//
				// As a baseline, here's how we'd accomplish this using the APIFacade class:

				// First we'll get all applications and find the one named "Policies"
				IEnumerable<IDictionary<string, dynamic>> applications = core.APIFacade.GetAllApplications();
				IDictionary<string, dynamic> policiesApp = applications.First(a => a["Name"] == "Policies");
				// Then we'll get all the levels for that application and find the one named "Section"
				int policiesModuleId = policiesApp["Id"];
				IEnumerable<IDictionary<string, dynamic>> policiesLevels = core.APIFacade.GetLevelsForModule(policiesModuleId);
				IDictionary<string, dynamic> sectionLevel = policiesLevels.First(l => l["Name"] == "Section");
				// Then we'll get all the fields for that level and find those that are Text fields
				int sectionLevelId = sectionLevel["Id"];
				IEnumerable<IDictionary<string, dynamic>> sectionFields = core.APIFacade.GetFieldDefinitionsForLevel(sectionLevelId);
				IEnumerable<IDictionary<string, dynamic>> textFields = sectionFields.Where(f => f["FieldType"] == FieldType.Text);
				// Then we'll capture the Ids of those text fields in a hashtable
				HashSet<int> textFieldIds = new HashSet<int>(textFields.Select(f => (int)f["Id"]));

				// However with the Metadata resolver we can do the same thing with a single line of code:

				HashSet<int> textFieldIdsByResolver = new HashSet<int>(
						core.Metadata.ApplicationByName("Policies") // find the "Policies" application
						.Level("Section") // and its "Section" level
						.Fields.Where<ITextField>() // filter the Fields down to only the Text fields
						.Select(f => f.Id) // and capture their integer Ids
						);

				// Confirm that we got the same Ids via both approaches
				bool resultsMatch = textFieldIds.Matches(textFieldIdsByResolver);

				Utilities.Log($"Retrieved text field Ids ({textFieldIds.Conjoin()}) via APIFacade and text field Ids ({textFieldIdsByResolver.Conjoin()}) via Metadata resolver",
					resultsMatch);

				// Next let's try to load some values lists.  In order to do this via the APIFacade we'll use the APIFacade.GetValuesListDefinition()
				// and APIFacade.GetValuesListValuesForValuesList() methods, both of take a values list Id as a parameter.  These Ids
				// can only be discovered by first encountering a values list field in one of the modules.  Therefore, we'll expand
				// on the above example, searching for ValuesList fields instead of Text fields, and retrieve those Ids via
				// the "RelatedValuesListId" property of each:

				// First we'll get all applications and find the one named "Policies"
				applications = core.APIFacade.GetAllApplications();
				policiesApp = applications.First(a => a["Name"] == "Policies");
				// Then we'll get all the levels for that application and find the one named "Section"
				policiesModuleId = policiesApp["Id"];
				policiesLevels = core.APIFacade.GetLevelsForModule(policiesModuleId);
				sectionLevel = policiesLevels.First(l => l["Name"] == "Section");
				// Then we'll get all the fields for that level and find those that are Text fields
				sectionLevelId = sectionLevel["Id"];
				sectionFields = core.APIFacade.GetFieldDefinitionsForLevel(sectionLevelId);
				IEnumerable<IDictionary<string, dynamic>> vlFields = sectionFields.Where(f => f["FieldType"] == FieldType.ValuesList);
				// Then we'll iterate through the RelatedValuesListIds of those VL fields
				foreach (int valuesListId in vlFields.Select(f => (int)f["RelatedValuesListId"]))
				{
					// and call the API to get the specific details of each Values List:
					IDictionary<string, dynamic> valuesList = core.APIFacade.GetValuesListDefinition(valuesListId);
					IEnumerable<IDictionary<string, dynamic>> valuesListValues = core.APIFacade.GetValuesListValuesForValuesList(valuesListId);
					Utilities.Log($"Got values list '{valuesList?["Name"]}' via APIFacade having values '{valuesListValues?.Select(vlv => (string)vlv["Name"]).Conjoin("', '")}'",
						valuesList != null);
				}

				// Again, the same task can be performed via the Metadata resolver with much less code:

				foreach(IArcherValuesList vl in core.Metadata
					.ApplicationByName("Policies")
					.Level("Section")
					.Fields.Where<IValuesListField>()
					.Select(f => f.ValuesList))
				{
					Utilities.Log($"Got values list '{vl.Name}' via Metadata resolver having values '{vl.Values.Select(vlv => vlv.Name).Conjoin("', '")}'", vl != null);
				}

				// Another method that's missing from Archer's API is a "get all subforms" method.  It provides methods to "get all applications" and
				// "get all questionnaires", but subforms may only be queried individually by subform id via Archer's API.  Furthermore, the Archer API
				// provides no way to determine the Id of a subforms, other than when it is encountered on a subform field (via the field's 
				// "RelatedSubformId" property.

				// To bridge this gap, the apiFacade provides a "GetAllSubformIds()" method which, when extensions are available, will return
				// all subform Ids in the system.  However, this method returns null in the absence of extensions (and throwExceptionIfUnavailable = false).

				// To remedy this, the MetadataResolver attempts to identify all subform Ids by inspecting each subform field that it sees
				// for its "RelatedSubformId", and caches those Ids internally (i.e. any fields returned via its various "Field" methods such
				// as "FieldById()", "FieldsByLevel()", "FieldsByModule()", etc.).  Thereafter, whenever the mdr's AllEncounteredSubforms() method is called,
				// it will use this list instead of the APIFacade's GetAllSubFormIds(false) method, should that method return null,
				// then call Archer's "get subform by id" method on each of them.

				// (Note that this MDR method is named "AllEncounteredSubforms()" rather than simply "AllSubforms()", just as a reminder of how it works
				// in the non-extension scenario.  When extensions are present this method can be treated as though it were an "AllSubforms()" method.)

				// This means that, if extensions are present, the mdr's AllEncounteredSubforms() method will work right out of the gate to retrieve
				// all subforms (via the APIFacade.GetAllSubFormIds() method) in the system.  However, in the absence of extensions, it can only
				// return those subforms that it learns of via a subform field, so it is a good idea to delay any activity that depends
				// on AllSubforms() until you have iterated all the subform fields in the system (or at least those that are relevant to the
				// subforms you want returned).

				// Furthermore, the mdr's AllModules() method returns the union of AllApplications(), AllQuestionnaires() and AllEncounteredSubforms(), in that
				// order, yielding each set as it goes.  Therefore, under the assumption that the caller will have called LevelsByModule()/FieldsByLevel() 
				// and iterated through all the subform fields in the first two sets by the time the AllEncounteredSubforms() set is called for, all subform Ids will
				// have been identified and the subforms will be returned correctly, regardless of extension availability.


				Utilities.Log("The next test will demonstrate how the MetadataResolver identifies subforms in the absence of extensions, by monitoring all the subform fields found in Applications and Questionnaires.", ConsoleColor.Cyan);
				Utilities.Pause();

				// First we'll clear anything that's been cached so far.  This will cause all subsequent calls for resolvers or APIFacade to
				// return freshly-created instances with empty caches.
				// Note that "ClearCache()" would typically never be called in a production app.  We're only doing it here to ensure that all the 
				// extensions/no-extensions scenarios below don't accidentally return cached results that violate our expectations.
				core.ClearCache();

				// Then we'll turn off extensions...
				core.APIFacade.EnableExtensions = false;

				int subformCount = 0;
				HashSet<int> expectedSubformIds = new HashSet<int>();
				foreach(IArcherModule module in core.Metadata.AllModules())
				{
					ModuleType moduleType = module.ModuleType;
					Utilities.Log($"{moduleType}: {module.Name}");
					foreach (IArcherLevel level in module.Levels)
					{
						Utilities.Log($"  Level: {level.Name}");
						foreach(IArcherField field in level.Fields)
						{
							Utilities.Log($"    Field: {field.Name} ({field.FieldType})");
							ISubformField subformField = field as ISubformField;
							if (subformField != null)
							{
								// Just as the mdr does internally, we'll capture any subform Ids that are encountered while examining subform fields,
								// so we can check to see that the mdr returned all of them as expected...
								Utilities.Log($"Encountered subform id {subformField.RelatedSubformId}", ConsoleColor.Cyan);
								expectedSubformIds.Add(subformField.RelatedSubformId);
							}
						}
					}
					if (module.ModuleType == ModuleType.Subform)
					{
						subformCount++;
					}
				}
				Utilities.Log("");
				Utilities.Log($"Identified {subformCount} subforms, expected to find {expectedSubformIds.Count} of them.", subformCount == expectedSubformIds.Count);
				Utilities.Log("");

				Utilities.Log("This test will demonstrate how subforms CANNOT be identified in the absence of extensions unless we also iterate through all the fields in Applications and Questionnaires", ConsoleColor.Cyan);
				Utilities.Pause();

				core.ClearCache();
				core.APIFacade.EnableExtensions = false; // need to turn this off again after clearing the cache for our test, since the new APIFacade will default to EnableExtensions = true...
				subformCount = 0;

				foreach (IArcherModule module in core.Metadata.AllModules())
				{
					Utilities.Log($"{module.ModuleType}: {module.Name}");
					foreach (IArcherLevel level in module.Levels)
					{
						Utilities.Log($"  Level: {level.Name}");
						// Here we will NOT ask the mdr for any fields, so it will not be able to parse them for subform Ids, and the
						// AllModules() result set will therefore NOT be expected to return any subforms at all.
						//foreach (ArcherMetadata field in level.Fields)
						//{
						//	log($"    Field: {field["Name"]} ({field["FieldType"]})");
						//}
					}
					if (module.ModuleType == ModuleType.Subform)
					{
						subformCount++;
					}
				}
				Utilities.Log("");
				Utilities.Log($"Identified {subformCount} subforms without extensions due to no field iterations, even though we know {core.SessionProvider.Instance} has at least {expectedSubformIds.Count} of them."
					, subformCount == 0); // expected 0
				Utilities.Log("");

				if (extensionsAvailable != APISource.None)
				{
					subformCount = 0;
					Utilities.Log("This test will demonstrate how subforms can be identified without first iterating through subform fields when extensions are available.", ConsoleColor.Cyan);
					Utilities.Log("This test will return at least as many subforms as were discovered above by iterating the fields, but may return more since there could be sub-forms in the system which are not linked via subform field to any available applications or questionnaires.", ConsoleColor.Cyan);
					Utilities.Pause();
					core.ClearCache();
					core.APIFacade.EnableExtensions = true; // this will be true by default at this point anyway since it's a fresh APIFacade, but setting it explicitly here to clarify intent
					foreach (IArcherSubForm module in core.Metadata.AllEncounteredSubforms()) // See note above, this is effectively "AllSubforms()" when extensions are available
					{
						Utilities.Log($"{module.ModuleType}: {module.Name}");
						subformCount++;
					}
					Utilities.Log("");
					Utilities.Log($"Identified {subformCount} subforms via {extensionsAvailable} without first iterating the fields, expected to find at least {expectedSubformIds.Count} of them."
						, subformCount >= expectedSubformIds.Count); // expected to find at least as many as were discovered by iterating the fields, perhaps more
					Utilities.Log("");
				}

				Utilities.Pause();

				Utilities.Log("Archer field objects returned by IMetadataResolver are strongly-typed, each having a FieldType property which returns a FieldType enumeration " +
					"indicating its type (e.g. FieldType.Text, FieldType.Date, FieldType.CrossReference), but each field object also implements a specific interface that exposes " +
					"the specific properties of its type (e.g. ITextField, IDateField, ICrossReferenceField, etc.), all of which inherit from the base IArcherField interface.", ConsoleColor.Cyan);

				Utilities.Log("Furthermore, cross-cutting concerns, or properties that apply across multiple fields of different types, are also expressed via their own interfaces, " +
					"(e.g. IIsRequiredProperty, IIsKeyProperty, IIsCalculatedProperty, etc.)", ConsoleColor.Cyan);

				Utilities.Log("Combined with the IEnumerable<IArcherField>.Where<T>() extension method, this makes it very simple to identify common sets of fields and their common properties.", ConsoleColor.Cyan);

				Utilities.Log("We'll demonstrated each of these techniques (selecting fields by type-based interface and by property-based interface) to show how these two " +
					"approaches can be used to identify fields by type or shared properties, and log specific aspects of the fields that are specific to those interfaces.", ConsoleColor.Cyan);

				Utilities.Pause();

				foreach(var application in core.Metadata.AllApplications.Take(5))
                {
					foreach(var level in application.Levels)
                    {
						// Identify all the Cross Reference fields in this level:
						var crossRefFields = level.Fields.Where<ICrossReferenceField>();
						Utilities.Log($"Application '{application.Name}', Level '{level.Name}' contains {crossRefFields.Count()} cross-reference fields");

						foreach(var crossRefField in crossRefFields)
                        {
							Utilities.Log($"  '{crossRefField.Name}' ({crossRefField.FieldType}) targets level(s): {crossRefField.RelatedLevels.Select(l => l.Name).Conjoin(", ")}");
						}
						// Note that we could also select out the Cross Reference fields by using
						//	var crossRefFields = level.Fields.Where(f => f.FieldType == FieldType.CrossReference);
						// However, this would return an IEnumerable<IArcherField> result, while the .Where<ICrossReferenceField>()
						// technique actually returns the results as the requested type (i.e. IEnumerable<ICrossReferenceField>).
						// This may be important if you need to query the results for something that's only available via the 
						// type-specific interface, e.g. ICrossReferenceField.RelatedLevels in the previous example.  That property
						// is specific to Cross Reference fields, so is not available via the base IArcherField interface.

						// Here we'll identify all the calc fields in the level.  The fact that a given field supports
						// the IIsCalculatedProperty interface only means that the field type is *capable* of being calculated.
						// It doesn't necessarily mean that this particular field instance actually *is* calculated.  To
						// determine that, we have to check to see if the IsCalculated boolean property on that interface is true.

						var calculatedFields = level.Fields.Where<IIsCalculatedProperty>(f => f.IsCalculated == true);
						Utilities.Log($"Application '{application.Name}', Level '{level.Name}' contains {calculatedFields.Count()} calculated fields");

						// If extensions are available, we can even show the Formula for each of the IIsCalculatedProperty fields
						// that were returned:
						if (core.APIFacade.ExtensionsAvailable() != APISource.None && calculatedFields.Count() > 0)
                        {
							Utilities.Log("And here are their calc formulas:");
							foreach(var calcField in calculatedFields)
                            {
								Utilities.Log($"  '{calcField.Name}' ({calcField.FieldType}): {calcField.Formula}");
                            }
                        }
						Utilities.Log("");
					}
				}
			}
			catch (Exception ex)
			{
				Utilities.Log(ex.ToString(), false);
			}
			finally
			{
				Utilities.LogResults();
			}
		}
	}
}
