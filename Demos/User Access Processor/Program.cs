using System;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Estrelica;
using Estrelica.Interfaces;
using Estrelica.Logging;
using Estrelica.Utility;
using Estrelica.Archer.Content;
using Estrelica.Archer.AccessControl;
using Estrelica.Archer.Metadata.Field;

namespace UserAccessProcessor
{
	class Program
	{
		/// <summary>
		/// UserAccessProcessor is a command-line utility which will process and fulfill requests that have been approved in 
		/// the "User Access Request" application.  This application may be installed in Archer via the package 
		/// User_Access_Request_Package.zip included with this project.  The minimum version for this package is Archer 6.7.
		/// 
		/// For details see https://castlehillsoftware.github.io/Estrelica.Core.Demos/articles/UserAccessProcessor.html
		/// 
		/// The "User Access Request" application allows users to request modification of access permissions
		/// for themselves or another user.  These modifications are implemented via Estrelica.Core's ability to add or remove
		/// group membership for a user.
		/// 
		/// To submit a request:
		/// 
		///   1. Create a new "User Access Request" record.
		/// 
		///   2. Select the "Request Type" to be performed ("Add Access" and/or "Remove Access").
		/// 
		///   3. Add "Request Details" describing the actions to be performed.
		/// 
		///   4. Select the "Impacted User" whose group membership you'd like to modify.
		///   
		///   5. Select the "Manager" who is responsible for approving this request.
		/// 
		///   5. Select the "Groups to Add" and/or "Groups to Remove" as appropriate for the selected request type.
		/// 
		///   6. Save the record to enroll it in workflow, then choose "Submit Request" from the "Actions" drop-down.
		/// 
		/// Once the request has been submitted, it's ready for review and approval (by the "Manager" you've selected).  In order to
		/// approve the request, view the record as the "Manager" (or as a system administrator) and select "Approve Request" 
		/// from the "Actions" drop-down.
		/// 
		/// At this point the "Request Status" will change to "Approved - Not Implemented".  The request will be 
		/// fulfilled by this utility (i.e. the UserAccessProcessor) the next time it executes.  See the code
		/// below for the specific actions that will occur.
		/// 
		/// In order to configure the UAP utility to work with your Archer environment, you must provide authentication
		/// information via appSettings.json and/or a local user secrets file (ID: Estrelica.Core.Demo).  See the documentation
		/// at https://castlehillsoftware.github.io/Estrelica.Core.Demos/articles/manage_configuration.html
		/// for more information on managing your Estrelica.Core configuration for this demo.
		/// 
		/// The Archer user account used for running the utility must have at least read and update permissions to the 
		/// User Access Request application.
		/// 
		/// Ideally the UAP utility should be configured (e.g. via Windows Task Scheduler) to run unattended on a periodic basis.
		/// 
		/// All activity will be logged to a local text file (UserAccessProcessor_{timestamp}.log) in the executing directory
		/// (so you must ensure that the utility runs under a user context with appropriate write permission to that folder).
		/// Activity for each processed request will also be written to the request records themselves (via the "Processor Activity"
		/// text field) in Archer.
		/// 
		/// </summary>
		/// <param name="args"></param>

