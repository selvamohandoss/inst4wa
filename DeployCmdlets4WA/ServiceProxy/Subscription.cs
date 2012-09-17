using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace DeployCmdlets4WA.ServiceProxy
{
    [DataContract(Namespace = ConfigurationConstants.ServiceManagementNamespace)]
    public class Subscription
    {
        [DataMember(Order = 1, EmitDefaultValue = false)]
        public string SubscriptionID { get; set; }

        [DataMember(Order = 2, EmitDefaultValue = false)]
        public string SubscriptionName { get; set; }

        [DataMember(Order = 3, EmitDefaultValue = false)]
        public string SubscriptionStatus { get; set; }

        [DataMember(Order = 4, EmitDefaultValue = false)]
        public int MaxStorageAccounts{get;set;}

        [DataMember(Order = 5, EmitDefaultValue = false)]
        public int CurrentStorageAccounts { get; set; }
    }
}
