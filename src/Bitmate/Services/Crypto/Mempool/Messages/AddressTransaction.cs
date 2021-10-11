using System.Collections.Generic;
using System.Text.Json;

namespace Bitmate.Services.Crypto.Mempool.Messages
{
    public class AddressTransaction
    {
        public long Vsize { get; set; }

        public double FeePerVsize { get; set; }

        public double EffectiveFeePerVsize { get; set; }

        public string Txid { get; set; }

        public long Version { get; set; }

        public long Locktime { get; set; }

        public List<Vin> Vin { get; set; }

        public List<Vout> Vout { get; set; }

        public long Size { get; set; }

        public long Weight { get; set; }

        public long Fee { get; set; }

        public Status Status { get; set; }

        public long FirstSeen { get; set; }

        public JsonElement BestDescendant { get; set; }

        public List<JsonElement> Ancestors { get; set; }

        public bool CpfpChecked { get; set; }
    }

    public class Status
    {
        public bool Confirmed { get; set; }
    }

    public class Vin
    {
        public string Txid { get; set; }

        public long Vout { get; set; }

        public Vout Prevout { get; set; }

        public string Scriptsig { get; set; }

        public string ScriptsigAsm { get; set; }

        public List<string> Witness { get; set; }

        public bool IsCoinbase { get; set; }

        public long Sequence { get; set; }
    }

    public class Vout
    {
        public string Scriptpubkey { get; set; }

        public string ScriptpubkeyAsm { get; set; }

        public string ScriptpubkeyType { get; set; }

        public string ScriptpubkeyAddress { get; set; }

        public long Value { get; set; }
    }
}
