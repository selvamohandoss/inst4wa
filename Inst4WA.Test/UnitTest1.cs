#region Copyright Notice
/*
Copyright © Microsoft Open Technologies, Inc.
All Rights Reserved
Apache 2.0 License

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

     http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.

See the Apache Version 2.0 License for specific language governing permissions and limitations under the License.
*/
#endregion

using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using DeployCmdlets4WA;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Data.SqlServerCe;
using System.Configuration;
using DeployCmdlets4WA.Utilities;
using System.Security.Permissions;

namespace Inst4WA.Test
{
    public class ModelHelperInfo
    {
        public DeploymentModelHelper modelHelper;
        public bool isNegativeTestCase;

        private static string _xmlConfigFileNameFormat = "TestConfig{0}.xml";
        private static int _iModel = 0;

        internal static ModelHelperInfo Create(string directory)
        {
            ModelHelperInfo modelHelperInfo = new ModelHelperInfo();
            modelHelperInfo.modelHelper = new DeploymentModelHelper(Path.Combine(directory, string.Format(_xmlConfigFileNameFormat, _iModel++)));
            modelHelperInfo.isNegativeTestCase = false;

            modelHelperInfo.modelHelper.Init();

            return modelHelperInfo;
        }

        internal static void MakeDeepCopies(List<ModelHelperInfo> modelHelperInfos, int cCopies, string directory)
        {
            int cModels = modelHelperInfos.Count;

            for (int iCopy = 0; iCopy < cCopies - 1; iCopy++)
            {
                for (int iModel = 0; iModel < cModels; iModel++)
                {
                    ModelHelperInfo modelHelperInfo = modelHelperInfos[iModel];
                    ModelHelperInfo modelHelperInfoCopy = ModelHelperInfo.Create(directory);

                    foreach (DeploymentModelStepsStep step in modelHelperInfo.modelHelper.AllSteps)
                    {
                        List<string> commandParamStrings = new List<string>();
                        for (int iCommandParam = 0; iCommandParam < step.CommandParam.Length; iCommandParam++)
                        {
                            commandParamStrings.Add(step.CommandParam[iCommandParam].Name);
                            commandParamStrings.Add(step.CommandParam[iCommandParam].ParameterName);
                        }

                        modelHelperInfoCopy.modelHelper.AddStep(step.Type, step.Command, step.Message, commandParamStrings.ToArray());
                    }

                    IEnumerable<DeploymentModelParametersParameter> dmParams = modelHelperInfo.modelHelper.AllParameters;
                    foreach (DeploymentModelParametersParameter dmParam in dmParams)
                    {
                        modelHelperInfoCopy.modelHelper.AddParameter(dmParam.Name, dmParam.Value, dmParam.Required, dmParam.ValuePrefixRef, dmParam.ValueSuffix);
                    }

                    modelHelperInfos.Add(modelHelperInfoCopy);
                }
            }
        }
    }

    public class ParamInfo
    {
        public DeploymentModelParametersParameter dmParam;
        public bool isNegativeTestCase;
        public bool isUnbound;
    }

    public class StepInfo
    {
        public DeploymentModelStepsStep step;
        public string commandString;
    }

    [TestClass]
    public class UnitTest1
    {
        // How this works: (Test case generation and execution)
        // Test case are generated from 3 csv files: ParamValues.csv, CommandParams.csv, and StepSequences.csv
        // ParamValues.csv contains one column for each param and one row for each param set that can be used together in a test case. Cells contain values for that param.
        // CommandParams.csv contains command params for each command. Note that a single command may have multiple sets of command params
        // StepSequences.csv contains one column for each step and one row for each test case. Also contains one column that specifies the index of the param set to be used with that test case.
        // each commandlet has a corresponding directory under TestCases with the pattern: Test-<commandlet name>, which contains the corresponding .csv files for generating test caese for that commandlet
        // when you run the test code, depending upon the pattern specified in app.config TestCasesDir, appropriate test case directories are enumerated, the .csv files in them are processed,
        // and test cases are generated from them. After all test caeses are generated and added to the database, they are run one after another and a report is generated.

        // In addition, the program also allows for hand generated static test cases (i.e. config XML files generated by hand). These can be placed in a directory pointed to by
        // StaticTestCasesDir in app.config. These, if present, are processed after the auto-generated test cases.

        private static List<string[]> _stepSequences;
        private static List<StepInfo> _stepInfos;
        private static List<ParamInfo> _paramInfos;
        private static string _currentDirName;

