namespace VideoMosaic;

internal static class TransportStreamProbe
{
    // Only inspect a bounded prefix after normal loading fails. Never rewrite media.
    internal static bool IsTransportStream(string path)
    {
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var prefix = new byte[64 * 1024];
            int count = 0, read;
            while(count < prefix.Length && (read = file.Read(prefix, count, prefix.Length-count)) > 0) count += read;
            return ContainsPackets(prefix.AsSpan(0,count));
        }
        catch(IOException) { return false; }
        catch(UnauthorizedAccessException) { return false; }
    }

    internal static bool ContainsPackets(ReadOnlySpan<byte> data)
    {
        // TS, M2TS (4-byte timestamp), and TS with 16-byte error correction.
        foreach(int stride in new[] { 188, 192, 204 })
        {
            for(int offset=0; offset + 7*stride + 188 <= data.Length; offset++)
            {
                bool valid = true;
                for(int packet=0; packet<8; packet++)
                {
                    int p = offset + packet*stride;
                    int adaptation = (data[p+3] >> 4) & 3;
                    if(data[p] != 0x47 || (data[p+1] & 0x80) != 0 || adaptation == 0 ||
                       ((adaptation & 2) != 0 && data[p+4] > 183))
                    { valid = false; break; }
                }
                if(valid) return true;
            }
        }
        return false;
    }
}
