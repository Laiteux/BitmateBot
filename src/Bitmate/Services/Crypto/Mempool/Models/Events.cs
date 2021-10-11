using System;
using Bitmate.Services.Crypto.Mempool.Responses;

namespace Bitmate.Services.Crypto.Mempool.Models
{
    public class Events
    {
        public Action<Block> BlockMined { get; set; }

        public Action<AddressTransaction> TxReceived { get; set; }

        public Action<string> TxReplaced { get; set; }

        public Action<Block> TxConfirmed { get; set; }
    }
}