        // state of current test case
        private bool _isNegativeTestCase;
        private bool _testFailed;
        private string _errorMsg;


        private TestContext _testContextInstance;
        public TestContext TestContext
        {
            get { return _testContextInstance; }
            set { _testContextInstance = value; }
        }

        [ClassInitialize]
        public static void ClassInit(TestContext context)
        {
            if (!IsValidConfig())
                return;

            CreateTestCaseDB();

            string testCaseDirs = ConfigurationManager.AppSettings["TestCaseDirs"];

            if (!string.IsNullOrEmpty(testCaseDirs))
            {
                string[] matchingDirs = null;

                try
                {
                    matchingDirs = Directory.GetDirectories(Environment.CurrentDirectory, testCaseDirs);
                }
                catch (Exception exc)
                {
                    Trace.WriteLine("!!! Test Configuration Error !!!: Invalid TestCaseDirs pattern in app.config: " + testCaseDirs);
                    Trace.WriteLine(exc);
                    return;
                }

                foreach (string eachDir in matchingDirs)
                {
                    _stepSequences = ParseStepSequencesFile(Path.Combine(eachDir, "StepSequences.csv"));
                    _stepInfos = ParseCommandParamsFile(Path.Combine(eachDir, "CommandParams.csv"));
                    _paramInfos = ParseParameterValuesFile(Path.Combine(eachDir, "ParamValues.csv"));

                    DirectoryInfo info = new DirectoryInfo(eachDir);
                    _currentDirName = Path.Combine(info.Parent.Name, info.Name);

                    // load csv files and generate test cases
                    List<ModelHelperInfo> modelHelperInfos = GenerateTestCases();

                    // save test cases to DB
                    SaveAutoGeneratedTestCasesToDB(modelHelperInfos);
                }
            }

            // save hand-generated test cases to DB
            string staticTestCaseFiles = ConfigurationManager.AppSettings["StaticTestCaseFiles"];

            if (!string.IsNullOrEmpty(staticTestCaseFiles))
            {
                IEnumerable<string> files;
                try
                {
                    files = Directory.EnumerateFiles(Environment.CurrentDirectory, staticTestCaseFiles);
                }
                catch (Exception exc)
                {
                    Trace.WriteLine("!!! Test Configuration Error !!!: Invalid StaticTestCaseFiles pattern in app.config: " + staticTestCaseFiles);
                    Trace.WriteLine(exc);
                    return;
                }

                // Save xmlConfigPath to database
                foreach (string file in files)
                    SaveTestCaseToDB(file);
            }
        }

        private static bool IsValidConfig()
        {
            if (string.IsNullOrEmpty(ConfigurationManager.AppSettings["Subscription"]) ||
                string.IsNullOrEmpty(ConfigurationManager.AppSettings["DomainName"]))
            {
                Trace.WriteLine("!!! Test Configuration Error !!!: Please specify a valid Windows Azure subscription name and domain name in the app.config file before starting the test run.");
                return false;
            }

            return true;
        }

        private static void SaveAutoGeneratedTestCasesToDB(List<ModelHelperInfo> modelHelperInfos)
        {
            for (int iModel = 0; iModel < modelHelperInfos.Count; iModel++)
            {
                ModelHelperInfo modelHelperInfo = modelHelperInfos[iModel];
                string xmlConfigPath = modelHelperInfo.modelHelper.Save();

                if (modelHelperInfo.isNegativeTestCase)
                {
                    // rename file to indicate negative test case
                    string xmlConfigPathNew = Path.Combine(Path.GetDirectoryName(xmlConfigPath), "N_" + Path.GetFileName(xmlConfigPath));
                    File.Move(xmlConfigPath, xmlConfigPathNew);
                    xmlConfigPath = xmlConfigPathNew;
                }

                // Save xmlConfigPath to database
                SaveTestCaseToDB(xmlConfigPath);
            }
        }

        private static void SaveTestCaseToDB(string xmlConfigPath)
        {
            using (SqlCeConnection conn = new SqlCeConnection(Inst4WA.Test.Properties.Settings.Default.XMLConfigsDBConnectionString))
            {
                string cmdString = @"INSERT INTO [XMLConfigs] ([XMLConfigPath]) VALUES ('" + xmlConfigPath + "')";
                using (SqlCeCommand cmd = new SqlCeCommand(cmdString, conn))
                {
                    try
                    {
                        conn.Open();
                        cmd.ExecuteNonQuery();
                    }
                    finally
                    {
                        conn.Close();
                    }
                }
            }
        }

