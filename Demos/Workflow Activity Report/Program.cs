﻿using Estrelica.Archer.Content;
using Estrelica.Archer.Metadata;
using Estrelica.Archer.Metadata.Field;
using Estrelica.Utility;
using Microsoft.Extensions.Configuration;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace WorkflowActivityReport
{
    class Program
    {
        public class Target
        {
            public string ModuleName { get; set; }
            public ModuleType ModuleType { get; set; }
            public string StatusField { get; set; }
            public string HistoryLogField { get; set; }
        }

        static int Main(string[] args)
        {
            /* This real-world application was developed for a specific use case, processing custom ODAs and workflows that are not present 
             * in a standard Archer instance, so if you attempt to run it, it will almost certainly fail.  However, the code is presented
             * here to demonstrate how you can access content history via Estrelica.Core to evaluate field content history, 
             * signature audits, and Advanced Workflow audits. */


            /*  This application parses workflow and status field change events (tracked by a History Log field) occurring in the last x 
                calendar days in order to generate CSV reports indicating which user performed any workflow actions and what change (if any) 
                resulted to a particular VL field (e.g. the workflow status field) as a result of each workflow action.
            
                UTC dates are used for all date checking and output in order to eliminate ambiguity across systems that might reference
                the output.

                The content of the CSV will include fully-intact results for a selected number of past UTC day(s) via the "/days" command
                line switch.  For example, if used with "/days 1" (or no "/days" switch), the output will include all the activity for yesterday's
                UTC date.  If "/days 2" is passed, the output will include all activity for yesterday's UTC date and the prior day.  No output 
                from the current UTC day will be present in any case.
            
             */
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            try
            {
                // Allow the number of days to be evaluated to be specified via the command line, e.g. "/days 2" will retrieve the activity
                // from the last 2 days.  Defaults to 1 if not specified (or zero).  Also treats negative and positive numbers the same
                // (e.g. "/days 2" and "/days -2" have the same behavior).

                var options = new Arguments(args);
                int daysToRetrieve = Math.Max(1, Math.Abs(int.Parse(options.SwitchValue("days", false) ?? "1")));

                // Note that unlike the other demos, this application has its own appSettings.config file (since it needs additional
                // configuration for the Target applications/fields) rather than using the shared one from Estrelica.Demo.Common.
                // That's why we don't specify an appConfigFilename: parameter here, as CoreConfig will automatically find the local one.
                // This means you'll either need to update the local appSettings.config file with your Estrelica.Core configuration
                // or just make sure that it's all available in your Estrelica.Core.Demo user secrets file.
                // See https://castlehillsoftware.github.io/Estrelica.Core.Demos/articles/manage_configuration.html
                // for more information on managing your Estrelica.Core configuration, and particularly about the use of user secrets.

                var core = Estrelica.CoreConfig.Load(w => Console.WriteLine(w), configOverrideKey: options.SwitchValue("env", false));

                // Load the config and identify the applications/fields we need to process

                var config = new ConfigurationBuilder().AddJsonFile("appSettings.json").Build();

                var targets = config.GetSection("Targets").Get<Target[]>();

                foreach (var target in targets)
                {
                    var application = core.Metadata.ModuleByName(target.ModuleName, moduleType: target.ModuleType);
                    var level = application.Levels.First();
                    var hlField = level.Field<IHistoryLogField>(target.HistoryLogField);
                    var statusField = level.Field<IValuesListField>(target.StatusField);
                    var ludField = level.FieldsOfType<ILastUpdatedField>().First();

                    // These dates will be used to retrieve affected records from Archer, as well as to evaluate the workflow
                    // events found within the history of each record.  The latter will be non-inclusive, so workflow events
                    // occurring in today's UTC date will *not* be included.
                    var endDate = DateTime.UtcNow.Date;
                    var startDate = endDate.AddDays(0-daysToRetrieve);

                    var sb = new StringBuilder();
                    sb.AppendLine("ContentId,Action Type,Action Taken,Original Workflow Status,Target Workflow Status,Name,Username,Modified On Date");

                    Console.WriteLine($"Processing workflow events for '{application.Name}'");
                    int totalRecordCount = 0;
                    int totalOutputCount = 0;

                    foreach (var record in level.Content(options => options
                        .AddDisplayField(ludField)
                        // Here we'll request all the records that have been updated since UTC yesterday (included any that were subsequently updated
                        // today).  This will ensure that all workflow actions with a "UTC yesterday" timestamp will be included regardless of whether
                        // the most recent change to a record happened yesterday or today.  We'll filter it down to only those workflow actions that occurred
                        // yesterday while iterating the IAdvancedWorkflowAudit results below.
                        .AddFilterCondition(ludField.CreateCondition(DateValueOperator.GreaterThan, startDate, true, "UTC Standard Time"))
                        .AddFilterCondition(ludField.CreateCondition(DateValueOperator.Equals, startDate, true, "UTC Standard Time"))
                        .FilterOperatorLogic("1 OR 2") // Combines the GreaterThan and Equals conditions into >=
                        .RecordCountCallback((e, count) => { if (e == RecordCountType.Expected) { Console.WriteLine($"({count} records expected)"); } })
                        ))
                    {
                        // Retrieve the content history for the History Log field configured in appSettings.json for this target
                        IContentHistory contentHistory = record.ContentHistory.SingleOrDefault(ch => ch.HistoryLogFieldId == hlField.Id);
                        if (contentHistory != null)
                        {
                            // Each row in the output csv will represent a workflow action that was taken by a user, including who did it
                            // and when.  If the action involved an update to the status VL field configured in appSettings.json, we'll
                            // capture the before/after values for that field as well.

                            // Unfortunately there's nothing that explicitly correlates a workflow action to a given status change event.
                            // Therefore we'll use their proximity in the sequence of history audits as an indicator that they are correlated.
                            // I.e. if we find a workflow action followed by a subsequent change to the status field, we'll output them
                            // together as a single row.  Conversely if we find a workflow action that is *not* followed by a change to
                            // the status field, that workflow action will be written to the CSV on its own (i.e. with nothing in the 
                            // "Original Workflow Status"/"Target Workflow Status" columns).

                            IAdvancedWorkflowAudit workflowAudit = null;
                            IFieldHistory statusChange = null;
                            bool needStatusChange = false;

                            Func<string, string> quoteString = value => value == null ? null : $"\"{value.Replace("\"", "\"\"")}\"";
                            Action writeOutput = () =>
                            {
                                if (workflowAudit != null)
                                {
                                    sb.AppendLine($"{record.Id},{workflowAudit.WorkflowAction},{quoteString(workflowAudit.TransitionName)},{quoteString(statusChange?.OriginalValue)},{quoteString(statusChange?.NewValue)},{quoteString(workflowAudit.ActionUser.FirstName + " " + workflowAudit.ActionUser.LastName)},{workflowAudit.ActionUser.UserName},{workflowAudit.ActionDate.ToString("O")}");
                                    if (++totalOutputCount % 100 == 0)
                                    {
                                        Console.WriteLine("Total output count: " + totalOutputCount);
                                    }
                                }
                                // Reset the two captured events
                                workflowAudit = null;
                                statusChange = null;
                                needStatusChange = false;
                            };

                            foreach (var historyAudit in contentHistory.HistoryAudits
                                // These should already be in ascending date order, but let's make sure...
                                .OrderBy(ha => ha.ActionDate)) 
                            {
                                if (historyAudit is IAdvancedWorkflowAudit wa
                                    // The HL field will return all the workflow events that it has tracked.
                                    // We want to focus on only what happened between startDate and endDate.
                                    // Note that endDate is today (UTC), so we use < rather than <=.
                                    && (wa.ActionDate.Date >= startDate && wa.ActionDate.Date < endDate))
                                {
                                    if (needStatusChange)
                                    {
                                        // This indicates we already captured a workflow audit, but then encountered another without finding an intervening status change.
                                        // Therefore we should just write out whatever we've already captured and then capture this one for the next output row.
                                        writeOutput();
                                    }
                                    // Capture this workflow audit and start looking for the corresponding status change
                                    workflowAudit = wa;
                                    needStatusChange = true;
                                }
                                else if (needStatusChange && historyAudit is IFieldAudit fa)
                                {
                                    // fa.FieldHistory will contain all the fields that were changed during this content save event,
                                    // so we'll check to see if one of them was the status field
                                    statusChange = fa.FieldHistory.SingleOrDefault(fh => fh.FieldId == statusField.Id);
                                    if (statusChange != null)
                                    {
                                        // We found a workflow audit event and the corresponding field change, so go ahead and output them
                                        writeOutput();
                                    }
                                }
                                else if (historyAudit is ISignatureAudit sa)
								{
                                    // Not significant to this application, but if signature audits were to be included,
                                    // this is how they'd be identified.  Signature audits introduce the properties
                                    // ConfigurationId (int) and ConfigurationName (string).
								}
                            }

                            // If we got to this point while needStatusChange == true, it means we have a buffered workflow audit (without a corresponding
                            // status change) that we still need to output.
                            if (needStatusChange)
                            {
                                writeOutput();
                            }
                        }
                        if (++totalRecordCount % 100 == 0)
                        {
                            Console.WriteLine($"Processed {totalRecordCount} records");
                        }
                    }
                    Console.WriteLine($"Processed {totalRecordCount} records");

                    var outputFilename = $"{application.Alias}_{DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")}.csv";
                    File.WriteAllText(outputFilename, sb.ToString());

                    Console.WriteLine($"Wrote {totalOutputCount} rows to {outputFilename}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return -1;
            }
            finally
			{
                stopwatch.Stop();
                Console.WriteLine("Elapsed: " + stopwatch.Elapsed.ToString());
			}
            return 0;
        }
    }
}
