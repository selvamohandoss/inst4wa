using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Management.Automation.Runspaces;
using System.Management.Automation;
using System.Collections.ObjectModel;

namespace DeployCmdlets4WA.Utilities
{
    public class ExecutePSCmdlet
    {
        private List<PSObject> _outputData;

        public bool ErrorOccurred { get; private set; }

        public IEnumerable<PSObject> OutputData 
        { 
            get
            { 
                return _outputData; 
            }
        }

        public Collection<PSObject> Execute(String beginMessage, String command)
        {
            _outputData = new List<PSObject>();
            Console.WriteLine(beginMessage);
            Runspace executePsCmdletRunspace = Runspace.DefaultRunspace;
            using (Pipeline executePsCmdletPipeline = executePsCmdletRunspace.CreateNestedPipeline(command, true))
            {
                executePsCmdletPipeline.Error.DataReady += new EventHandler(Error_DataReadyExecutePsCmdlet);
                executePsCmdletPipeline.Output.DataReady += new EventHandler(Output_DataReadyExecutePsCmdlet);
                return executePsCmdletPipeline.Invoke();
            }
        }

        private void Output_DataReadyExecutePsCmdlet(object sender, EventArgs e)
        {
            PipelineReader<PSObject> reader = sender as PipelineReader<PSObject>;
            if (reader != null)
            {
                while (reader.Count > 0)
                {
                    PSObject output = reader.Read();
                    if (output.BaseObject is System.String)
                    {
                        Console.WriteLine(output.ToString());
                    }
                    _outputData.Add(output);
                }
            }
        }

        private void Error_DataReadyExecutePsCmdlet(object sender, EventArgs e)
        {
            PipelineReader<Object> reader = sender as PipelineReader<Object>;
            if (reader != null)
            {
                while (reader.Count > 0)
                {
                    ErrorOccurred = true;
                    object result = reader.Read();
                    Console.WriteLine(result.ToString());
                }
            }
        }
    }
}
