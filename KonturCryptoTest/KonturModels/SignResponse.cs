using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KonturCryptoTest.KonturModels
{
    class SignResponse
    {
        public string OperationId { get; set; }
        public ConfirmType ConfirmType { get; set; }
        public string PhoneLastNumbers { get; set; }
        public AdditionalDssInfo AdditionalDssInfo { get; set; }
    }
}
