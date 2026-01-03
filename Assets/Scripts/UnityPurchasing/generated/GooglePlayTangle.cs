// WARNING: Do not modify! Generated file.

namespace UnityEngine.Purchasing.Security {
    public class GooglePlayTangle
    {
        private static byte[] data = System.Convert.FromBase64String("f9aeRuFbIFfl/ec2mWI3mHVYAsclo3rmSwI6vYpvBKVdFBUY2WEXkJ7BflRFiqv1Uu0KpRheSvITBEHwkmX7FgV0xGT64zp/zTiXLjItistyyVpW33yrGZWH7lbo0tw2TwgkqJAGFoaeVht4TBTaljknty66cCm4Olxau45Zn4hckEZLHdMAfi5fgkUysb+wgDKxurIysbGwKDn5XSSFU6vWGu/Wvsx9o9g+6m1pqJHeqADEgDKxkoC9trmaNvg2R72xsbG1sLP/jZxEhWz6XHwYE9eDSqmoL2vJZA9+LaBXgApH1m4T6PFeNBmCEjvLcfbMgVAuO4UcMOAjFmZRyU2EIKDvGPkjE2uhVd77MqG69jYqZX6QO5vhsLCIDic5hbKzsbCx");
        private static int[] order = new int[] { 10,2,12,3,9,7,13,12,13,10,10,13,13,13,14 };
        private static int key = 176;

        public static readonly bool IsPopulated = true;

        public static byte[] Data() {
        	if (IsPopulated == false)
        		return null;
            return Obfuscator.DeObfuscate(data, order, key);
        }
    }
}