        private static void CreateTestCaseDB()
        {
            string xmlConfigsDBFile = Inst4WA.Test.Properties.Settings.Default.XMLConfigsDBFile;
            if (File.Exists(xmlConfigsDBFile))
                File.Delete(xmlConfigsDBFile);

            // create Database
            SqlCeEngine engine = new SqlCeEngine(Inst4WA.Test.Properties.Settings.Default.XMLConfigsDBConnectionString);
            engine.CreateDatabase();

            // create table
            using (SqlCeConnection conn = new SqlCeConnection(Inst4WA.Test.Properties.Settings.Default.XMLConfigsDBConnectionString))
            {
                using (SqlCeCommand cmd = new SqlCeCommand(@"CREATE TABLE XMLConfigs (ID int, XMLConfigPath NVARCHAR(2000))", conn))
                {
                    try
                    {
                        conn.Open();
                        cmd.ExecuteNonQuery();
                    }
                    finally
                    {
                        conn.Close();
                    }
                }
            }
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
        }

        [DataSource(@"XMLConfigsDataSource")]
        [TestMethod]
        [Timeout(1800000)]
        [EnvironmentPermissionAttribute(SecurityAction.LinkDemand, Unrestricted = true)]
        public void AllConfigXMLTestCases()
        {
            string xmlConfigPath = _testContextInstance.DataRow["XMLConfigPath"].ToString();

            _isNegativeTestCase = IsNegativeTestCase(xmlConfigPath);
            _testFailed = false;
            _errorMsg = null;

            // run test using that config XML and collect output
            RunTestCase(xmlConfigPath, !IsForAzure(xmlConfigPath));

            if ((_testFailed && !_isNegativeTestCase) || (!_testFailed && _isNegativeTestCase))
                Assert.Fail(_errorMsg);
        }

        private bool IsNegativeTestCase(string xmlConfigPath)
        {
            return Path.GetFileName(xmlConfigPath).StartsWith("N_");
        }

        private bool IsForAzure(string xmlConfigPath)
        {
            DeploymentModelHelper modelHelper = new DeploymentModelHelper(xmlConfigPath);
            modelHelper.Init();
            return modelHelper.IsForAzure();
        }

        private static List<ModelHelperInfo> GenerateTestCases()
        {
            List<ModelHelperInfo> modelHelperInfos = new List<ModelHelperInfo>();

            for (int iStepSequence = 0; iStepSequence < _stepSequences.Count; iStepSequence++)
            {
                string[] stepSequence = _stepSequences[iStepSequence];
                modelHelperInfos.AddRange(GetModelsForStepSequence(stepSequence));
            }

            return modelHelperInfos;
        }

        private static List<ModelHelperInfo> GetModelsForStepSequence(string[] stepSequence)
        {
            List<ModelHelperInfo> modelHelperInfos = new List<ModelHelperInfo>();

            modelHelperInfos.Add(ModelHelperInfo.Create(_currentDirName)); // initialize with one model

            // add models for every variation of every step
            for (int iCommand = 0; iCommand < stepSequence.Length; iCommand++)
            {
                string commandString = stepSequence[iCommand];

                if (string.IsNullOrEmpty(commandString))
                    continue;

                IEnumerable<DeploymentModelStepsStep> steps = GetStepsForCommand(commandString);

                if ((steps == null) || (steps.Count() == 0))
                    continue;

                // make deep copies of existing models for each step in steps
                ModelHelperInfo.MakeDeepCopies(modelHelperInfos, steps.Count(), _currentDirName);

                // add each step to that many models
                int cModelsPerStep = modelHelperInfos.Count / steps.Count();
                for (int iStep = 0; iStep < steps.Count(); iStep++)
                {
                    for (int iModel = iStep * cModelsPerStep; iModel < ((iStep + 1) * cModelsPerStep); iModel++)
                    {
                        modelHelperInfos[iModel].modelHelper.AddStep(steps.ElementAt(iStep));
                    }
                }
            }

            // now add param values for every variation of param value for every commandParam
            List<ModelHelperInfo> modelHelperInfosNew = new List<ModelHelperInfo>();
            int cModels = modelHelperInfos.Count;
            for (int iModel = 0; iModel < cModels; iModel++)
            {
                modelHelperInfosNew.AddRange(GetModelParamPermutations(modelHelperInfos[iModel]));
            }

            return modelHelperInfosNew;
        }

