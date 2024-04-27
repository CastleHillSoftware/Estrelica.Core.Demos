using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Estrelica;
using Estrelica.Utility;
using Estrelica.Interfaces;
using Estrelica.Archer.Content;
using Estrelica.Archer.Entity;
using Estrelica.Archer.Metadata;
using Estrelica.Archer.Metadata.Field;
using Estrelica.Archer.AccessControl;
using Estrelica.Demo;
using System.IO;
using Newtonsoft.Json;
using Estrelica.Archer.Metadata.Field.Properties;

namespace ContentDemo
{
    class Program
    {
        private static Estrelica.Core core = null;

        static void Main(string[] args)
        {
            try
            {
                // This application demonstrates some examples for use of the Estrelica Core in communicating with RSA Archer to retrieve
                // and manipulate content.

                // Note that if you have enabled "Common Language Runtime Exceptions" in your Exception Settings, your debugger *will* stop
                // on insignificant exceptions that are handled internally by Estrelica.Core (e.g. type conversion errors, HTTP timeouts,
                // server disconnections, key duplications, etc.).  Don't be alarmed by these -- you can simply hit F5 to continue, or
                // better still check the box at Tools -> Options -> Debugging -> "Enable Just My Code".  This will cause the debugger to only
                // stop on meaningful exceptions that occur in this project (including any unhandled or thrown exceptions surfaced by Estrelica.Core).

                Utilities.Log(@"This demo shows how to perform various Archer content operations via Estrelica.Core.  The UI output is not significant, and you are encouraged to instead evaluate the code, read the comments to understand how Estrelica.Core works, set various breakpoints, modify the code to test different behavior, etc.");
                Utilities.Log(@"In order to function in all environments, this demo depends on three of Archer's core applications (Policies, Control Standards and Applications) and may exhibit unexpected behavior if they have been modified (or removed).  If so, feel free to change the code to reference other applications/fields/etc. that are available in your test environment.");
                Utilities.Pause();

                // Instantiate Estrelica.Core and authenticate with Archer...
                core = CoreConfig.Load(
                    w => Utilities.Log(w.Message, LogLevel.Warning),

                    // The configuration CoreConfig will use to instantiate the Core is defined via JSON files.

                    // "appConfigFilename" specifies a JSON app settings file where your configuration is stored.  If not
                    // explicitly provided this will default to "appSettings.json" in the current executing directory.
                    // The string below will direct it to use the common appSettings.json file found in the Estrelica.Demo.Common project.

                    // This requires that you modify the file at
                    //		..\..\..\..\..\Estrelica.Demo.Common\appSettings.json (i.e. in the Estrelica.Demo.Common project)
                    // and/or a local user secrets file at
                    //		%appdata%\Microsoft\UserSecrets\Estrelica.Core.Demo\secrets.json
                    // with your CastleHill Software authentication key and your Archer instance details and credentials.

                    // The user account that you specify for this configuration must have, at minimum, read access to the "Policies" and
                    // "Control Standards" applications in order to demonstrate the content read capabilities, and full CRUD permission
                    // to the "Applications" application in order to demonstrate the create, update and delete capabilities.

                    // See https://castlehillsoftware.github.io/Estrelica.Core.Demos/articles/manage_configuration.html
                    // for more information on managing your Estrelica.Core configuration, and particularly about the use of user secrets.

                    appConfigFilename: @"..\..\..\..\..\Estrelica.Demo.Common\appSettings.json",

                    // "userSecretsId" specifies the (optional) Id of a user secrets JSON file on the local filesystem containing values
                    // which should override the corresponding values in the app settings file.

                    // Note: As of Estrelica.Configuration 1.0.21, the userSecretsId parameter no longer needs to be explicitly passed if
                    // you have defined a user secrets Id (via "Manage User Secrets", which creates a <UserSecretsId> node) in your
                    // .csproj file (as is the case with this project).

                    //userSecretsId: "Estrelica.Core.Demo", <-- This line is no longer need as of Estrelica.Congifuration 1.0.21

                    // "configOverrideKey" specifies a particular instance configuration to be selected from the file(s) above.
                    // If not explicitly specified *or* if the app settings/user secrets files have nothing configured for this
                    // override key, the default (base) Archer configuration will be used.

                    configOverrideKey: null);

                Utilities.Pause("This example shows how to evaluate levelled content via the Archer search API as XElement results.", ConsoleColor.Green);

                LoadMultipleLevelsAsXml();

                Utilities.Pause("This example shows to evaluate content from a specific level via the Archer search API as XElement results.", ConsoleColor.Green);

                int sampleContentId = LoadSingleLevelAsXml();

                Utilities.Pause("This example shows how to load a single content record via the REST API, returned as a JSON Dictionary result.", ConsoleColor.Green);

                LoadSingleContentAsJsonDictionary(sampleContentId);

                Utilities.Pause("The examples above show how to work with Archer records as XElement objects and JSON Dictionary objects.  The extension method 'ContentAccess' makes these much simpler to work with, converting them into IArcherContentAccess results which expose the record metdata and field content in those XElement or JSON nodes via properties and methods.", ConsoleColor.Green);

                LoadContentAsIArcherContentAccess();

                Utilities.Pause("This example shows how Estrelica.Core makes it easy to traverse Archer's content and metadata as a virtual object graph, without ever needing to make direct API calls.", ConsoleColor.Green);

                TraverseRelatedContent();

                Utilities.Pause("The examples above demonstrated how to READ content from Archer.  Now we'll demonstrate how to insert, update and delete records.", ConsoleColor.Green);

                CreateUpdateAndDeleteApplicationsRecord();

            }
            catch (System.Exception ex)
            {
                Utilities.Log(ex.ToString(), false);
            }
            finally
            {
                Utilities.LogResults();
            }
        }

        public static void LoadMultipleLevelsAsXml()
        {
            int expectedRecordCount = 0;
            int totalRecordCount = 0;

            // All of the IContentResolver "GetContent" methods support an optional record count callback Action.  We
            // won't demonstrate that in all cases, but we'll show it here.  This can be useful in your code for showing progress during 
            // long-running searches, or notifying the user of potential problems if a RecordCount.Discrepancy event occurs.

            Action<RecordCountType, int> recordCallback = (t, c) =>
            {
                switch (t)
                {
                    case RecordCountType.Expected: expectedRecordCount = c; totalRecordCount = 0; break;
                    case RecordCountType.Current: Utilities.Log($"Loaded {c} records of {expectedRecordCount}..."); break;
                    case RecordCountType.Total: totalRecordCount = c; break;
                    case RecordCountType.Discrepancy: throw new InvalidDataException($"Archer told us to expect {expectedRecordCount} records but instead returned {totalRecordCount} (a difference of {c} records).  " +
                        "This indicates that either a) the data in Archer changed while the search was executing (causing records to shift position within the result and therefore be skipped or duplicated) or " +
                        "b) Archer's search index has become corrupt and needs to be rebuilt.");
                }
            };

            string moduleName = "Policies";

            // This method demonstrates the "raw" content returned from an Archer webservices API search, which is
            // simply a series of <Record> xml nodes containing <Field> nodes and, potentially sub-<Record> nodes (in
            // the case of a levelled result).

            Utilities.Log($"Loading all records from all levels in '{moduleName}'...", ConsoleColor.Yellow);

            // The Content.GetContent() method is overloaded to accept module/level by name, integer Id, Guid, or IArcherModule/IArcherLevel
            // reference(s).  Here we'll just use the module name.

            // For this example we are not specifying any particular fields that we want return, so the results will include
            // all fields for the module.  If desired, we could limit this to a specific subset of fields via either the
            // optional IEnumerable<int> includeFieldIds parameter (in which we'd specify the integer field Ids that we want
            // included in the results) or optional the Func<IArcherField, bool> includeFieldCallback parameter (in which
            // we'd implement a callback method which returns true for each field that we want included in the result).

            // Also, by passing null for the level, we're telling the Content resolver to return records from all levels in
            // the specified module.  If this instead specified a particular level within the module, only those fields/records
            // from that level would be returned (see the next example for a demonstration of this behavior).

            IEnumerable<XElement> content = core.Content.GetContent(moduleName, null, recordCallback);
            int returnedRecordCount = content.Count();
            if (returnedRecordCount > 0)
            {
                Utilities.Log("");
                Utilities.Log($"Total records loaded: {totalRecordCount} of {expectedRecordCount} records", totalRecordCount == expectedRecordCount);
                Utilities.Log("This count represents the number of records in the first level.  Records from deeper levels are nested inside their parent records.  Hit any key to see an example nested record from the results.", ConsoleColor.Green);
                Utilities.Log("");
                Console.ReadKey();

                // This line will find the first record in the results having something at both the second and third levels (i.e.
                // the first record containing a record that also contains a record), in order to show how the levelled content is returned.
                // This will occur during a full search on a levelled module since we haven't specifically requested a particular level
                // within that module, or otherwise filtered the fields to be returned.

                // We expect the result to have at least one record satisfying this condition since Policies has 3 levels.
                Utilities.Log(content.FirstOrDefault(r => r.Element("Record")?.Element("Record") != null)?.ToString(SaveOptions.None) ?? String.Empty);
            }
            // Make sure the total count that Archer told us to expect matches the actual count that were returned
            Assert.AreEqual("Comparing expected count vs. returned count", returnedRecordCount, expectedRecordCount);
            // Confirm that we got > 0 results
            Assert.IsGreaterThanZero("Returned record count: " + returnedRecordCount, returnedRecordCount);
        }

        public static int LoadSingleLevelAsXml()
        {
            int expectedRecordCount = 0;
            int totalRecordCount = 0;

            Action<RecordCountType, int> recordCallback = (t, c) =>
            {
                switch (t)
                {
                    case RecordCountType.Expected: expectedRecordCount = c; totalRecordCount = 0; break;
                    case RecordCountType.Current: Utilities.Log($"Loaded {c} records of {expectedRecordCount}..."); break;
                    case RecordCountType.Total: totalRecordCount = c; break;
                }
            };

            string moduleName = "Policies";

            // This method demonstrates the "raw" content returned from an Archer webservices API search, which is
            // simply a series of <Record> xml nodes containing <Field> nodes and, potentially sub-<Record> nodes (in
            // the case of a levelled result).

            int contentId = 0;

            // Here we'll get the name of the deepest level from Policies and use it for the search...
            string levelName = core.Metadata.ApplicationByName("Policies").Levels.Last().Name;

            Utilities.Log("");
            Utilities.Log($"Loading all records from just the '{levelName}' level in '{moduleName}'...", ConsoleColor.Yellow);
            IEnumerable<XElement> content = core.Content.GetContent(moduleName, levelName, recordCallback);
            if (content.Count() > 0)
            {
                Utilities.Log("");
                Utilities.Log($"Total records loaded: {totalRecordCount} of {expectedRecordCount} records", totalRecordCount == expectedRecordCount);
                Utilities.Log($"This count represents just the number of records in the '{levelName}' level.  Records from other levels are not included here.  Hit any key to see an example flat record from the results.", ConsoleColor.Green);
                Utilities.Log("");
                Console.ReadKey();
                XElement firstRecord = content.First();
                Utilities.Log(firstRecord.ToString(SaveOptions.None));

                contentId = firstRecord.AttrInt("contentId");
            }
            return contentId;
        }

        public static void LoadSingleContentAsJsonDictionary(int contentId)
        {
            Utilities.Log("");
            Utilities.Log($"Loading a single content record (content Id {contentId}) via the REST API content method", ConsoleColor.Yellow);
            
            // IArcherContent is a simple type that implements IDictionary<string, dynamic> to capture the JSON name/value pairs and 
            // adds an integer Id property representing the content_id of the record
            IArcherContent content = core.Content.GetContentById(contentId);

            Utilities.Log(JsonConvert.SerializeObject(content));
        }

