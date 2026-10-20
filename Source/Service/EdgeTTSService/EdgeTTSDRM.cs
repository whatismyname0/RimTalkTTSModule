using System;
using System.Collections.Generic;
using System.Text;

namespace RimTalk.TTS.Service.EdgeTTSService
{
    /// <summary>
    /// Implementation of Microsoft Edge's <c>Sec-MS-GEC</c> anti-abuse token.
    ///
    /// Since October 2026 the legacy v1 token (plain SHA-256 over
    /// "&lt;windows-filetime-ticks&gt;&lt;TrustedClientToken&gt;") is rejected with HTTP 403 by the
    /// read-aloud WebSocket, while the v2 token - the value Microsoft Edge 155 itself
    /// sends - is accepted.
    ///
    /// The v2 token is <c>&lt;16 hex&gt;-&lt;64 hex&gt;</c>: a "CS64" digest followed by a salted
    /// SHA-256 digest of the same input string. Both are Microsoft's obfuscated "patent
    /// hash" primitives (reverse engineered in https://github.com/rany2/edge-tts/issues/490
    /// and implemented upstream in https://github.com/rany2/edge-tts/blob/master/src/edge_tts/drm.py),
    /// so they are ported faithfully here instead of using System.Security.Cryptography:
    /// the salted variant is not standard SHA-256.
    ///
    /// The input string is "&lt;ticks rounded down to a 5 minute window&gt;&lt;TrustedClientToken&gt;",
    /// so the token only changes every five minutes.
    /// </summary>
    internal static class EdgeTTSDRM
    {
        /// <summary>Client key baked into every Edge build.</summary>
        public const string TRUSTED_CLIENT_TOKEN = "6A5AA1D4EAFF4E9FB37E23D68491D6F4";

        private const long WinEpoch = 11644473600L;
        private const long WindowSeconds = 300L;
        private const uint U32Mask = 0xFFFFFFFFu;

        // --- Salted SHA-256 constants -------------------------------------------------

        private static readonly uint[] Sha256Initial =
        {
            0x6A09E667u, 0xBB67AE85u, 0x3C6EF372u, 0xA54FF53Au,
            0x510E527Fu, 0x9B05688Cu, 0x1F83D9ABu, 0x5BE0CD19u,
        };

        private static readonly uint[] Sha256State =
        {
            0xFD9FBEA2u, 0x91243CE7u, 0xBE499094u, 0xCD2E1E0Au,
            0x6C006F49u, 0x04BD35ABu, 0xFEB0A7E4u, 0x03EFFBD2u,
        };

        private static readonly uint[] Sha256Schedule =
        {
            0xEB24BF9Eu, 0x1558691Fu, 0xA0C09EE2u, 0x0CFA32BAu, 0xA622EE8Cu, 0xD1DD9491u,
            0xC0DD25A0u, 0xF9E70AB6u, 0xF0C31573u, 0x7058CE4Eu, 0x7188E606u, 0x00E5AB33u,
            0xD258B060u, 0x72766564u,
        };

        private static readonly uint[] Sha256RoundConstants =
        {
            0x428A2F98u, 0x71374491u, 0xB5C0FBCFu, 0xE9B5DBA5u, 0x3956C25Bu, 0x59F111F1u,
            0x923F82A4u, 0xAB1C5ED5u, 0xD807AA98u, 0x12835B01u, 0x243185BEu, 0x550C7DC3u,
            0x72BE5D74u, 0x80DEB1FEu, 0x9BDC06A7u, 0xC19BF174u, 0xE49B69C1u, 0xEFBE4786u,
            0x0FC19DC6u, 0x240CA1CCu, 0x2DE92C6Fu, 0x4A7484AAu, 0x5CB0A9DCu, 0x76F988DAu,
            0x983E5152u, 0xA831C66Du, 0xB00327C8u, 0xBF597FC7u, 0xC6E00BF3u, 0xD5A79147u,
            0x06CA6351u, 0x14292967u, 0x27B70A85u, 0x2E1B2138u, 0x4D2C6DFCu, 0x53380D13u,
            0x650A7354u, 0x766A0ABBu, 0x81C2C92Eu, 0x92722C85u, 0xA2BFE8A1u, 0xA81A664Bu,
            0xC24B8B70u, 0xC76C51A3u, 0xD192E819u, 0xD6990624u, 0xF40E3585u, 0x106AA070u,
            0x19A4C116u, 0x1E376C08u, 0x2748774Cu, 0x34B0BCB5u, 0x391C0CB3u, 0x4ED8AA4Au,
            0x5B9CCA4Fu, 0x682E6FF3u, 0x748F82EEu, 0x78A5636Fu, 0x84C87814u, 0x8CC70208u,
            0x90BEFFFAu, 0xA4506CEBu, 0xBEF9A3F7u, 0xC67178F2u,
        };

