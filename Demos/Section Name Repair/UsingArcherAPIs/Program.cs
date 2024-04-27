using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SimpleArcherAPIExample
{
	class Program
	{
		/// <summary>
		///  This demonstrates the implementation of a simple task (see README.md for requirements), programmed via direct calls to the Archer API(s).
		/// </summary>
		static async Task Main(string[] args)
		{
			try
			{
				// These are the REST API methods we're going to call, copied from the RSA Archer REST API documentation:
				// POST	http://RsaArcher/platformapi/core/security/login   <- To establish a session
				// POST	http://rsaarcher/platformapi/core/system/application  <- To load all application metadata and find the "Policies" application
				// POST http://rsaarcher/platformapi/core/system/level/module/*moduleid*  <- To load the "Policies" metadata for all levels and find the "Section" level
				// POST http://rsaarcher/platformapi/core/system/fielddefinition/level/*levelid*  <- To load the "Section" fields metadata and identify the "Section Name" text field
				// PUT http://rsaarcher/platformapi/core/content <- To persist changed content

				// We'll also make one SOAP call to "ExecuteSearch" at /ws/search.asmx (to retrieve the "Section" records matching our criteria)

				// As a point of interest, note how the "fielddefinition/application" method (which returns all fields from all levels in the application, 
				// i.e. GET http://localhost/rsaarcher/platformapi/core/system/fielddefinition/application/*applicationid*) differs
				// from the "fielddefinition/level" method above (which returns only the fields from a specific application level).
				// Notably one requires a POST while the other requires a GET, even though they're subtle variations on the same operation.
				// Note also that documentation's URL example is inconsistent (the latter having "localhost").
				// Inconsistencies like this are typical of the Archer API and its documentation, and learning how to deal with these
				// variations only comes through experience.  Estrelica.Core hides the details of making API calls, resolves these inconsistencies
				// and makes everything work the way you would expect out of the box.

				#region Initialization

				// We'll get our connection details and credentials using the same basic approach that the Estrelica.Core.Configuration
				// loader uses, except we'll only reference the top-most "Archer" definition -- we won't attempt to support overrides.
				var config = new ConfigurationBuilder()
					.AddJsonFile(@"..\..\..\..\..\..\Estrelica.Demo.Common\appSettings.json", optional: true)
					.AddUserSecrets("Estrelica.Core.Demo")
					.Build()
					.GetSection("Archer");

				// Create variables for the parameters needed to authenticate with Archer
				string archerUrl = config.GetSection("url").Value;
				string archerInstance = config.GetSection("instance").Value;
				string archerUsername = config.GetSection("username").Value;
				string archerDomain = config.GetSection("domain").Value;
				string archerPassword = config.GetSection("password").Value;

				// And one to hold the session token resulting from that authentication.  This will be passed in all subsequent API calls to 
				// authorize each action.
				string sessionToken = null;
				
				#endregion

				#region Helper methods (these are the sorts of utility functions a developer would typically hand-craft to avoid copy/paste code reuse when working with Archer)
				// Helper methods to make the API calls

				HttpClient client = new HttpClient();

				// Note that some Archer API POST calls require an HTTP GET override while others do not, hence the forceGETOverride parameter.
				// Reference the Archer documentation to determine whether this parameter should be true or false for each call.
				Func<HttpMethod, string, string, bool, bool, Task<string>> getAPIResponse = async (method, requestDoc, requestBody, forceGETOverride, useRestAPI) =>
				{
					using (HttpRequestMessage request = new HttpRequestMessage(method, archerUrl + requestDoc))
					{
						if (useRestAPI && sessionToken != null)
						{
							request.Headers.TryAddWithoutValidation("Authorization", $"Archer session-id=\"{sessionToken}\"");
						}

						string contentType = useRestAPI ? "application/json" : "text/xml";
						request.Headers.TryAddWithoutValidation("Content-Type", contentType);
						request.Headers.TryAddWithoutValidation("Accept", contentType + ";q=0.9,*/*;q=0.8");

						if (forceGETOverride)
						{
							request.Headers.TryAddWithoutValidation("X-Http-Method-Override", "GET");
						}
						if (requestBody != null)
						{
							request.Content = new StringContent(requestBody, Encoding.UTF8, contentType);
						}
						var response = await client.SendAsync(request);
						response.EnsureSuccessStatusCode();
						return await response.Content.ReadAsStringAsync();
					}
				};

				Func<HttpMethod, string, object, bool, Task<string>> getRestAPIResponse = async (method, requestDoc, requestObject, forceGetOverride) =>
					await getAPIResponse(method, requestDoc, JsonConvert.SerializeObject(requestObject), forceGetOverride, true);

				Func<HttpMethod, string, XDocument, bool, Task<string>> getSoapAPIResponse = async (method, requestDoc, requestXml, forceGetOverride) =>
					await getAPIResponse(method, requestDoc, requestXml.ToString(), forceGetOverride, false);

				// Helper method to validate and extract the REST API (JSON) response content

				Func<JObject, JObject> unpackResult = (r) =>
				{
					if (r.Value<bool>("IsSuccessful"))
					{
						return r.Value<JObject>("RequestedObject");
					}
					else
					{
						throw new Exception(JsonConvert.SerializeObject(r.Value<JArray>("ValidationMessages").First()));
					}
				};

				// Helper methods to deserialize the JSON response content into usable objects

				Func<string, JObject> deserializeResult = (r) =>
				{
					return unpackResult(JsonConvert.DeserializeObject<JObject>(r));
				};

                Func<string, IEnumerable<JObject>> deserializeResults = (value) =>
                {
                    return JsonConvert.DeserializeObject<IEnumerable<JObject>>(value).Select(d => unpackResult(d));
                };

				#endregion

				#region Authenticate and retrieve metadata

				// Step 1: Establish a session with the Archer instance.  Note that this session may timeout or get invalidated by another login using the 
				// same credentials during the execution of a long-running application.  No attempt at recovery or retrying any failed API calls due to
				// such scenarios will be made here.  Estrelica.Core manages those situations and retries any affected API calls transparently.

				var response = await getRestAPIResponse(HttpMethod.Post, @"platformapi/core/security/login", 
					new { InstanceName = archerInstance, Username = archerUsername, UserDomain = archerDomain, Password = archerPassword }, false);

				var session = deserializeResult(response);
				sessionToken = session.Value<string>("SessionToken");

				// Set up some variables for the application/level/field we're looking for
				string applicationName = "Policies";
				string levelName = "Section";
				string sectionNameField = "Section Name";

				// and the Ids we'll capture for each
				int applicationId = 0;
				int levelId = 0;
				int sectionNameFieldId = 0;

				// Step 2: Find the "Policies" application

				// Retrieve all available Applications
				response = await getRestAPIResponse(HttpMethod.Post, @"platformapi/core/system/application", null, true);

                var applications = deserializeResults(response);

				// Find the returned dictionary where the "Name" key contains "Policies" and capture the integer value of its "Id" key
				foreach(var application in applications)
				{
					if (application.Value<string>("Name") == applicationName)
					{
						applicationId = application.Value<int>("Id");
						break;
					}
				}

				// If no matching Application was found, raise an exception
                if (applicationId == 0)
                {
                    throw new ArgumentException($"Could not find application named '{applicationName}'");
                }

				// Step 3: Find the "Section" level of the "Policies" application

				// Retrieve all Levels in "Policies"
				response = await getRestAPIResponse(HttpMethod.Post, @"platformapi/core/system/level/module/" + applicationId, null, true);

				var levels = deserializeResults(response);

				// Find the returned dictionary where the "Name" key contains "Section" and capture the integer value of its "Id" key
				foreach(var level in levels)
				{
					if (level.Value<string>("Name") == levelName)
					{
						levelId = level.Value<int>("Id");
						break;
					}
				}

				// If no matching Level was found, raise an exception
				if (levelId == 0)
				{
					throw new ArgumentException($"Could not find level named '{levelName}'");
				}

				// Step 4: Load the fields from the Section level and find the Text field "Section Name"

				response = await getRestAPIResponse(HttpMethod.Post, @"platformapi/core/system/fielddefinition/level/" + levelId, null, true);
				var fields = deserializeResults(response);

				// Find the "Section Name" Text field
				foreach (var field in fields)
				{
					if (field.Value<int>("Type") == 1 && // <- Field Type Id for Text field (see Archer documentation for all possible integer Field Type identifiers)
						field.Value<string>("Name") == sectionNameField)
					{
						sectionNameFieldId = field.Value<int>("Id");
						break;
					}
				}

				// If no matching field was found, raise an exception
				if (sectionNameFieldId == 0)
				{
					throw new ArgumentException($"Could not find a Text field named '{sectionNameField}'");
				}

				#endregion

				#region Build search criteria

				// Step 5: Execute a search against the Section level in Policies to find all records having a misspelled name (as
				// communicated in the requirements) and update any misspellings.

				// This requires three significant sub-steps:
				// 1. Create the SearchOptions XML needed to identify the records we want
				// 2. Call the Archer SOAP API "ExecuteSearch" endpoint, iterating as necessary to retrieve as many result pages as Archer returns
				// 3. Evaluate the search results returned on each page to determine the current value of the "Section Name"
				// 4. If the name contains "Controll" but not "Controlling" or "Controller", fix the misspelling and save the change in Archer

				// Step 5.1: Create the SearchReport XML.  Be sure to keep the Archer SOAP API documentation handy for this step.

				var searchReport = XDocument.Parse("<SearchReport/>");

				// We're going to need this Id as a string quite a lot in the code that follows, so let's just call .ToString() once
				var sectionNameFieldIdString = sectionNameFieldId.ToString();

				// Build the DisplayFields node to tell Archer which field(s) we want returned.  In this case it's just the "Section Name" field.
				var displayFieldsNode = new XElement("DisplayFields");
				var displayFieldNode = new XElement("DisplayField");
				displayFieldNode.Value = sectionNameFieldIdString;
				displayFieldsNode.Add(displayFieldNode);
				searchReport.Root.Add(displayFieldsNode);

				// Archer's search returns results in pages, and we can adjust the page size as needed to handle bandwidth/memory/timeout
				// constraints.  Estrelica.Core manages this automatically, but we need to specify a value in this case.  Here we'll use 100
				// as the page size in hopes that it will fit those criteria (we could probably go even higher since we're only returning one
				// field per record).
				var pageSizeNode = new XElement("PageSize");
				pageSizeNode.Value = "100";
				searchReport.Root.Add(pageSizeNode);

				// Now we'll add criteria for our search
				var criteriaNode = new XElement("Criteria");
				searchReport.Root.Add(criteriaNode);

				// Add a "ModuleCriteria" node to identify the module we want to search
				var moduleCriteriaNode = new XElement("ModuleCriteria");
				criteriaNode.Add(moduleCriteriaNode);
				var moduleNode = new XElement("Module");
				moduleNode.Value = applicationId.ToString();
				moduleCriteriaNode.Add(moduleNode);

				// Add a Filter condition to only return those records having "Controll" in the "Section Name"
				var filterNode = new XElement("Filter");
				var conditionsNode = new XElement("Conditions");
				var textFilterConditionNode = new XElement("TextFilterCondition");
				var filterFieldNode = new XElement("Field");
				filterFieldNode.Value = sectionNameFieldIdString;
				var operatorNode = new XElement("Operator");
				operatorNode.Value = "Contains"; // lots of options here, specific by field type, and easy to get wrong.  Keep that documentation handy.
				var valueNode = new XElement("Value");
				valueNode.Value = "Controll";

				textFilterConditionNode.Add(filterFieldNode);
				textFilterConditionNode.Add(operatorNode);
				textFilterConditionNode.Add(valueNode);
				conditionsNode.Add(textFilterConditionNode);
				filterNode.Add(conditionsNode);
				criteriaNode.Add(filterNode);

				// One thing that's often overlooked is the Sort order.  By default (if no "SortFields" are specified), Archer will
				// return records sorted by the Key field (if one is defined).

				// Each time a page is requested, the search engine effectively re-executes the entire search and returns a slice
				// representing the requested page.  This means that if the Key field is modified on a given record while the search
				// is in progress with no sort order specified, it will cause records to "jump around" in the search results, leading
				// to some records being skipped and others to be returned multiple times on different pages.

				// This happens to be exactly the case here.  We're potentially going to modify "Section Name", which is the Key
				// field in the "Section" level.  Therefore, to "lock down" the order of records and ensure that they cannot
				// move within the results, we'll sort on one of the non-modifiable fields like "Tracking Id" or "First Published".
				// This will allow us to change any field values in a given record without affecting its position in the search results.

				// This is another of those gotchas that you only learn through experience, but which Estrelica.Core handles implicitly.

				int sortFieldId = 0;
				foreach(var field in fields)
				{
					int fieldTypeId = field.Value<int>("Type");
					if (fieldTypeId == 6 /* Tracking ID */ || fieldTypeId == 21 /* First Published Date */)
					{
						sortFieldId = field.Value<int>("Id");
						break;
					}
				}

				// If we found a Tracking Id or First Published field, use that for the sort.  Otherwise we'll take our chances.
				if (sortFieldId > 0)
				{
					var sortFields = new XElement("SortFields");
					var sortField = new XElement("SortField");
					var sortFieldIdNode = new XElement("Field");
					sortFieldIdNode.Value = sortFieldId.ToString();

					var sortType = new XElement("SortType");
					sortType.Value = "Ascending";
					sortFields.Add(sortField);
					sortField.Add(sortFieldIdNode);
					sortField.Add(sortType);
					moduleCriteriaNode.Add(sortFields);
				}

				#endregion

				#region Execute the search and update any affected records

				// Now we're ready to perform the search.  For this we need to use the SOAP API "ExecuteSearch" command at /ws/search.asmx.
				// We'll start at page 1 and keep calling it with incremental page numbers until no further results are returned.
				// The body of this request is complicated, full of namespaces, but relatively static, so rather than building it node by node
				// as above, we'll use a string template to get things started:

				var soapBody = XDocument.Parse("<?xml version=\"1.0\" encoding=\"utf - 8\"?>" +
					"<soap:Envelope xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" " +
					"xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\" ><soap:Body>" +
					"<ExecuteSearch xmlns=\"http://archer-tech.com/webservices/\" >" +
					"<sessionToken>" + sessionToken + "</sessionToken>" +
					"<searchOptions></searchOptions>" +
					"<pageNumber></pageNumber>" +
					"</ExecuteSearch></soap:Body></soap:Envelope>");

				// The two things we'll need to change are the <searchOptions> (inserting the SearchReport we created above) and <pageNumber>
				// (starting at 1 and incrementing on each iteration), so let's grab those nodes here:
				XNamespace archerNS = "http://archer-tech.com/webservices/";
				var searchOptionsNode = soapBody.Descendants(archerNS + "searchOptions").First();
				var pageNumberNode = soapBody.Descendants(archerNS + "pageNumber").First();

				// Insert our search options (doing it this way will automatically XML-encode the <SearchReport>... string):
				searchOptionsNode.Value = searchReport.ToString();

				// Create a regex pattern to identify Section Names containing "Controll" but not "Controlling" or "Controller"
				var regexPattern = new Regex(@"Controll(?!(ing|er))", RegexOptions.Compiled);

				// And start iterating the pages
				int pageNumber = 1; // Note that the search begins at page 1.  If you try to request page 0 you'll get an empty result
									// which will appear to be the end of the search and you'll wonder why your code isn't working.
				bool moreContent = true; // This will become false when we stop receiving records from Archer

				while (moreContent)
				{
					pageNumberNode.Value = pageNumber.ToString();
					string result = await getSoapAPIResponse(HttpMethod.Post, @"ws/search.asmx", soapBody, false);

					XDocument soapResponse = XDocument.Parse(result);
					string searchResult = soapResponse.Descendants(archerNS + "ExecuteSearchResult").FirstOrDefault()?.Value;
					if (moreContent = searchResult != null)
					{
						var records = XDocument.Parse(searchResult).Element("Records")?.Descendants("Record");
						if (moreContent = records != null && records.Count() > 0)
						{
							foreach(var recordNode in records)
							{
								// Find the "Section Name" field in this doc
								var fieldNode = recordNode.Elements("Field").Where(f => f.Attribute("id")?.Value == sectionNameFieldIdString).FirstOrDefault();
								if (fieldNode != null)
								{
									var originalValue = fieldNode.Value;
									if (regexPattern.IsMatch(originalValue))
									{
										// Found a "Controll" misspelling that needs to be corrected.
										string newValue = regexPattern.Replace(originalValue, "Control");
										// Get the Id and build a new JSON object to push an update back to Archer
										int contentId = int.Parse(recordNode.Attribute("contentId")?.Value ?? "0");
										if (contentId == 0)
										{
											throw new InvalidOperationException("Could not identify content Id for node");
										}
										else
										{
											var fieldContents = new Dictionary<int, object>();
											fieldContents[sectionNameFieldId] = new { FieldId = sectionNameFieldId, Type = 1, Value = newValue };
											var updateRecord = new { Content = new { Id = contentId, LevelId = levelId, FieldContents = fieldContents } };
											var updateResult = await getRestAPIResponse(HttpMethod.Put, "platformapi/core/content/", updateRecord, false);
											var deserializedResult = deserializeResult(updateResult);
											// Confirm that we got the content Id returned by the API call
											int updatedId = deserializedResult.Value<int>("Id");
											if (updatedId != contentId)
											{
												throw new InvalidOperationException("API returned unexpected Id result: " + updatedId);
											}
											else
											{
												Console.WriteLine($"Updated record {contentId} 'Section Name' from '{originalValue}' to '{newValue}'");
											}
										}
									}
								}
							}
						}
					}
					// Move to the next page
					pageNumber++;
				}

				#endregion
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}
		}
	}
}
