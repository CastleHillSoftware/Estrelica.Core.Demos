The demos in this solution provide examples for using <a href="https://castlehillsoftware.github.io/Estrelica.Core.Demos/index.html" target="_blank">Estrelica.Core</a> to build your own .NET projects to implement integration activities with Archer.

These are the demo applications referenced in the "Getting Started with Estrelica.Core" page at https://castlehillsoftware.github.io/Estrelica.Core.Demos/articles/getting_started.html.  Please refer to that page for info about accessing the Estrelica.Core package.  Details about configuring your license key, and connecting Estrelica.Core to your Archer instance can be found in the "Connecting to Archer" page at https://castlehillsoftware.github.io/Estrelica.Core.Demos/articles/connecting_to_archer.html.

Since every Archer deployment is different, some of these demos may fail to run to completion in your Archer environment.

Notably, the "Content Demo" and "Metadata Demo" applications (demonstrating how to work with Archer content and metadata, respectively) make use of the "Policies" and "Applications" application, as well as the Levels and Fields they contain.

These applications were chosen for the demos because they are included by Archer in all standard deployments, so it is anticipated that these demos will run unmodified on most Archer systems.

However, these applications (and/or the levels and fields they contain) may well have been deleted or renamed by an administrator in your particular environment.  This will cause the corresponding code which attempts to resolve these entities by name to fail.

If you encounter any errors when running these demos, feel free to modify the code as needed to align with whatever names are appropriate for your environment.

Furthermore, there's nothing special about "Policies" and "Applications" as far as these demos are concerned.  You can change the code to use whatever applications/levels/fields you'd like to work with in your environment.

It's also important to note that the Content and Metadata demos don't really do anything productive.  The code is the important thing here, showing *how* to do content- and metadata-related activities with Estrelica.Core.  Don't expect to be amazed by the console output scrolling by onscreen, you should instead focus on the code to understand how these activities are implemented.

The other applications (Datafeed Explorer, CHUM Tool and UserAccessProcessor) are functional applications which perform productive actions.

As the name implies, the Datafeed Explorer demo shows how to explore the datafeeds configured in your Archer environment and examine their statuses and run history.  This demo application does not depend on any particular Archer modules or content being present in your Archer environment, as it works directly with datafeeds, regardless of their configuration.  It does however depend on Estrelica.Core's Extended API methods.  For details see https://castlehillsoftware.github.io/Estrelica.Core.Demos/articles/extensions.html.

The CHUM Tool (CastleHill User Management) and UserAccessProcessor demos each perform similar functions, asserting group membership for users via an external process which fulfills change requests via the API.  Each depends on specific Archer configuration -- two new fields added to the standard Contacts application in the case of the CHUM Tool, and a new ODA in the case of the UserAccessProcessor.  The details of each are described in the comments of their respective Program.cs files.

## Newly added (April 2024)

Report Explorer - Similar to Datafeed Explorer, this one allows you to explore and execute reports

Workflow Activity Report - Demonstrates how to use ContentHistory to evaluate Advanced Workflow activities and resulting field content changes

Section Name Repair - Compares how a simple task might be implemented with and without the help of Estrelica.Core