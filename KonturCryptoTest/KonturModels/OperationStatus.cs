using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KonturCryptoTest.KonturModels
{
    enum OperationStatus
    {
        Enqueued,
        InProgress,
        Completed,
        CanceledByUser,
        Timeout,
        Crashed,
        UserHasUnconfirmedOperation,
        AwaitingForConfirmation,
        ExceededConfirmationAttemptsCount,
        StartConfirmationFailed
    }
}
