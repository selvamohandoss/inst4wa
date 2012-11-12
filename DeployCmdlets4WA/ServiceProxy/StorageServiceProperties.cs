using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace DeployCmdlets4WA.ServiceProxy
{
    [DataContract(Name = "StorageServiceProperties", Namespace = "http://schemas.microsoft.com/windowsazure")]
    public class StorageServiceProperties
    {
        [DataMember(Order = 1)]
        public string Description { get; set; }

        [DataMember(Order = 2)]
        public string AffinityGroup { get; set; }

        [DataMember(Order = 3)]
        public string Location { get; set; }

        [DataMember(Order = 4)]
        public string Label { get; set; }

        [DataMember(Order = 5)]
        public string Status { get; set; }
    }
}