        public static void LoadContentAsIArcherContentAccess()
        {
            int expectedRecordCount = 0;
            int totalRecordCount = 0;

            Action<RecordCountType, int> recordCallback = (t, c) =>
            {
                switch (t)
                {
                    case RecordCountType.Expected: expectedRecordCount = c; totalRecordCount = 0; break;
                    case RecordCountType.Current: Utilities.Log($"Loaded {c} records of {expectedRecordCount}..."); break;
                    case RecordCountType.Total: totalRecordCount = c; break;
                }
            };

            string moduleName = "Policies";

            // This method demonstrates the "raw" content returned from an Archer webservices API search, which is
            // simply a series of <Record> xml nodes containing <Field> nodes and, potentially sub-<Record> nodes (in
            // the case of a levelled result).

            // The XElement record nodes are clumsy to work with, so let's pass them through a wrapper class to 
            // make working with the content much simpler.  This is achieved by simply calling .ContentAccess(core) on
            // the collection (or on a single record), which returns an IArcherContentAccess instance.

            string firstPublishedFieldName = null;
            string lastUpdatedFieldAlias = null;
            Guid? trackingIdFieldGuid = null;
            ITextField firstTextField = null;
            IEnumerable<IUserGroupListField> userGroupFields = null;
            IEnumerable<IValuesListField> valuesListFields = null;
            IEnumerable<IDocumentField> documentFields = null; // image and attachment fields
            IEnumerable<IExternalLinksField> externalLinksFields = null;

            foreach (IArcherContentAccess record in core.Content.GetContent(moduleName, null, recordCallback) // <-- This call returns standard XmlElement structures, same as before
                .ContentAccess(core)) // <-- and this extension method converts them into IArcherContentAccess objects which allow simple interrogation by field names, guids, aliases, etc.
            {
                // The Archer record's content_id is accessible via the Id property:

                Utilities.Log($"Content Id: {record.Id}");

                // All field values from the record can be resolved by string identifiers using the "Value(string identifier)" method.
                // This allows mappings to be loaded from some external source like an Xml file, maintaining the identifiers needed to retrieve
                // values as strings, rather than having to make your code call the .Key property when the mapping calls for it.
                // For example, the Key property can be retrieved via record.Value("[Key]") as well.

                // We can access all the metadata for each record directly from the records themselves...
                IArcherModule module = record.Module;
                IArcherLevel level = record.Level;

                // ...as well as the metadata for all fields in the result.  We'll use that here to demonstrate four different ways of pulling field attributes from the record
                // (by name, alias, guid, and field metadata object).
                firstPublishedFieldName ??= record.Fields.First<IFirstPublishedField>().Name;
                lastUpdatedFieldAlias ??= record.Fields.First<ILastUpdatedField>().Alias;
                trackingIdFieldGuid ??= record.Fields.First<ITrackingIDField>().Guid;
                firstTextField ??= record.Fields.First<ITextField>();

                Utilities.Log($"First Published (by field name): {record.Value(firstPublishedFieldName)}");
                Utilities.Log($"Last Updated (by field alias): {record.Value(lastUpdatedFieldAlias)}");

                // If extensions are available, the update information can be retrieved for each record via the UpdateInformation property (or
                // via Value("[UpdateInformation]") regardless of whether any FirstPublished/LastUpdated fields were included in the search:
                IContentUpdateInformation updateInfo = record.UpdateInformation;

                // alternately, this could be retrieved via
                // var updateInfo = record.Value<IContentUpdateInformation>("[UpdateInformation]");

                DateTime? lastSystemUpdate = updateInfo.SystemUpdateDate;
                if (lastSystemUpdate.HasValue) // this won't be present if we only have access to the Archer API
                {
                    Utilities.Log($"Last system update occurred: {lastSystemUpdate.Value}");
                }
                // Like all properties, this value can also be retrieved via a string identifier, e.g.
                // DateTime? lastSystemUpdate = (DateTime?)record["[UpdateInformation].SystemUpdateDate"];
                // or var lastSystemDate = record.Value<DateTime>("[UpdateInformation].SystemUpdateDate");


                Utilities.Log($"Tracking Id (by field Guid): {record.Value(trackingIdFieldGuid.Value)}");
                Utilities.Log($"First text field '{firstTextField.Name}': {record[firstTextField]}");

                // Here's an example of a more complex type.  The value returned by UserGroupList fields is a UserGroupList object which
                // exposes both the UserIds and GroupIds set for the field, as well as the complete User/Group objects for those Ids
                // if you want to know something specific like a user's UserName or a groups DistinguishedName.

                // Here we'll iterate all the IUserGroupListFields returned for the record and show their details:
                userGroupFields ??= record.Fields.Where<IUserGroupListField>();
                foreach (IUserGroupListField userGroupField in userGroupFields)
                {
                    // We can go at this in two different ways, either by getting the UserGroupList object into a local variable and then acting
                    // on its properties, or by using string identifiers with dot delimiters (i.e. "[Field Identifier].[Property]")...
                    IUserGroupListSelection ugs = record.Value(userGroupField);
                    if (ugs != null)
                    {
                        Utilities.Log($"UG Field '{userGroupField["Name"]}' contains userIds: {ugs.UserIds.Conjoin().NullIfEmpty() ?? "(none)"} and groupIds: {ugs.GroupIds.Conjoin().NullIfEmpty() ?? "(none)"}");
                    }

                    // The actual user and group objects are available via the ugs.Users and ugs.Groups properties, but this just demonstrates two
                    // more ways to access them, either by specific known type and property accessor (IEnumerable<IArcherUser> and UserName property in 
                    // the user example) or by the common IArcherEntity type (with property resolution by "Name" as a string) in the Group example...
                    string usersIdentifier = userGroupField["Alias"] + ".Users"; // e.g. "Some_Field_Alias.Users"
                    string groupsIdentifier = userGroupField["Name"] + ".Groups"; // e.g. "Some Field Name.Groups"
                    Utilities.Log($"  (Usernames: '{record.Value<IEnumerable<IArcherUser>>(usersIdentifier)?.Select(u => u.UserName).Conjoin("', '").NullIfEmpty() ?? "(none)"}')");
                    Utilities.Log($"  (Groupnames: '{((IEnumerable<IArcherEntity>)record.Value(groupsIdentifier))?.Select(g => g["Name"]).Conjoin("', '").NullIfEmpty() ?? "(none)"}')");
                }

                // Values list fields also return a complex type implementing IValuesListSection.  This features properties to access the values selected for the
                // field (both as integer Ids and as fully-hydrated IArcherValuesListValue objects), as well as any "Other Text" that may have been entered for the record.
                valuesListFields ??= record.Fields.Where<IValuesListField>();
                foreach (IValuesListField valuesListField in valuesListFields)
                {
                    string vlFieldName = valuesListField["Name"];
                    // note that the above could have used the IValuesListField.Name property in the form
                    //	   vlFieldName = valuesListField.Name;
                    // as the two calls are functionally identical.

                    // This next line will do a hard-cast of whatever is in the field for the given VL fieldname to an IValuesListSelection.  If for 
                    // some reason the wires get crossed (e.g. you give it a Text field's name instead) and the value in that field is NOT a VL selection,
                    // this will throw an exception.
                    IValuesListSelection valueSelection = (IValuesListSelection)record[vlFieldName];

                    // A cleaner way to do it is via record.Value<V>(identifier).  This will return null if the value in the field is not of type V
                    // and cannot be converted to type V. E.g.:
                    valueSelection = record.Value<IValuesListSelection>(vlFieldName); // need not be the field's name here, could also be its Guid, its integer Id, or the valuesListField reference itself

                    // Or if you already have a specific field reference (like the valuesListField here, which we know is an IValuesListField) you
                    // can simply call Value() with that field object as a parameter.  The Value() method recognizes that valuesListField is an IValuesListField
                    // and therefore knows to return the correct type (IValuesListSelection in this case) with no casting required:
                    valueSelection = record.Value(valuesListField);

                    // Here we'll query the selected values of this field and get the Name from each, via the IArcherValuesListValue.Name property:
                    IEnumerable<string> selectedValueNames = valueSelection?.Values.Select(v => v.Name);
                    int valueCount = selectedValueNames?.Count() ?? 0;
                    if (valueCount > 0)
                    {
                        Utilities.Log($"  VL Field '{vlFieldName}' has selected {"value".Pluralize(valueCount)}: '{selectedValueNames.Conjoin("', '")}'");
                    }

                    // Some values lists define an "Other Text" option which allows users to enter supporting information for their selection.  In these
                    // cases the value entered for a record can be accessed via the IValuesListSelection.OtherText property or by appending ".OtherText"
                    // to the field identifier:

                    string otherTextIdentifier = vlFieldName + ".OtherText";
                    string otherText = record[otherTextIdentifier];
                    if (otherText != null)
                    {
                        Utilities.Log($"  and other text '{otherText}'");
                        // Or it can be accessed via property from the IValuesListSelection result as well
                        string alternateWayOfGettingOtherText = valueSelection.OtherText;
                        Utilities.Log($" Got '{alternateWayOfGettingOtherText}' via property for the same field value");
                    }
                }

                // Image and Attachment fields (commonly handled as IDocumentField) are two more complex field types.  Their record contents
                // are represented via an IEnumerable<IArcherDocument> result which can return the Filename and Data for each file
                // (or even download the file from the Archer instance to local storage):

                documentFields ??= record.Fields.Where<IDocumentField>(); // this will return both Image fields and Attachment fields
                foreach (IDocumentField documentField in documentFields)
                {
                    IEnumerable<IArcherDocument> uploadedFiles = record.Value<IEnumerable<IArcherDocument>>(documentField.Id); // use the field's Id in this example
                    if (uploadedFiles?.Count() > 0)
                    {
                        Utilities.Log($"  {documentField.FieldType} field {documentField.Name} contains these files:");
                        foreach (IArcherDocument uploadedFile in uploadedFiles)
                        {
                            Utilities.Log($"    Id: {uploadedFile.Id}  Filename: {uploadedFile.Filename}");
                            // The byte[] content of the file is accessible via the Data property, and can
                            // be saved to a local file via the Download(filename, overwriteExisting) method:
                            uploadedFile.Download(@"c:\temp\" + Guid.NewGuid().ToString());
                        }
                    }
                }

                // Similarly, External Links fields return IEnumerable<IExternalLink> results, with each IExternalLink returning its Name and Url as
                // separate properties: 

                externalLinksFields ??= record.Fields.Where<IExternalLinksField>();
                foreach (IExternalLinksField externalLinksField in externalLinksFields)
                {
                    IEnumerable<IExternalLink> externalLinks = record.Value<IEnumerable<IExternalLink>>(externalLinksField.Guid); // use the field's Guid in this example
                    if (externalLinks?.Count() > 0)
                    {
                        Utilities.Log($"  External link field '{externalLinksField.Name}' contains these links:");
                        foreach (IExternalLink externalLink in externalLinks)
                        {
                            Utilities.Log($"    Name: {externalLink.Name}  Url: {externalLink.Url}");
                        }
                    }
                }

                // The record.Value<V>() method will return default(V) if the type conversion fails, for example, if an invalid field name/guid/alias/id is
                // requested, or if the field is valid but the value in the field is not of the type requested.  So be aware of your type defaults, e.g.
                // this call will return 0 (the default for int) since we're requested a field name that does not exist for the record:
                int notAnIntField = record.Value<int>("Fake field that does not exist");
                if (notAnIntField == 0)
                {
                    Utilities.Log("Got a zero from the fake field as expected", true);
                }
                else
                {
                    // This should never happen...
                    Utilities.Log($"Got unexpected non-zero value {notAnIntField} from the fake field", false);
                }

                // If levelled content was requested, each "parent" record from one level will return its "child" records from the next
                // level in its ChildContent properties.  Each child record is itself an IArcherContentAccess result, so all of the
                // above logic will apply to them as well.  And if there are more levels beneath those records, they can likewise be
                // retrieved via the ChildContent property of each child record.

                if (record.ChildContent.Count() > 0) // you could also use record.Value<IEnumerable<IArcherContentAccess>>("[ChildContent]") to get this
                {
                    Utilities.Log("This record contains child (i.e. levelled) content which can be processed in the same way", ConsoleColor.Yellow);
                    foreach (IArcherContentAccess childRecord in record.ChildContent)
                    {
                        if (childRecord.UpdateInformation.UpdateDate.HasValue)
                        {
                            // UpdateInformation is not provided by the Archer search API, so this will only have results if
                            // the API extensions are enabled.
                            Utilities.Log($"  Child Content Id: {childRecord.Key} was last updated on {childRecord.UpdateInformation.UpdateDate} by {childRecord.UpdateInformation.UpdateUser?.UserName}");
                        }
                    }
                }
            }

        }

        private static Random rnd = new Random();
        private static T randomField<T>(IEnumerable<IArcherField> fields) where T:IArcherField 
        {
            // This is a helper function used below while visiting records to show the content of random fields
            // in each record just to keep things interesting...
            return fields.OfType<T>().OrderBy<T, int>(f => rnd.Next()).FirstOrDefault();
        }

        public static void TraverseRelatedContent()
		{
            // This method demonstrates how Archer content and metadata is (typically) interrelated -- content records do not generally exist on their
            // own in isolation, but instead are part of a bigger picture comprising multiple records linked together via the three reference field types
            // (Cross-Reference, Related Records, and Subform fields).  Given the thousands of possible relationships that might exist, this "big picture"
            // can resemble a massive web of connections between content records, and the task of navigating through that web via discrete API calls can
            // be challenging.

            // Estrelica.Core represents this "big picture" view of content and metadata as a virtual object graph via the IArcherContentAccess interface.
            // Given a single Archer content record represented by an IArcherContentAccess reference, all of that record's metadata and related content
            // can be accessed through the methods and properties of that interface.
            //
            // Since related content is itself returned as IArcherContentAccess references, this forms a hiearchy of content that is easily navigable
            // without ever needing to make any direct calls the Archer API in your code.

            // Here we'll use the "Control Standards" application as a starting point, which (in a stock Archer installation) has direct and indirect 
            // relations to "Policies", "Authoritative Sources", "Question Library" and many other Archer applications.  We'll pick one record from that
            // application as an entry point into the web of records, then traverse that hierarchy to visit all the extended relations and
            // show values from a few random fields.

            // Hit Q at any time to stop the iteration, or any other key to pause.
            
            // Feel free to specify a different application name here if you'd like to explore other content relationships:
            var applicationName = "Control Standards";
            int stackLimit = 4; // limit how deep we go into the content hierarchy, otherwise this could take hours

            // We'll select a record from the first level of the application (which in the case of Control Standards happens to be the only level).
            IArcherLevel level = core.Metadata.ApplicationByName(applicationName).Level();

            Utilities.Log($"This demo will select a single record from the '{applicationName}' application as a starting point into the Archer content web, then show all the records that are within {stackLimit} degrees of separation from it.", ConsoleColor.Yellow);
            Utilities.Log("Press 'Q' at any time to stop the iteration, any other key to pause.", ConsoleColor.Cyan);

            // Create a search on the application, filtering to find the first record having something in one of its Cross-Reference and/or Related Records
            // fields (we'll exclude subform fields in this search, since those are less likely to have references to other modules).  That will provide us
            // with a good starting point to branch out from.
            IEnumerable<IReferenceField> referenceFields = level.FieldsOfType<IReferenceField>(f => f.FieldType != FieldType.Subform);

            if (referenceFields.Count() == 0)
			{
                throw new ArgumentException($"Application '{applicationName}' contains no Cross-Reference or Related Records fields.  Please choose another application for this demo.");
			}

            // Our filter condition(s) will simply be "where this field is not empty"
            XElement[] filterConditions = referenceFields.Select(f => f.CreateIsNotEmptyCondition()).ToArray();
            // And build an operator logic string to OR all the conditions, e.g. "1 OR 2 OR 3 OR 4 .. etc."
            string filterOperatorLogic = Enumerable.Range(1, filterConditions.Length).Select(i => i.ToString()).Conjoin(" OR ");

            // This demonstrates another, cleaner way to get content, by calling .Content() directly from an IArcherLevel
            // (or IArcherModule) reference and providing search options via an Action<ISearchOptions> callback:
            IArcherContentAccess startingRecord = level.Content(options => {
                options.filterConditions = filterConditions;
                options.filterOperatorLogic = filterOperatorLogic;
            }).FirstOrDefault();

            if (startingRecord == null)
			{
                throw new ArgumentException($"Unable to find a record in application '{applicationName}' with referenced content in any Cross-Reference or Related Records fields");
			}

            // Now we've found a candidate record that has references to other content.  Here we'll declare/define a "visitContent" method
            // that will recurse through this record and everything that it's related to in order to show how record navigation works.
            // After it's defined, we'll call visitContent(startingRecord) to kick off the process...

            #region visitContent method

            // Helper method for showing field labels for content
            Action<IArcherField> showFieldLabel = (field) =>
            {
                // Special handling to create a label for Record Permissions fields, which are simply UG fields with .IsRecordPermission = true.
                // For all other cases we'll just show the field type in the label.
                string fieldTypeLabel = field is IUserGroupListField ug && ug.IsRecordPermission ? "RecordPermission" : field.FieldType.ToString();

                Console.Write($"{fieldTypeLabel} field '{field.Name}' in Module '{field.Level.Module.Name}' ");
                if (field.Level.Module.Levels.Count() > 1)
                {
                    Console.Write($"(Level: '{field.Level.Name}') ");
                }
                Console.Write("contains ");
            };

            HashSet<int> visitedContent = new HashSet<int>();
            HashSet<int> visitedModules = new HashSet<int>();
            HashSet<int> visitedValuesLists = new HashSet<int>();

            bool showMoreRecords = true;

            int stackDepth = 0;
            int recordCount = 0;

            Action<IArcherContentAccess> visitContent = null; // must declare and assign this variable on separate lines so it can be recursive
            visitContent = (record) =>
            {
                // Make sure a) we haven't exceeded the stack limit, b) we haven't been told to stop, c) that we actually have a record, and
                // d) that we haven't already seen it (otherwise we'll end up in an endless circular reference)
                if (++stackDepth <= stackLimit && showMoreRecords && record != null && visitedContent.Add(record.Id))
                {
                    if (Console.KeyAvailable)
                    {
                        showMoreRecords = Char.ToUpper(Console.ReadKey(true).KeyChar) != 'Q';
                        if (showMoreRecords)
                        {
                            Utilities.Log("Iteration paused.  Press any key to continue or 'Q' to quit.", ConsoleColor.Cyan);
                            showMoreRecords = Char.ToUpper(Console.ReadKey(true).KeyChar) != 'Q';
                        }
                    }

                    if (showMoreRecords)
                    {
                        Console.WriteLine();
                        Console.WriteLine($"Record count: {++recordCount}, Depth: {stackDepth}");
                        var separator = new String('=', (stackDepth - 1) * 4) + "> ";
                        Console.Write(separator);

                        visitedModules.Add(record.ModuleId);

                        // If this is a levelled module, indicate its level name
                        if (record.Module.Levels.Count() > 1)
                        {
                            Console.WriteLine($"{record.Module.Name} ({record.Level.Name}): {record.TrackingId}");
                        }
                        // Otherwise just note the module
                        else
                        {
                            Console.WriteLine($"{record.Module.Name}: {record.TrackingId}");
                        }

                        // Show the standard First Published and Last Updated info:
                        Console.WriteLine($"First Published {record.UpdateInformation.CreateDate?.ToLocalTime()} by {record.UpdateInformation.CreateUser?.UserName}");
                        Console.WriteLine($"Last Updated {record.UpdateInformation.UpdateDate?.ToLocalTime()} by {record.UpdateInformation.UpdateUser?.UserName}");

                        // Let's grab a few random fields from this record and show what's in each of them.

                        ITextField textField = randomField<ITextField>(record.Level.Fields);
                        if (textField != null)
                        {
                            showFieldLabel(textField);
                            string textFieldValue = record.Value(textField);
                            Console.WriteLine(textFieldValue.IsNullOrEmpty() ? "no value" : "\"" + textFieldValue + "\"");
                        }

                        IValuesListField vlField = randomField<IValuesListField>(record.Level.Fields);
                        if (vlField != null)
                        {
                            int valueCount = record.Value(vlField).Values.Count();
                            showFieldLabel(vlField);
                            if (valueCount == 0)
                            {
                                Console.Write("no selection");
                            }
                            else
                            {
                                Console.Write($"the selection{(valueCount == 1 ? null : "s")} \"{record.Value(vlField).Values.Select(vlv => vlv.Name).Conjoin("\", \"")}\"");
                            }
                            
                            if (visitedValuesLists.Add(vlField.RelatedValuesListId))
                            {
                                // This can get very chatty, so let's show the available sections the first time we hit a values list we haven't already
                                // seen, but skip it on subsequent visits...
                                Console.Write($" from the possible selections \"{vlField.ValuesList.Values.Select(vlv => vlv.Name).Conjoin("\", \"")}\"");
                            }


                            // If the field has "Other Text" selected, we'll display that too

                            string otherText = record.Value(vlField).OtherText;
                            if (!otherText.IsNullOrEmpty())
                            {
                                Console.Write($" as well as the text value: \"{otherText}\"");
                            }
                            Console.WriteLine();
                        }

                        INumericField numericField = randomField<INumericField>(record.Level.Fields);
                        if (numericField != null)
                        {
                            // Numeric fields in Archer are an odd case, since they're always stored as decimals in the database, but may be configured
                            // to display as simple integers in the UI for view/edit purposes.  Therefore, Estrelica.Core overloads the IArcherContentAccess.Value()
                            // method for INumericField to let you as the developer retrieve values in these fields in whatever .NET numeric type is correct
                            // for your code.  So if you know your field only contains integers you can retrieve its values cast at ints using record.Value<int>(numericField),
                            // and if you know it's being used for decimals you can retrieve them with record.Value<decimal>(numericField).  For that matter you can
                            // call it with double or Float if that's your use case.  Any .NET numeric type is valid for this call.

                            // For simplicity's sake we'll just consider two cases, treating the field as an int if the field has 0 or no decimal places configured,
                            // otherwise we'll treat it as a double, and format the output with however many decimal places are configured for the UI:
                            showFieldLabel(numericField);
                            int decimalPlaces = numericField.DecimalPlaces.GetValueOrDefault();
                            if (decimalPlaces == 0)
                            {
                                int? numericValue = record.Value<int>(numericField);
                                Console.WriteLine(numericValue.HasValue ? numericValue.Value.ToString() : "no value");
                            }
                            else
                            {
                                double? numericValue = record.Value<double>(numericField);
                                Console.WriteLine(numericValue.HasValue ? numericValue.Value.ToString($"N{decimalPlaces}") : "no value");
                            }
                        }

						IDateField dateField = randomField<IDateField>(record.Level.Fields);
                        if (dateField != null)
                        {
                            showFieldLabel(dateField);
                            DateTime? dateValue = record.Value(dateField);
                            if (dateValue.HasValue)
                            {
                                Console.WriteLine(dateValue.Value.ToLocalTime().ToString(dateField.IncludeTimeInformation ? "yyyy-MM-dd HH:mm:ss" : "yyyy-MM-dd"));
                            }
                            else
                            {
                                Console.WriteLine("no value");
                            }
                        }

                        // Image fields and Attachment fields both implement the base IDocumentField interface, so this will grab
                        // a random field of either type, if any exist on this level:
                        IDocumentField docField = randomField<IDocumentField>(record.Level.Fields);
                        if (docField != null)
						{
                            showFieldLabel(docField);
                            IEnumerable<IArcherDocument> imagesOrAttachments = record.Value(docField) ?? Enumerable.Empty<IArcherDocument>();
                            if (imagesOrAttachments.Count() == 0)
							{
                                Console.WriteLine("no documents");
							}
                            else
							{
                                Console.WriteLine($"{imagesOrAttachments.Count()} documents: \"{imagesOrAttachments.Select(d => d.Filename).Conjoin("\", \"")}\"");
							}
						}

