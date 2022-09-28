using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Security.Cryptography;
using Newtonsoft.Json;
using KonturCryptoTest.KonturModels;
using System.Threading;

namespace KonturCryptoTest
{
    // Сайт: https://developer.kontur.ru/doc/crypto.api?about=0
    internal class Program
    {
        // Утилита cpverify нужна для генерации хэша по стандарту ГОСТ 34.11-2012
        const string PATH_TO_CPVERIFY = @"C:\cpverify\cpverify.exe";
        // Работа осуществляется через TLS-туннель при помощи stunnel
        const string API_URL = "http://localhost:8200/v3/";
        // Максимальный размер блока, установленный Контур - 16 Мб
        const int MAX_CHUNK_SIZE = 1048576;
        // Задержка при получении статуса опреации, чтобы не заспамить сервис
        const int GET_STATUS_DELAY = 5000;

        static void Main(string[] args)
        {
            // Проверка работоспособности сервиса
            Console.WriteLine("-> Servies test: " + Hello());

            // Проверяем, передан ли путь к файлу как аргумент
            string pathToFile = "";
            string pathToSign = "";
            string pathToCertificate = "";

            if (args.Length == 0)
            {
                pathToFile = @"C:\test_docs\Супер-секретный документ.docx";
                pathToCertificate = @"C:\test_certs\Панкрашов_Александр_Сергеевич_до_28.12.2023.cer";
                pathToSign = @"C:\test_docs\";
            }
            else
            {
                pathToFile = string.Format(@"{0}", args[0]);
                pathToCertificate = string.Format(@"{0}", args[1]);
                pathToSign = string.Format(@"{0}", args[2]);
            }

            // Собираем необходимую информацию
            var gostHash = GetGOSTFileHash(pathToFile);
            var md5Hash = GetMD5FieHash(pathToFile);
            var fileLenght = GetFileLength(pathToFile);
            var binary = File.ReadAllBytes(pathToFile);

            var certificate = File.ReadAllBytes(pathToCertificate);
            var certificateBase64 = Convert.ToBase64String(certificate);

            Console.WriteLine(string.Format("\n>> File: {0}", pathToFile));
            Console.WriteLine(string.Format(">> GOST 34.11-2012: {0}", gostHash));
            Console.WriteLine(string.Format(">> MD5: {0}", md5Hash));
            Console.WriteLine(string.Format(">> File length (bytes): {0}", fileLenght));

            Console.WriteLine(string.Format("\n>> Certificate: {0}", pathToCertificate));
            Console.WriteLine(string.Format(">> Base64: {0}", certificateBase64));

            // Создаём файл на сервере
            var fileInfo = CreateFile(binary, md5Hash, fileLenght, "secret_file");
            Console.WriteLine("\n[+] File created:");
            Console.WriteLine(string.Format("\t[-] File Id: {0}", fileInfo.FileId));
            Console.WriteLine(string.Format("\t[-] Uploaded length: {0}", fileInfo.Length));

            // Формируем блоки для передачи на сервер
            var countChunks = (fileLenght / MAX_CHUNK_SIZE) + 1;
            var fileChunks = new Dictionary<int, byte[]>();
            int binaryOffset = 0;

            for (int chunkNo = 1; chunkNo <= countChunks; chunkNo++) 
            {
                int chunkSize = (chunkNo == countChunks) ? fileLenght - ((countChunks - 1) * MAX_CHUNK_SIZE) : MAX_CHUNK_SIZE;
                byte[] chunk = new byte[chunkSize];
                for (int byteIndex = 0; byteIndex < chunkSize; byteIndex++) 
                {
                    chunk[byteIndex] = binary[byteIndex + binaryOffset];
                }
                fileChunks.Add(binaryOffset, chunk);
                binaryOffset += chunkSize;
            }

            // Передаем блоки на сервер
            Console.WriteLine("\n[+] Uploading file:");
            foreach (var chunk in fileChunks) 
            {
                Console.WriteLine(string.Format("\t[-] Chunk offset: {0}", chunk.Key));
                var uploadingResult = UploadChunk(chunk.Value, fileInfo.FileId, chunk.Key);
                if (uploadingResult == 200) 
                {
                    Console.WriteLine(string.Format("\t[-] Uploading successed: {0}", uploadingResult));
                }
                else
                {
                    Console.WriteLine(string.Format("\t[-] Uploading failed: {0}", uploadingResult));
                }
            }

            // Регистрируем операцию подписания
            var fileIds = new string[] { fileInfo.FileId };
            var confirmMessage = new KonturModels.ConfirmMessage();
            confirmMessage.Template = KonturModels.Template.InMyDssWithOperationInfo;
            var signType = KonturModels.SignType.DetachedCMS;

            var registeredOperation = Sign(certificateBase64, fileIds, confirmMessage, signType, true);

            Console.WriteLine("\n[+] Operation registered:");
            Console.WriteLine(string.Format("\t[-] Operation Id: {0}", registeredOperation.OperationId));
            Console.WriteLine(string.Format("\t[-] Confirm type: {0}", registeredOperation.ConfirmType));
            Console.WriteLine(string.Format("\t[-] Phone last numbers: {0}", registeredOperation.PhoneLastNumbers));
            Console.WriteLine(string.Format("\t[-] Additional DSS info: DSS operator: {0}", registeredOperation.AdditionalDssInfo.DssOperator));

            var operationResult = GetStatus(registeredOperation.OperationId);
            while (true) 
            {
                Console.WriteLine("\n[+] *** STATUS ***");
                Console.WriteLine(string.Format("[+] Operation Id: {0}", operationResult.OperationId));
                Console.WriteLine(string.Format("[+] Operation status: {0}", operationResult.OperationStatus));
                Console.WriteLine(string.Format("[+] File statuses ({0}):", operationResult.FileStatuses.Length));
                foreach (var fileStatus in operationResult.FileStatuses)
                {
                    Console.WriteLine("\n");
                    Console.WriteLine(string.Format("\t[-] File Id: {0}", fileStatus.FileId));
                    Console.WriteLine(string.Format("\t[-] Hash: {0}", fileStatus.Hash));
                    Console.WriteLine(string.Format("\t[-] Error Id: {0}", fileStatus.ErrorId));
                    Console.WriteLine(string.Format("\t[-] Error code: {0}", fileStatus.ErrorCode));
                    Console.WriteLine(string.Format("\t[-] Result Id: {0}", fileStatus.ResultId));
                    Console.WriteLine(string.Format("\t[-] Result size: {0}", fileStatus.ResultSize));
                    Console.WriteLine(string.Format("\t[-] Result MD5: {0}", fileStatus.ResultMD5));
                    Console.WriteLine("\n");
                }
                Console.WriteLine("[+] *** ****** ***\n");

                if (operationResult.OperationStatus == OperationStatus.Completed) 
                {
                    break;
                }

                operationResult = GetStatus(registeredOperation.OperationId);
                Thread.Sleep(GET_STATUS_DELAY);
            }

            Console.WriteLine("\n[+] Operation completed!");

            foreach (var fileStatus in operationResult.FileStatuses) 
            {
                var resultId = fileStatus.ResultId;
                var resultSize = fileStatus.ResultSize;
                var resultCountChunks = (resultSize / MAX_CHUNK_SIZE) + 1;
                var resultChunks = new List<byte[]>();
                int resultbinaryOffset = 0;

                for (int chunkNo = 1; chunkNo <= resultCountChunks; chunkNo++) 
                {
                    int chunkSize = (chunkNo == resultCountChunks) ? resultSize - ((resultCountChunks - 1) * MAX_CHUNK_SIZE) : MAX_CHUNK_SIZE;
                    var chunk = GetResult(resultId, resultbinaryOffset, chunkSize);
                    resultChunks.Add(chunk);
                    resultbinaryOffset += chunkSize;
                }

                if (resultChunks.Count == 1) 
                {
                    File.WriteAllBytes(string.Format("{0}Супер-секретный документ.docx.sign", pathToSign), resultChunks[0]);
                    Console.WriteLine(GetMD5FieHash(string.Format("{0}Супер-секретный документ.docx.sign", pathToSign)));
                    Console.WriteLine(fileStatus.ResultMD5);
                }
            }

            Console.ReadKey();
        }