        private static IEnumerable<DeploymentModelStepsStep> GetStepsForCommand(string commandString)
        {
            IEnumerable<DeploymentModelStepsStep> steps =
                from s in _stepInfos
                where s.commandString == commandString
                select s.step;

            return steps;
        }

        private static List<ModelHelperInfo> GetModelParamPermutations(ModelHelperInfo mhi)
        {
            List<ModelHelperInfo> modelHelperInfos = new List<ModelHelperInfo>();
            modelHelperInfos.Add(mhi);

            // for each step in model
            List<DeploymentModelStepsStep> steps = mhi.modelHelper.AllSteps.ToList();
            for (int iStep = 0; iStep < steps.Count; iStep++)
            {
                // for each commandParam in step
                for (int iCommandParam = 0; iCommandParam < steps[iStep].CommandParam.Length; iCommandParam++)
                {
                    // get all variations of param values
                    List<ParamInfo> paramInfos = GetParamInfosForParameterName(steps[iStep].CommandParam[iCommandParam].ParameterName);

                    if ((paramInfos == null) || (paramInfos.Count == 0))
                        continue;

                    // make deep copies of existing models for each paramInfo in paramInfos
                    ModelHelperInfo.MakeDeepCopies(modelHelperInfos, paramInfos.Count, _currentDirName);

                    // add each param to that many models
                    int cModelsPerParam = modelHelperInfos.Count / paramInfos.Count;
                    for (int iParam = 0; iParam < paramInfos.Count; iParam++)
                    {
                        for (int iModel = iParam * cModelsPerParam; iModel < ((iParam + 1) * cModelsPerParam); iModel++)
                        {
                            modelHelperInfos[iModel].modelHelper.AddParameter(paramInfos[iParam].dmParam);

                            if (paramInfos[iParam].isNegativeTestCase)
                                modelHelperInfos[iModel].isNegativeTestCase = true;
                        }
                    }
                }
            }

            List<ParamInfo> unboundParams = _paramInfos.Where(e => e.isUnbound == true).ToList();
            if (unboundParams != null && unboundParams.Count() != 0)
            {
                foreach (ModelHelperInfo eachModelHelperInfo in modelHelperInfos)
                {
                    foreach (ParamInfo eachUnboundParam in unboundParams)
                    {
                        eachModelHelperInfo.modelHelper.AddParameter(eachUnboundParam.dmParam);
                    }
                }
            }
            return modelHelperInfos;
        }

        private static List<ParamInfo> GetParamInfosForParameterName(string parameterName)
        {
            return _paramInfos.FindAll(p => p.dmParam.Name == parameterName);
        }

        private void RunTestCase(string xmlConfigPath, bool bEmulator)
        {
            // create process info to run inst4wa
            Process p = new Process();

            p.StartInfo.FileName = "Inst4WA.exe";

            if (bEmulator)
                p.StartInfo.Arguments = string.Format("-XmlConfigPath \"{0}\"", xmlConfigPath);
            else
                p.StartInfo.Arguments = string.Format("-XmlConfigPath \"{0}\" -Subscription \"{1}\" -DomainName \"{2}\"",
                    xmlConfigPath, ConfigurationManager.AppSettings["Subscription"], ConfigurationManager.AppSettings["DomainName"]);

            p.StartInfo.UseShellExecute = false;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardError = p.StartInfo.RedirectStandardOutput = true;
            p.OutputDataReceived += new DataReceivedEventHandler(OutputDataReceived);
            p.ErrorDataReceived += new DataReceivedEventHandler(ErrorDataReceived);
            p.StartInfo.WorkingDirectory = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            Trace.WriteLine(p.StartInfo.FileName + " " + p.StartInfo.Arguments);
            p.Start();

            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            p.WaitForExit();
            p.Close();
        }

        void ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                // npm puts its normal output in the error output stream, so we have to filter it out
                if (e.Data.StartsWith("npm") && !e.Data.StartsWith("npm ERR!"))
                    return;

                Trace.WriteLine(e.Data);

