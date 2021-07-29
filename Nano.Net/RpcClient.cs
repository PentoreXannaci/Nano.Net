﻿// This is a modified version of the code from https://github.com/Flufd/NanoDotNet/blob/master/NanoDotNet/NanoRpcClient.cs


using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Nano.Net
{
    public class RpcClient
    {
        public string NodeAddress { get; }

        private readonly HttpClient _httpClient = new HttpClient();

        private readonly JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver()
            {
                NamingStrategy = new SnakeCaseNamingStrategy()
            }
        };

        public RpcClient(string nodeAddress)
        {
            NodeAddress = nodeAddress;
        }

        private async Task<T> RpcRequestAsync<T>(object request)
        {
            var content = new StringContent(JsonConvert.SerializeObject(request, _jsonSerializerSettings), Encoding.UTF8, "application/json");
            HttpResponseMessage response = await _httpClient.PostAsync(NodeAddress, content);
            string json = await response.Content.ReadAsStringAsync();

            if (json.Contains("\"error\":"))
            {
                JObject errorMessage = JObject.Parse(json);
                throw new RpcException(errorMessage["error"]?.ToString());
            }

            return JsonConvert.DeserializeObject<T>(json);
        }

        // Default node RPC calls

        public async Task<AccountInfoResponse> AccountInfoAsync(string address, bool representative = true)
        {
            try
            {
                return await RpcRequestAsync<AccountInfoResponse>(new
                {
                    Action = "account_info",
                    Account = address,
                    Representative = representative
                });
            }
            catch (RpcException exception)
            {
                if (exception.OriginalError == "Account not found")
                    throw new UnopenedAccountException();
                else
                    throw;
            }
        }

        /// <summary>WARNING: This command is usually disabled on public nodes. You need to use your own node.</summary>
        public async Task<WorkGenerateResponse> WorkGenerateAsync(string hash)
        {
            return await RpcRequestAsync<WorkGenerateResponse>(new
            {
                Action = "work_generate",
                Hash = hash
            });
        }

        public async Task<ReceivableBlocksResponse> PendingBlocksAsync(string address, int count = 5)
        {
            var pendingBlocks = await RpcRequestAsync<ReceivableBlocksResponse>(new
            {
                Action = "pending",
                Account = address,
                Count = count.ToString(),
                Source = true,
                IncludeOnlyConfirmed = true
            });

            foreach ((string key, ReceivableBlock value) in pendingBlocks.PendingBlocks)
                value.Hash = key;

            return pendingBlocks;
        }

        public async Task<ProcessResponse> ProcessAsync(Block block)
        {
            if (block.Signature is null)
                throw new Exception("This block hasn't been signed yet.");

            if (block.Work is null)
                throw new Exception("The PoW nonce for this block hasn't been set.");

            return await RpcRequestAsync<ProcessResponse>(new
            {
                Action = "process",
                JsonBlock = true,
                Subtype = block.Subtype,
                Block = block
            });
        }

        // Custom calls

        public async Task UpdateAccountAsync(Account account)
        {
            AccountInfoResponse accountInfo;

            try
            {
                accountInfo = await AccountInfoAsync(account.Address);
            }
            catch (UnopenedAccountException)
            {
                account.Opened = false;
                account.Frontier = new string('0', 64);
                account.Balance = Amount.FromRaw("0");
                account.Representative = account.Address;
                return;
            }
            
            account.Opened = true;
            account.Frontier = accountInfo.Frontier;
            account.Balance = Amount.FromRaw(accountInfo.Balance);
            account.Representative = accountInfo.Representative;
        }
    }
}