        #region Обращение к API

        // Узнать о текущем состоянии сервиса
        // Документация: https://developer.kontur.ru/doc/crypto.api/method?type=get&path=%2FHello
        static string Hello()
        {
            string url = API_URL + "Hello";

            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);
            request.Method = "GET";
            request.Host = "cloudtest.kontur-ca.ru";

            string data = "";

            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            using (StreamReader stream = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
            {
                data = stream.ReadToEnd();
            }

            return data;
        }

        // Создать файл на сервере
        // Документация: https://developer.kontur.ru/doc/crypto.api/method?type=post&path=%2FCreateFile
        // * Возвращенная длина может быть нулевой. Значит, файл не загружен, его необходимо загрузить полностью.
        static KonturModels.FileInfo CreateFile(byte[] binary, string md5, int length, string fileName = "")
        {
            string url = string.Format("{0}CreateFile?md5={1}&length={2}&fileName={3}", API_URL, md5, length, fileName);
            string data = "";

            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);
            request.Method = "POST";
            request.Host = "cloudtest.kontur-ca.ru";

            // * При запросе требует заголовок Content-Length (ошибка 411)
            request.ContentLength = length;
            Stream requestStream = request.GetRequestStream();
            requestStream.Write(binary, 0, length);
            requestStream.Close();

            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            using (StreamReader stream = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
            {
                data = stream.ReadToEnd();
            }

            return JsonConvert.DeserializeObject<KonturModels.FileInfo>(data);
        }

        // Загрузить один блок файла на сервер
        // Документация: https://developer.kontur.ru/doc/crypto.api/method?type=post&path=%2FUploadChunk
        static int UploadChunk(byte[] binary, string fileId, int offset)
        {
            string url = string.Format("{0}UploadChunk?fileId={1}&offset={2}", API_URL, fileId, offset);

            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);
            request.Method = "POST";
            request.Host = "cloudtest.kontur-ca.ru";

