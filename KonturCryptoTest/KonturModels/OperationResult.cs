using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KonturCryptoTest.KonturModels
{
    class OperationResult
    {
        public string OperationId { get; set; }
        public OperationStatus OperationStatus { get; set; }
        public FileStatus[] FileStatuses { get; set; }
    }
}
