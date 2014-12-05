Inst4WA - Simple Deployment Framework for Windows Azure
=======================================================

## Introduction

## Testing

This comment is added only for testing purpose. Not to taken seriously.

## Introduction

This tool consists of a commandline tool and a set of commandlets for deploying open source software to Windows Azure by specifying a very simple configuration file. The following sections describe how to use the tool for deploying a Hello world type solution as well as Apache CouchDB and Solr to Windows Azure. This is followed by instructions for how to run the automated test suite that is included in the project.

## Start Here - Common Steps for Using the Tool

The following steps are common to all the sections below:

1. Powershell 3.0 is required to run this installer. Please follow the instructions described at http://technet.microsoft.com/en-us/library/hh847837.aspx to install Powershell 3.0.

2. The recommended way is to download and expand the binaries for the latest build located at http://msopentechrelease.blob.core.windows.net/inst4wa/Inst4wa.zip and skip to step 3 in the next section. 

3. If you would rather download the source code and build it yourself, you can do so by clicking on the ZIP button above. Note that the zip file has a long file name that will cause problems while building, so you will need to rename the folder name to make it short. You will need Visual Studio 2010 Ultimate Edition, the Windows Azure SDK 2.4, Azure Powershell 0.8.4 and SQL Server Compact Edition 3.5. There is a build.cmd file in the root that you can run to do a full build.

4. Change directory to the build\Debug or build\Release folder.

5. If you are going to use the configuration files given below without modification, then you will need to replace "Azure.publishsettings" with the publish setting file containing your Azure subscription details. You can also create your own configuration files and then choose to either not specify the publishsettings file (in which case the tool will ask you to download it from Azure portal) or use your own path to your publishsettings file.

6. Please make sure that you unblock all the dll's and config files using instructions at http://msdn.microsoft.com/en-us/library/ee890038(VS.100).aspx. 

## Specific Steps for Deploying a HelloWorld Solution Consisting of a WebRole (PaaS)

1. The HelloWorld with WebRole binaries are included in the build inside the TestData\WebRole directory.

2. The configuration XML file to be used for deploying this solution is 'TestCases\StaticTests-Azure\HelloWorldWebRoleWR.xml' 

3. Use the following command to deploy the solution to Windows Azure: 

    Inst4WA.exe -XmlConfigPath "TestCases\StaticTests-Azure\HelloWorldWebRoleWR.xml" -DomainName "\<your unique name\>" -Subscription "\<your subscription name\>" -Location "\<datacenterlocation\>"

4. The above command will create a Hello World PaaS service consisting of Web Roles under the given subscription with a deployment URL that is based on the specified domain name. Please refer to the HelloWorldWebRoleWR.xml for description of other parameters and steps involved.

## Specific Steps for Deploying a HelloWorld Solution Consisting of a WorkerRole (PaaS)

1. The Hello World with WorkerRole binaries are included in the build inside the TestData\WorkerRole directory.

2. The configuration XML file to be used for deploying this solution is 'TestCases\StaticTests-Azure\HelloWorldWorkerRoleWR.xml' 

3. Use the following command to deploy the solution to Windows Azure: 

    Inst4WA.exe -XmlConfigPath "TestCases\StaticTests-Azure\HelloWorldWorkerRoleWR.xml" -DomainName "\<your unique name\>" -Subscription "\<your subscription name\>" -Location "\<datacenterlocation\>"

4. The above command will create a Hello World PaaS service consisting of Worker Roles under the given subscription with a deployment URL that is based on the specified domain name. Please refer to the HelloWorldWorkerRoleWR.xml for description of other parameters and steps involved.

## Specific Steps for Deploying a HelloWorld Solution Consisting of a VM Role (IaaS)

1. The Hello World with VMRole binaries and related files are included in the build inside the TestData\VMRole directory.

2. The configuration XML file to be used for deploying this solution is 'TestCases\StaticTests-Azure\HelloWorldVMRole.xml' 

3. Use the following command to deploy the solution to Windows Azure: 

    Inst4WA.exe -XmlConfigPath "TestCases\StaticTests-Azure\HelloWorldVMRole.xml" -DomainName "\<your unique name\>" -Subscription "\<your subscription name\>" -Location "\<data center location\>" -Force "\<True or False\>"

4. The above command will create a Hello World IaaS service under the given subscription in the specified data center location and with a deployment URL that is based on the specified domain name. Each VM in the service will have the Windows Web Server Role enabled and a TCP endpoint connected to it, so you can connect to it from the outside. The Location parameter specifies the Windows Azure data center location where the service and its storage account will be located. The "Force" parameter specifies what should be done in case a previous instance of the service already exists. Setting "Force" to "true" will delete all the VM's associated with the previous instance. Setting it to "false" will lead to simply reusing the VM's from the previous instance. Please refer to the HelloWorldVMRole.xml for description of other parameters and steps involved.

## Steps for Deploying CouchDb PaaS or IaaS Solution to Windows Azure

Please refer to the ReadMe at the https://github.com/MSOpenTech/Windows-Azure-CouchDB.

## Steps for Deploying Solr PaaS Solution to Windows Azure

Please refer to the ReadMe at the https://github.com/MSOpenTech/Windows-Azure-Solr.

## Specific Steps for Executing Automated Test Cases

1. Pre-requisites

- Visual Studio 2010 Ultimate Edition

- SQL Server Compact Edition 3.5

2. Edit the Inst4Wa.Test.Dll.config file to specify your subscription name, domain name, and test case directories and files that you can select to specify the subset of test cases you want to run.

3. Use following command on Visual Studio Command Prompt to execute the all test cases

    MSTEST.EXE /testcontainer:Inst4WA.Test.dll  /testsettings:Build.testsettings

4. Alternatively, you can also load the Inst4WA.sln into Visual Studio, set the Inst4WA.Test project as your startup project, and run it.

## Details on How Test Cases are Organized

- All automated test cases are included in the build inside TestCases directory. Some tests are dynamically generated and some are static.

- Local static tests are inside StaticTests-Local and Azure static tests are inside StaticTests-Azure.

- Cmdlet-specific dynamically generated tests are inside folders named as Test-<cmdlet name>. Each such directory consists of 3 csv files

- StepSequences.csv - CSV contains the list of cmdlets that should be invoked as part of test. 

- CommandParams.csv - CSV contains the list of parameters that should be passed to the cmdlets listed inside stepsequences.csv.

- ParamValues.csv - CSV contains all possible combinations of parameters with which a cmdlet should be invoked.

- The appSettings section inside Inst4WA.Test.dll.config allows you to specify your Azure subscription, domain name, and select specific sets of test cases to run.

- Use the TestCaseDirs setting to specify a directory name pattern to select cmdlet-specific dynamic test cases to run.

- Use the StaticTestCaseFiles setting to specify a file name pattern to select specific static tests to run.
- Comment is only given for testing purposes