        private const uint Sha256Adjustment = 0xC9DC06A7u;
        private const uint Sha256Sigma0Adjustment = 0x059C0B80u;
        private const uint Sha256Sigma1Adjustment = 0x000B92C0u;

        // --- CS64 ("patent hash") constants -------------------------------------------

        /// <summary>
        /// Coefficient table: each entry is (coefficient, offset) and contributes exactly
        /// one term and one offset to the digest.
        /// </summary>
        private static readonly uint[,] Cs64Coefficients =
        {
            { 0xE82E91B1u, 0xB32700D0u }, { 0x44DE24C6u, 0x88DFF51Du },
            { 0x3BEB65BCu, 0x29514038u }, { 0xC89C5933u, 0xB328CBDFu },
            { 0x8D1EC211u, 0xD405B4D5u }, { 0x314EF5D5u, 0x26E55F3Cu },
            { 0xE92491B1u, 0x607801C6u }, { 0xF0A5B9D5u, 0x82B2C1FBu },
            { 0x06064CF3u, 0xFA51A3DAu }, { 0xDA79FA60u, 0x207CC464u },
            { 0xBED20560u, 0x3C3C74F3u }, { 0x89F9C9F8u, 0xD2D14B61u },
            { 0x35FA38C6u, 0xBB04A2C1u }, { 0xCB49E3C6u, 0xC5626167u },
        };

        private const int Cs64CoefficientCount = 14;
        private const uint Cs64Factor = 0x82893933u;
        private const uint Cs64Adjustment0 = 0xF2F90B1Au;
        private const uint Cs64Adjustment1 = 0x0B1A0000u;

        /// <summary>Round constant 14 is replaced in the first block of the salted variant.</summary>
        private static readonly uint[] Sha256BlockConstants = BuildBlockConstants();

        private static uint[] BuildBlockConstants()
        {
            var result = new uint[Sha256RoundConstants.Length];
            Array.Copy(Sha256RoundConstants, result, result.Length);
            result[14] = Sha256Adjustment;
            return result;
        }

        // --- Public API ----------------------------------------------------------------

        /// <summary>
        /// Returns the Windows file time (100 ns ticks since 1601) at the start of the five
        /// minute window containing <paramref name="unixSeconds"/>.
        /// </summary>
        public static long WindowTicks(double unixSeconds)
        {
            // Integer arithmetic keeps this exact for every representable Unix time.
            long seconds = (long)Math.Floor(unixSeconds) + WinEpoch;
            seconds -= seconds % WindowSeconds;
            return seconds * 10000000L;
        }

        /// <summary>The string both digests are computed over.</summary>
        public static string TokenInput(double unixSeconds)
        {
            return WindowTicks(unixSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture)
                   + TRUSTED_CLIENT_TOKEN;
        }

