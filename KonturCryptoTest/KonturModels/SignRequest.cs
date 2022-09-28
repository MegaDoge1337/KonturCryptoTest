using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KonturCryptoTest.KonturModels
{
    class SignRequest
    {
        public string CertificateBase64 { get; set; }
        public string[] FileIds { get; set; }
        public ConfirmMessage ConfirmMessage { get; set; }
        public SignType SignType { get; set; }
        public bool DisableServerSign { get; set; }
    }
}
