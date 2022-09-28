using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KonturCryptoTest.KonturModels
{
    enum ErrorCode
    {
        None,
        Unknown,
        BadEncryptedData,
        EncryptedForOtherRecipient,
        UnknownCertificate,
        Declined
    }
}