                        IUserGroupListField userGroupListField = randomField<IUserGroupListField>(record.Level.Fields);
                        if (userGroupListField != null)
						{
                            showFieldLabel(userGroupListField);
                            // UserGroupList fields can contain any combination of users and/or groups, separated into the
                            // Users and Groups properties of IUserGroupListSelection:
                            IUserGroupListSelection usersAndOrGroups = record.Value(userGroupListField);
                            
                            if (usersAndOrGroups.Users.Count() == 0)
							{
                                Console.Write("no users and ");
							}
                            else
							{
                                Console.Write($"these users: {usersAndOrGroups.Users.Select(u => u.UserName).Conjoin()} and ");
							}

                            if (usersAndOrGroups.Groups.Count() == 0)
							{
                                Console.WriteLine("no groups");
							}
                            else
							{
                                Console.WriteLine($"these groups: \"{usersAndOrGroups.Groups.Select(g => g.Name).Conjoin("\", \"")}\"");
							}
						}

                        Console.WriteLine();

                        // Now iterate through this record's reference fields, i.e. its Cross-Reference, Related Records and Subforms,
                        // and if we haven't already visited whatever records they are linked to, go ahead and traverse that content too...
                        foreach (IReferenceField referenceField in record.Fields.OfType<IReferenceField>())
                        {
                            // First just have a look at the referenced content Ids to see if any of them are Ids that we haven't already 
                            // visited.  The ".Ids" property is just a set of integer content Ids that are already loaded, by virtue of having loaded
                            // the current (parent) record.  Calling the ".Content" property will actually make an API call to fully hydrate an
                            // IEnumerable<IArcherContentAccess> collection with all of the content for each of those Ids.
                            // Therefore, if we evaluate all those Ids and see that we've already visited all of them, there's no need to
                            // visit them again, so we can skip everything that follows and avoid unneccessary API calls.
                            if (showMoreRecords && stackDepth < stackLimit && record.Value(referenceField).Ids.Any(id => !visitedContent.Contains(id)))
                            {
								IEnumerable<IArcherContentAccess> referencedContent = record.Value(referenceField).Content;
                                if (referencedContent.Count() > 0)
                                {
                                    showFieldLabel(referenceField);
                                    Console.WriteLine($"TrackingIds {referencedContent.Select(c => c.TrackingId).Conjoin()} in Module '{referenceField.ReferencedModule.Name}'");
                                    foreach (IArcherContentAccess referencedRecord in referencedContent)
                                    {
                                        if (showMoreRecords)
                                        {
                                            visitContent(referencedRecord);
                                        }
                                        else
										{
                                            break;
										}
                                    }
                                }
                            }
                        }
                    }                    
                }
                stackDepth--;
            };

            #endregion

            // Here's where actually we dive into the web of content, starting with the record we identified above
            visitContent(startingRecord);

            Utilities.Log($"Visited {visitedContent.Count()} unique records spanning {visitedModules.Count()} modules ({visitedModules.Select(id => core.Metadata.ModuleById(id)).OrderBy(m => m.Name).Select(m => $"'{m.Name}' ({m.ModuleType})").Conjoin()})", ConsoleColor.Green);
		}

        public static void CreateUpdateAndDeleteApplicationsRecord()
        {
            // This test will demonstrate a few common CRUD activities related to Archer content.

            // The code below will show you how to:

            // - Insert a new record into an Archer application (we'll use the core 'Applications' application for this)
            // - Identify some fields of various types on that record to be populated with values
            // - Gather some prerequisites needed for a Related Records field and an Attachments field
            // - Populate the fields of the new record with values
            // - Add a new "Integrations" subform record to the record
            // - Save all changes to Archer
            // - Read the inserted record back from Archer to confirm that the insert was successful with the values we populated
            // - Put the freshly-reloaded record into edit mode and make further modifications to its fields
            // - Save the modified record back to Archer
            // - Read the modified record back from Archer to confirm that the modifications where successful
            // - Delete the newly-created content from Archer

            // This depends on the standard out-of-the-box "Applications" and "Integrations" subform shipped by Archer,
            // with a few standard fields (see below) having their names unchanged.
            // If you've removed/retired these modules and/or deleted or modified the names of any of the relevant fields,
            // this test will fail.
            // It will also fail if the current user does not have CRUD permissions to the "Applications" application
            // and/or the "Integrations" subform.

            string tempFilename = null;
            string attachmentFilename = null;
            int contentId = 0;
            int newSubformContentId = 0;
            IArcherValuesListValue newlyAddedVLV = null;

            try
            {
                Utilities.Log("This test will demonstrate how to create, update, and delete Archer content records.");

                // Identify the "Applications" application.  See notes above.
                IArcherApplication application = core.Metadata.ApplicationByName("Applications");
                Utilities.Log($"For this we'll use the '{application.Name}' application.  First, we'll insert a new record representing Estrelica.Core.");

                // Records belong to levels rather than to applications/modules, so in order to manage records we need to identify the default level
                // for this application:
                IArcherLevel level = application.Level(); // <-- If we wanted to select a specific level, we could pass its name/alias/guid/id here,
                                    // but we know Applications only has one level so we'll just take the default (i.e. with no parameter specified)

                // Step 1: Identify some fields from this level, to be used in making edits to the record in Step 4.

                // Note: some fields may be required to have a value before saving, so you can evaluate that like so if needed:
                IEnumerable<IArcherField> requiredFields = level.Fields.Where<IIsRequiredProperty>(p => p.IsRequired);
                Utilities.Log($"Identified {requiredFields.Count()} required fields for level '{level.Name}': {(requiredFields.Select(f => $"'{f.Name}' ({f.FieldType})").Conjoin())}");

                // Next we need to identify a few fields that we're going to populate with values.  This depends on
                // your Archer "Applications" app having these standard fields with the out-of-the-box names, so
                // this will fail here if any of these fields can't be found. (Note that, as with all similar selectors,
                // we could use aliases, Guids, or Ids here to identify the fields as well.)

                // Here we're getting these fields as objects from the Level object, passing it the name and expected
                // (interface) type for each field we need.  In Step 4 We'll use those field objects to retrieve editors
                // from the content record we created above, in order to set values for those fields on that record.
                // In Step 5 we'll demonstrate an alternate way of getting those editors directly from the record
                // using just a field identifer (name, alias, Guid or integer Id).  Both approaches return the same
                // editors, it's just a matter of which approach is more convenient for your purposes.

                IUserGroupListField ownerField = level.Field<IUserGroupListField>("Application Owner");
                IAttachmentField attachmentField = level.Field<IAttachmentField>("Attachments");
                ITextField versionField = level.Field<ITextField>("Version");
                INumericField licensedQuantityField = level.Field<INumericField>("Licensed Quantity");
                IValuesListField applicationTypeField = level.Field<IValuesListField>("Application Type");
                ISubformField subformField = level.Field<ISubformField>("Integrations");

                ITextField descriptionField = level.Field<ITextField>("Description");

                // The assignments above will throw an exception if a field of the given type/(name or alias) is not found.
                // You can pass "throwExceptionIfInvalid: false" to those calls if you'd rather have it return a
                // null in those instances.  For example, if you're unsure whether your "Application Name" field
                // is really "Application Name", "App Name", or just "Name", you could resolve it to any one of those
                // identifiers using a coalesce pattern like this:

                ITextField nameField = level.Field<ITextField>("Name", throwExceptionIfInvalid: false) ?? // <-- returns null if there's no Text field with the name or alias "Name"
                    level.Field<ITextField>("App Name", throwExceptionIfInvalid: false) ?? // <-- returns null if there's no Text field with the name or alias "App Name"
                    level.Field<ITextField>("App_Name", throwExceptionIfInvalid: false) ?? // <-- returns null if there's no Text field with the name or alias "App_Name"
                    level.Field<ITextField>("Application Name", throwExceptionIfInvalid: true); // <-- throws an exception if there's no Text field named "Application Name"

                // Using this strategy, the first text field found having any of the three names/aliases above will be returned.
                // If no text fields are found for any of the strings provided, the final call (with throwExceptionIfInvalid: true) will raise
                // an exception as expected.

                // Check for the presence of a HistoryLog field that tracks Field changes.  If one exists, we'll use it later to verify
                // that changes are being tracked on the record via ContentHistory later.
                IHistoryLogField historyLogField = level.FieldsOfType<IHistoryLogField>(f => f.IncludeFieldValueChangeAudit).SingleOrDefault();

                // Step 2: Make sure there's not already a record for 'Estrelica.Core' in this level, as that will cause our insert to
                // fail due to the uniqueness constraint on the Name field.  If a record already exists, we'll give the user an
                // opportunity to delete it before proceeding (this may be the result of having run this demo in the past without
                // allowing it to perform its cleanup in the 'finally' block below).
                // We'll do this by performing a simple search "where Name = 'Estrelica.Core'" using the nameField we identified above:

                int? existingContentId = level.Content(options => {
                    // This filter will return the record we're looking for
                    options.AddFilterCondition(nameField.CreateCondition(ValuesOperator.Equals, "Estrelica.Core")); } 
                 ).FirstOrDefault()?.Id;

                // If no content Id was returned, it means the "Estrelica.Core" record does not already exist so we're safe to proceed with the insert
                bool proceedWithInsert = !existingContentId.HasValue;

                if (!proceedWithInsert)
				{
                    // We already have a record, so prompt the user to delete it before proceeding
                    if (proceedWithInsert = 
                            Utilities.Prompt($"A record already exists in '{application.Name}' for 'Estrelica.Core'.  Would you like to delete it and proceed with the demo?") == 'Y')
					{
                        core.Content.Delete(existingContentId.Value);
                        Utilities.Log("Deleted content Id " + existingContentId.Value);
					}
				}

                if (proceedWithInsert)
                {
                    // Step 3: Create a new record

                    // Here we'll create a new empty content record at the identified level.  At this point the record only exists in memory --
                    // it has *not* been saved to Archer yet.  That'll happen when we call contentEdit.SaveChanges() below.

                    // level.CreateContent() returns an IArcherContentEdit interface that includes all the type-specific editors we'll need for the 
                    // fields in the level
                    IArcherContentEdit contentEdit = level.CreateContent();

                    Assert.IsTrue($"Created new empty content record for level '{level.Name}'",
                        contentEdit != null && // we got a fresh IArcherContentEdit record
                        contentEdit.Id == 0); // and its Id is 0, meaning that it does not (yet) exist in Archer

                    // Prepare some items for inserting into CrossReference, RelatedRecords and Attachment fields

                    // This helper function will return the first available content Id from a given level, or 0 if no records are available.
                    Func<IArcherLevel, int> getFirstContentIdFromLevel = (levelToSearch) => levelToSearch.Content().FirstOrDefault()?.Id ?? 0;

                    // We'll use this to keep track of target levels that we've already determined have no content
                    // available for the cross-ref or related records fields, so we don't do it unnecessarily.
                    HashSet<IArcherLevel> checkedLevelsForContent = new HashSet<IArcherLevel>();

                    // Identify a cross ref field in the level which has available content that we can
                    // add to it.

                    ICrossReferenceField crossRefField = null;
                    int targetCrossRefContentId = 0;

                    foreach (ICrossReferenceField crf in level.FieldsOfType<ICrossReferenceField>())
                    {
                        // Look for cross-ref fields that don't require > 1 reference, because
                        // we're only going to use a single content Id for this.
                        // Note that MinimumSelection may be left undefined in Archer, so GetValueOrDefault(0)
                        // will treat it as though the MinimumSelection is explicitly set to 0 (i.e., "no minimum
                        // selection required").
                        if (crf.MinimumSelection.GetValueOrDefault(0) < 2)
                        {
                            foreach (IArcherLevel relatedLevel in crf.RelatedLevels)
                            {
                                // And try to find a candidate content Id that we can insert into it.
                                if (!checkedLevelsForContent.Contains(relatedLevel))
                                {
                                    targetCrossRefContentId = getFirstContentIdFromLevel(relatedLevel);
                                    if (targetCrossRefContentId > 0)
                                    {
                                        crossRefField = crf;
                                        break;
                                    }
                                    else
                                    {
                                        // Note that we found 0 records in this one so we don't check it again.
                                        checkedLevelsForContent.Add(relatedLevel);
                                    }
                                }
                            }
                        }
                        // Stop if we found a candidate cross-ref field that we can insert a value into
                        if (crossRefField != null)
						{
                            break;
						}
                    }

                    if (crossRefField != null)
                    {
                        Utilities.Log($"Identified content Id {targetCrossRefContentId} for use in populating {crossRefField.FieldType} field '{crossRefField.Name}'");
                    }
                    else
                    {
                        Utilities.Log("Unable to identify any existing content for use in populating any CrossReference fields, skipping this step.");
                    }

                    // Do the same for a Related Records field

                    IRelatedRecordsField relatedRecordsField = null;
                    int targetRelatedRecordsContentId = 0;

                    foreach (IRelatedRecordsField rrf in level.FieldsOfType<IRelatedRecordsField>())
                    {
                        // Look for related records fields that don't require > 1 reference, because
                        // we're only going to use a single content Id for this
                        if (rrf.MinimumSelection.GetValueOrDefault(0) < 2)
                        {
                            // And try to find a candidate content Id that we can insert into it.
                            if (!checkedLevelsForContent.Contains(rrf.RelatedLevel))
                            {
                                targetRelatedRecordsContentId = getFirstContentIdFromLevel(rrf.RelatedLevel);
                                if (targetRelatedRecordsContentId > 0)
                                {
                                    relatedRecordsField = rrf;
                                    break;
                                }
                                else
                                {
                                    // Note that we found 0 records in this one so we don't check it again.
                                    checkedLevelsForContent.Add(rrf.RelatedLevel);
                                }
                            }
                        }
                    }

                    if (relatedRecordsField != null)
                    {
                        Utilities.Log($"Identified content Id {targetRelatedRecordsContentId} for use in populating {relatedRecordsField.FieldType} field '{relatedRecordsField.Name}'");
                    }
                    else
                    {
                        Utilities.Log("Unable to identify any existing content for use in populating any RelatedRecords fields, skipping this step.");
                    }


                    // We also need a file that we can upload as an attachment
                    attachmentFilename = Path.Combine(Path.GetTempPath(), "test file.txt");
                    File.WriteAllText(attachmentFilename, "This is some sample text for the attachment");

                    // We're also going to need the current user account for a few things below, so we'll capture that here:
                    IArcherUser currentUser = core.Access.CurrentUser;

                    // Step 4: Populate the new record's field with some values

                    // Now we're ready to start populating the record with values.  Calling the IArcherContentEdit.Field()
                    // method with a type-specific field variable will return an editor specific to that field type.
                    // For example, calling it with an ITextField returns an ITextValueEdit editor, which only allows
                    // string values to be assigned to its .Value property:

                    contentEdit.Field(versionField).Value = typeof(Estrelica.Core).Assembly.GetName().Version.ToString();

                    contentEdit.Field(descriptionField).Value = "Estrelica.Core provides simplified access to the Archer APIs";

                    // Before setting the "Application Name" field, however, we'll take this opportunity to show what happens when you attempt
                    // to save a record with one or more required fields left unpopulated.

                    // In the standard OOBE "Applications" application, "Application Name" is the only field that is required,
                    // so if that rule is still true in the current environment, we'll leave that field blank and show what
                    // happens when we call contentEdit.SaveChanges() for the record:
                    if (nameField.IsRequired)
                    {
                        Exception raisedException = null;
                        // This is expected to throw an aggregate exception combining one or more specific exceptions describing whatever problems are
                        // found with the record.

                        // In this instance we expect to see an inner exception telling us that the "Application Name" field is required.
                        // (Note that this error scenario also applies to fields having min/max selection requirements, numeric fields with
                        // min/max value limits, etc., although we don't have any of those to test in the "Applications" application.)


                        // #### DO NOT PANIC IF YOUR DEBUGGER STOPS ON AN EXCEPTION HERE! ####  This means that the code is working *correctly*.
                        // We're testing to make sure that an exception *IS* thrown (via Assert.ThrowsException(...)) when we try to do something
                        // against the rules.  The same is true for all of the other Assert.ThrowsException() calls that follow below.



                        if (Assert.ThrowsException<AggregateException>("Attempting to save record with one or more required fields left empty", 
                            () => contentEdit.SaveChanges(), out raisedException))
                        {
                            var innerExceptions = ((AggregateException)raisedException).InnerExceptions;
                            // Confirm that the aggregate contains at least one inner exception
                            if (Assert.IsTrue("Confirming inner exceptions describing the actual errors", innerExceptions.Count() > 0))
                            {
                                // and specifically one describing the missing required field...
                                Assert.IsTrue($"Confirming error describing the required '{nameField.Name}' field",
                                    innerExceptions.FirstOrDefault(e => e.Message == $"The {nameField.Name} field is a required field.") != null);
                            }
                        }
                    }

                    // And now that we've completed that test, we can go ahead and set the Name:
                    contentEdit.Field(nameField).Value = "Estrelica.Core";

                    // Likewise, calling IArcherContentEdit.Field() with an INumericField returns a INumericValueEdit editor, which
                    // only allows numbers to be assigned to its value:

                    contentEdit.Field(licensedQuantityField).Value = 1;

                    // Editors for Archer's complex field types are also provided.  For example, calling IArcherContentEdit.Field()
                    // with an IUserGroupListField returns an IUserGroupListSelectionEdit, which has distinct
                    // methods for setting/adding/removing users and/or groups on the field.  Here we'll
                    // use it to make the current user the "Application Owner" for this application:

                    IUserGroupListSelectionEdit ownerFieldEditor = contentEdit.Field(ownerField);

                    // We know this is a newly-minted record, so it has no users in this field at present.
                    // Therefore we can use the .AddUser() method since we know the field is currently empty:
                    ownerFieldEditor.AddUser(currentUser);

                    // But the preferred way in this case is to use .SetUsers(), so that if this happened to
                    // be a modification to an existing record, any users already in that field will be completely replaced by our
                    // new selection:
                    ownerFieldEditor.SetUsers(currentUser);

                    // Note that if this is a multi-select field, we can set multiple users for the field at once
                    // by calling .SetUsers() with a params list of users rather than making repeated calls to .AddUser().
                    // Since we only have one user in scope, let's just pass that same user to the method three times to demonstrate:
                    ownerFieldEditor.SetUsers(currentUser, currentUser, currentUser);

                    // Estrelica.Core will recognize any duplicates in the params list, reducing them down to the unique values before
                    // saving.  In other words, even though we passed 3 user references above, Estrelica.Core will recognize that
                    // they're all the same and will therefore treat it as though we only passed 1:
                    Assert.AreEqual("Confirming that Estrelica.Core correctly handles duplicate values on reference fields",
                        1, ownerFieldEditor.UserIds.Count());

                    // Furthermore, the methods on the IUserGroupListSelectionEdit editor are overloaded to
                    // accept users/groups by reference to objects (as above), or by their integer Ids if
                    // that's all we have available:
                    int userId = currentUser.Id;
                    ownerFieldEditor.SetUsers(userId);

                    // And as before, we can do it with a params list of user Ids if we choose, with the same backing
                    // intelligence to ignore duplicates:
                    ownerFieldEditor.SetUsers(userId, userId, userId, userId, userId, userId);

					// Note that all of the .AddUser() and .SetUsers() calls on the ownerFieldEditor shown above did
					// exactly the same thing: set the current user as the owner of the record we're editing.
					// The multiple calls are only presented here to show several different approaches available
					// to accomplish the same task.  

					// IImageField and IAttachmentField are two other complex types, both handled by the IDocumentFieldEdit
					// editor.  This editor provides a method called Upload() which will upload a file to Archer and attach it
                    // to the field in a single step:
					IDocumentSelectionEdit documentEditor = contentEdit.Field(attachmentField);

                    // Here we'll attach the "test file.txt" file we created above.  The Upload() method takes the
                    // filename to be sent to Archer as an attachment, with two optional parameters:
                    //   - a display name (defaulting to the original filename if not specified) and
                    //   - a boolean value indicating whether the file should be encrypted at rest (default is false, i.e. no encryption).
                    // Here we'll explicitly set the displayName parameter to give the attachment a different display name in Archer:
                    documentEditor.Upload(attachmentFilename, "Sample Text File.txt");

                    // And just as a test, here we'll show what happens when you try to upload a file that doesn't
                    // exist...
                    Assert.ThrowsException<FileNotFoundException>("Verifying that the document field will not accept invalid filenames",
                        () => documentEditor.Upload(@"Z:\SomeInvalidDirectory\SomeInvalidFilename.txt"));


					// IValuesList field is another complex type, supported by the IValuesListSelectionEdit editor.
					// This editor allows values to be managed for the field using whatever identifiers you 
					// choose, by value Id, value text, or even a reference to an IArcherValuesListValue object.
					// Furthermore, if your selection supports and/or requires "Other Text", the editor will allow you
					// to set that as well via its .OtherText property.

					// Next we'll set the value of the "Application Type" field using the string name of the value.  Note that
					// the .Set() method is overloaded to a) accept multiple values via a params list (if the field allows
					// multiple selections), and b) allow values to be set by the values' integer Ids, names, or
					// IValuesListValue references.

					// Here we'll do it using the value name.  This will of course fail if the value name has been changed
					// to something else in your environment.  You can identify the valid values for this field by examining
					// applicationTypeField.ValuesList.Values if you want to use a different value.

					IValuesListSelectionEdit valuesListFieldEditor = contentEdit.Field(applicationTypeField);
                    string valueText = "Product Engineering Software";

                    // The values list editor's Set method (and all methods that reference values by name) use
                    // case-insensitive value lookups by default (via the optional caseSensitiveLookup boolean parameter),
                    // so we can demonstrate that here by using the lowercase version of the value's name to
                    // set the value:
                    valuesListFieldEditor.Set(valueText.ToLower());

                    // If the Values List field in question supports "Other Text", and if the value we selected above
                    // requires it, we can also set the "Other Text" on the field using this syntax:
                    //      valuesListFieldEditor.OtherText = "Some other text";
                    // However, no Values List fields in the "Applcations" application require "Other Text", so
                    // we'll leave that commented out for this demo.

                    // And then confirm that the correct value, with a properly-cased name, resulted from the 
                    // call above:
                    Assert.AreEqual("Verifying that the case-insensitive value name lookup was successful",
                        valuesListFieldEditor.Values.Single().Name, "Product Engineering Software");

                    // If the above fails in your environment because you have no value defined with the name
                    // "Product Engineering Sample", try modifying the valueText string above to a different
                    // value name that is valid in your environment.
                    // Alternately, you can try commenting out the "valuesListFieldEditor.Set(..) line above
                    // and uncomment the code below. It demonstrates instead how to set a value using an
                    // IArcherValuesListValue object, and here we'll just use the first one that's available
                    // on the associated Values List for the field:

                    //IArcherValuesListValue randomValue = valuesListFieldEditor.Values.FirstOrDefault();
                    //valuesListFieldEditor.Set(randomValue);
                    //// Alternately, if we know the value's integer Id, we could set it using that value too:
                    //// int valueId = randomValue.Id;
                    //// valuesListFieldEditor.Set(valueId);
                    //valueText = randomValue.Name; // capture the value's name for validation below

                    // As with all of the multi-value editors, IValuesListSelectionEdit supports setting
                    // multiple values as params as well (assuming the field permits multiple selections), e.g.
                    //	valuesListFieldEditor.Set("Value 1 Name", "Value 2 Name", "Value 3 Name", "etc.");
                    // or even by an IEnumerable<> of names, Ids or IArcherValuesListValue, e.g.
                    //	IEnumerable<IArcherValuesListValue> selectedValues = valuesListFieldEditor.Values.Where(v => v.Name.StartsWith("P"));
                    //	valuesListFieldEditor.Set(selectedValues);


                    // Reference fields (i.e. Cross-Reference, Related Records, and Subforms) are handled by
                    // the IReferencedRecordsSelectionEdit editor. It supports adding/removing/setting the field's
                    // reference(s) to other Archer records by ContentId(s) or IArcherRecord record reference(s).

                    // If we were able to identify a CrossReference field that relates to a level with
                    // identifiable content in Step 3 above, we'll populate that field with a reference here...

                    if (targetCrossRefContentId > 0)
                    {
						ICrossReferencedRecordsSelectionEdit referenceFieldEditor = contentEdit.Field(crossRefField);

                        // Cross-Reference fields are supported by the ICrossReferencedRecordsSelectionEdit editor.
                        // This is a subclass of the IReferencedRecordsSelectionEdit editor which overloads the
                        //      Add(int contentId)
                        // method with
                        //      Add(int contentId, int levelId)
                        // in order to support multi-level Cross-Reference fields.  If you know which level the
                        // referenced content resides in, it is more efficient to call the latter method.  However
                        // it is optional in all cases, for two reasons:
                        //   1. If this is a simple single-level Cross-Reference field, the target level can be
                        //      assumed from the field's definition, so there's no need to specify a levelId
                        //   2. If this is a multi-level Cross-Reference field and no levelId is specified,
                        //      Estrelica.Core will query the Archer API in the background to determine the levelId
                        //      for whatever contentId we provide here
                        // Therefore, this overloaded method is only necessary if the Cross-Reference field in
                        // question targets multiple levels, and you already know which level the content resides
                        // in and want to spare Estrelica.Core the effort of making another API call to look it up.

                        // In this case, we'll just take the easy approach and call the simpler Add(int contentId) method.
                        referenceFieldEditor.Add(targetCrossRefContentId);

                        // This logic only applies to Cross-Reference fields, since they are the only reference
                        // field type which may target multiple levels, hence the reason why the Cross-Reference
                        // field type gets its own editor with this extra method.  Related Records and Subform fields
                        // are handled by the base IReferencedRecordsSelectionEdit editor since they each may only
                        // reference content at a single level.
                    }

                    // And likewise for a RelatedRecords field, if we were able to find one...

                    if (targetRelatedRecordsContentId > 0)
                    {
                        // IReferencedRecordsSelectionEdit supports Related Records and Subform fields.  The only
                        // difference vs. ICrossReferencedRecordsSelectionEdit (above) is that this interface
                        // does not have the Add(int contentId, int levelId) method (but instead only Add(int contentId))
                        // since RR and Subform fields can only target a single level, therefore it's not necessary
                        // to specify it when adding a content reference.
						IReferencedRecordsSelectionEdit referenceFieldEditor = contentEdit.Field(relatedRecordsField);
                        referenceFieldEditor.Add(targetRelatedRecordsContentId);
                    }


                    // Step 5: Create a new linked record via .AddNewRecord() on one of the referenced records field types

                    // The CrossReference and/or RelatedRecords field reference(s) that we added above shows how to link a record
                    // via one of those fields if the referenced record already exists in Archer with a known content Id.
                    // Sometimes, however, you need to create a new referenced record as part of the edit process.
                    // This is where the IReferencedRecordsSelectionEdit.AddNewRecord() method comes in handy.  It does three things for you:
                    //   1. Returns a new IArcherContentEdit record associated with the field's target level, so you can
                    //      start populating field values on the new referenced record right away
                    //   2. Handles the saving of that new record automatically, before the parent record is persisted
                    //   3. Adds a reference to the new record in the relevant field of the parent before the parent is persisted

                    // This is particularly useful for Subform fields, where the referenced record cannot exist
                    // outside the context of the parent record (i.e. the one we're currently editing), so we can't 
                    // pre-fetch the subform record's ContentId the way we did for the CrossReference/RelatedRecords examples above.

                    IReferencedRecordsSelectionEdit subformEditor = contentEdit.Field(subformField);

					// This returns an IReferencedRecordsSelectionEdit for the subform field, which we can call
					// .AddNewRecord() on below.

					// IReferencedRecordsSelectionEdit.AddNewRecord() supports two optional parameters.  The first is an integer levelId or
					// IArcherLevel reference, telling it which target level it should create the new content in.  If we were
					// creating a new record for an ICrossReferenceField which targets multiple levels, this parameter would be
					// required.  In all other cases (ISubformField, IRelatedRecordsField, and any ICrossReferenceField which
					// only targets a single level), Estrelica.Core will identify the target level from the field's
					// metadata so we don't need to specify anything.

					// The method also supports another optional parameter which we *WILL* use here however, an Action<int>
					// callback that allows us to be informed of the newly-inserted record's content Id at some point
					// in the future after it has been saved to Archer.  Since the new record's persistence is managed by
					// the parent record (contentEdit in this case), we cannot call .SaveChanges() directly on
					// this new record (in fact we'll demonstrate that it throws an exception below to prevent us if we try).
					// Therefore, we can't retrieve the new contentId directly from that method call if we need to know what it
					// is for some reason.  Passing an Action<int> callback here gives the parent record
					// a way to inform us of the new record's Id as soon as it is inserted into Archer, so
					// we can capture that Id for cleanup later.  (Again, this is optional.  If you don't care what
					// the newly-created reference record's Id will be, you can call .AddNewRecord() with no parameters.)

					IArcherContentEdit subformContentEdit = subformEditor.AddNewRecord(
                        // This method will be called later, when we call .SaveChanges() on the parent record,
                        // and the parent in turn calls IContentResolver.Update() on this new record in order to obtain
                        // its content Id.  At that point it invoke this method to let us know what the new subform record's Id is:
                        (newId) => newSubformContentId = newId
                     );

                    // Here we'll edit the subform content using the alternate approach described earlier.  Whereas
                    // on the contentEdit record we first identified field objects from its level and used those later
                    // to retrieve editors from the record, that's really only necessary if you need to use those
                    // field references for something else in addition to making the edits.

                    // A more direct approach, assuming you already know the field type and name (or alias or guid
                    // or id) for a given field, is to directly access its editor using one of these field-type specific
                    // methods, each of which returns a type-specific editor for the field you've identified:

                    //	DateField() - Returns an IDateValueEdit editor for the identified Date field
                    //	TextField() - Returns an ITextValueEdit editor for the identified Text field
                    //	NumericField() - Returns an INumericValueEdit editor for the identified Text field
                    //	IPAddressField() - Returns an ITextValueEdit editor for the identified IPAddress field
                    //	DocumentField() - Returns an IDocumentSelectionEdit editor for the identified Attachment or Image field
                    //	ReferenceField() - Returns an IReferencedRecordsSelectionEdit editor for the identified Cross-reference, Related Records, or Subform field
                    //	UserGroupField() - Returns an IUserGroupListSelectionEdit editor for the identified Users/Groups List or Record Permissions field
                    //	ValuesListField() - Returns an IValuesListSelectionEdit editor for the identified Values List field

                    subformContentEdit.TextField("Integration Name").Value = "Test integration name";
                    subformContentEdit.TextField("Integration Description").Value = "This is a test record for the Integrations subform";
                    subformContentEdit.UserGroupField("Integration Owner").SetUsers(core.Access.CurrentUser);

                    // In order for this newly-created subform content to be associated with the parent record, the parent record
                    // needs to know the subform record's content Id.  You might think that we need to persist the subform record,
                    // get its Id, and then call subformEditor.Add(newSubformContentId) at this point.  However, that is incorrect.
                    // All we need to do is call .SaveChanges() on the parent record, and it will take care of the rest.

                    // In fact, if we *do* try to call SaveChanges() directly on this subform record, it will throw an exception telling
                    // us that we're not allowed to do that -- its persistence is under the control of the parent record.  Persisting
                    // a managed record like this would be specifically illegal in the case of a subform, since a subform record cannot exist in
                    // Archer without a parent (which has not been saved yet, so Archer doesn't even know it exists).  However, the same logic
                    // applies to any referenced content (Cross-Reference, Related Record or Subform) that is initiated via the
                    // IReferencedRecordsEditor.AddNewRecord() method we used above (even though Cross-Referenced and Related Records may exist
                    // in Archer independent of a "parent" record).

                    Assert.ThrowsException<InvalidOperationException>("Confirm that we get an exception if we try to directly update a parent-managed record",
                        () => subformContentEdit.SaveChanges());

                    // Step 6: Push the record to Archer for insert, and get the resulting contentId of the newly-inserted record:

                    // If you see an exception here, it probably means that there's already an Application record having "Estrelica.Core" as the 
                    // "Application Name", probably resulting from an earlier execution of this demo that did not clean up after itself.
                    // If so, go to the Archer UI and delete that record, then try again.
                    contentId = contentEdit.SaveChanges();

                    Assert.IsGreaterThanZero($"Inserted content record {contentId} into Archer instance {core.SessionProvider.Instance}", contentId);

                    // This assertion will confirm that the Action<int> callback we provided above (when creating the subform content) was invoked
                    // and notified about the new subform content's Id:
                    Assert.IsGreaterThanZero($"Inserted subform record {newSubformContentId} as part of the content record insert", newSubformContentId);

                    Utilities.Log($"The record has been inserted into the '{application.Name}' application with content Id {contentId}.  You may want to toggle over to the Archer UI at {core.SessionProvider.Url} and review the record at this point.  Hit any key when you're ready to proceed.");
                    Utilities.Pause();

                    // Step 7: Reload the record from Archer and confirm that it has the values we inserted:

                    // Archer's APIs provide two ways of loading content, directly via the REST API if you know the record's Id, or
                    // via search using the SOAP API if you only know specific field values on the record that you want to retrieve.
                    // Unfortunately the results returned by these two methods are completely different (JSON and XML), so if you
                    // want to work with them directly you need two completely different code paths to handle both.

                    // Estrelica.Core provides a .ContentAccess() extension method for both of those result types, returning a common
                    // IArcherContentAccess interface that allows you to maintain a single programming experience regardless of how the
                    // content was retrieved from Archer.

                    // Here we'll demonstrate both approaches, first via the direct REST API call to get the content by Id (returning a
                    // JSON object serialized to Dictionary<string, dynamic>), then again using the Archer webservices "search" to find the
                    // record by Application Name (which returns an XElement (xml) node).  Both cases will leverage the .ContentAccess()
                    // extension method to convert those results into IArcherContentAccess, so that all the following code that acts
                    // on those two records remains the same, eliminating the need to maintain two skillsets to handle the XML vs. JSON
                    // distinction.

                    Utilities.Log(@"Here we'll reload the record and confirm that the changes we made to it were successfully saved. 
We'll do this twice, once via the Webservices SOAP API (which returns the record as XML), and again via the REST API (returning JSON).  
Both results will leverage the .ContentAccess() method, demonstrating that regardless of where the record content came from and which 
format it carries, Estrelica.Core provides a common programming model via IArcherContentAccess that works identically for both...");

                    IArcherContentAccess record = null;
                    foreach (IArcherContentAccess savedRecord in (new bool[] { true, false }).Select(useRestAPI =>
                        {
                            Utilities.Log("");
                            if (useRestAPI)
                            {
                                Utilities.Log("Verifying the record we just saved via the REST API");
                                return core.Content.GetContentById(contentId).ContentAccess(core);
                            }
                            else
                            {
                                Utilities.Log("Verifying the record we just saved via the webservices Search API");
                                return level.Content(options =>
                                    options.AddFilterCondition(nameField.CreateCondition(ValuesOperator.Equals, "Estrelica.Core"))
                                        .AddFilterCondition(level.Fields.First<IFirstPublishedField>().CreateCondition(DateValueOperator.Equals, DateTime.Now, false))
                                        .AddFilterCondition(level.Fields.First<ILastUpdatedField>().CreateCondition(DateValueOperator.Equals, DateTime.Now, false))
                                        ).First();
                            }
                        }))
                    {

                        // Since this is a newly inserted record, its create date and update date should match
                        Assert.AreEqual("Verifying create/update dates",
                            savedRecord.UpdateInformation.CreateDate,
                            savedRecord.UpdateInformation.UpdateDate);

                        // And the current user should be found as both the creator and updater
                        Assert.AreEqual("Verifying create user", currentUser.Id, savedRecord.UpdateInformation.CreateUser.Id);
                        Assert.AreEqual("Verifying update user", currentUser.Id, savedRecord.UpdateInformation.UpdateUser.Id);

                        // KeyFieldValue will attempt to return the value of the record's key field, if a key field is
                        // defined and if it has been loaded for this record.  Otherwise it will return the Tracking Id
                        // (if one is defined), and lastly falling back to the record's Content Id.
                        Assert.IsNotNull("Verifying key field value", savedRecord.KeyFieldValue);

                        // Confirm that all of our edits are present on the record
                        Assert.AreEqual($"Verifying '{nameField.Name}' {nameField.FieldType} field",
                            "Estrelica.Core", savedRecord.Value(nameField));

                        Assert.AreEqual($"Verifying '{versionField.Name}' {nameField.FieldType} field",
                            typeof(Estrelica.Core).Assembly.GetName().Version.ToString(), savedRecord.Value(versionField));

                        Assert.AreEqual($"Verifying '{licensedQuantityField.Name}' {licensedQuantityField.FieldType} field",
                            1, savedRecord.Value(licensedQuantityField));

                        // For the owner (UserGroupList) field, we expect it to have no groups and exactly 1 user, which should be the current user
                        IUserGroupListSelection owner = savedRecord.Value(ownerField);
                        Assert.IsTrue($"Verifying '{ownerField.Name}' {ownerField.FieldType} field",
                            owner.Users.Count() == 1 &&
                            owner.Groups.Count() == 0 &&
                            // owner.Users and currentUser are both resolved from the same IMetadataResolver cache, so we *could* do a simple
                            // equality comparison here (i.e. owner.Users.First() == currentUser), but it is possible that the cache was
                            // flushed and reloaded between the time that currentUser was set above and when owner.Users gets resolved
                            // here, so the references could be to different objects representing the same user.
                            // Therefore the best practice is to check that their Ids match, rather than their references.
                            owner.Users.First().Id == currentUser.Id);

                        // The attachment field should contain exactly 1 file, and the file attached to it should match the temp file we created earlier
                        IEnumerable<IArcherDocument> attachments = savedRecord.Value(attachmentField);
                        Assert.AreEqual($"Verifying '{attachmentField.Name}' {attachmentField.FieldType} field",
                            1, attachments.Count());

                        Assert.AreEqual($"Verifying attachment filename",
                            "Sample Text File.txt", attachments.FirstOrDefault()?.Filename);

                        // Download the attachment to a temp filename
                        tempFilename = Path.GetTempFileName();
                        attachments.First().Download(tempFilename, true);

                        string originalText = File.ReadAllText(attachmentFilename);
                        string downloadedText = File.ReadAllText(tempFilename);

                        Assert.AreEqual($"Verifying '{attachmentField.Name}' attachment contents",
                            originalText, downloadedText);

                        // The Application Type (ValuesList) field should contain exactly 1 value (matching the name we assigned above) and no Other Text
                        IValuesListSelection applicationType = savedRecord.Value(applicationTypeField);
                        Assert.IsTrue($"Verifying '{applicationTypeField.Name}' {applicationTypeField.FieldType} field",
                            applicationType.Values.Count() == 1 &&
                            // valueText is the name of the IArcherValuesList value we assigned above to this field,
                            // either "Product Engineering Software", or the first available value for the field,
                            // so either way this will confirm that the assignment was successful:
                            applicationType.Values.Contains(valueText) &&
                            applicationType.OtherText == null);

                        // If we were able to identify a CrossReference field with applicable content, verify that
                        // the field now contains the value we inserted into it.

                        if (targetCrossRefContentId > 0)
                        {
                            IReferencedRecordsSelection referencedFieldValues = savedRecord.Value(crossRefField);
                            Assert.IsTrue($"Verifying '{crossRefField.Name}' {crossRefField.FieldType} field",
                                referencedFieldValues.Ids.Count() == 1 &&
                                referencedFieldValues.Ids.First() == targetCrossRefContentId);
                            Assert.IsTrue($"Verifying '{crossRefField.Name}' content keys",
                                referencedFieldValues.ContentKeys.Count() == 1 &&
                                referencedFieldValues.ContentKeys.All(k => !String.IsNullOrEmpty(k)));
                        }

                        // And likewise for the RelatedRecords field

                        if (targetRelatedRecordsContentId > 0)
                        {
                            IReferencedRecordsSelection referencedFieldValues = savedRecord.Value(relatedRecordsField);
                            Assert.IsTrue($"Verifying '{relatedRecordsField.Name}' {relatedRecordsField.FieldType} field",
                                referencedFieldValues.Ids.Count() == 1 &&
                                referencedFieldValues.Ids.First() == targetRelatedRecordsContentId);
                            Assert.IsTrue($"Verifying '{relatedRecordsField.Name}' content keys",
                                referencedFieldValues.ContentKeys.Count() == 1 &&
                                referencedFieldValues.ContentKeys.All(k => !String.IsNullOrEmpty(k)));
                        }


                        // The Integrations (subform) field should contain exactly one record, having the text field values we set above:
                        IReferencedRecordsSelection integrations = savedRecord.Value(subformField);
                        Assert.AreEqual($"Verifying '{subformField.Name}' {subformField.FieldType} field",
                            1, integrations.Ids.Count());

                        Assert.IsTrue($"Verifying '{subformField.Name}' content keys",
                            integrations.ContentKeys.Count() == 1 &&
                            integrations.ContentKeys.All(k => !String.IsNullOrEmpty(k)));


                        // Retrieve the subform record we saved earlier.  The Content property returns an 
                        // IEnumerable<IArcherContentAccess> for the records in the field, so we'll just take the
                        // first since we know there's exactly 1 subform record.

                        IArcherContentAccess subformContent = integrations.Content.First();
                        Assert.AreEqual($"Verifying 'Integrations' subform 'Integration Name' Text field",
                            "Test integration name", subformContent.TextValue("Integration Name"));

                        Assert.AreEqual($"Verifying 'Integrations' subform 'Integration Description' Text field",
                            "This is a test record for the Integrations subform", subformContent.TextValue("Integration Description"));

                        // The expectation for this next test will vary depending on how Archer is configured.
                        // Prior to saving this subform record, we set the "Integration Owner" to be the current user,
                        // but unless the configuration of that field has been fixed, we will not expect to see the user
                        // in that field now that we've reloaded it from Archer.

                        // As of Archer 6.9, in a standard deployment this field is configured to only allow a user to be
                        // selected from the "Everyone" group.  Unfortunately, since "Everyone" is not a true Archer group,
                        // this results in no users being valid for this field, and any users we attempt to insert into it
                        // via an API call will be rejected.

                        // If you haven't already fixed this configuration you can confirm this behavior by attempting to
                        // edit the subform record we just created via the UI -- the listbox of available user selections
                        // will be empty, so no user can be selected for the record.  The REST API handles our Update()
                        // attempt similarly, silently rejecting the user we assigned to the field earlier.

                        // One way to fix this is to add an "All Users" rule to the field population config (which
                        // is returned as "AllUsersRead" = true by the Archer API for the field definition itself)
                        // but this is not present by default and must be configured explicitly by an admin.

                        IUserGroupListField integrationOwnerField = subformContent.Level.Field<IUserGroupListField>("Integration Owner");
						IAvailableUserGroupSelections availableSelections = integrationOwnerField.AvailableSelections;

                        // This check will confirm if the "broken" configuration described above is present for the field,
                        // and that no "All Users" rule has been defined to override it:
                        bool brokenConfiguration = integrationOwnerField.AllUsersRead == false &&
                            availableSelections.Users.Count() == 0 &&
                            availableSelections.Groups.Count() == 1 &&
                            availableSelections.Groups.First().Group.Name == "Everyone" &&
                            availableSelections.Groups.First().DisplayUsers == true;

                        if (brokenConfiguration)
                        {
                            // Under these circumstances we expect that Archer ignored the value we attempted to save, so
                            // the currentUser will *NOT* be in the integrationOwnerField.
                            Assert.AreNotEqual($"Verifying 'Integrations' subform '{integrationOwnerField.Name}' {integrationOwnerField.FieldType} field has an invalid configuration",
                                currentUser.Id, subformContent.Value(integrationOwnerField).Users.FirstOrDefault()?.Id);
                        }
                        else
                        {
                            // Apparently the configuration for this field has been modified from the out-of-the-box default
                            // (either via the "All User" rule or a different users/groups rule).
                            // Let's see if its new configuration permitted our assignment above to be saved to the record:
                            Assert.AreEqual($"Verifying 'Integration' subform '{integrationOwnerField.Name}' {integrationOwnerField.FieldType} field configuration has been fixed",
                                currentUser.Id, subformContent.Value(integrationOwnerField).Users.FirstOrDefault()?.Id);
                            // It's still possible for this condition to fail, if, for example, a different user/group rule was
                            // configured which does not include the currentUser in its available selections.
                        }

						// 'Applications' is not expected to have workflow enabled, so this should fall through to the "Record is not 
						// enrolled in workflow" message, but this demonstrates how to perform workflow actions for enrolled records:
						IWorkflowAction applicableWorkflowAction = savedRecord.WorkflowActions().FirstOrDefault();
						if (applicableWorkflowAction != null)
						{
							Assert.IsTrue($"Performing workflow action '{applicableWorkflowAction.TransitionName}' on record",
								savedRecord.PerformWorkflowAction(applicableWorkflowAction));
						}
						else
						{
							Utilities.Log("Record is not enrolled in workflow, or no actions are applicable for the current user");
						}

						// Load the record's ContentHistory.  If we identified a History Log field earlier, ContentHistory should
						// have some values in it (specifically, it should have an IContentHistory result for the History Log field
						// we identified, but it may return more if there are other History Log fields in the level).  If we didn't
						// find a History Log field, it should return an empty collection.

						// The following assertions could still fail if we found a History Log field, but it doesn't track any of the
						// fields we've modified above.  The stock Archer "Applications" application *does* include a 'History Log'
						// field that is configured to track the 'Application Name' and 'Description' text fields, so assuming that's
						// the one we've identified here, the assertions below should succeed.
						// Unfortunately there's no way to programatically determine via Archer's API exactly which fields are being
						// tracked (only whether it tracks all fields or selected fields via the boolean IHistoryLogField.TrackAllFields
						// property) so if your "Applications" module has been modified from stock in any way that affects these
						// assumptions, expect to see these assertions fail.

						IEnumerable<IContentHistory> contentHistories = savedRecord.ContentHistory;

                        // .ContentHistory returns an IEnumerable<IContentHistory>, where each IContentHistory in the set corresponds
                        // to one of the History Log fields on the record's level.

                        if (historyLogField != null)
						{
                            // If we identified a historyLogField, let's confirm that we got some ContentHistory for it too.
                            // We'll do this by identifying the IContentHistory result from the returned collection that references
                            // the same History Log field:
                            IContentHistory contentHistory = contentHistories.SingleOrDefault(ch => ch.HistoryLogField.Equals(historyLogField));

                            if (Assert.IsNotNull($"Verifying ContentHistory for '{historyLogField.Name}'", contentHistory))
							{
                                // We've identified the audit history for the History Log field in question, now let's verify
                                // what it contains.  First we'll make sure it actually has some IHistoryAudit results to
                                // evaluate:

                                if (Assert.IsGreaterThanZero($"Verifying History Audit count for '{historyLogField.Name}'",
                                    contentHistory.HistoryAudits?.Count() ?? 0))
                                {
                                    // fieldHistory.HistoryAudits returns IEnumerable<IHistoryAudit>, but IHistoryAudit is just a base
                                    // interface for three kinds of audits (Field, Advanced Workflow, and Signature) that may be returned.

                                    foreach (IHistoryAudit historyAudit in contentHistory.HistoryAudits)
                                    {
                                        // The user who performed whatever activity was audited can be identified directly from
                                        // the base IHistoryAudit interface:
                                        Assert.IsNotNull($"Verifying audit action user", historyAudit.ActionUser);
                                        // As well as the date/time that the action occurred:
                                        Assert.IsGreater($"Verifying audit action date", historyAudit.ActionDate, DateTime.UtcNow.AddYears(-15));

                                        // But there's more information to be found on the audit-type-specific interfaces which
                                        // inherit from IHistoryAudit (IFieldAudit, IAdvancedWorkflowAudit, and ISignatureAudit).
                                        // We'll identify which type each historyAudit is by soft-casting to those interfaces in turn:

                                        if (historyAudit is IFieldAudit fa)
										{
                                            Assert.AreEqual("Verifying audit type", HistoryAuditType.Field, historyAudit.Type);
                                            // Field Audits will tell us which fields were changed and what changes occurred on each
                                            // (i.e. their old values vs. their new values):
                                            if (Assert.IsGreaterThanZero($"Verifying field audit count", fa.FieldHistory?.Count() ?? 0))
                                            {
                                                foreach(IFieldHistory fh in fa.FieldHistory)
												{
                                                    if (Assert.IsNotNull("Verifying field history", fh.Field))
													{
                                                        Assert.AreNotEqual($"Verifying field '{fh.Field.Name}' change", fh.OriginalValue, fh.NewValue);
													}
												}
                                            }
										}
                                        else if (historyAudit is IAdvancedWorkflowAudit wa)
										{
                                            Assert.AreEqual("Verifying audit type", HistoryAuditType.AdvancedWorkflow, historyAudit.Type);
                                            Assert.IsNotNull("Verifying workflow node name", wa.NodeName);
                                            Assert.IsNotNull("Verifying workflow transition name", wa.TransitionName);
                                            // NodeId and TransitionId are available via this interface as well, but may be null
                                            // so we won't attempt any assertions...
                                        }
                                        else if (historyAudit is ISignatureAudit sa)
										{
                                            Assert.AreEqual("Verifying audit type", HistoryAuditType.Signature, historyAudit.Type);
                                            Assert.IsNotNull("Verifying signature configuration name", sa.ConfigurationName);
                                            Assert.IsGreaterThanZero("Verifying signature configuration Id", sa.ConfigurationId);
                                            // This audit event indicates that a workflow action was signed by a user.  sa.ActionUser will
                                            // tell us who signed the action.
                                            Assert.IsNotNull($"Verifying user signature '{(sa.ActionUser?.UserName ?? "null")}'", sa.ActionUser);
                                        }
                                    }
                                }

							}
						}

						// Keep a reference to this, as we'll re-use it after we exit the loop to perform some edits...
						record = savedRecord;
                    }

                    // We can also identify the content history for specific fields via the FieldHistory methods.  Let's first check
                    // to see if the nameField is tracked by any available History Log fields:
                    if (record.TrackedFieldIds.Contains(nameField.Id))
                    {
						// If so, let's see what has been entered into it over time.  Since we've only set the name one time, we
						// expect it to have exactly one history event:
						IEnumerable<IFieldHistoryEx> nameHistory = record.FieldHistory(nameField);
                        if (Assert.AreEqual("Confirming that the name field has only one history event", 1, nameHistory.Count()))
                        {
                            IFieldHistoryEx nameHistoryEvent = nameHistory.First();
                            // And it represents the initial insert, where its BeforeValue was null and its NewValue
                            Assert.IsNull("Confirming that the name field did not have a value previously", nameHistoryEvent.OriginalValue);
                            // And that its NewValue is the name we provided
                            Assert.AreEqual("Confirming that the history event tracked the name field value we provided", "Estrelica.Core", nameHistoryEvent.NewValue);
                            // And that the change was made by us (the current user)
                            Assert.AreEqual("Confirming that the history event tracked our change", currentUser, nameHistoryEvent.ActionUser);
                            // And that it was made in the past few minutes (assuming the debugger hasn't paused for too long during this run, and that
                            // the local system clock is relatively in sync with the Archer instance db server):
                            Assert.IsGreater("Confirming that the history event was recently tracked", nameHistoryEvent.ActionDate, DateTime.UtcNow.AddMinutes(-3));

							// We can also request a specific point-in-time history event, e.g. if we want to know what the name field contained
							// an hour ago:
							IFieldHistoryEx priorHistoryEvent = record.FieldHistoryAsOf(nameField, DateTime.UtcNow.AddHours(-1));
                            // And of course since we only recently inserted the record, this should be null (i.e. there was no value 1 hour ago):
                            Assert.IsNull("Confirming that no prior history event exists for the name field", priorHistoryEvent);
							// However, if we specify a date in the future, it should return the same value as 
							// above, since the record has not changed since that initial entry:
							IFieldHistoryEx nextHistoryEvent = record.FieldHistoryAsOf(nameField, DateTime.Now.AddYears(10));
                            Assert.AreEqual("Confirming that no 'future' history has occurred", nameHistoryEvent, nextHistoryEvent);
                        }
                    }
    
                    // We can also examine the history for all tracked fields across the entire record.  For example, 


                    // Step 8: Edit the newly-created record with some different values and push the update to Archer:

                    // First, we put the record we just retrieved into edit mode (reusing the contentEdit variable from above)
                    contentEdit = record.ForEdit();

                    // And now we'll change a few fields.  Here we'll show how Estrelica.Core allows you to perform record
                    // updates using whichever method is best for you, by demonstrating a mix of the two edit strategies
                    // discussed above.  We'll use the ITextField versionField object to retrieve an editor for the "Version" field
                    // while using names/aliases/guids/ids to retrieve editors for the rest.

                    // First we'll capture some identifiers:
                    string licensedQuantityFieldName = licensedQuantityField.Name;
                    string applicationTypeFieldAlias = applicationTypeField.Alias;
                    int subformFieldId = subformField.Id;

                    // By passing an ITextField (versionField) to the Field() method, the content record knows it should return an
                    // ITextValueEdit editor, which has a strongly-typed "Value" property that only accepts strings:

                    contentEdit.Field(versionField).Value = "unknown";

                    // But when retrieving a field editor using string/guid/int field Identifiers, the compiler doesn't know which
                    // field type that identifier represents, so we have to choose the method appropriate to the field type we expect
                    // (e.g. "NumericField()", "ValuesListField()", etc.).  This way Estrelica.Core knows which editor type is relevant
                    // for the field:

                    // INumericValueEdit likewise has a strongly-typed "Value" property which only accepts numbers
                    // (specifically, nullable decimals):
                    contentEdit.NumericField(licensedQuantityFieldName).Value = 3;

                    // IReferencedRecordsEdit doesn't have an atomic "Value" property like the others above.  Instead it provides
                    // Add, Remove, Set, etc. methods to maintain the collection of records that are referenced by the field:
                    contentEdit.SubformField(subformFieldId).Remove(newSubformContentId);

                    // And here we'll demonstrate how the IValuesListSelectionEdit throws an exception if we
                    // attempt to set a value that isn't valid for the field:
                    Assert.ThrowsException<ArgumentException>($"Confirming '{applicationTypeField.Name}' {applicationTypeField.FieldType} won't accept invalid values",
                        () =>
                         contentEdit.ValuesListField(applicationTypeFieldAlias).Set("Some bogus invalid value")
                    );

                    // Likewise if we try to do it using an invalid integer Id for the value
                    Assert.ThrowsException<ArgumentException>($"Confirming '{applicationTypeField.Name}' {applicationTypeField.FieldType} won't accept invalid values",
                        () =>
                        contentEdit.ValuesListField(applicationTypeFieldAlias).Set(9999999)
                    );

					// And again, if we try to do it using an IValuesListValue from a different ValuesList.
					// Here we'll take one from the ValuesList associated with the "Platform" field and try
					// to put it in the "Application Type" field where it does not belong.
					IArcherValuesListValue invalidVLV = level.Field<IValuesListField>("Platform").ValuesList.Values.First();

                    Assert.ThrowsException<ArgumentException>($"Confirming '{applicationTypeField.Name}' {applicationTypeField.FieldType} won't accept invalid values",
                        () =>
                        contentEdit.ValuesListField(applicationTypeFieldAlias).Set(invalidVLV)
                    );

                    // However, we can add new/unknown values to the underlying VL and set it on the field as well in a single call if we
                    // pass true in the "addUnknownValues" parameter (we'll delete this value later in the cleanup/finally section):
                    Assert.ThrowsNoException($"Confirming '{applicationTypeField.Name}' {applicationTypeField.FieldType} will accept unknown values if we pass 'addUnknownValues: true'",
                        () =>
                            contentEdit.ValuesListField(applicationTypeFieldAlias).Set("Some new unknown value", addUnknownValues: true)
                    );

                    // Remove the CrossReference/RelatedRecords values we inserted earlier, if any.
                    if (targetCrossRefContentId > 0)
                    {
                        contentEdit.Field(crossRefField).Remove(targetCrossRefContentId);
                    }

                    if (targetRelatedRecordsContentId > 0)
                    {
                        contentEdit.Field(relatedRecordsField).Remove(targetRelatedRecordsContentId);
                    }

					// Here we'll edit a new field directly by Id that wasn't involved in the previous insert
					INumericField activeUsageField = level.Field<INumericField>("Active Usage Quantity");

                    contentEdit.NumericField(activeUsageField.Id).Value = 1;

                    // Save the changes
                    int updatedContentId = contentEdit.SaveChanges();

                    // Confirm that we got the same contentId this time that we got when we performed
                    // the original insert:

                    Assert.AreEqual($"Updated content {updatedContentId}", contentId, updatedContentId);

                    // And now reload the record to confirm our edits

                    record = core.Content.GetContentById(contentId).ContentAccess(core);

					IContentUpdateInformation updateInfo = record.UpdateInformation;
                    // Update Date should now be greater than Create Date, but the user Ids should still be the same
                    Assert.IsGreater("Verifying create/update dates", updateInfo.UpdateDate.Value, updateInfo.CreateDate.Value);
                    Assert.AreEqual("Verifying create user", updateInfo.CreateUser.Id, currentUser.Id);
                    Assert.AreEqual("Verifying update user", updateInfo.UpdateUser.Id, currentUser.Id);

                    Assert.AreEqual($"Verifying '{versionField.Name}' {nameField.FieldType} field",
                        "unknown", record.Value(versionField));

                    // The IArcherContentAccess.Value() method is overloaded for each of the specific field-type interfaces,
                    // and will return a specific data type for each depending on which interface is passed into it (e.g.
                    // string for ITextField, DateTime for IDateField, IUserGroupListSelection for IUserGroupListField,
                    // IValuesListSelection for IValuesListField, etc.).
                    // In this case we have a specific type reference (INumericField) for licensedQuantityField,
                    // so the result returned below will be a nullable decimal (the most encompassing numeric type available).
                    Assert.AreEqual($"Verifying '{licensedQuantityField.Name}' {licensedQuantityField.FieldType} field by INumericField reference",
                        3, record.Value(licensedQuantityField));

                    // Note that the IArcherContentAccess.Value() method is also overloaded to accept any of the standard
                    // field identifiers (i.e name/alias/guid/id).  Here we'll resolve one by Guid to demonstrate.
                    // However, since a simple identifier like this doesn't carry any field type information,
                    // the result will be returned as "dynamic".  Our expectation is that it's a numeric value, but
                    // this assertion will fail if we use the wrong Guid and the method returns something else (e.g. string,
                    // DateTime, etc.):
                    Assert.AreEqual($"Verifying '{activeUsageField.Name}' {activeUsageField.FieldType} field by Guid",
                        1, record.Value(activeUsageField.Guid));

                    // Since we know activeUsageField is a Numeric field, we can also use those same identifiers with the
                    // NumericValue<N>() method, specifying the value type we want via N (i.e. int, double, decimal, float, etc.)
                    Assert.AreEqual($"Verifying '{activeUsageField.Name}' {activeUsageField.FieldType} field by Guid cast to int",
                        1, record.NumericValue<int>(activeUsageField.Guid));

                    // The difference in the cases above is that using a field reference (e.g. an ITextField, an IValueListField, an
                    // IUserGroupListField, etc.) dictates the field's type *at compile time*, so the compiler knows what the returned
                    // value's data type will be (e.g. a string, a values list selection, a users/groups list selection).

                    // However, when retrieving field's .Value() by field name (or alias or guid or integer id), the field type cannot
                    // be determined at compile time, so the result in those cases is always "dynamic".  This is basically the same as
                    // System.Object, but the .NET compiler allows you to treat it as any data type you like in your code without
                    // complaint (however, you will see errors at runtime if you assumed the wrong type).  For the numeric field
                    // case, Estrelica.Core provides the .NumericValue<N>() override which handles the casting/conversion to the
                    // desired numeric type.  Similarly there's a .DateValue() override for IDateField/IFirstPublishedField/ILastUpdatedField
                    // and .TextValue() for ITextField when you don't have a field reference but instead only have their name/alias/guid/id.

                    // The name/alias/guid/id methods are provided simply for convenience however.  The best practice is to always use a
                    // field reference when calling .Value() since that guarantees type safety at compile time.





                    // There's a calculated field named "Unused Licenses" having the formula ([Licensed Quantity]) - ([Active Usage Quantity])
                    // so let's confirm that its value is as expected too.  Note that here we're just dereferencing the field's value
                    // by its field name, rather than using an explicit field reference as in the earlier examples.  The call to
                    // .Value() will therefore return "dynamic" as the type, but the compiler will allow us to pass that to
                    // the Assert.AreEqual<int>() method as though it were an integer.

                    Assert.AreEqual($"Verifying 'Unused Licenses' Numeric field",
                        2, record.Value("Unused Licenses"));

                    // Verify that any CrossReference/RelatedRecords fields we modified earlier are now empty

                    if (crossRefField != null)
                    {
                        Assert.AreEqual($"Verifying {crossRefField.FieldType} '{crossRefField.Name}' is now empty",
                            0, record.Value(crossRefField).Ids.Count());
                    }

                    if (relatedRecordsField != null)
                    {
                        Assert.AreEqual($"Verifying {relatedRecordsField.FieldType} '{relatedRecordsField.Name}' is now empty",
                            0, record.Value(relatedRecordsField).Ids.Count());
                    }

                    // Verify that the new VLV we added to the VL was set as the sole value on the applicationType field:
                    newlyAddedVLV = record.Value(applicationTypeField).Values.SingleOrDefault(vlv => vlv.Name == "Some new unknown value");

                    Assert.IsNotNull($"Verifying {applicationTypeField.FieldType} '{applicationTypeField.Name}' was set to a newly-added/previously-unknown value",
                        newlyAddedVLV);

                    Utilities.Log($"The record has been updated in the '{application.Name}' application.  This is your last chance to see it in Archer, as we will delete it in the next step.  Hit any key when you're ready to proceed.");
                    Utilities.Pause();
                } // end of "if (proceedWithInsert)"
            }
            catch (Exception ex)
            {
                Utilities.Log(ex);
                if (ex is AggregateException aggregateException &&
                    aggregateException.InnerException is Exception innerException &&
                    innerException.Message == "The value in the following field must be unique: Application Name")
                {
                    Utilities.Log(@"This exception suggests that you've run this demo in the past, but it did not successfully clean up after itself in the 'finally' block below.  
If so, please delete the existing 'Estrelica.Core' record from 'Applications' via the UI before proceeding.  
If not, and the existing 'Estrelica.Core' record is a valid production record in your environment, try substituting a different Name for this test record.", ConsoleColor.Yellow);
                }
            }
            finally
            {
                // Cleanup
                if (attachmentFilename != null)
                {
                    File.Delete(attachmentFilename);
                }

                if (tempFilename != null)
                {
                    File.Delete(tempFilename);
                }

                if (newSubformContentId > 0)
                {
                    Assert.IsTrue($"Deleted subform content id {newSubformContentId}",
                        core.Content.Delete(newSubformContentId));
                }

                if (contentId > 0)
                {
                    // Delete the record.  This call will return true if the delete operation was successful.
                    bool deleted = core.Content.Delete(contentId);

                    Assert.IsTrue($"Deleted content id {contentId}", deleted);

                    Utilities.Log($"Now that record we just created and edited ({contentId}) has been deleted from Archer, attempting to reload it at this point should result in either null (if treatResourceNotFoundAsNull = true, the default) or an InvalidOperationException (if treatResourceNotFoundAsNull = false)", ConsoleColor.Cyan);

                    Assert.IsNull(
                        $"Confirming that an attempt to load the deleted content Id {contentId} with treatResourceNotFoundAsNull: true returns null",
                        // Archer will return a "resource not found" error for this call.  Passing treatResourceNotFoundAsNull = true will cause
                        // Estrelica.Core to simply return a null result in that case.
                        core.Content.GetContentById(contentId, treatResourceNotFoundAsNull: true));

                    Assert.ThrowsException<InvalidOperationException>(
                        $"Confirming that an attempt to load the deleted content Id {contentId} with treatResourceNotFoundAsNull: false throws an InvalidOperationException",
                        // Passing treatResourceNotFoundAsNull = false will cause Estrelica.Core to raise an InvalidOperationException for the error returned by Archer.
                        // If the debugger stops here, that's a good thing.
                        () => core.Content.GetContentById(contentId, treatResourceNotFoundAsNull: false));
                }

                // Since the VLV we added was attached to the content record we created/updated, we can't remove the VLV until 
                // that record has been deleted above, so we'll do it now
                if (newlyAddedVLV != null)
                {
                    Assert.IsTrue($"Deleted newly-added Values List Value: '{newlyAddedVLV.Name}'",
                        core.Metadata.DeleteValuesListValue(newlyAddedVLV));
                }

            }
        }

	}
}
