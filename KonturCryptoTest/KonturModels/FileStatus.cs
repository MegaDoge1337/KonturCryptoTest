using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KonturCryptoTest.KonturModels
{
    class FileStatus
    {
        public string FileId { get; set; }
        public string Hash { get; set; }
        public Status Status { get; set; }
        public string ErrorId { get; set; }
        public ErrorCode ErrorCode { get; set; }
        public string ResultId { get; set; }
        public int ResultSize { get; set; }
        public string ResultMD5 { get; set; }
    }
}
