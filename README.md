# Axosoft SCM Integration for Sourcegear Vault

This utility adds source control checkin information to Bugs and Work Items in Axosoft OnTime. The information 
from each matched checkin including the checkin timestamp, comment, users and details of each file that is part 
of the checkin will then be visible against the item in Axosoft.

If after configuring the source control provider in Axosoft you cant see the panel containing the checkins, 
try clicking the filter icon on the details pane - the source control provider name that you enter during 
configuration (detailed below) should be visible, simply check it.



## How it works

When a checkin is made to the source control you simply prefix the checkin comment with the items id from Axosoft.

__Comment example for a bug__

B#nnn - fixed xyz problem.


__Comment example for a work item / request__

R#nnn - added new feature abc

### Configuration

__Sourcegear Vault__

Simply add a set of credentials, your repository name and the root path that you want this utility 
to monitor changes for.

__Axosoft__

In Axosoft, you will need to generate an API key and configre a Source Control provider. This can be done by 
the administrator from the Tools menu. 

Select Manage Extensions->Source control. Then click the Add source control type, enter a name for the integration
e.g. Sourcegear Vault, the API Key will be automatically generated - copy this to your app.config file.

Select an approrpriate Default User, this will be used if Axosoft cant match the username passed from 
Sourcegear Vault.

You should then select the option/checkbox for *Attempt to map source control users to Axosoft users*.

Under Advanced Mappings, setup the following:

| Field | Value |
| ---------- | ---------- |
| ID: | Version |
| URL: | Url |
| Files: | Files |
| File URL: | FileUrl |
| User login: | Username |
| Message: | Comment |
| Timestamp: | CheckInDate |
| File display name: | FilePath |
| File action: | FileAction Type |
| User display name: | DisplayName |




The url used is the url that you use to access your Axosoft Tennant, if hosted.

At this time I have only tested this with the hosted solution from Axosoft, but it should work equally as well 
with an onsite installation.

### Missing DLL's

You will initially find that you are missing three references

* VaultLib
* VaultClientIntegrationLib
* VaultClientOperationsLib

You can download these files from Sourcegears Website - they are found in the ClientAPI download package.

http://www.sourcegear.com/vault/downloads.html

Remember to get the versions that match your version of vault.






