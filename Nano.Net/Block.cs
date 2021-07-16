﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Blake2Sharp;
using Chaos.NaCl;
using Newtonsoft.Json;
using static Nano.Net.Utils;

namespace Nano.Net
{
    /// <summary>
    /// Represents a block. Can be created manually or using the CreateSendBlock and CreateReceiveBlock methods. Can be used in relevant RPC commands.
    /// </summary>
    public class Block : BlockBase
    {
        [JsonProperty("signature")] public string Signature { get; private set; }
        [JsonProperty("work")] public string Work { get; private set; }

        [JsonIgnore] public string Hash => GetHash();

        /// <summary>
        /// Create a send block and sign it. Requires the account object to have all the properties correctly set.
        /// </summary>
        /// <param name="sender">The account that will send the funds.</param>
        /// <param name="receiver">The address that will be receiving the funds.</param>
        /// <param name="amount">The amount of Nano being sent.</param>
        public static Block CreateSendBlock(Account sender, string receiver, Amount amount)
        {
            if (sender.MissingInformation)
                throw new Exception("Not all properties for this account have been set.");

            var block = new Block()
            {
                Account = sender.Address,
                Previous = sender.Frontier,
                Representative = sender.Representative,
                Balance = (sender.Balance.Raw - amount.Raw).ToString("0"),
                Link = receiver,
                Subtype = BlockSubtype.Send
            };

            return block;
        }

        public static Block CreateReceiveBlock(Account sender, string receiver, Amount amount)
        {
            if (sender.MissingInformation)
                throw new Exception("Not all properties for this account have been set.");

            var block = new Block()
            {
                Account = sender.Address,
                Previous = sender.Frontier,
                Representative = sender.Representative,
                Balance = (sender.Balance.Raw - amount.Raw).ToString("0"),
                Link = receiver,
                Subtype = BlockSubtype.Send
            };

            return block;
        }

        public string GetHash()
        {
            var bytes = new List<byte>();

            var type = new byte[32];
            type[31] = 0x6;
            bytes.AddRange(type);

            byte[] account = PublicKeyFromAddress(Account);
            bytes.AddRange(account);

            byte[] previous = HexToBytes(Previous);
            bytes.AddRange(previous);

            byte[] representative = PublicKeyFromAddress(Representative);
            bytes.AddRange(representative);

            byte[] balanceBytes = BigInteger.Parse(Balance).ToByteArray(true, true);
            bytes.AddRange(new byte[16 - balanceBytes.Length]); // pad balance
            bytes.AddRange(balanceBytes);

            byte[] link = Subtype == BlockSubtype.Send ? PublicKeyFromAddress(Link) : HexToBytes(Link);
            bytes.AddRange(link);

            byte[] final = Blake2BHash(32, bytes.ToArray());

            return BytesToHex(final);
        }

        public void Sign(byte[] privateKey)
        {
            Ed25519.KeyPairFromSeed(out byte[] publicKey, out byte[] expandedPrivateKey, privateKey);

            if (publicKey != PublicKeyFromAddress(Account))
                throw new Exception("The private key doesn't match this block's account");

            byte[] signatureBytes = Ed25519.Sign(HexToBytes(Hash), expandedPrivateKey);

            Signature = BytesToHex(signatureBytes);
        }
    }
}