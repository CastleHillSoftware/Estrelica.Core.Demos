using System;
using System.Collections.Generic;
using System.Linq;
using Estrelica;
using Estrelica.Demo;
using Estrelica.Archer.Entity;
using Estrelica.Archer.AccessControl;
using Estrelica.Archer.Content;
using Estrelica.Archer.Metadata;
using Estrelica.Archer.Metadata.Field;
using Estrelica.Utility;
using Estrelica.Interfaces;

namespace CHUM
{
    class Program
    {
        /// <summary>
        /// The CHUM (CastleHill User Management) Tool provides a simple way of managing group membership for users.
        /// https://nuget.castlehillsoftware.com/Estrelica.Core/documentation/articles/CHUM_tool.html
        /// 
        /// It uses three User/Groups List fields from the Contacts application, one of which identifies the Archer User associated 
        /// with that Contact record, and two others which define the Archer Groups that user should (the "Include in Groups"
        /// field) and should not (the "Exclude from Groups" field) be a member of.
        /// 
        /// The first of these fields ("RSA Archer User Account") exists by default in the standard Archer Contacts application.
        /// 
        /// The other two will need to be created before you run this the first time.  They should be:
        ///    a) simple User/Groups List fields (not Record Permissions)
        ///    b) given the names "Include in Groups" and "Exclude from Groups", respectively (if you elect to use different
        ///       names, make sure you change them in the two lines where they are referenced below as well)
        ///    c) configured to allow multiple selections (i.e. not "Dropdown" or "Radio Buttons") with no maximum selection
        ///    d) configured via "Field Population" to allow only groups ("All Groups") to be selected (user selections are 
        ///       not applicable for these fields)
        ///    e) added to the layout
        /// 
        /// You can then edit a given user's Contact record, select via these two fields the groups that the user
        /// should be included in and/or excluded from, then run this application.  The app will modify the user's group 
        /// membership, if necessary, to ensure that the user is included in the first set of groups and excluded from the 
        /// second.
        /// 
        /// (For a more robust approach to solve the same problem, involving a new ODA, an approval process implemented via 
        /// Advanced Workflow, and process activity logging, please see the UserAccessProcessor demo.)
        /// 
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            try
            {
                // Instantiate Estrelica.Core and authenticate with Archer...

                var core = CoreConfig.Load(
                        w => Utilities.Log(w.Message, LogLevel.Warning),

                        // The configuration under which CoreConfig will instantiate the Core is defined via JSON files.
                        // This requires that you modify the file at
                        //		..\..\..\..\..\Estrelica.Demo.Common\appSettingsSample.json (i.e. in the Estrelica.Demo.Common project)
                        // and/or a local user secrets file at
                        //		%appdata%\Microsoft\UserSecrets\Estrelica.Core.Demo\secrets.json
                        // with your CastleHill Software authentication key and your Archer instance details and credentials.

                        // See https://nuget.castlehillsoftware.com/Estrelica.Core/documentation/articles/manage_configuration.html
                        // for more information on managing your configuration.

                        // The user account that you specify for this configuration must have, at minimum, read access to the 
                        // "Contacts" application in order to identify the users and groups to be modified, as well as update permissions
                        // to the groups involved.


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

                        // "configInstanceName" specifies a particular instance configuration to be selected from the file(s) above.
                        // If not explicitly specified *or* if the app settings/user secrets files have nothing configured for this instance
                        // name, the default (base) Archer configuration will be used.
                        configOverrideKey: null); // If you've configured valid override settings via the app settings and/or user secrets file,
                                                  // specify that override key here.


                // If the Contacts app doesn't exist or the current user doesn't have permission to it, an exception will 
                // be thrown by this call.
                IArcherApplication contactsApplication = core.Metadata.ApplicationByName("Contacts");

                // Here we'll identify the Contacts fields that will contain
                //    a) the Archer User for each Contact record,
                //    b) the Archer Groups that the user SHOULD be a member of, and
                //    c) the Archer Groups that the user should NOT be a member of.

                var rsaArcherUserAccountField = contactsApplication.Fields.ByName<IUserGroupListField>("RSA Archer User Account");
                var includeGroupsField = contactsApplication.Fields.ByName<IUserGroupListField>("Include in Groups");
                var excludeGroupsField = contactsApplication.Fields.ByName<IUserGroupListField>("Exclude from Groups");

                // Next we'll use the Core.Content.GetContent() method to iterate through all the records in Contacts
                // and find the users and the groups that they should be included in/excluded from.

                // recordCallback will be invoked periodically by the Content resolver as it fetches records from Archer,
                // in order to provide metrics about what is happening as it retrieves results.  Records are returned in a paged
                // fashion, and with each page this method will be invoked one or more times to allow the calling process
                // to keep track of the resolver's progress.  The RecordCountType enumerable indicates what information
                // is being transmitted by each call:
                //  RecordCountType.Expected: The c value indicates the total number of records that Archer says it will 
                //    eventually return for this search.  (Called by the resolver only once, when the first page is returned
                //    by Archer.)
                //  RecordCountType.Current: The c value represents the cumulative number of records that have been returned 
                //    so far, for use in updating a progress bar, etc.  (Called by the resolver for each page returned 
                //    by Archer.)
                //  RecordCountType.Total: The c value represents the final count of all records returned for the search, as
                //    calculated by the resolver by counting the records returned by Archer on all pages.  (Called once by the 
                //    resolver after all pages have been returned.)  This number SHOULD match the RecordCountType.Expected
                //    number returned earlier.  If it does not, it may indicate that Archer's search index has become 
                //    corrupted and needs to be rebuilt.  It could also mean that records were inserted or deleted while
                //    the search was in progress.
                int expectedRecordCount = 0;
                int totalRecordCount = 0;
                Action<RecordCountType, int> recordCallback = (t, c) =>
                {
                    switch (t)
                    {
                        case RecordCountType.Expected: expectedRecordCount = c; break;
                        case RecordCountType.Current: Utilities.Log($"Loaded {c} records of {expectedRecordCount}..."); break;
                        case RecordCountType.Total: totalRecordCount = c; break;
                    }
                };

                Dictionary<int, HashSet<int>> addUsersToGroups = new Dictionary<int, HashSet<int>>();
                Dictionary<int, HashSet<int>> removeUsersFromGroups = new Dictionary<int, HashSet<int>>();

                // Iterate through the users referenced in all the "Contacts" records and make sure those users are members
                // of the "include" groups, and remove them from membership in any of the "exclude" groups.
                foreach (IArcherContentAccess record in core.Content.GetContent(
                    contactsApplication, // get records from the Contacts application
                    null, // no specific level (Contacts only has one level so this doesn't really matter)
                    recordCallback, // the resolver will call this method periodically to let us know about its progress                    
                    includeFieldCallback: f => // tell the search engine to return only the three fields we're interested in
                        f == rsaArcherUserAccountField || f == includeGroupsField || f == excludeGroupsField
                    ).ContentAccess(core))
                {
                    // Get the Archer User Id for this contact record (from the rsaArcherUserAccount field).  It should have exactly one user in
                    // it, so we'll just take the first.  If the record has no user in this field, contactUserId will be 0.
                    int userUserId = record.Value<IUserGroupListSelection>(rsaArcherUserAccountField).UserIds.FirstOrDefault();

                    if (userUserId > 0)
                    {
                        // Get all groups that the user is currently in 
                        IEnumerable<int> userGroupIds = core.Access.GroupIdsForUser(userUserId);

                        // And all the groups that the contact record's includeGroupsField says the user SHOULD be in.
                        IEnumerable<int> requiredGroupIds = record.Value<IUserGroupListSelection>(includeGroupsField).GroupIds;
                        // As well as all the groups that the excludeGroupsField says the user should NOT be in.
                        IEnumerable<int> disallowedGroupIds = record.Value<IUserGroupListSelection>(excludeGroupsField).GroupIds;

                        // Identify the groups that the user is in, but should not be, per the groups indicated by "disallowedGroupIds".
                        foreach (int groupId in userGroupIds.Where(id => disallowedGroupIds.Contains(id)))
                        {
                            // The user is in a group that they should not be in, so we'll capture the relationship for removal
                            removeUsersFromGroups.ValueOrCreate(groupId).Add(userUserId);
                        }
                        // And likewise, any groups that the user is not in, but should be, per the groups indicated by "requiredGroupIds".
                        foreach (int groupId in requiredGroupIds.Where(id => !userGroupIds.Contains(id)))
                        {
                            // The user is NOT in a group that they should be in, so we'll capture the relationship for adding
                            addUsersToGroups.ValueOrCreate(groupId).Add(userUserId);
                        }
                    }
                }
                if (totalRecordCount == 0)
                {
                    Utilities.Log("No records were found in Contacts");
                }
                else
                {
                    // We now have two dictionaries (addUsersToGroups and removeUsersFromGroups) where the key is a group Id
                    // and the value is an IEnumerable<int> of userIds that are to be added or removed from that group.
                    // Here we'll union all the keys from both dictionaries to identify all the groups that need to be updated
                    // (i.e. having users added, removed, or both).
                    IEnumerable<int> groupIdsToUpdate = addUsersToGroups.Keys.Union(removeUsersFromGroups.Keys).Distinct();

                    if (groupIdsToUpdate.Count() == 0)
                    {
                        Utilities.Log("All groups are current with the rules defined in Contacts");
                    }
                    else
                    {
                        // Update the groups that were identified above as needing changes, and add/remove users as necessary
                        foreach (int groupId in groupIdsToUpdate)
                        {
                            // Retrieve the group and put it in edit mode
                            var tempGroup = core.Access.GroupById(groupId).ForEdit();
                            // Add any users that exist in the "add" dictionary, and remove any found in the "remove" dictionary
                            tempGroup.UserIds.AddAll(addUsersToGroups.ValueOrDefault(groupId));
                            tempGroup.UserIds.RemoveAll(removeUsersFromGroups.ValueOrDefault(groupId));
                            // And finally, call Update on the group to push the changes back to Archer...
                            core.Access.Update(tempGroup);
                        }
                        Utilities.Log($"Updated {groupIdsToUpdate.Count()} groups with {addUsersToGroups.Sum(d => d.Value.Count())} additions and {removeUsersFromGroups.Sum(d => d.Value.Count())} removals");
                    }
                }
            }
            catch (Exception ex)
            {
                Utilities.Log(ex.ToString(), Estrelica.Interfaces.LogLevel.Error);
            }
        }

    }
}
