using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CinemaTest.Services.Payment
{
    public class MoMoService
    {
        private readonly IConfiguration _configuration;
        private readonly string _partnerCode;
        private readonly string _accessKey;
        private readonly string _secretKey;
        private readonly string _apiEndpoint;
        private readonly string _returnUrl;
        private readonly string _notifyUrl;

        public MoMoService(IConfiguration configuration)
        {
            _configuration = configuration;
            _partnerCode = _configuration["MoMo:PartnerCode"];
            _accessKey = _configuration["MoMo:AccessKey"];
            _secretKey = _configuration["MoMo:SecretKey"];
            _apiEndpoint = _configuration["MoMo:ApiEndpoint"];
            _returnUrl = _configuration["MoMo:ReturnUrl"];
            _notifyUrl = _configuration["MoMo:NotifyUrl"];
        }

        public async Task<string> CreatePaymentAsync(string orderId, decimal amount, string orderInfo)
        {
            try
            {
                if (string.IsNullOrEmpty(_partnerCode) || string.IsNullOrEmpty(_accessKey) ||
                    string.IsNullOrEmpty(_secretKey) || string.IsNullOrEmpty(_apiEndpoint))
                {
                    Console.WriteLine("MoMo configuration missing");
                    return "";
                }

                // Đảm bảo orderId và orderInfo không bị null
                orderId = string.IsNullOrEmpty(orderId) ? "ORDER" + DateTime.Now.ToString("yyyyMMddHHmmss") : orderId;
                orderInfo = string.IsNullOrEmpty(orderInfo) ? "Payment for tickets" : orderInfo;

                var requestId = Guid.NewGuid().ToString();

                // Phải đảm bảo chuyển đổi amount sang long để tránh vấn đề định dạng
                long amountLong = (long)amount;

                Console.WriteLine($"Creating MoMo payment with orderId: {orderId}, amount: {amountLong}");

                var rawData = $"partnerCode={_partnerCode}&accessKey={_accessKey}&requestId={requestId}&amount={amountLong}&orderId={orderId}&orderInfo={WebUtility.UrlEncode(orderInfo)}&returnUrl={WebUtility.UrlEncode(_returnUrl)}&notifyUrl={WebUtility.UrlEncode(_notifyUrl)}&extraData=";

                var signature = CreateSignature(rawData, _secretKey);

                var requestData = new
                {
                    partnerCode = _partnerCode,
                    accessKey = _accessKey,
                    requestId = requestId,
                    amount = amountLong,
                    orderId = orderId,
                    orderInfo = orderInfo,
                    returnUrl = _returnUrl,
                    notifyUrl = _notifyUrl,
                    extraData = "",
                    requestType = "captureMoMoWallet",
                    signature = signature
                };

                // In ra thông tin request để debug
                Console.WriteLine($"MoMo request: {JsonConvert.SerializeObject(requestData)}");

                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(30); // Thêm timeout

                var jsonRequest = JsonConvert.SerializeObject(requestData);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(_apiEndpoint, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                // In ra response để debug
                Console.WriteLine($"MoMo response: {responseContent}");

                if (response.IsSuccessStatusCode)
                {
                    var responseObject = JObject.Parse(responseContent);

                    // Kiểm tra mã lỗi
                    var errorCode = responseObject["errorCode"]?.ToString();
                    if (errorCode != "0") // 0 = success
                    {
                        Console.WriteLine($"MoMo error: {responseObject["message"]}");
                        return "";
                    }

                    var payUrl = responseObject["payUrl"]?.ToString();
                    if (string.IsNullOrEmpty(payUrl))
                    {
                        Console.WriteLine("MoMo payUrl is empty");
                        return "";
                    }

                    return payUrl;
                }
                else
                {
                    Console.WriteLine($"MoMo request failed: {response.StatusCode}");
                    return "";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating MoMo payment: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return "";
            }
        }

        public bool ValidateCallback(Dictionary<string, string> response)
        {
            if (response == null || !response.ContainsKey("signature") || !response.ContainsKey("errorCode"))
                return false;

            var errorCode = response["errorCode"];
            if (errorCode != "0")
                return false;

            var requestRawData = "";
            foreach (var key in response.Keys)
            {
                if (key != "signature")
                {
                    requestRawData += $"{key}={response[key]}&";
                }
            }

            if (requestRawData.EndsWith("&"))
                requestRawData = requestRawData.Remove(requestRawData.Length - 1);

            var signature = CreateSignature(requestRawData, _secretKey);
            return signature == response["signature"];
        }

        private string CreateSignature(string data, string secretKey)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }
    }
}