                _testFailed = true;
                _errorMsg = e.Data;
            }
        }

        void OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                Trace.WriteLine(e.Data);

                if (e.Data.Contains("Error: "))
                {
                    _testFailed = true;
                    _errorMsg = e.Data;
                }
            }
        }

        private static List<StepInfo> ParseCommandParamsFile(string commandParamsFileName)
        {
            // read command params file
            string commandParamsFileText = File.ReadAllText(commandParamsFileName, Encoding.Default);
            string[] commandParamsLines = commandParamsFileText.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

            // parse command params
            List<StepInfo> stepInfos = commandParamsLines.Select(commandParamsLine =>
                {
                    if (commandParamsLine.StartsWith("Command,"))
                        return null;

                    var commandParamsSplitLine = commandParamsLine.CsvSplit();

                    DeploymentModelStepsStep step = new DeploymentModelStepsStep();

                    // command string format: <type>:<command>[.<index>]
                    // index is used in case we have multiple steps with the same command e.g. Cmdlet:Add-LoadAssembly.1, Cmdlet:Add-LoadAssembly.2 etc.
                    string commandString = commandParamsSplitLine[0];
                    string[] commandStringParts = commandString.Split(":".ToCharArray());

                    step.Type = commandStringParts[0];
                    step.Command = commandStringParts[1].Split(".".ToCharArray())[0];

                    List<DeploymentModelStepsStepCommandParam> commandParams = new List<DeploymentModelStepsStepCommandParam>();
                    for (int iCol = 1; iCol < commandParamsSplitLine.Length; )
                    {
                        if (string.IsNullOrEmpty(commandParamsSplitLine[iCol]))
                            break;

                        DeploymentModelStepsStepCommandParam commandParam = new DeploymentModelStepsStepCommandParam();
                        commandParam.Name = commandParamsSplitLine[iCol++];
                        commandParam.ParameterName = commandParamsSplitLine[iCol++];
                        commandParams.Add(commandParam);
                    }

                    step.CommandParam = commandParams.ToArray();

                    StepInfo stepInfo = new StepInfo();
                    stepInfo.commandString = commandString;
                    stepInfo.step = step;

                    return stepInfo;
                }
            ).ToList<StepInfo>();

            stepInfos.RemoveAll(s => s == null); // remove nulls

            return stepInfos;
        }

        private static List<ParamInfo> ParseParameterValuesFile(string paramValuesFileName)
        {
            // read paramValues file
            string paramValuesFileText = File.ReadAllText(paramValuesFileName, Encoding.Default);
            string[] paramvaluesLines = paramValuesFileText.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

            // parse command params
            List<ParamInfo> paramInfos = paramvaluesLines.Select(paramValuesLine =>
            {
                if (paramValuesLine.StartsWith("Name,"))
                    return null;

                var paramValuesSplitLine = paramValuesLine.CsvSplit();

                DeploymentModelParametersParameter dmParam = new DeploymentModelParametersParameter();

                dmParam.Name = paramValuesSplitLine[0];
                dmParam.Value = paramValuesSplitLine[1];
                dmParam.Required = paramValuesSplitLine[2];
                dmParam.ValuePrefixRef = paramValuesSplitLine[3];
                dmParam.ValueSuffix = paramValuesSplitLine[4];

                ParamInfo paramInfo = new ParamInfo();
                paramInfo.dmParam = dmParam;
                paramInfo.isNegativeTestCase = !string.IsNullOrEmpty(paramValuesSplitLine[5]) && (paramValuesSplitLine[5].ToLower() == "true");
                if (paramValuesSplitLine.Length >= 7)
                {
                    paramInfo.isUnbound = !string.IsNullOrEmpty(paramValuesSplitLine[6]) && (paramValuesSplitLine[6].ToLower() == "true");
                }
                else
                {
                    paramInfo.isUnbound = false;
                }
                return paramInfo;
            }
            ).ToList<ParamInfo>();

            paramInfos.RemoveAll(p => p == null); // remove nulls

            return paramInfos;
        }

        private static List<string[]> ParseStepSequencesFile(string stepSequencesFileName)
        {
            // read step sequences file
            string stepSequencesFileText = File.ReadAllText(stepSequencesFileName, Encoding.Default);
            string[] stepSequencesLines = stepSequencesFileText.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

            // parse step sequences
            List<string[]> stepSequences = stepSequencesLines.Select(stepSequencesLine =>
            {
                if ((stepSequencesLine.Trim() == "Command") || (stepSequencesLine.StartsWith("Command,")))
                    return null;

                var stepSequencesSplitLine = stepSequencesLine.CsvSplit();

                return stepSequencesSplitLine;
            }
            ).ToList<string[]>();

            stepSequences.RemoveAll(s => s == null); // remove nulls

            return stepSequences;
        }

    }
}
