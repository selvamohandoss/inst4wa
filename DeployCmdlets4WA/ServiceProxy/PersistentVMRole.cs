using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace DeployCmdlets4WA.ServiceProxy
{
    [DataContract(Name = "PersistentVMRole", Namespace = "http://schemas.microsoft.com/windowsazure")]
    public class PersistentVMRole : Role
    {
        [DataMember(Order = 1)]
        public OSVirtualHardDisk OSVirtualHardDisk { get; set; }
    }
}