		static void Main(string[] args)
		{
			try
			{
				FileLogger.Subscribe();

				var core = CoreConfig.Load(
					w => Logger.Log(w.Message, LogLevel.Warning),

					// The configuration under which CoreConfig will instantiate the Core is defined via JSON files.
					// This requires that you modify the file at
					//		..\..\..\..\..\Estrelica.Demo.Common\appSettings.json (i.e. in the Estrelica.Demo.Common project)
					// and/or a local user secrets file at
					//		%appdata%\Microsoft\UserSecrets\Estrelica.Core.Demo\secrets.json
					// with your CastleHill Software authentication key and your Archer instance details and credentials.

					// See https://castlehillsoftware.github.io/Estrelica.Core.Demos/articles/manage_configuration.html
					// for more information on managing your configuration.

					// The user account that you specify for this configuration must have, at minimum, read and update access to the 
					// "User Access Request" application.

					// "appConfigFilename" specifies a JSON app settings file where your configuration is stored.  If not
					// explicitly provided this will default to "appSettings.json" in the current executing directory.
					// The string below will direct it to use the common appSettings.json file found in the Estrelica.Demo.Common project.
					appConfigFilename: @"..\..\..\..\..\Estrelica.Demo.Common\appSettings.json",

					// "userSecretsId" specifies the (optional) Id of a JSON user secrets file on the local machine containing values
					// which should override the corresponding values in the app settings file (above).  If not explicitly provided, none of the values
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
					configOverrideKey: null); // If you've configured valid override settings via the app settings and/or user secrets file,
											  // specify that override key here.

				// Identify the UAR app and the fields we need
				string uarApplicationName = "User Access Request";
				string requestTypeFieldName = "Request Type";
				string impactedUserFieldName = "Impacted User";
				string groupsToAddFieldName = "Groups to Add";
				string groupsToRemoveFieldName = "Groups to Remove";
				string implementedFieldName = "Implemented?";
				string activityLogFieldName = "Processor Activity";

				// Identify the Contacts app and User field
				//string contactsApplicationName = "Contacts";
				//string contactsUserFieldName = "RSA Archer User Account";

				// Get the application/level/field references we need for the UAR application
				var uarLevel = core.Metadata.ApplicationByName(uarApplicationName).Level(null); // <-- Returns the first level.  We know the UAR app only has one level so the name doesn't matter here.

				var requestTypeField = uarLevel.Field<IValuesListField>(requestTypeFieldName);
				var impactedUserField = uarLevel.Field<IUserGroupListField>(impactedUserFieldName);
				var groupsToAddField = uarLevel.Field<IUserGroupListField>(groupsToAddFieldName);
				var groupsToRemoveField = uarLevel.Field<IUserGroupListField>(groupsToRemoveFieldName);
				var implementedField = uarLevel.Field<IValuesListField>(implementedFieldName);
				var activityLogField = uarLevel.Field<ITextField>(activityLogFieldName);

				//var contactsLevel = core.Metadata.ApplicationByName(contactsApplicationName).Level(null);
				//var contactsUserField = contactsLevel.Fields.ByName<IUserGroupListField>(contactsUserFieldName);

				// First we'll load up all users from the Contacts application into a dictionary so we can
				// easily find each user via xref from UAR later
				//Dictionary<int, IArcherUser> contactLookup = new Dictionary<int, IArcherUser>();
				//foreach (var record in core.Content.GetContent(level: contactsLevel, includeFieldCallback: f => f == contactsUserField)
				//	.ContentAccess(core))
				//{
				//	// We expect each contact record to have exactly 1 user in the "RSA Archer User Account"
				//	// field, but there may be some where it isn't populated.  We'll log an error if
				//	// we encounter any of these cases below, while processing the open UAR requests.
				//	contactLookup[record.Id] = record.Value(contactsUserField).Users.FirstOrDefault();
				//}

				int addCount = 0;
				int removeCount = 0;
				int recordCount = 0;

				StringBuilder actions = new StringBuilder();

				Action<string> log = m =>
				{
					Logger.Log(m);
					actions.Append("<p><b>")
						.Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm"))
						.Append("</b>: ")
						.Append(m)
						.Append("</p>");
				};

				// Now search through the UAR records...
				foreach (var record in core.Content.GetContent(
						level: uarLevel,
						// for any having "Implemented?" == "No"...
						filterConditions: new XElement[] { implementedField.CreateCondition(ValuesOperator.Equals, "No") },
						// returning just the six fields we need for each
						includeFieldCallback: f => f == requestTypeField || f == implementedField || f == impactedUserField ||
												   f == groupsToAddField || f == groupsToRemoveField || f == activityLogField)
					
					// and convert the resulting xml into IArcherContentAccess objects so they're easier to work with
					.ContentAccess(core))
				{
					Logger.Log("Processing record " + record.Id);

					bool success = true; // assume success (meaning we'll update "Implemented?" to "Yes") unless an exception occurs in this block
					try
					{
						recordCount++;
						actions.Clear();

						string priorActivity = record.Value(activityLogField);
						if (!String.IsNullOrWhiteSpace(priorActivity))
						{
							// If we're reprocessing a record that has failed (or has logged activity for some other reason),
							// preserve that activity so we can write it back when finished.
							actions.Append(priorActivity);
						}

						// We found a UAR that's not implemented, so let's find the linked Contacts record in order to identify the Archer user

						// We expect at most one record in the Contacts xref, so just grab the first,
						// or 0 (default) if the xref field is empty...
						//int contactsXrefId = record.Value(impactedUserField).FirstOrDefault();

						// We expect to find exactly one Archer user in the "Impacted User" field, so take the first selection found there
						var archerUser = record.Value(impactedUserField).Users.FirstOrDefault()?
							// and put the IArcherUser object in edit mode so we can modify its group membership below
							.ForEdit();

						if (archerUser == null)
						{
							// "Impacted User" is a required field, so we should not be able to get here without finding one,
							// so we'll throw an exception if not:
							throw new InvalidOperationException($"Record has no '{impactedUserFieldName}' selected");
						}
						else
						{
							bool groupsChanged = false;

							//// Find the user associated with the xreferenced Contacts record in our lookup
							//var archerUser = contactLookup.ValueOrDefault(contactsXrefId)?
							//	// ...and put the user in edit mode so we can update its groups below
							//	.ForEdit();

							//// Make sure we actually got a user, and if so, confirm/add/remove the groups for that user as dictated by the request
							//if (archerUser == null)
							//{
							//	log("Contacts record has no associated Archer User");
							//}
							//else
							//{
								var requestTypes = record.Value(requestTypeField);

								log($"Processing request type(s): '{requestTypes.Values.Select(v => v.Name).Conjoin("', '")}'");

								if (requestTypes.Contains("Add Access"))
								{

									var groupsToAdd = record.Value(groupsToAddField).Groups;
									if (groupsToAdd.Count() == 0)
									{
										log($"User '{archerUser.Name}' has not been requested for add to any groups");
									}
									else
									{
										foreach (var group in groupsToAdd)
										{
											if (group.EveryoneFlag || archerUser.GroupIds.Contains(group.Id))
											{
												log($"User '{archerUser.Name}' is already in group '{group.Name}'");
											}
											else
											{
												log($"Adding user '{archerUser.Name}' to group '{group.Name}'");
												archerUser.GroupIds.Add(group.Id);
												groupsChanged = true;
												addCount++;
											}
										}
									}
								}

								if (requestTypes.Contains("Remove Access"))
								{
									var groupsToRemove = record.Value(groupsToRemoveField).Groups;
									if (groupsToRemove.Count() == 0)
									{
										log($"User '{archerUser.Name}' has not been requested for removal from any groups");
									}
									else
									{
										foreach (var group in groupsToRemove)
										{
											if (group.EveryoneFlag)
											{
												// This is an invalid use case, so note that we're going to ignore the request...
												log($"Unable to remove user '{archerUser.Name}' from the '{group.Name}' group");
											}
											else if (!archerUser.GroupIds.Contains(group.Id))
											{
												log($"User '{archerUser.Name}' is not in group '{group.Name}'"); //, LogLevel.Warning);
											}
											else
											{
												log($"Removing user '{archerUser.Name}' from group '{group.Name}'");
												archerUser.GroupIds.Remove(group.Id);
												groupsChanged = true;
												removeCount++;
											}
										}
									}
								}
							//}

							if (groupsChanged)
							{
								// If we added or removed any groups, update the user in Archer
								core.Access.Update(archerUser);
							}
							else
							{
								log("No changes were made to the requested user's group membership");
							}
						}
					}
					catch(Exception ex)
					{
						log(ex.ToString());
						success = false; // something went wrong, so we'll leave "Implemented?" unchanged so it will get retried on next execution
					}

					// We've finished processing this record (or encountered an exception while attempting to)
					// so put the record in edit mode so we can update its "implemented" status and/or log what we did.

					var recordEdit = record.ForEdit();

					if (success)
					{
						// Set "Implemented?" to "Yes" to indicate that we've handled the request.
						recordEdit.Field(implementedField).Set("Yes");
					}

					// and log whatever happened on the activityLogField (regardless of success, so we'll capture exception info here
					// if something went wrong)
					recordEdit.Field(activityLogField).Value = actions.ToString();

					// Post the updated record back to Archer
					core.Content.Update(recordEdit);
				}

				log($"Added {addCount} group(s) and removed {removeCount} group(s) from {recordCount} User Access Request(s)");

			}
			catch (Exception ex)
			{
				Logger.Log(ex.Message, LogLevel.Error);
			}
		}
	}
}
