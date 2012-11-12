using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DeployCmdlets4WA.ServiceProxy
{
    public enum CopyStatus
    {
        Pending,
        Success,
        Aborted,
        Failed
    }

    public class BlobProperties
    {
        public string LastModified { get; set; }

        public string BlobType { get; set; }

        public CopyStatus CopyStatus { get; private set; }

        public String CopyStatusDescription { get; set; }

        public string BytesCopied { get; private set; }

        public string BytesTotal { get; private set; }

        public void SetCopyStatus(string status)
        {
            switch (status)
            {
                case "pending":
                    CopyStatus = CopyStatus.Pending;
                    break;
                case "success":
                    CopyStatus = CopyStatus.Success;
                    break;
                case "aborted":
                    CopyStatus = CopyStatus.Aborted;
                    break;
                default:
                case "failed":
                    CopyStatus = CopyStatus.Failed;
                    break;
            }
        }

        public void SetCopyProgress(string progress)
        {
            string[] progressParts = progress.Split('/');
            BytesCopied = progressParts[0];
            BytesTotal = progressParts[1];
        }
    }
}
