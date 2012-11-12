using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace DeployCmdlets4WA.ServiceProxy
{
    [DataContract(Name = "OSVirtualHardDisk", Namespace = "http://schemas.microsoft.com/windowsazure")]
    public class OSVirtualHardDisk
    {
        [DataMember(Order = 1)]
        public string HostCaching { get; set; }

        [DataMember(Order = 2)]
        public string DiskName { get; set; }

        [DataMember(Order = 3)]
        public string MediaLink { get; set; }

        [DataMember(Order = 4)]
        public string SourceImageName { get; set; }

        [DataMember(Order = 5)]
        public string OS { get; set; }
    }
}
