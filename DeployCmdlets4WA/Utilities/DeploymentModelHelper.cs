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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.IO;

namespace DeployCmdlets4WA.Utilities
{
    public class DeploymentModelHelper
    {
        private String _xmlConfigPath;
        private DeploymentModel _model;

        private List<DeploymentModelParametersParameter> _deploymentParams;
        private List<DeploymentModelStepsStep> _steps;

        public DeploymentModelHelper(string location)
        {
            _xmlConfigPath = location;
        }

        public void Init()
        {
            if (File.Exists(_xmlConfigPath))
            {
                XmlSerializer xmlSerializer = new XmlSerializer(typeof(DeploymentModel));
                using (FileStream configFileStream = new FileStream(_xmlConfigPath, FileMode.Open, FileAccess.Read))
                {
                    _model = xmlSerializer.Deserialize(configFileStream) as DeploymentModel;
                    for (int i = 0; i < _model.Items.Length; i++)
                    {
                        if (_model.Items[i] is DeploymentModelParameters)
                        {
                            _deploymentParams = (_model.Items[i] as DeploymentModelParameters).Parameter.ToList();
                        }
                        else if (_model.Items[i] is DeploymentModelSteps)
                        {
                            _steps = (_model.Items[i] as DeploymentModelSteps).Step.ToList();
                        }
                    }
                }
            }
            else
            {
                _model = new DeploymentModel();
                _deploymentParams = new List<DeploymentModelParametersParameter>();
                _steps = new List<DeploymentModelStepsStep>();

                DeploymentModelParameters dmParams = new DeploymentModelParameters();
                dmParams.Parameter = _deploymentParams.ToArray();

                DeploymentModelSteps dmSteps = new DeploymentModelSteps();
                dmSteps.Step = _steps.ToArray();

                _model.Items = new object[2];
                _model.Items[0] = dmParams;
                _model.Items[1] = dmSteps;
            }
        }

        public bool IsForAzure()
        {
            string emulParam = this.GetParameterByName("Emulator");
            if(emulParam == null)
            {
                return false;
            }
            return emulParam.Trim().ToUpperInvariant() == "FALSE";
        }

        public string Save()
        {
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(DeploymentModel));
            using (FileStream configFileStream = new FileStream(_xmlConfigPath, FileMode.Create, FileAccess.Write))
            {
                xmlSerializer.Serialize(configFileStream, _model);
            }

            return _xmlConfigPath;
        }

        public IEnumerable<String> GetAllParameterNames 
        {
            get
            {
                return _deploymentParams.Select(e => e.Name);
            }
        }

        public IEnumerable<DeploymentModelParametersParameter> AllParameters
        {
            get
            {
                return _deploymentParams;
            }
        }

        // the value of a param is either given directly as the Value attribute Or as a combination of the value of another parameter and a suffix. In that case, the name of the other
        // parameter is specified as ValuePrefixRef and the suffix is specified as ValueSuffix.
        public string GetParameterByName(String key)
        {
            IEnumerable<DeploymentModelParametersParameter> paramEnum = _deploymentParams.Where(e => e.Name == key);
            if ((paramEnum == null) || (paramEnum.Count() == 0))
                return null;

            return paramEnum.Select(e => e.Value).FirstOrDefault();
        }

        public bool IsParamValueRequired(String key)
        {
            IEnumerable<DeploymentModelParametersParameter> paramEnum = _deploymentParams.Where(e => e.Name == key);
            if ((paramEnum == null) || (paramEnum.Count() == 0))
                return false;

            string value = paramEnum.Select(e => e.Required).FirstOrDefault();
            return (string.Compare(value, "yes", StringComparison.OrdinalIgnoreCase) == 0) || (string.Compare(value, "true", StringComparison.OrdinalIgnoreCase) == 0) || (value == "1");
        }

        public IEnumerable<DeploymentModelStepsStep> AllSteps
        {
            get 
            { 
              return  _steps;
            }
        }

        public DeploymentModelStepsStep GetStepAtIndex(int stepIndex) 
        {
            return _steps.Count - 1 < stepIndex ? null : _steps[stepIndex];
        }

        public void SetParameterByName(String key, String value)
        {
            DeploymentModelParametersParameter param = _deploymentParams.Where(e => e.Name == key).FirstOrDefault();
            if (param == null)
            {
                param = new DeploymentModelParametersParameter();
                _deploymentParams.Add(param);
                param.Name = key;
            }

            param.Value = value;
        }

        public string GetParamValuePrefixRef(string key)
        {
            IEnumerable<DeploymentModelParametersParameter> paramEnum = _deploymentParams.Where(e => e.Name == key);
            if ((paramEnum == null) || (paramEnum.Count() == 0))
                return null;

            return paramEnum.Select(e => e.ValuePrefixRef).FirstOrDefault();
        }

        public string GetParamValueSuffix(string key)
        {
            IEnumerable<DeploymentModelParametersParameter> paramEnum = _deploymentParams.Where(e => e.Name == key);
            if ((paramEnum == null) || (paramEnum.Count() == 0))
                return null;

            return paramEnum.Select(e => e.ValueSuffix).FirstOrDefault();
        }

        public bool AddParameter(DeploymentModelParametersParameter paramToAdd)
        {
            // add the param only if it has not already been added
            if (_deploymentParams.Find(p => p.Name == paramToAdd.Name) != null)
                return false;

            _deploymentParams.Add(paramToAdd);

            // update model
            DeploymentModelParameters parameters = _model.Items[0] as DeploymentModelParameters;
            List<DeploymentModelParametersParameter> parameterList = parameters.Parameter.ToList();
            parameterList.Add(paramToAdd);
            parameters.Parameter = parameterList.ToArray();
            _model.Items[0] = parameters;

            return true;
        }

        public bool AddParameter(string name, string value, string required, string valuePrefixRef, string valueSuffix)
        {
            // create param
            DeploymentModelParametersParameter param = new DeploymentModelParametersParameter();
            param.Name = name;
            param.Required = required;
            param.Value = value;
            param.ValuePrefixRef = valuePrefixRef;
            param.ValueSuffix = valueSuffix;

            // add param to params
            return AddParameter(param);
        }

        public bool AddStep(DeploymentModelStepsStep stepToAdd)
        {
            _steps.Add(stepToAdd);

            // update model
            DeploymentModelSteps steps = _model.Items[1] as DeploymentModelSteps;
            List<DeploymentModelStepsStep> stepList = steps.Step.ToList();
            stepList.Add(stepToAdd);
            steps.Step = stepList.ToArray();
            _model.Items[1] = steps;

            return true;
        }

        public bool AddStep(string type, string command, string message, string[] stepParams)
        {
            // creat step
            DeploymentModelStepsStep step = new DeploymentModelStepsStep();
            step.Type = type;
            step.Command = command;
            step.Message = message;

            // add command params to step
            List<DeploymentModelStepsStepCommandParam> commandParams = new List<DeploymentModelStepsStepCommandParam>();

            for (int iString = 0; iString < stepParams.Length; )
            {
                DeploymentModelStepsStepCommandParam commandParam = new DeploymentModelStepsStepCommandParam();
                commandParam.Name = stepParams[iString++];
                commandParam.ParameterName = stepParams[iString++];
                commandParams.Add(commandParam);
            }

            step.CommandParam = commandParams.ToArray();

            // add step to steps
            return AddStep(step);
        }
    }
}