        /// <summary>
        /// Legacy v1 token: plain SHA-256 (uppercase hex) of the token input. Still accepted
        /// by both endpoints, but only used when a v2 handshake has already failed.
        /// </summary>
        public static string GenerateSecMsGecV1(double unixSeconds)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.ASCII.GetBytes(TokenInput(unixSeconds)));
                return ToHexUpper(hash);
            }
        }

        /// <summary>
        /// Current v2 token: "&lt;16 hex CS64&gt;-&lt;64 hex salted SHA-256&gt;".
        /// </summary>
        public static string GenerateSecMsGecV2(double unixSeconds)
        {
            byte[] data = Encoding.ASCII.GetBytes(TokenInput(unixSeconds));
            byte[] digest = Sha256Digest(data);
            uint[] cs64 = Cs64Digest(data, digest);
            return ToHexUpper(PackLe32(cs64[0], cs64[1])) + "-" + ToHexUpper(digest);
        }

        // --- Bit helpers ---------------------------------------------------------------

        private static uint Rol16(uint value)
        {
            return (value << 16) | (value >> 16);
        }

        private static uint ShiftRight32(ulong value, int shift)
        {
            return (uint)(value >> shift);
        }

        /// <summary>
        /// SHA-256 small sigma zero. Written exactly as upstream: each shift is masked
        /// separately and the three terms are combined with OR rather than the usual
        /// rotations-plus-XOR, because the masked-vs-unmasked choice changes the result.
        /// </summary>
        private static uint Sigma0(uint value)
        {
            return ((value >> 7) | ((value << 25) & U32Mask))
                   ^ ((value >> 18) | ((value << 14) & U32Mask))
                   ^ (value >> 3);
        }

        private static uint Sigma1(uint value)
        {
            return ((value >> 17) | ((value << 15) & U32Mask))
                   ^ ((value >> 19) | ((value << 13) & U32Mask))
                   ^ (value >> 10);
        }

        /// <summary>
        /// Little-endian 32-bit words of <paramref name="data"/>, zero padded to a multiple
        /// of four bytes and then to an even number of words.
        /// </summary>
        private static List<uint> WordsLe4Even(byte[] data)
        {
            var words = new List<uint>();
            int padding = (((-data.Length) % 4) + 4) % 4;
            int padded = data.Length + padding;
            if (padded == 0)
            {
                return words;
            }

            byte[] buffer = new byte[padded];
            Array.Copy(data, buffer, data.Length);
            for (int i = 0; i < padded; i += 4)
            {
                words.Add((uint)(buffer[i] | (buffer[i + 1] << 8) | (buffer[i + 2] << 16) | (buffer[i + 3] << 24)));
            }

            if (words.Count % 2 != 0)
            {
                words.Add(0u);
            }

            return words;
        }

        // --- Salted SHA-256 ------------------------------------------------------------

        /// <summary>
        /// The salted SHA-256 rounds.
        ///
        /// The key is mixed into the first block, so that block starts at round 14 instead
        /// of round 0. This is the canonical SHA-256 register rotation (an eight slot working
        /// vector rotated by one per round) rather than upstream's unrolled variant; the two
        /// implement the same recurrence.
        /// </summary>
        private static uint[] Sha256Rounds(uint[] state, uint[] words, int firstRound)
        {
            var v = new uint[8];
            for (int i = 0; i < 8; i++)
            {
                v[i] = state[i];
            }

            uint[] constants = firstRound == 14 ? Sha256BlockConstants : Sha256RoundConstants;
            for (int index = firstRound; index < 64; index++)
            {
                // For this iteration v[0]=a, v[1]=b, ... v[7]=h.
                ulong rotE = (ulong)v[4] * 0x100000001UL; // rotate right e
                ulong rotA = (ulong)v[0] * 0x100000001UL; // rotate right a

                uint s1 = ShiftRight32(rotE, 6) ^ ShiftRight32(rotE, 11) ^ ShiftRight32(rotE, 25);
                uint ch = v[6] ^ (v[4] & (v[5] ^ v[6]));
                uint t1 = unchecked(v[7] + s1 + ch + constants[index] + words[index]);

                uint s0 = ShiftRight32(rotA, 2) ^ ShiftRight32(rotA, 13) ^ ShiftRight32(rotA, 22);
                uint maj = (v[0] & v[1]) | (v[2] & (v[0] | v[1]));
                uint t2 = unchecked(s0 + maj);

                // h = g; g = f; f = e; e = d + t1; d = c; c = b; b = a; a = t1 + t2.
                v[7] = v[6];
                v[6] = v[5];
                v[5] = v[4];
                v[4] = unchecked(v[3] + t1);
                v[3] = v[2];
                v[2] = v[1];
                v[1] = v[0];
                v[0] = unchecked(t1 + t2);
            }

            return v;
        }

        private static void ExpandSchedule(uint[] words, int firstWord)
        {
            for (int index = firstWord; index < 64; index++)
            {
                uint x = words[index - 2];
                uint y = words[index - 15];
                uint sigma1 = (x >> 17) ^ (x << 15) ^ (x >> 19) ^ (x << 13) ^ (x >> 10);
                uint sigma0 = (y >> 7) ^ (y << 25) ^ (y >> 18) ^ (y << 14) ^ (y >> 3);
                words[index] = unchecked(sigma1 + words[index - 7] + sigma0 + words[index - 16]);
            }
        }

        private static uint[] Sha256Init(byte[] data)
        {
            if (data.Length < 7)
            {
                throw new ArgumentException("Token salt must contain at least seven bytes", nameof(data));
            }

            var words = new uint[64];
            words[14] = (uint)((data[0] << 16) | (data[1] << 8) | data[2]);
            words[15] = (uint)((data[3] << 24) | (data[4] << 16) | (data[5] << 8) | data[6]);

            for (int index = 16; index < 30; index++)
            {
                uint value = Sha256Schedule[index - 16];
                if (index - 2 >= 14)
                {
                    value = unchecked(value + (index == 16
                        ? (Sigma1(words[index - 2]) ^ Sha256Sigma1Adjustment)
                        : Sigma1(words[index - 2])));
                }

                if (index - 7 >= 14)
                {
                    value = unchecked(value + words[index - 7]);
                }

                if (index - 15 >= 14)
                {
                    value = unchecked(value + (index == 29
                        ? (Sigma0(words[index - 15]) ^ Sha256Sigma0Adjustment)
                        : Sigma0(words[index - 15])));
                }

                // The high bits must be dropped here: the sigma functions above truncate,
                // so a sum left at full precision would feed different bits into the next
                // iteration than the reference implementation does.
                words[index] = value & U32Mask;
            }

            words[30] = unchecked(Sigma1(words[28])
                                  + words[23]
                                  + Sigma0(words[15])
                                  + words[14]
                                  + Sha256Adjustment
                                  - Sha256RoundConstants[14]);

            ExpandSchedule(words, 31);
            uint[] working = Sha256Rounds(Sha256State, words, 14);

            var init = new uint[8];
            for (int i = 0; i < 8; i++)
            {
                init[i] = unchecked(Sha256Initial[i] + working[i]);
            }

            return init;
        }

        private static byte[] Sha256Digest(byte[] data)
        {
            if (data.Length < 7)
            {
                throw new ArgumentException("Token salt must contain at least seven bytes", nameof(data));
            }

            int length = 57 + data.Length;
            int tailLength = data.Length - 7;
            // Python's "%" is always non-negative; C#'s is not.
            int zeroPadding = (((55 - length) % 64) + 64) % 64;
            int paddedLength = tailLength + 1 + zeroPadding + 8;

            var padded = new byte[paddedLength];
            Array.Copy(data, 7, padded, 0, tailLength);
            padded[tailLength] = 0x80;
            ulong bitLength = (ulong)length * 8UL;
            for (int i = 0; i < 8; i++)
            {
                padded[paddedLength - 1 - i] = (byte)(bitLength >> (8 * i));
            }

            uint[] state = Sha256Init(data);
            var block = new uint[64];
            for (int offset = 0; offset < padded.Length; offset += 64)
            {
                for (int i = 0; i < 16; i++)
                {
                    int p = offset + (i * 4);
                    block[i] = (uint)((padded[p] << 24) | (padded[p + 1] << 16) | (padded[p + 2] << 8) | padded[p + 3]);
                }

                ExpandSchedule(block, 16);
                uint[] working = Sha256Rounds(state, block, 0);
                var next = new uint[8];
                for (int i = 0; i < 8; i++)
                {
                    next[i] = unchecked(state[i] + working[i]);
                }

                state = next;
            }

            var digest = new byte[32];
            for (int i = 0; i < 8; i++)
            {
                digest[i * 4] = (byte)(state[i] >> 24);
                digest[(i * 4) + 1] = (byte)(state[i] >> 16);
                digest[(i * 4) + 2] = (byte)(state[i] >> 8);
                digest[(i * 4) + 3] = (byte)state[i];
            }

            return digest;
        }

        // --- CS64 ----------------------------------------------------------------------

        private static uint[] Cs64Digest(byte[] data, byte[] digest)
        {
            uint keyA = ((uint)(digest[0] | (digest[1] << 8) | (digest[2] << 16) | (digest[3] << 24))) | 1u;
            uint keyB = ((uint)(digest[4] | (digest[5] << 8) | (digest[6] << 16) | (digest[7] << 24))) | 1u;

            uint factorA = unchecked(keyA * Cs64Factor);
            uint factorB = unchecked(keyB * Cs64Factor);

            var terms = new List<uint>(64);
            var offsets = new List<uint>(64);

            // One term and one offset per table entry, then one of each per data word, so
            // both lists stay the same length and the fold can walk them in lockstep.
            for (int index = 0; index < Cs64CoefficientCount; index++)
            {
                terms.Add(unchecked(Cs64Coefficients[index, 0] * (index % 2 == 0 ? factorA : factorB)));
                offsets.Add(Cs64Coefficients[index, 1]);
            }

            // The service hashes a NUL-prefixed copy of the input.
            var prefixed = new byte[data.Length + 1];
            Array.Copy(data, 0, prefixed, 1, data.Length);
            List<uint> dataWords = WordsLe4Even(prefixed);

            for (int index = 0; index < dataWords.Count; index++)
            {
                bool even = index % 2 == 0;
                uint word = dataWords[index];
                uint term = unchecked(word * (even ? keyA : keyB));
                uint offset = unchecked(Rol16(word) * (even ? 0xEF0569FBu : 0xC31713DBu));
                if (index == 0)
                {
                    term = unchecked(term + (Cs64Adjustment0 * factorA));
                    offset = unchecked(offset + Cs64Adjustment1);
                }

                terms.Add(term);
                offsets.Add(offset);
            }

            if (terms.Count != offsets.Count || terms.Count % 2 != 0)
            {
                throw new InvalidOperationException(
                    "CS64 term/offset mismatch: " + terms.Count + " terms, " + offsets.Count + " offsets");
            }

            uint swapAcc = 0, swapLast = 0, state = 0, acc = 0;
            for (int index = 0; index < terms.Count; index += 2)
            {
                uint x = terms[index];
                uint y = terms[index + 1];

                uint t = Rol16(unchecked(offsets[index] + x));
                uint q = unchecked(t * 0x76F50F34u);
                swapAcc = unchecked(swapAcc + unchecked(Rol16(q) * 0xC3EFEA97u) + q);

                t = Rol16(unchecked(offsets[index + 1] + y));
                q = unchecked(t * 0x1881CE3Cu);
                swapLast = unchecked(unchecked(Rol16(q) * 0x35BD1EC9u) + q);
                swapAcc = unchecked(swapAcc + swapLast);

                state = unchecked((state * keyA) + x);
                state = unchecked(Rol16(state) * 0xCF98B111u);
                state = unchecked(Rol16(state) * 0x87085B9Fu);
                state = unchecked(Rol16(state) * 0x12CEB96Du);
                state = unchecked(Rol16(state) * 0x257E1D83u);
                acc = unchecked(acc + state);

                t = unchecked((state * keyB) + y);
                t = unchecked(Rol16(t) * 0xA27416F5u);
                t = unchecked(Rol16(t) * 0xD38396FFu);
                state = unchecked(Rol16(unchecked(Rol16(t) * 0x7C932B89u)) * 0xBFA49F69u);
                acc = unchecked(acc + state);
            }

            return new[] { swapLast ^ state, swapAcc ^ acc };
        }

        private static byte[] PackLe32(uint low, uint high)
        {
            var result = new byte[8];
            for (int i = 0; i < 4; i++)
            {
                result[i] = (byte)(low >> (8 * i));
                result[4 + i] = (byte)(high >> (8 * i));
            }

            return result;
        }

        private static string ToHexUpper(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
            {
                builder.Append(b.ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
    }
}
