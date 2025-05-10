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
    public class ZaloPayService
    {
        private readonly IConfiguration _configuration;
        private readonly string _appId;
        private readonly string _key1;
        private readonly string _key2;
        private readonly string _apiEndpoint;
        private readonly string _callbackUrl;

        public ZaloPayService(IConfiguration configuration)
        {
            _configuration = configuration;
            _appId = _configuration["ZaloPay:AppId"];
            _key1 = _configuration["ZaloPay:Key1"];
            _key2 = _configuration["ZaloPay:Key2"];
            _apiEndpoint = _configuration["ZaloPay:ApiEndpoint"];
            _callbackUrl = _configuration["ZaloPay:CallbackUrl"];
        }

        public async Task<string> CreatePaymentAsync(string orderId, decimal amount, string orderInfo)
        {
            try
            {
                // Kiểm tra cấu hình
                if (string.IsNullOrEmpty(_appId) || string.IsNullOrEmpty(_key1) ||
                    string.IsNullOrEmpty(_key2) || string.IsNullOrEmpty(_apiEndpoint))
                {
                    Console.WriteLine("ZaloPay configuration missing");
                    return "";
                }

                // Đảm bảo orderId không bị null
                orderId = string.IsNullOrEmpty(orderId) ? "ORDER" + DateTime.Now.ToString("yyyyMMddHHmmss") : orderId;

                // Đảm bảo orderInfo không bị null
                orderInfo = string.IsNullOrEmpty(orderInfo) ? "Payment for tickets" : orderInfo;

                // Tạo item và embed_data
                var embedData = new { redirecturl = _callbackUrl };
                var item = new[] { new { name = "Movie Ticket", amount = amount } };

                var embedDataStr = JsonConvert.SerializeObject(embedData);
                var itemsStr = JsonConvert.SerializeObject(item);
                var timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();

                // Tạo MAC
                var dataStr = $"{_appId}|{orderId}|{(long)amount}|{orderInfo}|{itemsStr}|{embedDataStr}|{timestamp}";
                Console.WriteLine($"ZaloPay data string: {dataStr}");
                var mac = ComputeHmacSha256(dataStr, _key1);

                // Tạo dữ liệu yêu cầu
                var requestData = new
                {
                    app_id = _appId,
                    app_trans_id = orderId,
                    app_user = "user123",
                    app_time = timestamp,
                    amount = (long)amount, // Chuyển đổi sang long để tránh vấn đề định dạng số
                    item = itemsStr,
                    description = orderInfo,
                    embed_data = embedDataStr,
                    mac = mac
                };

                // In ra thông tin request để debug
                Console.WriteLine($"ZaloPay request: {JsonConvert.SerializeObject(requestData)}");

                // Gửi request
                using var httpClient = new HttpClient();
                var jsonRequest = JsonConvert.SerializeObject(requestData);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync(_apiEndpoint, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                // In ra response để debug
                Console.WriteLine($"ZaloPay response: {responseContent}");

                if (response.IsSuccessStatusCode)
                {
                    var responseObject = JObject.Parse(responseContent);

                    // Kiểm tra mã lỗi
                    var returnCode = responseObject["return_code"]?.ToString();
                    if (returnCode != "1") // 1 = success
                    {
                        Console.WriteLine($"ZaloPay error: {responseObject["return_message"]}");
                        return "";
                    }

                    var orderUrl = responseObject["order_url"]?.ToString();
                    if (string.IsNullOrEmpty(orderUrl))
                    {
                        Console.WriteLine("ZaloPay order_url is empty");
                        return "";
                    }

                    return orderUrl;
                }
                else
                {
                    Console.WriteLine($"ZaloPay request failed: {response.StatusCode}");
                    return "";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating ZaloPay payment: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return "";
            }
        }

        public bool ValidateCallback(Dictionary<string, string> response)
        {
            try
            {
                if (response == null)
                {
                    Console.WriteLine("ZaloPay callback: response is null");
                    return false;
                }

                // In ra response để debug
                Console.WriteLine($"ZaloPay callback data: {JsonConvert.SerializeObject(response)}");

                if (!response.ContainsKey("mac") || !response.ContainsKey("status"))
                {
                    Console.WriteLine("ZaloPay callback: missing required fields");
                    return false;
                }

                var status = response["status"];
                if (status != "1") // 1 = success
                {
                    Console.WriteLine($"ZaloPay callback: status is not success: {status}");
                    return false;
                }

                var data = $"{response["app_id"]}|{response["app_trans_id"]}|{response["app_user"]}|{response["amount"]}|{response["app_time"]}|{response["embed_data"]}|{response["item"]}";
                var mac = ComputeHmacSha256(data, _key1);

                if (mac != response["mac"])
                {
                    Console.WriteLine("ZaloPay callback: invalid MAC");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error validating ZaloPay callback: {ex.Message}");
                return false;
            }
        }

        private string ComputeHmacSha256(string data, string key)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }
    }
}