            request.ContentLength = binary.Length;
            Stream requestStream = request.GetRequestStream();
            requestStream.Write(binary, 0, binary.Length);
            requestStream.Close();

            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            return (int) response.StatusCode;
        }

        // Зарегистрировать операцию подписания
        // Документация: https://developer.kontur.ru/doc/crypto.api/method?type=post&path=%2FSign
        static SignResponse Sign(
            string certificate, 
            string[] fileIds, 
            KonturModels.ConfirmMessage confirmMessage,  
            KonturModels.SignType signType,
            bool disableServerSign = false
        ) 
        {
            string url = string.Format("{0}Sign", API_URL);

            var signRequest = new SignRequest();
            signRequest.CertificateBase64 = certificate;
            signRequest.FileIds = fileIds;
            signRequest.ConfirmMessage = confirmMessage;
            signRequest.SignType = signType;
            signRequest.DisableServerSign = disableServerSign;

            var requestBody = JsonConvert.SerializeObject(signRequest);
            var requestBodyBytes = Encoding.UTF8.GetBytes(requestBody);
            var requestBodyLength = requestBodyBytes.Length;

            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);
            request.Method = "POST";
            request.Host = "cloudtest.kontur-ca.ru";

            Stream requestStream = request.GetRequestStream();
            requestStream.Write(requestBodyBytes, 0, requestBodyLength);

            HttpWebResponse response = (HttpWebResponse)request.GetResponse();

            string data = "";

            using (StreamReader reader = new StreamReader(response.GetResponseStream()))
            {
                data = reader.ReadToEnd();
            }

            var statusCode = (int)response.StatusCode;

            if (statusCode != 200)
            {
                throw new Exception(string.Format("HTTP Code {0}: Sing cannot be registered", statusCode));
            }

            return JsonConvert.DeserializeObject<SignResponse>(data);
        }

        // Узнать о текущем состоянии криптооперации
        // Документация: https://developer.kontur.ru/doc/crypto.api/method?type=get&path=%2FGetStatus
        static OperationResult GetStatus(string operationId) 
        {
            string url = string.Format("{0}GetStatus?operationId={1}", API_URL, operationId);

            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);
            request.Method = "GET";
            request.Host = "cloudtest.kontur-ca.ru";

            string data = "";

            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            using (StreamReader stream = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
            {
                data = stream.ReadToEnd();
            }

            return JsonConvert.DeserializeObject<OperationResult>(data);
        }

        // Получить блок результата
        // Документация: https://developer.kontur.ru/doc/crypto.api/method?type=get&path=%2FGetResult
        static byte[] GetResult(string resultId, int offset, int size) 
        {
            string url = string.Format("{0}GetResult?resultId={1}&offset={2}&size={3}", API_URL, resultId, offset, size);

            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);
            request.Method = "GET";
            request.Host = "cloudtest.kontur-ca.ru";

            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            Stream responseStream = response.GetResponseStream();
            byte[] data = new byte[size];
            responseStream.Read(data, 0, size);
            return data;
        }

        #endregion

        #region Вспомогательные функции

        // Получит хэш файла, соответствующий ГОСТ 34.11-2012
        static string GetGOSTFileHash(string pathToFile)
        {
            string command = string.Format("{0} -mk -alg GR3411_2012_256 \"{1}\"", PATH_TO_CPVERIFY, pathToFile);

            Process cmd = new Process();
            cmd.StartInfo.FileName = "cmd.exe";
            cmd.StartInfo.RedirectStandardInput = true;
            cmd.StartInfo.RedirectStandardOutput = true;
            cmd.StartInfo.CreateNoWindow = true;
            cmd.StartInfo.UseShellExecute = false;
            cmd.Start();

            cmd.StandardInput.WriteLine(command);
            cmd.StandardInput.Flush();
            cmd.StandardInput.Close();
            cmd.WaitForExit();

            string result = cmd.StandardOutput.ReadToEnd();
            string hash = result.Split(new string[] { "\r\n" }, StringSplitOptions.None)[4];

            return hash;
        }

        // Получить хэш файла, соотвествующий алгоритму MD5
        static string GetMD5FieHash(string pathToFile)
        {
            using (FileStream stream = File.OpenRead(pathToFile))
            {
                MD5 md5 = new MD5CryptoServiceProvider();

                byte[] fileData = new byte[stream.Length];

                stream.Read(fileData, 0, (int) stream.Length);

                byte[] checkSum = md5.ComputeHash(fileData);
                string result = BitConverter.ToString(checkSum).Replace("-", String.Empty);

                return result.ToUpper();
            }
        }

        // Получить размер файла в байтах
        static int GetFileLength(string pathToFile) 
        {
            long length = new System.IO.FileInfo(pathToFile).Length;
            int convertedLength = 0;

            if (int.TryParse(length.ToString(), out convertedLength)) 
            {
                return convertedLength;
            }

            return -1;
        }

        #endregion

    }
}
