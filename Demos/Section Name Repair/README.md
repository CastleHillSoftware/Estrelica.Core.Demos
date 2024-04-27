### Overview

**Section Name Repair** is actually two demo projects (**UsingArcherAPIs** and **UsingEstrelicaCore**), showing how to perform the same task by programming directly against the Archer APIs versus how to do it using Estrelica.Core.

Each project exercises the two API subject areas (Metadata and Content) common to most Archer integration tasks.

Note how the Estrelica.Core approach requires ~90% less code.  After considering the two examples, decide for yourself which approach you'd rather use while coding, reviewing, debugging, supporting, maintaining and delegating your next Archer integration project.


### Task Requirements

Here are the hypothetical requirements provided by a hypothetical analyst for this hypothetical task:

"Someone entered a bunch of new policy records in Archer, but they misspelled the section names in some cases.  I need you to fix all the section names where the user spelled 'Control' or 'Controls' with double 'L's, e.g. 'Controll' or 'Controlls'."

After a couple of phone calls and emails, the analyst confirmed that these names are stored in the "Section Name" Text field in the "Section" level of the "Policies" application, and advised there may be some legitimate names involving words like "Controller" or "Controlling", so we need to be careful not to modify those occurrences.



### Warning

These are not practical applications in the sense of doing anything useful, they merely demonstrate how such a task might be implemented using each of the two approaches.  As such, it is *not* a good idea to execute them blindly, as they will potentially make changes to your Archer content.  Instead just review them to understand the differences in the code.

If you would like to actually run them, **first make sure** that you don't have any *legitimate* Policies "Section" records having "Controll" in their "Section Name" (which will get fixed to "Control" by these applications), then go ahead and modify one or more of those records to introduce the "Controll" misspelling as described below and see how each of these apps does its work to address that misspelling.

All the usual caveats apply.  Run this at your own risk, don't run it against production data, etc.  For educational purposes only.

